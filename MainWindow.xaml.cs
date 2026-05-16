using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace fflauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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
        private ConfigManager configManager;
        private Dictionary<string, ServerConfig> configs;
        private ServerConfig selectedConfig;
        private Logger logger;

        public MainWindow()
        {
            InitializeComponent();
            logger = new Logger(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.log"));
            logger.Clear();
            this.StateChanged += MainWindow_StateChanged;
            LoadConfigs();
            InitializeWindowSize();
            ApplyTabletMode(configManager?.TabletMode ?? false);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isAspectUpdating) return;
            try
            {
                _isAspectUpdating = true;
                double newWidth = this.Width;
                double newHeight = this.Height;

                double widthFromHeight = Math.Max(this.MinWidth, Math.Round(newHeight * AspectRatio));
                double heightFromWidth = Math.Max(this.MinHeight, Math.Round(newWidth / AspectRatio));

                double deltaW = Math.Abs(newWidth - (this.ActualWidth));
                double deltaH = Math.Abs(newHeight - (this.ActualHeight));

                if (deltaW >= deltaH)
                {
                    this.Height = Math.Max(this.MinHeight, heightFromWidth);
                }
                else
                {
                    this.Width = Math.Max(this.MinWidth, widthFromHeight);
                }
            }
            finally
            {
                _isAspectUpdating = false;
            }
        }

        private bool _isAspectUpdating = false;
        private const double AspectRatio = 16.0 / 9.0;

        private void ApplyTabletMode(bool enabled)
        {
            try
            {
                // First, remove any existing Viewbox wrapping
                if (this.Content is Viewbox viewbox && viewbox.Child is Border border && border.Name == "RootBorder")
                {
                    // Remove RootBorder from Viewbox
                    viewbox.Child = null;
                    // Set RootBorder directly as window content
                    this.Content = border;
                }

                // Now apply tablet mode if needed
                if (!enabled)
                {
                    this.MaximizeButton.IsEnabled = true;
                    return;
                }

                // For tablet mode wrap RootBorder in a Viewbox so UI scales to the screen like before
                Rect workArea = SystemParameters.WorkArea;

                Viewbox tabletViewbox = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.Both,
                    Width = workArea.Width,
                    Height = workArea.Height
                };

                this.Content = null;
                tabletViewbox.Child = RootBorder;
                
                this.Content = tabletViewbox;
                this.WindowState = WindowState.Maximized;
                this.MaximizeButton.IsEnabled = false; // disable maximize button in tablet mode since we're already maximizing and handling resizing ourselves
                this.Topmost = true;
            }
            catch (Exception ex)
            {
                logger.Log($"Error applying tablet mode: {ex.Message}", "ERROR");
            }
        }

        private void InitializeWindowSize()
        {
            const double baseWidth = 900.0;
            const double baseHeight = 620.0;
            Rect workArea = SystemParameters.WorkArea;

            double width = Math.Min(Math.Max(baseWidth, workArea.Width * 0.95), Math.Min(workArea.Width, 1600.0));
            double height = Math.Min(Math.Max(baseHeight, workArea.Height * 0.85), Math.Min(workArea.Height, 900.0));

            this.Width = width;
            this.Height = height;
            this.MinWidth = Math.Min(baseWidth, workArea.Width * 0.6);
            this.MinHeight = Math.Min(baseHeight, workArea.Height * 0.6);

            // Ensure maximize doesn't exceed usable work area
            this.MaxWidth = workArea.Width;
            this.MaxHeight = workArea.Height;
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            try
            {
                Rect workArea = SystemParameters.WorkArea;
                if (this.WindowState == WindowState.Maximized)
                {
                    // If we're in tablet mode the content will be wrapped in a Viewbox.
                    if (this.Content is Viewbox vb && vb.Child is Border b && b.Name == "RootBorder")
                    {
                        // Expand the viewbox to fill the screen and aggressively fill horizontally
                        vb.Stretch = Stretch.UniformToFill;
                        vb.Width = workArea.Width;
                        vb.Height = workArea.Height;
                        this.Topmost = true;
                        if (b != null)
                            b.CornerRadius = new CornerRadius(0);
                    }
                    else
                    {
                        // Normal non-tablet maximize behavior
                        this.Width = workArea.Width;
                        this.Height = workArea.Height;
                        this.MaxWidth = workArea.Width;
                        this.MaxHeight = workArea.Height;
                        if (RootBorder != null)
                            RootBorder.CornerRadius = new CornerRadius(0);
                    }
                }
                else
                {
                    // Restoring from maximized
                    if (this.Content is Viewbox vb && vb.Child is Border b && b.Name == "RootBorder")
                    {
                        // Use uniform scaling when not maximized so aspect ratio is preserved and not cropped
                        vb.Stretch = Stretch.Uniform;
                        this.Topmost = false;
                        if (b != null)
                            b.CornerRadius = new CornerRadius(16);
                    }
                    else
                    {
                        if (RootBorder != null)
                            RootBorder.CornerRadius = new CornerRadius(16);
                    }
                }
            }
            catch
            {
                // swallow any errors related to state changes at startup
            }
        }

        private bool TryMakeProcessWindowBorderless(int processId)
        {
            IntPtr found = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == processId && IsWindowVisible(hWnd))
                {
                    found = hWnd;
                    return false; // stop enumerating
                }

                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
                return false;

            int style = GetWindowLong(found, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU);
            SetWindowLong(found, GWL_STYLE, style);

            Rect work = SystemParameters.WorkArea;
            SetWindowPos(found, new IntPtr(HWND_TOPMOST), 0, 0, (int)work.Width, (int)work.Height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            ShowWindow(found, SW_MAXIMIZE);

            return true;
        }

        public void LoadConfigs()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
                configManager = new ConfigManager(configPath);
                configs = configManager.LoadConfigs();

                ApplyTheme(configManager.GlobalTheme);
                // apply tablet mode if enabled in global config
                ApplyTabletMode(configManager.TabletMode);

                logger.Log($"Loaded {configs.Count} server configs");

                var list = configs.Values.ToList();

                // 👇 Add "+" item at the end
                list.Add(new ServerConfig
                {
                    Name = "+",
                    IsAddNew = true
                });

                ServerCarousel.ItemsSource = list;

                // Ensure carousel is interactable after reloading
                ServerCarousel.IsEnabled = true;
                ServerCarousel.IsHitTestVisible = true;
                ServerCarousel.UpdateLayout();

                if (list.Any())
                    ServerCarousel.SelectedItem = list[0];
            }
            catch (Exception ex)
            {
                logger.Log($"Error loading config: {ex.Message}", "ERROR");
                MessageBox.Show($"Error loading config: {ex.Message}");
            }
        }

        private void ServerCarousel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            logger.Log("ServerCarousel_MouseLeftButtonUp fired");
            if (ServerCarousel.SelectedItem is ServerConfig config && config.IsAddNew)
            {
                // The add tile is handled by its button click. Ignore selection-based activation.
                if (_isDragging || _maybeDragging)
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void ServerCarousel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            logger.Log("ServerCarousel_SelectionChanged fired");
            if (ServerCarousel.SelectedItem is not ServerConfig config)
                return;

            logger.Log($"Selected config: {config.Name} (IsAddNew={config.IsAddNew})");
            if (config.IsAddNew)
            {
                if (_isDragging || _maybeDragging)
                {
                    // Ignore add-new selection while dragging and restore previous selection.
                    if (selectedConfig != null)
                        ServerCarousel.SelectedItem = selectedConfig;
                    else if (ServerCarousel.Items.Count > 1)
                        ServerCarousel.SelectedIndex = 0;

                    return;
                }

                // Prevent the add tile from acting like a real selection.
                if (selectedConfig != null)
                    ServerCarousel.SelectedItem = selectedConfig;
                else if (ServerCarousel.Items.Count > 1)
                    ServerCarousel.SelectedIndex = 0;

                return;
            }

            selectedConfig = config;
            DisplayConfigDetails(config);

            Dispatcher.BeginInvoke(() =>
            {
                ServerCarousel.ScrollIntoView(config);
            });
        }
        private ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer sv)
                return sv;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _startOffset;
        private ScrollViewer? _scrollViewer;
        private bool _maybeDragging = false;
        private bool _configDragging;
        private Point _configStartPoint;
        private double _configStartOffset;
        private const double DragSensitivity = 1.0;
        private const double ConfigDragSensitivity = 1.0;
        private const double DragThreshold = 6.0;

        private void ConfigDrag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _configDragging = true;
            _configStartPoint = e.GetPosition(this);
            _configStartOffset = ConfigScrollViewer.VerticalOffset;

            ConfigScrollViewer.CaptureMouse();
        }

        private void ConfigDrag_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_configDragging) return;

            Point current = e.GetPosition(this);
            double delta = _configStartPoint.Y - current.Y;

            ConfigScrollViewer.ScrollToVerticalOffset(_configStartOffset + delta);
        }

        private void ConfigDrag_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _configDragging = false;
            ConfigScrollViewer.ReleaseMouseCapture();
        }

        private void Carousel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            logger.Log("Carousel_MouseDown fired");
            var source = e.OriginalSource as DependencyObject;
            if (source != null && FindParent<Button>(source) != null)
                return;

            _scrollViewer ??= FindScrollViewer(ServerCarousel);
            if (_scrollViewer == null) return;

            // don't immediately capture mouse; start a tentative drag and only begin actual dragging
            _maybeDragging = true;
            _dragStartPoint = e.GetPosition(ServerCarousel);
            _startOffset = _scrollViewer.HorizontalOffset;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;
                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void Carousel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_maybeDragging && !_isDragging) return;

            if (_scrollViewer == null)
            {
                _scrollViewer = FindScrollViewer(ServerCarousel);
                if (_scrollViewer == null) return;
            }

            Point current = e.GetPosition(ServerCarousel);
            double deltaX = Math.Abs(current.X - _dragStartPoint.X);

            if (!_isDragging)
            {
                if (deltaX < DragThreshold)
                    return;

                // begin dragging
                _isDragging = true;
                ServerCarousel.CaptureMouse();
            }

            double delta = _dragStartPoint.X - current.X;
            _scrollViewer.ScrollToHorizontalOffset(_startOffset + ApplyDrag(delta));
        }

        private void Carousel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            logger.Log("Carousel_MouseUp fired");
            if (_isDragging)
            {
                _isDragging = false;
                ServerCarousel.ReleaseMouseCapture();
            }

            // reset tentative dragging state so clicks proceed normally
            _maybeDragging = false;
        }

        private double ApplyDrag(double delta)
        {
            return delta * 1.0; // match config panel responsiveness
        }

        private void AddTile_Click(object sender, RoutedEventArgs e)
        {
            logger.Log("AddTile_Click fired");
            OpenAddConfig();
            e.Handled = true;
        }

        private void OpenAddConfig()
        {
            var newConfig = new ServerConfig
            {
                Name = "New Config",
                Mode = "offline",
                GraphicsApi = "vulkan",
                Fullscreen = true,
                FpsLimit = "60"
            };
            var settingsWindow = new SettingsWindow(configManager, configs, newConfig)
            {
                Owner = this
            };

            settingsWindow.ShowDialog();

            // Reload after adding
            LoadConfigs();
        }

        private void DisplayConfigDetails(ServerConfig config)
        {
            ConfigDetailsPanel.Children.Clear();

            AddDetail("Mode", config.Mode);
            AddDetail("Server Path", config.ServerPath);
            AddDetail("Client Path", config.ClientPath);
            AddDetail("Cache Dir", config.CacheDir);
            AddDetail("Address", config.Address);
            AddDetail("Username", config.Username);
            AddDetail("Log File", config.LogFile);
            AddDetail("Verbose", config.Verbose.ToString());
            AddDetail("DXVK HUD", config.DxvkHud.ToString());
            AddDetail("FPS Limit", config.FpsLimit);
            AddDetail("Fullscreen", config.Fullscreen.ToString());
        }

        private void AddDetail(string label, string value)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 120 });
            panel.Children.Add(new TextBlock { Text = value });
            ConfigDetailsPanel.Children.Add(panel);
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedConfig == null)
            {
                MessageBox.Show("Please select a server.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await LaunchGameAsync(selectedConfig);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LaunchGameAsync(ServerConfig config)
        {
            logger.Log($"Launching game for server: {config.Name}");
            Process? serverProcess = null;

            if (config.Mode.ToLower() == "offline" && !string.IsNullOrEmpty(config.ServerPath))
            {
                logger.Log("Starting server");
                string serverDir = Path.GetDirectoryName(config.ServerPath) ?? string.Empty;
                var serverInfo = new ProcessStartInfo
                {
                    FileName = config.ServerPath,
                    WorkingDirectory = serverDir
                };
                serverProcess = Process.Start(serverInfo);
            }

            // Endpoint integration
            string? overrideAddress = null;
            string? overrideUsername = null;
            string? overrideToken = null;

            if (config.Mode == "endpoint")
            {
                if (string.IsNullOrEmpty(config.Address))
                {
                    throw new Exception("Endpoint address is empty for endpoint mode.");
                }

                using var ec = new EndpointClient(config.Address);
                try
                {
                    var info = await ec.GetInfoAsync();
                    if (!string.IsNullOrEmpty(info?.LoginAddress))
                        overrideAddress = info.LoginAddress;

                    if (!string.IsNullOrEmpty(config.Token))
                    {
                        // config.Token stores refresh token
                        var session = await ec.GetSessionAsync(config.Token);
                        if (session == null || string.IsNullOrEmpty(session.SessionToken))
                            throw new Exception("Failed to get session from endpoint.");

                        var cookie = await ec.GetCookieAsync(session.SessionToken);
                        if (cookie == null || string.IsNullOrEmpty(cookie.Cookie))
                            throw new Exception("Failed to obtain cookie from endpoint.");

                        overrideUsername = cookie.Username;
                        overrideToken = cookie.Cookie;
                    }
                    else
                    {
                        MessageBox.Show("No refresh token present for this endpoint. Please login in Settings.", "Login required", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.Log($"Endpoint error: {ex.Message}", "ERROR");
                    throw;
                }
            }

            // Build client args
            string args = BuildClientArgs(config, overrideAddress, overrideUsername, overrideToken);
            logger.Log($"Client args: {args}");

            // Start client with proper working directory and environment
            string clientDir = Path.GetDirectoryName(config.ClientPath) ?? string.Empty;

            var clientInfo = new ProcessStartInfo
            {
                FileName = config.ClientPath,
                Arguments = args,
                WorkingDirectory = clientDir,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            logger.Log($"Config name: {config.Name}");
            logger.Log($"Config dir: {Path.GetDirectoryName(config.ClientPath)}");
            logger.Log($"Client path: {config.ClientPath}");
            logger.Log($"Cache dir: {config.CacheDir}");
            logger.Log($"Client dir: {clientDir}");
            logger.Log($"Full arguments string: {args}");
            logger.Log($"Working directory: {clientInfo.WorkingDirectory}");

            // Pass environment variables (UseShellExecute = true inherits parent env automatically)
            SetClientEnvironment(clientInfo, config);

            logger.Log("Starting client");
            var clientProcess = Process.Start(clientInfo);
            if (clientProcess == null)
                throw new Exception("Failed to start client process.");

            if (config.Fullscreen == true)
            {
                // Try to find the client's top-level window by process id and make it borderless fullscreen.
                DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (TryMakeProcessWindowBorderless(clientProcess.Id))
                        {
                            timer.Stop();
                        }
                    }
                    catch
                    {
                        // ignore and retry
                    }
                };
                timer.Start();
            }

            // Wait for client to exit asynchronously
            await Task.Run(() => clientProcess.WaitForExit());

            if (serverProcess != null && !serverProcess.HasExited)
            {
                serverProcess.Kill();
            }
        }

        private string BuildClientArgs(ServerConfig config, string? addressOverride = null, string? usernameOverride = null, string? tokenOverride = null)
        {
            string cache = NormalizePathOrUrl(Path.GetFullPath(config.CacheDir), false);

            var address = string.IsNullOrEmpty(addressOverride) ? config.Address : addressOverride;
            string args = $"-m \"{cache}/main.unity3d\" --asseturl \"{cache}/\" -a \"{address}\"";

            var username = string.IsNullOrEmpty(usernameOverride) ? config.Username : usernameOverride;
            var token = string.IsNullOrEmpty(tokenOverride) ? config.Token : tokenOverride;

            if (!string.IsNullOrEmpty(username))
                args += $" --username \"{username}\"";
            if (!string.IsNullOrEmpty(token))
                args += $" --token \"{token}\"";
            if (!string.IsNullOrEmpty(config.LogFile))
                args += $" -l \"{config.LogFile}\"";

            if (config.GraphicsApi == "opengl")
                args += " --force-opengl";
            else if (config.GraphicsApi == "vulkan")
                args += " --force-vulkan";

            if (config.Fullscreen == true)
            {
                args += $" --width {SystemParameters.PrimaryScreenWidth} --height {SystemParameters.PrimaryScreenHeight}";
            }

            if (config.Verbose == true)
                args += " -v";

            return args;
        }

        public static string NormalizePathOrUrl(string? value, bool ensureTrailingSlash)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;


            // If it's a rooted filesystem path, force file:///
            if (Path.IsPathRooted(value))
            {
                var full = Path.GetFullPath(value);
                if (ensureTrailingSlash && Directory.Exists(full) && !full.EndsWith(Path.DirectorySeparatorChar) &&
                    !full.EndsWith(Path.AltDirectorySeparatorChar))
                    full += Path.DirectorySeparatorChar;

                var rooted = new Uri(full).AbsoluteUri.Replace("%20", " ");

                return rooted;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                {
                    var path = uri.LocalPath;
                    if (ensureTrailingSlash && Directory.Exists(path) && !path.EndsWith(Path.DirectorySeparatorChar) &&
                        !path.EndsWith(Path.AltDirectorySeparatorChar))
                        path += Path.DirectorySeparatorChar;

                    var fileUri = new Uri(path).AbsoluteUri;
                    return fileUri;
                }

                return value;
            }

            value = Path.GetFullPath(value);

            return value;
        }

        private void SetClientEnvironment(ProcessStartInfo info, ServerConfig config)
        {
            // When UseShellExecute = true, environment variables are inherited from parent process
            // So we need to set them on the parent process
            if (!string.IsNullOrEmpty(config.FpsLimit))
            {
                Environment.SetEnvironmentVariable("DXVK_FRAME_RATE", config.FpsLimit);
                logger.Log($"Set DXVK_FRAME_RATE={config.FpsLimit}");
            }

            if (config.DxvkHud == true)
            {
                Environment.SetEnvironmentVariable("DXVK_HUD", "full");
                logger.Log("Set DXVK_HUD=full");
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if(configs == null)
            {
                configs = SettingsWindow.EndpointPresets.ToDictionary(p => p.Name, p => new ServerConfig
                {
                    Name = p.Name,
                    Mode = "endpoint",
                    Address = p.Address
                });
            }
            ;
            var settingsWindow = new SettingsWindow(configManager, configs)
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
            // Reload configs after editing
            LoadConfigs();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaximize();
                }
                else
                {
                    this.DragMove();
                }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            this.WindowState =
                this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public void ApplyTheme(string theme)
        {
            var app = Application.Current;
            switch (theme?.ToLower())
            {
                case "light":
                    ApplySet(app, "Light");
                    break;
                case "blue":
                    ApplySet(app, "Blue");
                    break;
                case "neon":
                    ApplySet(app, "Neon");
                    break;
                case "green":
                    ApplySet(app, "Green");
                    break;
                case "forest":
                    ApplySet(app, "Forest");
                    break;
                case "ocean":
                    ApplySet(app, "Ocean");
                    break;
                case "purple":
                    ApplySet(app, "Purple");
                    break;
                case "sunset":
                    ApplySet(app, "Sunset");
                    break;

                case "gray":
                    ApplySet(app, "Gray");
                    break;

                case "amoled":
                    ApplySet(app, "Amoled");
                    break;
                default:
                    app.Resources["BgColor"] = app.Resources["BgColorDark"];
                    app.Resources["FgColor"] = app.Resources["FgColorDark"];
                    app.Resources["AccentColor"] = app.Resources["AccentColorDark"];
                    app.Resources["CardColor"] = app.Resources["CardColorDark"];
                    app.Resources["ButtonBackground"] = app.Resources["ButtonColorDark"];
                    app.Resources["ButtonForeground"] = app.Resources["FgColorDark"];
                    app.Resources["BorderColor"] = app.Resources["BorderColorDark"];
                    break;
            }
        }

        private void ApplySet(Application app, string prefix)
        {
            app.Resources["BgColor"] = app.Resources[$"BgColor{prefix}"];
            app.Resources["FgColor"] = app.Resources[$"FgColor{prefix}"];
            app.Resources["AccentColor"] = app.Resources[$"AccentColor{prefix}"];
            app.Resources["CardColor"] = app.Resources[$"CardColor{prefix}"];
            app.Resources["ButtonBackground"] = app.Resources[$"ButtonColor{prefix}"];
            app.Resources["ButtonForeground"] = app.Resources[$"FgColor{prefix}"];
            app.Resources["BorderColor"] = app.Resources[$"BorderColor{prefix}"];
        }
    }
}