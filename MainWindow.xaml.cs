using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using WForms = System.Windows.Forms;

namespace fflauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        private static readonly Brush ToastInfoBackground = Brushes.DimGray;
        private static readonly Brush ToastSuccessBackground = CreateFrozenBrush(Color.FromRgb(56, 162, 116));
        private static readonly Brush ToastWarningBackground = CreateFrozenBrush(Color.FromRgb(222, 176, 18));
        private static readonly Brush ToastErrorBackground = CreateFrozenBrush(Color.FromRgb(215, 86, 58));
        private static readonly Brush ToastForeground = Brushes.White;
        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        private ConfigManager configManager;
        Dictionary<string, ServerConfig> configs; // key = Id
        private ServerConfig selectedConfig;
        private ServerConfig editingConfig;
        private ServerConfig? ActiveConfig =>
    ServerCarousel.SelectedItem is ServerConfig c && !c.IsAddNew ? c : null;
        private readonly HashSet<string> _imageLoading = new(StringComparer.OrdinalIgnoreCase);
        private GameLauncher gameLauncher;
        private readonly Dictionary<string, ImageSource> _imageCache = new();
        private readonly ObservableCollection<ServerConfig> _configList = new();
        private static readonly List<string> ThemeMap =
         new List<string>
            {
                "Blue",
                "Gray",
                "Neon",
                "Forest",
                "Corruption",
                "Nano",
                "Tech",
                "Apocalypse",
                "Cosmic",
                "Purple",
                "Sunset",
                "Amoled"
            };
        private Logger logger;

        private enum ToastType
        {
            Info,
            Success,
            Warning,
            Error
        }

        public static readonly List<ServerConfig> EndpointPresets = new()
        {
            new ServerConfig
            {
                Address = "api.dexlabs.systems",
                CacheDir = string.Empty,
                ClientPath = string.Empty,
                DxvkHud = false,
                FpsLimit = "60",
                GraphicsApi = "vulkan",
                LogFile = string.Empty,
                Mode = "online",
                Name = "Public - Original",
                ServerPath = string.Empty,
            },
            new ServerConfig
            {
                Address = "api.dexlabs.systems/academy",
                CacheDir = string.Empty,
                ClientPath = string.Empty,
                DxvkHud = false,
                FpsLimit = "60",
                GraphicsApi = "vulkan",
                LogFile = string.Empty,
                Mode = "online",
                Name = "Public - Academy",
                ServerPath = string.Empty,
            }
        };


        public MainWindow(Logger logger)
        {
            InitializeComponent();
            this.logger = logger;

            Closed += (_, _) => logger.Dispose();

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            configManager = new ConfigManager(configPath);   // ✅ create once here
            configs = configManager.LoadConfigs();
            gameLauncher = new GameLauncher(logger, configManager);

            try
            {
                ApplyTheme(configManager.GlobalTheme);

                var defaultCfg = configManager.GetDefaultConfig();
                if (configManager.BypassGui && defaultCfg != null)
                {
                    this.Hide();
                    Dispatcher.BeginInvoke(async () =>
                    {
                        try
                        {
                            await gameLauncher.LaunchAsync(defaultCfg);
                        }
                        catch (Exception ex)
                        {
                            logger.Log($"Bypass GUI launch failed: {ex.Message}", "ERROR");
                        }
                        finally
                        {
                            Application.Current.Shutdown();
                        }
                    });
                }
            }
            catch { }

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            await InitializeAsync();
            ServerDropdown.ItemsSource = _configList;
        }

        private async Task InitializeAsync()
        {
            try
            {
                PopulateConfigsUI();

                // wait for UI to finish generating
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

                // 🔥 NEW: preload all carousel images after configs load
                _ = PreloadAllCarouselImages();

                if (configs.Any())
                {
                    var first = configs.Values.FirstOrDefault();
                    if (first != null)
                    {
                        ServerCarousel.SelectedItem = first;
                        ServerDropdown.SelectedItem = first;
                        selectedConfig = first;
                        LoadConfigToForm(first);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log($"InitializeAsync error: {ex.Message}", "ERROR");
            }
            finally
            {
                Show();
                Visibility = Visibility.Visible;
                ShowInTaskbar = true;
            }
        }

        private async Task PreloadAllCarouselImages()
        {
            try
            {
                if (_configList == null || _configList.Count == 0)
                    return;

                var tasks = new List<Task>();

                foreach (var cfg in _configList)
                {
                    if (cfg == null || cfg.IsAddNew)
                        continue;

                    tasks.Add(LoadImageAsync(cfg));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.Log($"PreloadAllCarouselImages failed: {ex.Message}", "WARN");
            }
        }

        private void PopulateConfigsUI()
        {
            if (configs == null) return;

            logger.Log($"Loaded {configs.Count} server configs");

            _configList.Clear();

            var seen = new HashSet<string>();

            foreach (var cfg in configs.Values)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id))
                    continue;

                // ✅ prevent duplicates by Id
                if (!seen.Add(cfg.Id))
                    continue;

                _configList.Add(cfg);
            }

            _configList.Add(new ServerConfig { Name = "+", IsAddNew = true });

            ServerCarousel.ItemsSource = _configList;
        }

        private async Task LoadImageAsync(ServerConfig cfg)
        {
            if (cfg == null || cfg.IsAddNew)
                return;

            if (string.IsNullOrWhiteSpace(cfg.ImagePath))
                return;

            var imagePath = cfg.ImagePath.Trim();
            if (imagePath.Length == 0)
                return;

            // SINGLE atomic check + add
            if (!_imageLoading.Add(imagePath))
                return;

            try
            {
                if (_imageCache.TryGetValue(imagePath, out var cached))
                {
                    cfg.Image = cached;
                    return;
                }

                ImageSource? bmp = null;

                await Task.Run(() =>
                {
                    try
                    {
                        using var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        frame.Freeze();
                        bmp = frame;
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"Bitmap creation failed for {imagePath}: {ex.Message}", "WARN");
                    }
                });

                if (bmp != null)
                {
                    if (Dispatcher.CheckAccess())
                    {
                        _imageCache[imagePath] = bmp;
                        cfg.Image = bmp;
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _imageCache[imagePath] = bmp;
                            cfg.Image = bmp;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log($"LoadImageAsync error: {ex.Message}", "ERROR");
            }
            finally
            {
                if (!string.IsNullOrEmpty(imagePath))
                    _imageLoading.Remove(imagePath);
            }
        }

        private void ShowSettingsView(ServerConfig? editConfig = null)
        {
            BackButton.Visibility = Visibility.Visible;
            SettingsButton.Visibility = Visibility.Collapsed;
            LauncherHeader.Visibility = Visibility.Collapsed;
            LauncherView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            LaunchButton.Visibility = Visibility.Collapsed;
            SettingsHeader.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Visible;

            editingConfig = editConfig ?? ActiveConfig;
            // 🔥 ALWAYS prioritize carousel selection first
            ThemeDropdown.ItemsSource = ThemeMap;
            ThemeDropdown.SelectedItem = configManager.GlobalTheme ?? "fusionfall";

            if (editingConfig.IsAddNew)
            {
                ServerDropdown.SelectedItem = ServerDropdown.Items.Cast<ServerConfig>().FirstOrDefault(c => c.Name == "+");
            }
            else
            {
                ServerDropdown.SelectedItem = editingConfig;
            }
            
            LoadConfigToForm(editingConfig);
            LoadEndpointPresets(editingConfig);
            SectionChanged(SettingsButton, new RoutedEventArgs());

        }

        private void ShowLauncherView()
        {
            TitleTextBlock.Text = "FusionFall Frontend";
            BackButton.Visibility = Visibility.Hidden;
            SettingsButton.Visibility = Visibility.Visible;
            LauncherHeader.Visibility = Visibility.Visible;
            LauncherView.Visibility = Visibility.Visible;
            SettingsView.Visibility = Visibility.Collapsed;
            LaunchButton.Visibility = Visibility.Visible;
            SettingsHeader.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Collapsed;

            Dispatcher.InvokeAsync(() =>
            {
                DisplayConfigDetails(selectedConfig);
            }, DispatcherPriority.Background);

        }

        private void ShowToast(string message, string title = "", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            ToastType type = ToastType.Info;
            if (icon == MessageBoxImage.Error)
                type = ToastType.Error;
            else if (icon == MessageBoxImage.Warning)
                type = ToastType.Warning;
            else if (icon == MessageBoxImage.Information)
                type = ToastType.Success;
            else if (!string.IsNullOrEmpty(title))
            {
                ReadOnlySpan<char> t = title.AsSpan();

                bool hasError = t.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                t.Contains("failed", StringComparison.OrdinalIgnoreCase);

                bool hasWarning = t.Contains("warning", StringComparison.OrdinalIgnoreCase);

                bool hasSuccess = t.Contains("success", StringComparison.OrdinalIgnoreCase) ||
                                  t.Contains("saved", StringComparison.OrdinalIgnoreCase) ||
                                  t.Contains("completed", StringComparison.OrdinalIgnoreCase);

                if (hasError)
                    type = ToastType.Error;
                else if (hasWarning)
                    type = ToastType.Warning;
                else if (hasSuccess)
                    type = ToastType.Success;
            }

            ShowToast(message, type, title);
        }

        private void ShowToast(string message, ToastType type, string title = "")
        {
            Brush background = ToastInfoBackground;

            switch (type)
            {
                case ToastType.Error:
                    background = ToastErrorBackground;
                    break;

                case ToastType.Warning:
                    background = ToastWarningBackground;
                    break;

                case ToastType.Success:
                    background = ToastSuccessBackground;
                    break;

                default:
                    background = TryFindResource("CardColor") as Brush ?? ToastInfoBackground;
                    break;
            }

            var toast = new ToastWindow(message, title, background, ToastForeground);
            toast.Show();

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                toast.Close();
            };

            timer.Start();
        }

        private void LoadEndpointPresets(ServerConfig config)
        {
            try
            {
                var list = new List<string>(EndpointPresets.Count + 1);
                var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var preset in EndpointPresets)
                {
                    var addr = preset.Address;
                    if (string.IsNullOrEmpty(addr))
                        continue;

                    if (addresses.Add(addr))
                        list.Add(addr);
                }

                var current = config?.Address;
                if (!string.IsNullOrEmpty(current) && addresses.Add(current))
                {
                    list.Add(current);
                }

                AddressComboBox.ItemsSource = list;
                AddressComboBox.SelectedItem = current;
            }
            catch { }
        }

        public static void SmoothScrollTo(ScrollViewer sv, double to, int durationMs = 300)
        {
            double from = sv.HorizontalOffset;
            double delta = to - from;
            int frames = durationMs / 15; // ~60fps
            int currentFrame = 0;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            timer.Tick += (s, e) =>
            {
                currentFrame++;
                double progress = (double)currentFrame / frames;
                // ease-out curve
                double eased = 1 - Math.Pow(1 - progress, 3);
                sv.ScrollToHorizontalOffset(from + delta * eased);

                if (currentFrame >= frames)
                    timer.Stop();
            };
            timer.Start();
        }


        private void ServerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerDropdown.SelectedItem is ServerConfig config)
            {

                if(config.Name == "+")
                {
                    
                    LoadEndpointPresets(config);
                }
                else
                {
                    if (editingConfig != null && config.Id != editingConfig.Id)
                        return;
                }

                LoadConfigToForm(config);
            }
        }

        private void SectionChanged(object sender, RoutedEventArgs e)
        {
            if(sender is Button settingsButton)
            {
                ServerSectionBtn.IsChecked = true;
                ThemeSectionBtn.IsChecked = false;

            }
            if (ServerPanel != null && ThemePanel != null)
            {
                if (ServerSectionBtn.IsChecked == true)
                {
                    ServerPanel.Visibility = Visibility.Visible;
                    ThemePanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ServerPanel.Visibility = Visibility.Collapsed;
                    ThemePanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void LoadConfigToForm(ServerConfig config)
        {
            NameTextBox.Text = config.Name;
            ModeOnlineRadio.IsChecked = string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase);
            ModeOfflineRadio.IsChecked = string.Equals(config.Mode, "offline", StringComparison.OrdinalIgnoreCase);
            ServerPathTextBox.Text = config.ServerPath;
            ClientPathTextBox.Text = config.ClientPath;
            CacheDirTextBox.Text = config.CacheDir;
            AddressComboBox.Text = config.Address;
            UsernameTextBox.Text = config.Username;
            TokenPasswordBox.Password = config.Token;
            LogFileTextBox.Text = config.LogFile;
            VerboseCheckBox.IsChecked = config.Verbose;
            DxvkHudCheckBox.IsChecked = config.DxvkHud;
            FpsLimitTextBox.Text = config.FpsLimit;
            GraphicsApiTextBox.Text = config.GraphicsApi;
            FullscreenCheckBox.IsChecked = config.Fullscreen;
            ImagePathTextBox.Text = config.ImagePath;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (editingConfig == null) return;

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            var name = NameTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                ShowToast("The config name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var updatedConfig = new ServerConfig
            {
                Id = editingConfig.Id,
                Name = name,
                Mode = ModeOnlineRadio.IsChecked == true ? "online" : "offline",
                ServerPath = ServerPathTextBox.Text,
                ClientPath = ClientPathTextBox.Text,
                CacheDir = CacheDirTextBox.Text,
                Address = AddressComboBox.Text,
                Username = UsernameTextBox.Text,
                Token = TokenPasswordBox.Password,
                LogFile = LogFileTextBox.Text,
                Verbose = VerboseCheckBox.IsChecked == true,
                DxvkHud = DxvkHudCheckBox.IsChecked == true,
                FpsLimit = FpsLimitTextBox.Text,
                GraphicsApi = GraphicsApiTextBox.Text,
                Fullscreen = FullscreenCheckBox.IsChecked == true,
                ImagePath = ImagePathTextBox.Text,
            };

            // OPTIONAL: prevent duplicate names (UI rule, not storage rule)
            if (configs.Values.Any(c =>
                c.Id != updatedConfig.Id &&
                string.Equals(c.Name, updatedConfig.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowToast("A config with this name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (button != null) button.IsEnabled = true;
                return;
            }

            // 🔥 ONLY THIS matters now
            configs[updatedConfig.Id] = updatedConfig;

            editingConfig = configs[updatedConfig.Id]; // ✅ always use dictionary instance

            configManager.GlobalTheme = ThemeDropdown.SelectedItem as string ?? configManager.GlobalTheme;

            // run IO OFF UI thread
            await Task.Run(() => configManager.SaveConfigs(configs));

            await Dispatcher.InvokeAsync(() =>
            {

                if (configs.TryGetValue(updatedConfig.Id, out var match))
                {
                    ServerCarousel.SelectedItem = match;
                    ServerDropdown.SelectedItem = match;
                    selectedConfig = match;
                }

                LoadConfigToForm(selectedConfig);
                ApplyTheme(configManager.GlobalTheme);

                var selected = selectedConfig;

                // remove old instance
                var existing = _configList.FirstOrDefault(c => c.Id == updatedConfig.Id);
                if (existing != null)
                {
                    existing.Name = updatedConfig.Name;
                    existing.Mode = updatedConfig.Mode;
                    existing.ServerPath = updatedConfig.ServerPath;
                    existing.ClientPath = updatedConfig.ClientPath;
                    existing.CacheDir = updatedConfig.CacheDir;
                    existing.Address = updatedConfig.Address;
                    existing.Username = updatedConfig.Username;
                    existing.Token = updatedConfig.Token;
                    existing.LogFile = updatedConfig.LogFile;
                    existing.Verbose = updatedConfig.Verbose;
                    existing.DxvkHud = updatedConfig.DxvkHud;
                    existing.FpsLimit = updatedConfig.FpsLimit;
                    existing.GraphicsApi = updatedConfig.GraphicsApi;
                    existing.Fullscreen = updatedConfig.Fullscreen;
                    existing.ImagePath = updatedConfig.ImagePath;
                }
                else
                {
                    // insert before "+"
                    int insertIndex = Math.Max(0, _configList.Count - 1);
                    _configList.Insert(insertIndex, editingConfig);
                }

                if (selected != null)
                    ServerCarousel.SelectedItem = selected;

                ShowToast("Settings saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                if (button != null) button.IsEnabled = true;
            });

        }

        private void ImagePathTextBox_Click(object sender, MouseButtonEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select carousel background image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ImagePathTextBox.Text = dialog.FileName;
            }
        }

        private void ServerPathTextBox_Click(object sender, MouseButtonEventArgs e)
        {
            SelectFilePath(ServerPathTextBox, "Select Server Executable", "Executable files|*.exe|All files|*.*");
        }

        private void ClientPathTextBox_Click(object sender, MouseButtonEventArgs e)
        {
            SelectFilePath(ClientPathTextBox, "Select Client Executable", "Executable files|*.exe|All files|*.*");
        }

        private void CacheDirTextBox_Click(object sender, MouseButtonEventArgs e)
        {
            SelectFolderPath(CacheDirTextBox, "Select Cache Directory");
        }

        private void SelectFilePath(System.Windows.Controls.TextBox target, string title, string filter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                target.Text = dialog.FileName;
            }
        }

        private void SelectFolderPath(System.Windows.Controls.TextBox target, string description)
        {
            using (var dialog = new WForms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == WForms.DialogResult.OK)
                {
                    target.Text = dialog.SelectedPath;
                }
            }
        }

        private async void FetchToken_Click(object sender, RoutedEventArgs e)
        {
            var cfg = editingConfig ?? ActiveConfig;
            if (cfg == null) return;

            if (string.IsNullOrEmpty(cfg.Address))
            {
                ShowToast("Endpoint address is required to fetch a token.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password ?? string.Empty;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowToast("Enter username and password to fetch a refresh token.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new EndpointClient(cfg.Address);
                var refreshToken = await client.GetRefreshTokenAsync(username, password);
                TokenPasswordBox.Password = refreshToken;

                cfg.Username = username;
                cfg.Token = refreshToken;

                configs[cfg.Id] = cfg;
                await Task.Run(() => configManager.SaveConfigs(configs));
                ShowToast("Refresh token fetched and saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to fetch token: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var cfg = editingConfig ?? ActiveConfig;
            if (cfg == null) return;

            if (string.IsNullOrEmpty(cfg.Address))
            {
                ShowToast("Endpoint address is required to register.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReadOnlySpan<char> userSpan = UsernameTextBox.Text.AsSpan().Trim();
            string username = userSpan.IsEmpty ? string.Empty : userSpan.ToString();
            var password = PasswordBox.Password ?? string.Empty;
            var email = EmailTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                ShowToast("Enter username, password, and email to register.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new EndpointClient(cfg.Address);
                var result = await client.RegisterUserAsync(username, password, email);
                ShowToast(string.IsNullOrWhiteSpace(result) ? "Registration completed." : result, "Register", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowToast($"Registration failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLauncherView();
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
            if (!IsLoaded)
                return;

            logger.Log("ServerCarousel_SelectionChanged fired");
            if (ServerCarousel.SelectedItem is not ServerConfig config)
                return;

            logger.Log($"Selected config: {config.Name} (IsAddNew={config.IsAddNew})");

            selectedConfig = config;
            DisplayConfigDetails(config);
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
            return delta * 3.0; // match config panel responsiveness
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
                Id = Guid.NewGuid().ToString(), // ✅ ADD THIS
                Name = "New Config",
                Mode = "offline",
                GraphicsApi = "vulkan",
                Fullscreen = true,
                FpsLimit = "60"
            };

            LoadEndpointPresets(newConfig);
            ShowSettingsView(newConfig);
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
            panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, FontSize = 14 });
            panel.Children.Add(new TextBlock { Text = value, FontSize = 14});
            ConfigDetailsPanel.Children.Add(panel);
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedConfig == null)
            {
                ShowToast("Please select a server.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LaunchButton.IsEnabled = false;
            ShowToast("Launching game...", "Info");

            try
            {
                await gameLauncher.LaunchAsync(selectedConfig);
            }
            catch (Exception ex)
            {
                ShowToast($"Error launching game: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchButton.IsEnabled = true;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (configs == null)
            {
                configs = EndpointPresets.ToDictionary(
                    p => Guid.NewGuid().ToString(),
                    p => new ServerConfig
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = p.Name,
                        Mode = "Online",
                        Address = p.Address
                });
            }

            // Show settings view immediately, defer heavy loading if needed
            ShowSettingsView();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                    this.DragMove();
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

            if (!string.IsNullOrEmpty(theme) && ThemeMap.Contains(theme))
            {
                ApplySet(app, theme);
                return;
            }

            // fallback (fusionfall)
            app.Resources["BgColor"] = app.Resources["BgColorFusionFall"];
            app.Resources["FgColor"] = app.Resources["FgColorFusionFall"];
            app.Resources["AccentColor"] = app.Resources["AccentColorFusionFall"];
            app.Resources["CardColor"] = app.Resources["CardColorFusionFall"];
            app.Resources["ButtonBackground"] = app.Resources["ButtonColorFusionFall"];
            app.Resources["ButtonForeground"] = app.Resources["FgColorFusionFall"];
            app.Resources["BorderColor"] = app.Resources["BorderColorFusionFall"];
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

        private void AddressComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var cfg = editingConfig ?? ActiveConfig;
            if (cfg != null)
            {
                cfg.Address = AddressComboBox.Text;
            }
        }
        private void CacheDirTextBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectFolderPath(CacheDirTextBox, "Select Cache Directory");
        }

    }
}
