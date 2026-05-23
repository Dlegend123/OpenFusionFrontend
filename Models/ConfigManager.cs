using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace fflauncher.Models
{
    public class ConfigManager
    {
        private readonly string configPath;
        private readonly string baseDir;

        public ConfigManager(string configPath)
        {
            this.configPath = configPath;
            baseDir = Path.GetDirectoryName(configPath)!;
            this.configFile = ReadConfigFile();
        }

        public string GlobalTheme { get; set; } = "fusionfall";
        public bool TabletMode { get; set; } = true;
        public bool BypassGui { get; set; } = false;

        public ConfigFile configFile { get; set; }
        /// <summary>
        /// When true, disables P/Invoke calls for borderless fullscreen (Proton/Android compatibility)
        /// </summary>
        public bool DisableBorderlessFullscreen { get; set; } = false;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public class ConfigFile
        {
            [JsonPropertyName("global")]
            public GlobalSettings Global { get; set; } = new();

            [JsonPropertyName("configs")]
            public List<ServerConfig>? Configs { get; set; }
        }

        public sealed class GlobalSettings
        {
            [JsonPropertyName("theme")]
            public string Theme { get; set; } = "fusionfall";

            [JsonPropertyName("disable_borderless_fullscreen")]
            public bool DisableBorderlessFullscreen { get; set; }

            [JsonPropertyName("bypass_gui")]
            public bool BypassGui { get; set; }
        }

        private ConfigFile ReadConfigFile()
        {
            if (!File.Exists(configPath))
                return new ConfigFile();

            try
            {
                using var stream = File.OpenRead(configPath);
                using var document = JsonDocument.Parse(stream);

                var root = document.RootElement;
                var result = new ConfigFile();

                if (root.TryGetProperty("global", out var globalElement))
                {
                    result.Global = globalElement.Deserialize<GlobalSettings>(JsonOptions)
                                    ?? new GlobalSettings();
                }

                if (root.TryGetProperty("configs", out var configsElement) &&
                    configsElement.ValueKind == JsonValueKind.Array)
                {
                    result.Configs = configsElement.Deserialize<List<ServerConfig>>(JsonOptions);
                }

                return result;
            }
            catch
            {
                return new ConfigFile();
            }
        }

        public Dictionary<string, ServerConfig> LoadConfigs()
        {
            var configs = new Dictionary<string, ServerConfig>(StringComparer.OrdinalIgnoreCase);

            GlobalTheme = configFile.Global.Theme ?? "fusionfall";
            DisableBorderlessFullscreen = configFile.Global.DisableBorderlessFullscreen;
            BypassGui = configFile.Global.BypassGui;

            var list = configFile.Configs;
            if (list is not { Count: > 0 })
                return configs;

            var span = CollectionsMarshal.AsSpan(list);

            foreach (ref var config in span)
            {
                if (string.IsNullOrEmpty(config.Name))
                    continue;

                config.ServerPath = ResolvePath(config.ServerPath);
                config.ClientPath = ResolvePath(config.ClientPath);

                if (!string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase))
                {
                    config.CacheDir = ResolvePath(config.CacheDir);
                }

                config.LogFile = ResolvePath(config.LogFile);
                config.ImagePath = ResolvePath(config.ImagePath);

                configs[config.Id] = config;
            }

            return configs;
        }

        // Load a single ServerConfig by its section key (e.g. "config:Name")
        public ServerConfig? GetDefaultConfig()
        {
            var config = configFile.Configs?.FirstOrDefault(c => c.IsDefault);

            return config;
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            ReadOnlySpan<char> span = path.AsSpan().Trim();

            // Avoid allocation unless needed
            if (Path.IsPathRooted(span))
                return path;

            return Path.Combine(baseDir, span.ToString());
        }

        public void SaveConfigs(Dictionary<string, ServerConfig> configs)
        {
            var configList = new List<ServerConfig>(configs.Count);

            foreach (var (key, config) in configs)
            {
                config.Id = key;
                configList.Add(config);
            }

            var root = new
            {
                global = new GlobalSettings
                {
                    Theme = GlobalTheme,
                    DisableBorderlessFullscreen = DisableBorderlessFullscreen,
                    BypassGui = BypassGui
                },
                configs = configList
            };

            File.WriteAllText(configPath, JsonSerializer.Serialize(root, JsonOptions));
            // keep in-memory representation in sync with on-disk file
            this.configFile.Global = root.global;
            this.configFile.Configs = configList;
        }
    }
}
