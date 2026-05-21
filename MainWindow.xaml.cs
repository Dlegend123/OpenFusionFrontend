using System.Collections.Generic;
using System.Linq;
    
using fflauncher.Models;
using fflauncher.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using WForms = System.Windows.Forms;
using Image = System.Windows.Controls.Image;

namespace fflauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ServerConfig CloneConfig(ServerConfig src)
        {
            return new ServerConfig
            {
                Id = src.Id,
                Name = src.Name,
                Mode = src.Mode,
                ServerPath = src.ServerPath,
                ClientPath = src.ClientPath,
                CacheDir = src.CacheDir,
                Address = src.Address,
                Username = src.Username,
                Token = src.Token,
                Password = src.Password,
                LogFile = src.LogFile,
                Verbose = src.Verbose,
                DxvkHud = src.DxvkHud,
                FpsLimit = src.FpsLimit,
                GraphicsApi = src.GraphicsApi,
                Fullscreen = src.Fullscreen,
                ImagePath = src.ImagePath
            };
        }
        private List<GameVersionInfo> _availableVersions = new();
        private ServerConfig? originalConfig;
        
        private ConfigManager configManager;
        Dictionary<string, ServerConfig> configs; // key = Id
        private ServerConfig selectedConfig;
        private ServerConfig editingConfig;
        private ServerConfig? ActiveConfig =>
    ServerCarousel.SelectedItem is ServerConfig c && !c.IsAddNew ? c : null;
        private GameLauncher gameLauncher;
        
        private readonly ObservableCollection<ServerConfig> _configList = new();
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private double _startOffset;
        private ScrollViewer? _scrollViewer;
        private bool _maybeDragging = false;
        private bool _configDragging;
        private Point _configStartPoint;
        private double _configStartOffset;
        private const double DragThreshold = 6.0;
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
        private ImageCacheService _imageService;
        private ThemeService _themeService;
        private ToastService _toastService;
        private ConfigService _configService;

        public MainWindow(Logger logger)
        {
            InitializeComponent();
            _imageService = new ImageCacheService();
            _themeService = new ThemeService(ThemeMap);
            _toastService = new ToastService();
            _configService = new ConfigService();
            this.logger = logger;

            Closed += (_, _) => logger.Dispose();

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            configManager = new ConfigManager(configPath);   // ✅ create once here
            configs = configManager.LoadConfigs();
            gameLauncher = new GameLauncher(logger, configManager);

            try
            {
                _themeService.Apply(configManager.GlobalTheme);

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
        }

        private async Task InitializeAsync()
        {
            try
            {
                _configList.Clear();

                // Load built-in default versions
                _availableVersions.AddRange(GameVersionInfo.DefaultVersions);

                // Versions are determined automatically at launch (no UI selection)
                var list = _configService.BuildList(configs);
                foreach (var item in list)
                    _configList.Add(item);

                ServerCarousel.ItemsSource = _configList;
                ServerDropdown.ItemsSource = _configList;
                // wait for UI to finish generating
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

                // 🔥 NEW: preload all carousel images after configs load
                _ = Task.Run(PreloadAllCarouselImages);

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

                    tasks.Add(_imageService.LoadImageAsync(cfg, logger));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.Log($"PreloadAllCarouselImages failed: {ex.Message}", "WARN");
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

            // ✅ FIX: always assign a config
            originalConfig = editConfig?.IsAddNew == true ? null : editConfig ?? selectedConfig;
            editingConfig = CloneConfig(editConfig ?? selectedConfig);

            ThemeDropdown.ItemsSource = ThemeMap;
            ThemeDropdown.SelectedItem = configManager.GlobalTheme ?? "fusionfall";

            // ✅ SAFE now
            if (editingConfig.IsAddNew)
            {
                ServerDropdown.SelectedItem =
                    ServerDropdown.Items.Cast<ServerConfig>()
                    .FirstOrDefault(c => c.IsAddNew);
            }
            else
            {
                ServerDropdown.SelectedItem = originalConfig;
            }

            LoadConfigToForm(editingConfig);
            LoadEndpointPresets(editingConfig);
            FormStackPanel.DataContext = editingConfig;
            SectionChanged(SettingsButton, new RoutedEventArgs());
        }

        private async Task ShowLauncherView()
        {

            try
            {
                var f = MainViewsGrid.Height;
                DisplayConfigDetails(selectedConfig);
            }
            catch (Exception ex)
            {
                logger.Log($"ShowLauncherView error: {ex.Message}", "ERROR");
            }
            finally
            {
                BackButton.Visibility = Visibility.Collapsed;
                SettingsButton.Visibility = Visibility.Visible;
                LauncherHeader.Visibility = Visibility.Visible;
                LauncherView.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Collapsed;
                LaunchButton.Visibility = Visibility.Visible;
                SettingsHeader.Visibility = Visibility.Collapsed;
                SaveButton.Visibility = Visibility.Collapsed;
                LauncherView.Visibility = Visibility.Visible;
            }
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


        private void ServerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerDropdown.SelectedItem is ServerConfig config)
            {
                if (config.IsAddNew)
                {
                    LoadEndpointPresets(config);
                    LoadConfigToForm(config);
                }
                else
                {
                    editingConfig = CloneConfig(config);
                    LoadConfigToForm(editingConfig);
                }
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
            EndpointTextBox.Text = config.Address;
            UsernameTextBox.Text = config.Username;
            UserPasswordBox.Password = config.Password;
            TokenPasswordBox.Password = config.Token;
            LogFileTextBox.Text = config.LogFile;
            VerboseCheckBox.IsChecked = config.Verbose;
            DxvkHudCheckBox.IsChecked = config.DxvkHud;
            FpsLimitTextBox.Text = config.FpsLimit;
            GraphicsApiTextBox.Text = config.GraphicsApi;
            FullscreenCheckBox.IsChecked = config.Fullscreen;
            ImagePathTextBox.Text = config.ImagePath;
            UpdateModeFields();
        }

        private void UpdateModeFields()
        {
            bool isOnline = ModeOnlineRadio.IsChecked == true;
            OnlineFieldsPanel.Visibility = isOnline ? Visibility.Visible : Visibility.Collapsed;
            OfflineFieldsPanel.Visibility = isOnline ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (editingConfig == null) return;
            
            editingConfig.Mode = ModeOnlineRadio.IsChecked == true ? "online" : "offline";
            UpdateModeFields();
        }

        private string GetEffectiveAddress()
        {
            var address = AddressComboBox.Text?.Trim();
            if (!string.IsNullOrEmpty(address))
                return address;

            return EndpointTextBox.Text?.Trim() ?? string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (editingConfig == null) return;

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            var name = NameTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                _toastService.Show("The config name cannot be empty.", ToastService.ToastType.Warning, "Warning");
                if (button != null) button.IsEnabled = true;
                return;
            }

            var cfg = originalConfig ?? editingConfig;

            // update IN PLACE (this is the key)
            cfg.Name = name;
            cfg.Mode = ModeOnlineRadio.IsChecked == true ? "online" : "offline";
            cfg.ServerPath = ServerPathTextBox.Text;
            cfg.ClientPath = ClientPathTextBox.Text;
            cfg.CacheDir = CacheDirTextBox.Text;
            cfg.Address = GetEffectiveAddress();
            cfg.Username = UsernameTextBox.Text;
            cfg.Password = UserPasswordBox.Password;
            cfg.Token = TokenPasswordBox.Password;
            cfg.LogFile = LogFileTextBox.Text;
            cfg.Verbose = VerboseCheckBox.IsChecked == true;
            cfg.DxvkHud = DxvkHudCheckBox.IsChecked == true;
            cfg.FpsLimit = FpsLimitTextBox.Text;
            cfg.GraphicsApi = GraphicsApiTextBox.Text;
            cfg.Fullscreen = FullscreenCheckBox.IsChecked == true;

            var newImagePath = ImagePathTextBox.Text;

            // 🔥 smart image update
            cfg.ImagePath = newImagePath;

            // 🔥 remove cache for OLD path (correct)
            await _imageService.LoadImageAsync(cfg, logger, force: true);

            // OPTIONAL: prevent duplicate names (UI rule, not storage rule)
            if (configs.Values.Any(c =>
                c.Id != cfg.Id &&
                string.Equals(c.Name, cfg.Name, StringComparison.OrdinalIgnoreCase)))
            {
                _toastService.Show("A config with this name already exists.", ToastService.ToastType.Warning, "Warning");
                if (button != null) button.IsEnabled = true;
                return;
            }

            if (!configs.ContainsKey(cfg.Id))
            {
                configs[cfg.Id] = cfg;

                // insert before "+"
                int insertIndex = Math.Max(0, _configList.Count - 1);
                _configList.Insert(insertIndex, cfg);
            }

            configManager.GlobalTheme = ThemeDropdown.SelectedItem as string ?? configManager.GlobalTheme;

            // run IO OFF UI thread
            await Task.Run(() => configManager.SaveConfigs(configs));

            await Dispatcher.InvokeAsync(() =>
            {

                // already updated in-place → just reselect it
                if (!ReferenceEquals(ServerCarousel.SelectedItem, cfg))
                    ServerCarousel.SelectedItem = cfg;

                ServerDropdown.SelectedItem = cfg;
                selectedConfig = cfg;

                // reload form + theme
                LoadConfigToForm(cfg);
                _themeService.Apply(configManager.GlobalTheme);

                _toastService.Show("Settings saved successfully.", ToastService.ToastType.Success, "Success");

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

            cfg.Address = GetEffectiveAddress();
            if (string.IsNullOrEmpty(cfg.Address))
            {
                _toastService.Show("Endpoint address is required to fetch a token.", ToastService.ToastType.Warning, "Warning");
                return;
            }

            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password ?? string.Empty;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _toastService.Show("Enter username and password to fetch a refresh token.", ToastService.ToastType.Warning, "Warning");
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

                _toastService.Show("Refresh token fetched and saved.", ToastService.ToastType.Success, "Success");
            }
            catch (Exception ex)
            {
                _toastService.Show($"Failed to fetch token: {ex.Message}", ToastService.ToastType.Error, "Error");
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var cfg = editingConfig ?? ActiveConfig;
            if (cfg == null) return;

            cfg.Address = GetEffectiveAddress();
            if (string.IsNullOrEmpty(cfg.Address))
            {
                _toastService.Show("Endpoint address is required to register.", ToastService.ToastType.Warning, "Warning");
                return;
            }

            ReadOnlySpan<char> userSpan = UsernameTextBox.Text.AsSpan().Trim();
            string username = userSpan.IsEmpty ? string.Empty : userSpan.ToString();
            var password = PasswordBox.Password ?? string.Empty;
            var email = EmailTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                _toastService.Show("Enter username, password, and email to register.", ToastService.ToastType.Warning, "Warning");
                return;
            }

            try
            {
                using var client = new EndpointClient(cfg.Address);
                var result = await client.RegisterUserAsync(username, password, email);
                _toastService.Show(string.IsNullOrWhiteSpace(result) ? "Registration completed." : result, ToastService.ToastType.Success, "Register");
            }
            catch (Exception ex)
            {
                _toastService.Show($"Registration failed: {ex.Message}", ToastService.ToastType.Error, "Error");
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowLauncherView();
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
            AddDetail("Username", config.Username);
            //AddDetail("Address", config.Address, string.Equals(config.Mode, "offline", StringComparison.OrdinalIgnoreCase));
            //AddDetail("Server Path", config.ServerPath, string.Equals(config.Mode, "offline", StringComparison.OrdinalIgnoreCase));
            //AddDetail("Client Path", config.ClientPath);
            //AddDetail("Cache Dir", config.CacheDir, string.Equals(config.Mode, "offline", StringComparison.OrdinalIgnoreCase));
            //AddDetail("Log File", config.LogFile);
            AddDetail("Verbose", config.Verbose.ToString());
            AddDetail("DXVK HUD", config.DxvkHud.ToString());
            AddDetail("FPS Limit", config.FpsLimit);
            AddDetail("Fullscreen", config.Fullscreen.ToString());
        }

        private void AddDetail(string label, string value, bool visible = true)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0), Visibility = visible ? Visibility.Visible : Visibility.Collapsed };
            panel.Children.Add(new TextBlock { Text = $"{label}: ", FontSize = 14, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = value, FontSize = 14, VerticalAlignment = VerticalAlignment.Bottom });
            ConfigDetailsPanel.Children.Add(panel);
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedConfig == null)
            {
                _toastService.Show("Please select a server.", ToastService.ToastType.Warning, "Warning");
                return;
            }

            LaunchButton.IsEnabled = false;
            _toastService.Show("Launching game...", ToastService.ToastType.Info, "Info");

            try
            {
                await gameLauncher.LaunchAsync(selectedConfig);
            }
            catch (Exception ex)
            {
                _toastService.Show($"Error launching game: {ex.Message}", ToastService.ToastType.Error, "Error");
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
