using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace fflauncher
{
    public class GameLauncher
    {

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowTitle);

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
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int HWND_TOPMOST = -1;
        private const int SW_MAXIMIZE = 3;

        private readonly Logger _logger;
        private readonly ConfigManager _configManager;


        ref struct SimpleValueStringBuilder
        {
            private Span<char> _buffer;
            private char[]? _arrayFromPool;
            private int _pos;

            public SimpleValueStringBuilder(Span<char> initialBuffer)
            {
                _buffer = initialBuffer;
                _arrayFromPool = null;
                _pos = 0;
            }

            public void Append(ReadOnlySpan<char> value)
            {
                if (_pos + value.Length > _buffer.Length)
                    Grow(value.Length);

                value.CopyTo(_buffer.Slice(_pos));
                _pos += value.Length;
            }

            public void Append(string s) => Append(s.AsSpan());

            public void Append(char c)
            {
                if (_pos >= _buffer.Length)
                    Grow(1);

                _buffer[_pos++] = c;
            }

            private void Grow(int needed)
            {
                int newSize = Math.Max(_buffer.Length * 2, _pos + needed);
                char[] newArray = new char[newSize];

                _buffer.Slice(0, _pos).CopyTo(newArray);

                _buffer = newArray;
                _arrayFromPool = newArray;
            }

            public override string ToString()
            {
                return new string(_buffer.Slice(0, _pos));
            }
        }

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
                    WorkingDirectory = serverDir
                };

                serverProcess = Process.Start(serverInfo);
            }

            // =========================
            // Endpoint handling
            // =========================
            string? endpoint = null;
            string? overrideUsername = null;
            string? overrideToken = null;
            if (string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase))
            {
                using var ec = new EndpointClient(config.Address);

                var info = await ec.GetInfoAsync();
                endpoint = info?.LoginAddress;

                // ✅ Version validation
                //var supportedVersions = info.GameVersions ??
                //                        (info.GameVersion != null ? new[] { info.GameVersion } : Array.Empty<string>());
                //if (!supportedVersions.Contains(config.GameVersion))
                //{
                //    throw new Exception($"Version {config.GameVersion} not supported by server {config.Name}");
                //}

                var session = await ec.GetSessionAsync(config.Token);
                var cookie = await ec.GetCookieAsync(session.SessionToken);

                overrideUsername = cookie.Username;
                overrideToken = cookie.Cookie;
            }

            // =========================
            // Build args
            // =========================
            string args = BuildArgs(config, endpoint, overrideUsername, overrideToken);
            _logger.Log($"Built client arguments: {args}");
            string clientDir = Path.GetDirectoryName(config.ClientPath) ?? string.Empty;

            var clientInfo = new ProcessStartInfo
            {
                FileName = config.ClientPath,
                Arguments = args,
                WorkingDirectory = clientDir,
                UseShellExecute = false
            };

            SetEnvironment(clientInfo, config);

            _logger.Log("Starting client...");
            var clientProcess = Process.Start(clientInfo);

            if (config.Fullscreen)
            {
                try
                {
                    if (TryMakeProcessWindowBorderless(clientProcess.Id))
                    {
                        _logger.Log("Successfully made client window borderless");
                           
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log($"Error attempting to make window borderless: {ex.Message}", "WARN");
                        
                }

            }
            else if (config.Fullscreen)
            {
                _logger.Log("Borderless fullscreen disabled (Proton/Android compatibility mode)", "INFO");
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

        private string BuildArgs(ServerConfig config, string? endpoint = null, string? usernameOverride = null, string? tokenOverride = null)
        {
            Span<char> buffer = stackalloc char[512];
            var sb = new SimpleValueStringBuilder(buffer);

            string cache = "";
            if (!string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase))
            {
                cache = NormalizePathOrUrl(Path.GetFullPath(config.CacheDir), false);
            }
            else
            {
                cache = config.CacheDir;
            }
            ///"api.ffretrobution.net"
            sb.Append("-m \"");
            sb.Append(cache);
            sb.Append("/main.unity3d\" --asseturl \"");
            sb.Append(cache);
            sb.Append('/');

            if (string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(endpoint))
            {
                sb.Append("\" -e \"");
                sb.Append(config.Address);

                sb.Append("\" -a \"");
                sb.Append(endpoint);
            }
            else
            {
                sb.Append("\" -a \"");
                sb.Append(config.Address);
            }

            sb.Append('"');


            string username = usernameOverride ?? config.Username;
            string token = tokenOverride ?? config.Token;

            if (!string.IsNullOrEmpty(username))
            {
                sb.Append(" --username \"");
                sb.Append(username);
                sb.Append('"');
            }

            if (!string.IsNullOrEmpty(token))
            {
                sb.Append(" --token \"");
                sb.Append(token);
                sb.Append('"');
            }

            if (!string.IsNullOrEmpty(config.LogFile))
            {
                sb.Append(" -l \"");
                sb.Append(NormalizePathOrUrl(Path.GetFullPath(config.LogFile), false));
                sb.Append('"');
            }

            switch (config.GraphicsApi?.ToLowerInvariant())
            {
                case "opengl":
                    sb.Append(" --force-opengl");
                    break;
                case "vulkan":
                    sb.Append(" --force-vulkan");
                    break;
            }

            if (config.Fullscreen == true)
            {
                var resolution = DisplaySettings.GetCurrentResolution();

                sb.Append(" --width ");
                AppendInt(ref sb, resolution.Width);
                sb.Append(" --height ");
                AppendInt(ref sb, resolution.Height);
            }

            if (config.Verbose == true)
                sb.Append(" -v");

            return sb.ToString();
        }

        private static void AppendInt(ref SimpleValueStringBuilder sb, int value)
        {
            Span<char> tmp = stackalloc char[16];
            if (value.TryFormat(tmp, out int written))
            {
                sb.Append(new string(tmp.Slice(0, written)));
            }
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

        private void SetEnvironment(ProcessStartInfo info, ServerConfig config)
        {
            if (!string.IsNullOrEmpty(config.FpsLimit))
                info.Environment["DXVK_FRAME_RATE"] = config.FpsLimit;

            if (config.DxvkHud)
                info.Environment["DXVK_HUD"] = "full";
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

                    Rect work = SystemParameters.WorkArea;
                    SetWindowPos(found, new IntPtr(HWND_TOPMOST), 0, 0, (int)work.Width, (int)work.Height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
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