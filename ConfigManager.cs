using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace fflauncher
{
    public class ServerConfig
    {
        public string Name { get; set; }
        public string Mode { get; set; }
        public string ServerPath { get; set; }
        public string ClientPath { get; set; }
        public string CacheDir { get; set; }
        public string Address { get; set; }
        public string Username { get; set; }
        public string Token { get; set; }
        public string LogFile { get; set; }
        public bool Verbose { get; set; }
        public bool DxvkHud { get; set; }
        public string FpsLimit { get; set; }
        public string GraphicsApi { get; set; }
        public bool Fullscreen { get; set; }
        public string ImagePath { get; set; }
        public bool IsAddNew { get; internal set; }
        public override string ToString()
        {
            return Name ?? base.ToString();
        }
    }

    public class ConfigManager
    {
        private readonly string configPath;

        public ConfigManager(string configPath)
        {
            this.configPath = configPath;
        }

        public string GlobalTheme { get; set; } = "dark";
        public bool TabletMode { get; set; } = false;

        public Dictionary<string, ServerConfig> LoadConfigs()
        {
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Config file not found: {configPath}");
            }

            var configs = new Dictionary<string, ServerConfig>();
            var iniData = ParseIniFile(configPath);
            var globalSection = iniData.ContainsKey("global") ? iniData["global"] : new Dictionary<string, string>();
            GlobalTheme = GetValue(globalSection, "theme", "dark");
            TabletMode = bool.Parse(GetValue(globalSection, "tablet_mode", "false"));
            string configDir = Path.GetDirectoryName(configPath) ?? "";

            foreach (var section in iniData)
            {
                if (section.Key.StartsWith("config:"))
                {
                    var config = new ServerConfig
                    {
                        Name = section.Key,
                        Mode = GetValue(section.Value, "mode", "offline"),
                        ServerPath = ResolvePath(GetValue(section.Value, "server", "")),
                        ClientPath = ResolvePath(GetValue(section.Value, "client", "")),
                        CacheDir = ResolvePath(GetValue(section.Value, "cache_dir", "")),
                        Address = GetValue(section.Value, "address", ""),
                        Username = GetValue(section.Value, "username", ""),
                        Token = GetValue(section.Value, "token", ""),
                        LogFile = ResolvePath(GetValue(section.Value, "log_file", "")),
                        Verbose = bool.Parse(GetValue(section.Value, "verbose", "false")),
                        DxvkHud = bool.Parse(GetValue(section.Value, "dxvk_hud", "false")),
                        FpsLimit = GetValue(section.Value, "fps_limit", "60"),
                        GraphicsApi = GetValue(section.Value, "graphics_api", "vulkan"), // Fixed to Vulkan
                        Fullscreen = bool.Parse(GetValue(section.Value, "fullscreen", "true")),
                        ImagePath = ResolvePath(GetValue(section.Value, "image", ""))
                    };
                    configs[section.Key] = config;
                }
            }

            return configs;
        }

        private Dictionary<string, Dictionary<string, string>> ParseIniFile(string path)
        {
            var data = new Dictionary<string, Dictionary<string, string>>();
            string currentSection = null;

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    data[currentSection] = new Dictionary<string, string>();
                }
                else if (currentSection != null && trimmed.Contains("="))
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        data[currentSection][parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            return data;
        }

        private string GetValue(Dictionary<string, string> section, string key, string defaultValue)
        {
            return section.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
                return path;
            return Path.Combine(Path.GetDirectoryName(configPath), path);
        }

        public void SaveConfigs(Dictionary<string, ServerConfig> configs)
        {
            var lines = new List<string>();
            lines.Add("[global]");
            lines.Add($"theme={GlobalTheme}");
            lines.Add($"tablet_mode={TabletMode}");
            lines.Add("");

            foreach (var kvp in configs)
            {
                lines.Add($"[{kvp.Key}]");
                var config = kvp.Value;
                lines.Add($"mode={config.Mode}");
                lines.Add($"server={config.ServerPath}");
                lines.Add($"client={config.ClientPath}");
                lines.Add($"cache_dir={config.CacheDir}");
                lines.Add($"address={config.Address}");
                lines.Add($"username={config.Username}");
                lines.Add($"token={config.Token}");
                lines.Add($"log_file={config.LogFile}");
                lines.Add($"verbose={config.Verbose}");
                lines.Add($"dxvk_hud={config.DxvkHud}");
                lines.Add($"fps_limit={config.FpsLimit}");
                lines.Add($"graphics_api={config.GraphicsApi}");
                lines.Add($"fullscreen={config.Fullscreen}");
                if (!string.IsNullOrEmpty(config.ImagePath))
                    lines.Add($"image={config.ImagePath}");
                lines.Add("");
            }

            File.WriteAllLines(configPath, lines);
        }
    }
}