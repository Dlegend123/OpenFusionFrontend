using fffrontend.Models;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace fffrontend
{
    public class GameLauncher
    {

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int SWP_FRAMECHANGED = 0x0020;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int HWND_TOPMOST = -1;
        private const int SW_MAXIMIZE = 3;

        private readonly Logger _logger;
        private readonly ConfigManager _configManager;

        private (int Width, int Height) Resolution { get; set; }

        public GameLauncher(Logger logger, ConfigManager configManager)
        {
            _logger = logger;
            _configManager = configManager;
        }

        public async Task LaunchAsync(ServerConfig config)
        {
            _logger.Log($"Launching game for server: {config.Name}");

            Process? serverProcess = null;

            // =========================
            // Start server (offline)
            // =========================
            if (string.Equals(config.Mode, "offline", StringComparison.OrdinalIgnoreCase))
            {
                string serverDir = Path.GetDirectoryName(config.ServerPath) ?? string.Empty;

                var serverInfo = new ProcessStartInfo
                {
                    FileName = config.ServerPath,
                    WorkingDirectory = serverDir,
                    // Start the server process hidden (no visible console/window)
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    // redirect output to avoid console popups and allow logging if desired
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                serverProcess = Process.Start(serverInfo);
            }

            // =========================
            // Endpoint handling
            // =========================
            string? endpoint = null;
            string? loginAddress = null;
            bool customLoadingScreen = false;
            string? overrideUsername = config.Username;
            string? overrideToken = null;
            string? assetBase = null;
            if (string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = NormalizeEndpoint(config.Endpoint);
                if (string.IsNullOrEmpty(endpoint))
                    throw new InvalidOperationException("Online mode requires an endpoint address.");

                using var ec = new EndpointClient(endpoint);

                // 1. Fetch endpoint info (matching Rust: endpoint::get_info)
                var info = await RetryAsync(() => ec.GetInfoAsync());

                if (info == null)
                    throw new Exception("Failed to fetch endpoint info");

                // 2. Get supported versions
                var supportedVersions = info.GetSupportedVersions();
                if (supportedVersions.Count == 0)
                    throw new Exception("Server returned no supported versions.");

                _logger.Log($"Server supports versions: {string.Join(", ", supportedVersions)}");

                // 3. Determine the version to use (prefer game_version, fall back to first in game_versions)
                string? versionString = info.GameVersion;
                if (string.IsNullOrEmpty(versionString) && supportedVersions.Count > 0)
                {
                    versionString = supportedVersions[0];
                }

                if (string.IsNullOrEmpty(versionString))
                    throw new Exception("Could not determine default version from endpoint.");

                // 4. Validate that the version is supported
                if (!supportedVersions.Contains(versionString))
                    throw new Exception($"Version {versionString} not supported by server. Supported: {string.Join(", ", supportedVersions)}");

                // 5. Parse version UUID and fetch version details
                if (!Guid.TryParse(versionString, out Guid versionUuid))
                    throw new Exception($"Invalid version UUID format: {versionString}");

                _logger.Log($"Using version: {versionString}");

                // Fetch the version from the endpoint (matching Rust: endpoint::fetch_version)
                var version = await RetryAsync(() => ec.FetchVersion(versionUuid));

                if (version == null)
                    throw new Exception($"Failed to fetch version details for {versionUuid}");

                // 6. Determine asset base from the fetched version
                // Use the version's URL/AssetUrl if available
                if (!string.IsNullOrEmpty(version.Url))
                {
                    assetBase = version.Url;
                    _logger.Log($"Using asset URL from version: {assetBase}");
                }
                else if (!string.IsNullOrEmpty(version.AssetUrl))
                {
                    assetBase = version.AssetUrl;
                    _logger.Log($"Using asset URL from version: {assetBase}");
                }

                // Fallback to local cache if asset URL not provided
                if (string.IsNullOrEmpty(assetBase) && !string.IsNullOrEmpty(config.CacheDir))
                {
                    assetBase = config.CacheDir;
                    _logger.Log($"Using local cache directory: {assetBase}");
                }

                loginAddress = info.LoginAddress?.Trim();
                if (string.IsNullOrEmpty(loginAddress))
                {
                    _logger.Log("Endpoint did not provide loginAddress, falling back to endpoint host", "WARN");
                    loginAddress = endpoint;
                }
                customLoadingScreen = info.CustomLoadingScreen == true;

                if (!string.IsNullOrEmpty(config.Password))
                {
                    // If the user provided a plain password, request a refresh token from the endpoint,
                    // then establish a session and cookie like the OpenFusionLauncher flow.
                    try
                    {
                        var refreshToken = await ec.GetRefreshTokenAsync(config.Username, config.Password);
                        var session = await ec.GetSessionAsync(refreshToken);
                        var cookie = await ec.GetCookieAsync(session.SessionToken);

                        if (string.IsNullOrEmpty(cookie?.Cookie))
                            throw new Exception("Endpoint did not return a valid session cookie.");

                        overrideUsername = cookie.Username ?? config.Username;
                        overrideToken = cookie.Cookie;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error attempting to retrieve token from endpoint: {ex.Message}");
                    }
                }
            }

            // =========================
            // Build args (pass asset base if determined)
            // =========================
            if (string.IsNullOrEmpty(assetBase))
            {
                // fallback to config.CacheDir or default built-in version
                if (!string.IsNullOrEmpty(config.CacheDir))
                    assetBase = config.CacheDir;
                else
                    assetBase = GameVersionInfo.DefaultVersions.FirstOrDefault()?.AssetUrl;
            }

            // Create cache directory if it's a local path (matching OpenFusionLauncher behavior)
            if (!string.IsNullOrEmpty(assetBase) && (!Uri.TryCreate(assetBase, UriKind.Absolute, out var uri) || uri.IsFile))
            {
                var cachePath = assetBase;
                if (!Path.IsPathRooted(cachePath))
                {
                    cachePath = Path.GetFullPath(cachePath);
                }
                try
                {
                    Directory.CreateDirectory(cachePath);
                    _logger.Log($"Ensured cache directory exists: {cachePath}");
                }
                catch (Exception ex)
                {
                    _logger.Log($"Warning: Failed to create cache directory {cachePath}: {ex.Message}", "WARN");
                }
            }

            var argsList = BuildArgs(config, endpoint, loginAddress, overrideUsername, overrideToken, customLoadingScreen, assetBase);

            _logger.Log("Built client arguments:");
            foreach (var a in argsList)
                _logger.Log(a);

            // Determine client executable path. Prefer configured path, but if missing
            // attempt to use ffrunner.exe in the application's directory.
            string clientPath = string.Empty;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            var candidate = Path.Combine(baseDir, "ffrunner.exe");

            if (File.Exists(candidate))
            {
                _logger.Log($"Using ffrunner.exe from app directory: {candidate}");
                clientPath = candidate;
            }

            if (string.IsNullOrWhiteSpace(clientPath) || !File.Exists(clientPath))
                throw new Exception("Client executable not found. Place ffrunner.exe next to this launcher.");

            string clientDir = Path.GetDirectoryName(clientPath) ?? string.Empty;

            var clientInfo = new ProcessStartInfo
            {
                FileName = clientPath,
                WorkingDirectory = clientDir,
                UseShellExecute = false
            };

            // Split properly OR better: build as list from the start
            foreach (var arg in argsList)
            {
                clientInfo.ArgumentList.Add(arg);
            }

            SetEnvironment(clientInfo, config, assetBase);

            _logger.Log("Starting client...");
            var clientProcess = Process.Start(clientInfo);

            if (config.Fullscreen)
            {
                // Try to find the client's top-level window by process id and make it borderless fullscreen.
                // NOTE: This uses P/Invoke which is very slow in Proton on Android, so limit retries
                int attemptCount = 0;
                const int MAX_ATTEMPTS = 15; // ~3 seconds with 200ms interval
                DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += (s, e) =>
                {
                    attemptCount++;
                    try
                    {
                        if (TryMakeProcessWindowBorderless(clientProcess.Id))
                        {
                            _logger.Log("Successfully made client window borderless");
                            timer.Stop();
                        }
                        else if (attemptCount >= MAX_ATTEMPTS)
                        {
                            _logger.Log($"Gave up making window borderless after {MAX_ATTEMPTS} attempts (Proton compatibility issue?)", "WARN");
                            timer.Stop();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log($"Error attempting to make window borderless: {ex.Message}", "WARN");
                        if (attemptCount >= MAX_ATTEMPTS)
                        {
                            _logger.Log($"Gave up making window borderless after {MAX_ATTEMPTS} attempts", "WARN");
                            timer.Stop();
                        }
                    }
                };

                timer.Start();
            }

            if (clientProcess == null)
                throw new Exception("Failed to start client.");

            await clientProcess.WaitForExitAsync();

            if (serverProcess != null && !serverProcess.HasExited)
                serverProcess.Kill();
        }

        // =========================
        // Helpers
        // =========================

        private List<string> BuildArgs(
            ServerConfig config,
            string? endpoint = null,
            string? loginAddress = null,
            string? usernameOverride = null,
            string? tokenOverride = null,
            bool customLoadingScreen = false,
            string? assetBase = null)
        {
            var args = new List<string>();

            string cache;

            if (!string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase))
            {
                cache = NormalizePathOrUrl(Path.GetFullPath(config.CacheDir), false);
            }
            else
            {
                cache = !string.IsNullOrEmpty(assetBase) ? assetBase : config.CacheDir;
            }

            string baseUrl = cache.TrimEnd('/');

            string mainUrl = $"{baseUrl}/main.unity3d";
            string assetUrl = $"{baseUrl}/";

            // 🔹 REQUIRED ORDER (matches Rust)
            args.Add("-m");
            args.Add(mainUrl);

            args.Add("--asseturl");
            args.Add(assetUrl);

            // 🔹 ADDRESS (IMPORTANT FIX APPLIED)
            string address =
                string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(endpoint)
                ? ResolveServerAddress(loginAddress ?? endpoint)
                : ResolveServerAddress(config.Address);

            args.Add("-a");
            args.Add(address);

            // 🔹 ENDPOINT
            if (!string.IsNullOrEmpty(endpoint))
            {
                args.Add("-e");
                args.Add(endpoint);
            }

            // 🔹 LOADER
            if (customLoadingScreen)
            {
                args.Add("--loader-images");
            }

            // 🔹 USER / TOKEN
            string username = usernameOverride ?? config.Username;
            string token = tokenOverride ?? config.Password;

            if (!string.IsNullOrEmpty(username))
            {
                args.Add("-u");
                args.Add(username);
            }

            if (!string.IsNullOrEmpty(token))
            {
                args.Add("-t");
                args.Add(token);
            }

            // 🔹 LOG FILE
            if (!string.IsNullOrEmpty(config.LogFile))
            {
                args.Add("-l");
                args.Add(NormalizePathOrUrl(Path.GetFullPath(config.LogFile), false));
            }

            // 🔹 FULLSCREEN SIZE
            if (config.Fullscreen == true)
            {
                Resolution = DisplaySettings.GetCurrentResolution();

                args.Add("--width");
                args.Add(Resolution.Width.ToString());

                args.Add("--height");
                args.Add(Resolution.Height.ToString());
            }

            return args;
        }


        public static string NormalizePathOrUrl(string? value, bool ensureTrailingSlash)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            ReadOnlySpan<char> span = value.AsSpan();

            if (Path.IsPathRooted(value))
            {
                var full = Path.GetFullPath(value);

                if (ensureTrailingSlash && Directory.Exists(full))
                {
                    ReadOnlySpan<char> fspan = full.AsSpan();
                    if (!fspan.EndsWith(Path.DirectorySeparatorChar) &&
                        !fspan.EndsWith(Path.AltDirectorySeparatorChar))
                    {
                        full += Path.DirectorySeparatorChar;
                    }
                }

                return new Uri(full).AbsoluteUri.Replace("%20", " ");
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                {
                    var path = uri.LocalPath;

                    if (ensureTrailingSlash && Directory.Exists(path))
                    {
                        var pspan = path.AsSpan();
                        if (!pspan.EndsWith(Path.DirectorySeparatorChar) &&
                            !pspan.EndsWith(Path.AltDirectorySeparatorChar))
                        {
                            path += Path.DirectorySeparatorChar;
                        }
                    }

                    return new Uri(path).AbsoluteUri;
                }

                return value;
            }

            return Path.GetFullPath(value);
        }

        private async Task<T> RetryAsync<T>(Func<Task<T>> action, int attempts = 3, int delayMs = 500)
        {
            Exception? last = null;

            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    last = ex;
                    await Task.Delay(delayMs);
                }
            }

            throw new Exception($"Operation failed after {attempts} attempts: {last?.Message}", last);
        }

        private static string ResolveServerAddress(string addr)
        {
            if (string.IsNullOrWhiteSpace(addr))
                return string.Empty;

            ReadOnlySpan<char> span = addr.AsSpan().Trim();
            ReadOnlySpan<char> hostSpan = span;
            int port = 23000;

            int colonIndex = span.LastIndexOf(':');
            if (colonIndex > 0)
            {
                ReadOnlySpan<char> portSpan = span.Slice(colonIndex + 1).Trim();
                if (portSpan.Length > 0 && int.TryParse(portSpan, out int parsed))
                {
                    port = parsed;
                    hostSpan = span.Slice(0, colonIndex).TrimEnd();
                }
            }

            if (IPAddress.TryParse(hostSpan, out _))
                return $"{hostSpan.ToString()}:{port}";

            string host = hostSpan.ToString();
            try
            {
                var addresses = Dns.GetHostAddresses(host);

                foreach (var a in addresses)
                {
                    if (a.AddressFamily == AddressFamily.InterNetwork)
                        return $"{a}:{port}";
                }

                if (addresses.Length > 0)
                    return $"{addresses[0]}:{port}";
            }
            catch
            {
                throw new Exception($"Failed to resolve game server address {addr}");
            }

            throw new Exception($"No IPv4 address found for {addr}");
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return string.Empty;

            endpoint = endpoint.Trim();

            if (!endpoint.StartsWith("http://") && !endpoint.StartsWith("https://"))
                endpoint = "http://" + endpoint;

            // Remove trailing slash
            if (endpoint.EndsWith("/"))
                endpoint = endpoint.TrimEnd('/');

            return endpoint;
        }

        private void SetEnvironment(ProcessStartInfo info, ServerConfig config, string? assetBase = null)
        {
            // FPS cap using DXVK_FRAME_RATE
            if (!string.IsNullOrEmpty(config.FpsLimit))
            {
                if (int.TryParse(config.FpsLimit, out _))
                {
                    // If it's a number, use it as the cap
                    info.Environment["DXVK_FRAME_RATE"] = config.FpsLimit;
                    _logger.Log($"Set DXVK_FRAME_RATE: {config.FpsLimit}");
                }
            }

            // DXVK HUD (debug visualization)
            if (config.DxvkHud)
                info.Environment["DXVK_HUD"] = "1";

            info.Environment["WINEESYNC"] = "1"; // Enable async mode in Wine for better performance
            info.Environment["WINEFSYNC"] = "1"; // Enable fsync in Wine for better performance and stability

        }

        private bool TryMakeProcessWindowBorderless(int processId)
        {
            try
            {
                IntPtr found = IntPtr.Zero;

                // P/Invoke calls are very slow in Proton - use a timeout mechanism
                try
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                            GetWindowThreadProcessId(hWnd, out uint pid);
                            if (pid == processId && IsWindowVisible(hWnd))
                            {
                                found = hWnd;
                                return false; // stop enumerating
                            }
                        }
                        catch
                        {
                            // Ignore errors and continue enumerating
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    _logger.Log($"EnumWindows failed (Proton compatibility?): {ex.Message}", "WARN");
                    return false;
                }

                if (found == IntPtr.Zero)
                    return false;

                try
                {
                    int style = GetWindowLong(found, GWL_STYLE);
                    style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);

                    SetWindowLong(found, GWL_STYLE, style);
                    SetWindowPos(found, new IntPtr(HWND_TOPMOST), 0, 0, Resolution.Width, Resolution.Height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                    ShowWindow(found, SW_MAXIMIZE);
                }
                catch (Exception ex)
                {
                    _logger.Log($"Window style manipulation failed (Proton compatibility?): {ex.Message}", "WARN");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Log($"TryMakeProcessWindowBorderless failed: {ex.Message}", "ERROR");
                return false;
            }
        }
    }
}