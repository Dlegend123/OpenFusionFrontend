
using fffrontend.Models;
using fffrontend.Services;
using fffrontend.UI;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using WForms = System.Windows.Forms;

namespace fffrontend
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
                CacheDir = src.CacheDir,
                Address = src.Address,
                Endpoint = src.Endpoint,
                Username = src.Username,
                Password = src.Password,
                LogFile = src.LogFile,
                Verbose = src.Verbose,
                DxvkHud = src.DxvkHud,
                FpsLimit = src.FpsLimit,
                Fullscreen = src.Fullscreen,
                ImagePath = src.ImagePath,
                IsAddNew = src.IsAddNew
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
        private ScrollViewer? _scrollViewer;
        private bool _suppressCarouselSelectionAfterDrag;
        private bool _suppressServerDropdownSelectionChanged;
        private bool _configDragging;
        private Point _configStartPoint;
        private double _configStartOffset;
        private static readonly List<string> ThemeMap =
         new List<string>
            {
                "Blue",
                "Gray",
                "Neon",
                "Forest",
                "FusionFall",
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
                DxvkHud = false,
                FpsLimit = "60",
                LogFile = string.Empty,
                Mode = "online",
                Name = "Public - Original",
                ServerPath = string.Empty,
            },
            new ServerConfig
            {
                Address = "api.dexlabs.systems/academy",
                CacheDir = string.Empty,
                DxvkHud = false,
                FpsLimit = "60",
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
        private readonly CarouselDragHandler _carouselDrag = new();

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

                // 🔥 NEW: preload all carousel images and fetch player counts before showing the window
                var preloadTask = PreloadAllCarouselImages();
                var fetchCountsTask = FetchPlayerCounts();
                await Task.WhenAll(preloadTask, fetchCountsTask);

                if (configs.Any())
                {
                    var first = configs.Values.FirstOrDefault();
                    if (first != null)
                    {
                        ServerCarousel.SelectedItem = first;
                        ServerDropdown.SelectedItem = first;
                        selectedConfig = first;
                        LoadConfigToForm(first);
                        DisplayConfigDetails(first);
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
        private async Task PreloadAllCarouselImages() =>
            await ProcessConfigsAsync(_configList, async cfg => await _imageService.LoadImageAsync(cfg, logger), "PreloadAllCarouselImages");

        private async Task FetchPlayerCounts() =>
            await ProcessConfigsAsync(
                _configList.Where(cfg => cfg.Mode == "online" && !string.IsNullOrEmpty(cfg.Endpoint)),
                FetchAndSetPlayerCountAsync,
                "FetchPlayerCounts");

        private async Task FetchCurrentCarouselPlayerCountsAsync()
        {
            try
            {
                if (ServerCarousel.SelectedItem is not ServerConfig selected)
                    return;

                int index = _configList.IndexOf(selected);
                if (index < 0)
                    return;

                var visibleConfigs = new List<ServerConfig>(3);
                for (int offset = -1; offset <= 1; offset++)
                {
                    int idx = index + offset;
                    if (idx >= 0 && idx < _configList.Count)
                        visibleConfigs.Add(_configList[idx]);
                }

                await ProcessConfigsAsync(
                    visibleConfigs.Where(cfg => cfg.Mode == "online" && !string.IsNullOrEmpty(cfg.Endpoint)),
                    FetchAndSetPlayerCountAsync,
                    "FetchCurrentCarouselPlayerCounts");
            }
            catch (Exception ex)
            {
                logger.Log($"FetchCurrentCarouselPlayerCountsAsync failed: {ex.Message}", "DEBUG");
            }
        }

        private async Task ProcessConfigsAsync<T>(IEnumerable<T> items, Func<T, Task> action, string taskName) where T : ServerConfig
        {
            try
            {
                var tasks = items?
                    .Where(cfg => cfg != null && !cfg.IsAddNew)
                    .Select(action)
                    .ToList() ?? new();
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.Log($"{taskName} failed: {ex.Message}", "WARN");
            }
        }

        private async Task FetchAndSetPlayerCountAsync(ServerConfig config)
        {
            try
            {
                using var client = new EndpointClient(config.Endpoint);
                config.PlayerCount = (await client.GetStatusAsync()).PlayerCount;
            }
            catch (Exception ex)
            {
                logger.Log($"Failed to fetch player count for {config.Name}: {ex.Message}", "DEBUG");
            }
        }

        private void ShowSettingsView(ServerConfig? editConfig = null)
        {
            // If nothing is selected yet, create a safe blank config for the settings view.
            var configToEdit = editConfig ?? selectedConfig ?? new ServerConfig
            {
                Id = Guid.NewGuid().ToString(),
                Name = "New Config",
                Mode = "offline",
                ServerPath = string.Empty,
                CacheDir = string.Empty,
                Address = string.Empty,
                Endpoint = string.Empty,
                Username = string.Empty,
                Password = string.Empty,
                LogFile = string.Empty,
                Verbose = false,
                DxvkHud = false,
                FpsLimit = "60",
                Fullscreen = true,
                ImagePath = string.Empty
            };

            originalConfig = configToEdit.IsAddNew ? null : (editConfig ?? selectedConfig);
            editingConfig = CloneConfig(configToEdit);

            _suppressServerDropdownSelectionChanged = true;

            // Update visibility with high priority to show settings view immediately
            _ = Dispatcher.InvokeAsync(() =>
            {
                // Batch visibility changes to minimize layout passes
                BackButton.Visibility = Visibility.Visible;
                SettingsButton.Visibility = Visibility.Collapsed;
                LauncherHeader.Visibility = Visibility.Collapsed;
                LauncherView.Visibility = Visibility.Collapsed;
                LaunchButton.Visibility = Visibility.Collapsed;
                SettingsHeader.Visibility = Visibility.Visible;
                SaveButton.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Visible;

                // Set theme immediately (cached)
                ThemeDropdown.ItemsSource = ThemeMap;
                ThemeDropdown.SelectedItem = configManager.GlobalTheme ?? "fusionfall";

                // Select the '+' entry when creating a new config
                if (editingConfig.IsAddNew)
                {
                    var addNewDropdownItem = _configList.FirstOrDefault(cfg => cfg.IsAddNew);
                    if (addNewDropdownItem != null)
                        ServerDropdown.SelectedItem = addNewDropdownItem;
                }
                else
                {
                    // Ensure the dropdown reflects the currently selected carousel item
                    ServerDropdown.SelectedItem = configToEdit;
                }

                // Section visibility updated immediately so the settings panel appears quickly.
                SectionChanged(SettingsButton, new RoutedEventArgs());
            }, DispatcherPriority.Render);

            // Defer setting DataContext and populating form controls to background to
            // allow the UI to render the settings view immediately.
            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    FormStackPanel.DataContext = editingConfig;

                    // Populate form fields
                    LoadConfigToForm(editingConfig);

                    // Load endpoint presets (non-blocking for the UI)
                    LoadEndpointPresets(editingConfig);

                    // Reset server panel scroll position to top after form is loaded
                    ServerPanelScrollViewer?.ScrollToVerticalOffset(0);
                }
                catch { }
                finally
                {
                    _suppressServerDropdownSelectionChanged = false;
                }
            }, DispatcherPriority.Background);
        }

        private async Task ShowLauncherView()
        {
            try
            {
                // Update visibility first with high priority
                await Dispatcher.InvokeAsync(() =>
                {
                    BackButton.Visibility = Visibility.Collapsed;
                    SettingsButton.Visibility = Visibility.Visible;
                    LauncherHeader.Visibility = Visibility.Visible;
                    LauncherView.Visibility = Visibility.Visible;
                    SettingsView.Visibility = Visibility.Collapsed;
                    LaunchButton.Visibility = Visibility.Visible;
                    SettingsHeader.Visibility = Visibility.Collapsed;
                    SaveButton.Visibility = Visibility.Collapsed;
                }, DispatcherPriority.Render);

                // Then defer the details population to background
                var configToShow = selectedConfig ?? ActiveConfig;
                if (configToShow != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        DisplayConfigDetails(configToShow);
                    }, DispatcherPriority.Background);
                }
                else
                {
                    ConfigDetailsPanel.Children.Clear();
                }
            }
            catch (Exception ex)
            {
                logger.Log($"ShowLauncherView error: {ex.Message}", "ERROR");
            }
        }

        private void LoadEndpointPresets(ServerConfig config)
        {
            try
            {
                // Build list with presets first to minimize allocations
                var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var list = new List<string>();

                // Add all unique preset addresses
                foreach (var preset in EndpointPresets)
                {
                    if (!string.IsNullOrEmpty(preset.Address) && addresses.Add(preset.Address))
                    {
                        list.Add(preset.Address);
                    }
                }

                // Add current endpoint if not already in list
                var current = config?.Endpoint;
                if (!string.IsNullOrEmpty(current) && addresses.Add(current))
                {
                    list.Add(current);
                }

                // Update combo box efficiently
                EndpointComboBox.ItemsSource = list;
                if (!string.IsNullOrEmpty(current))
                {
                    EndpointComboBox.SelectedItem = current;
                }
            }
            catch { }
        }


        private void ServerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressServerDropdownSelectionChanged)
                return;

            if (ServerDropdown.SelectedItem is ServerConfig config)
            {
                // ensure originalConfig references the actual stored config (or null for add-new)
                originalConfig = config.IsAddNew ? null : config;
                editingConfig = CloneConfig(config);

                // Defer heavy form loading to background priority to keep UI responsive
                _ = Dispatcher.InvokeAsync(() =>
                {
                    LoadEndpointPresets(editingConfig);
                    LoadConfigToForm(editingConfig);
                }, DispatcherPriority.Background);
            }
        }

        private void SectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is Button settingsButton)
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
            // Batch property assignments to reduce layout recalculations
            try
            {
                NameTextBox.Text = config.Name;
                ModeOnlineRadio.IsChecked = string.Equals(config.Mode, "online", StringComparison.OrdinalIgnoreCase);
                ModeOfflineRadio.IsChecked = string.Equals(config.Mode, "offline", StringComparison.OrdinalIgnoreCase);
                ServerPathTextBox.Text = config.ServerPath;
                CacheDirTextBox.Text = config.CacheDir;
                AddressTextBox.Text = config.Address;
                EndpointComboBox.Text = config.Endpoint;
                UsernameTextBox.Text = config.Username;
                UserPasswordBox.Password = config.Password;
                LogFileTextBox.Text = config.LogFile;
                VerboseToggle.IsChecked = config.Verbose;
                DxvkHudToggle.IsChecked = config.DxvkHud;
                FullscreenToggle.IsChecked = config.Fullscreen;
                FpsLimitTextBox.Text = config.FpsLimit;
                ImagePathTextBox.Text = config.ImagePath;
            }
            finally
            {
                // Defer visibility updates to allow batched text updates to render first
                _ = Dispatcher.InvokeAsync(() => UpdateModeFields(), DispatcherPriority.Render);
            }
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
            var endpoint = EndpointComboBox.Text?.Trim();
            if (!string.IsNullOrEmpty(endpoint))
                return endpoint;

            return AddressTextBox.Text?.Trim() ?? string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (editingConfig == null) return;

            var button = sender as Button;
            if (button != null) button.IsEnabled = false;

            // debug: log UI values being saved
            try
            {
                logger.Log($"Save_Click UI values: Name='{NameTextBox.Text}', Address='{AddressTextBox.Text}', Endpoint='{EndpointComboBox.Text}', ServerPath='{ServerPathTextBox.Text}'", "DEBUG");
            }
            catch { }

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
            cfg.CacheDir = CacheDirTextBox.Text;
            cfg.Address = AddressTextBox.Text;
            cfg.Endpoint = EndpointComboBox.Text;
            cfg.Username = UsernameTextBox.Text;
            cfg.Password = UserPasswordBox.Password;
            cfg.LogFile = LogFileTextBox.Text;
            cfg.Verbose = VerboseToggle.IsChecked == true;
            cfg.DxvkHud = DxvkHudToggle.IsChecked == true;
            cfg.FpsLimit = FpsLimitTextBox.Text;
            cfg.Fullscreen = FullscreenToggle.IsChecked == true;

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

            try
            {
                logger.Log($"Saved config id={cfg.Id} name={cfg.Name} endpoint={cfg.Endpoint} address={cfg.Address}", "DEBUG");
            }
            catch { }

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

        private void ImagePathTextBox_DoubleClick(object sender, MouseButtonEventArgs e)
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

        private void ServerPathTextBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectFilePath(ServerPathTextBox, "Select Server Executable", "Executable files|*.exe|All files|*.*");
        }

        private void LogFileTextBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectLogFilePath(LogFileTextBox, "Select Log File", "Log files|*.log;*.txt|All files|*.*");
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

        private void SelectLogFilePath(System.Windows.Controls.TextBox target, string title, string filter)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = filter,
                OverwritePrompt = false,
                CheckPathExists = true,
                AddExtension = true,
                DefaultExt = ".log"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var dir = Path.GetDirectoryName(dialog.FileName);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (!File.Exists(dialog.FileName))
                        File.WriteAllText(dialog.FileName, string.Empty);
                }
                catch { }

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

        // Fetch token removed: tokens are auto-refreshed when launching the game

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            var cfg = editingConfig ?? ActiveConfig;
            if (cfg == null) return;

            cfg.Address = GetEffectiveAddress();
            cfg.Endpoint = EndpointComboBox.Text;
            if (string.IsNullOrEmpty(cfg.Endpoint))
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
                using var client = new EndpointClient(cfg.Endpoint);
                var result = await client.RegisterUserAsync(username, password, email);

                UserPasswordBox.Password = password;
                Save_Click(sender, e); // save entered credentials
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
            if (ServerCarousel.SelectedItem is ServerConfig config && config.IsAddNew)
            {
                // The add tile is handled by its button click. Ignore selection-based activation when the carousel was actually dragging.
                if (_carouselDrag.SuppressClick)
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

            if (_carouselDrag.SuppressClick || _suppressCarouselSelectionAfterDrag)
                return;

            logger.Log("ServerCarousel_SelectionChanged fired");
            if (ServerCarousel.SelectedItem is not ServerConfig config)
                return;

            if (config.IsAddNew)
                return;

            selectedConfig = config;

            _ = Dispatcher.InvokeAsync(async () =>
            {
                DisplayConfigDetails(config);
                await FetchCurrentCarouselPlayerCountsAsync();
            }, DispatcherPriority.Background);
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
            // Prevent drag start when the user interacts with buttons or toggle switches.
            var source = (DependencyObject)e.OriginalSource;
            if (FindParent<ToggleButton>(source) != null || FindParent<Button>(source) != null)
                return;

            _configDragging = true;
            _configStartPoint = e.GetPosition(ConfigScrollViewer);
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
            var source = e.OriginalSource as DependencyObject;
            if (source != null && FindParent<Button>(source) != null)
                return;

            _scrollViewer ??= FindScrollViewer(ServerCarousel);
            if (_scrollViewer == null) return;

            _carouselDrag.MouseDown(_scrollViewer, e.GetPosition(ServerCarousel));
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
            if (_scrollViewer == null)
                _scrollViewer = FindScrollViewer(ServerCarousel);

            if (_scrollViewer == null) return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                _carouselDrag.Cancel(_scrollViewer);
                return;
            }

            _carouselDrag.MouseMove(_scrollViewer, e.GetPosition(ServerCarousel));
        }

        private void Carousel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_scrollViewer == null)
                return;

            bool wasDragging = _carouselDrag.MouseUp(_scrollViewer);

            // 🔥 IMPORTANT: prevent selection if it was a drag
            if (wasDragging)
            {
                _suppressCarouselSelectionAfterDrag = true;
                Dispatcher.BeginInvoke(() =>
                {
                    _suppressCarouselSelectionAfterDrag = false;
                    _carouselDrag.Cancel(_scrollViewer);
                }, DispatcherPriority.Input);
                e.Handled = true;
            }
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
                Fullscreen = true,
                FpsLimit = "60",
                IsAddNew = true
            };

            // Defer loading presets and heavy form population to ShowSettingsView
            ShowSettingsView(newConfig);
        }

        private void DisplayConfigDetails(ServerConfig config)
        {
            ConfigDetailsPanel.Children.Clear();

            var grid = new Grid
            {
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Text column, separator, toggle column, separator, button column
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // separator
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // button column

            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Left column: text details
            var textPanel = new Grid
            {
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            int row = 0;

            textPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddDetailToPanel(textPanel, "Mode", config.Mode, row++);

            textPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddDetailToPanel(textPanel, "Username", config.Username, row++);

            textPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            AddDetailToPanel(textPanel, "FPS Limit", config.FpsLimit, row++);

            // Separator
            var separator = new Border
            {
                Width = 1,
                Background = Application.Current?.TryFindResource("BorderColor") as Brush,
                Margin = new Thickness(10, 0, 10, 0)
            };

            // Right column: toggles
            var togglePanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            AddToggleDetailToPanel(togglePanel, "Verbose", config.Verbose, val => config.Verbose = val);
            AddToggleDetailToPanel(togglePanel, "DXVK HUD", config.DxvkHud, val => config.DxvkHud = val);
            AddToggleDetailToPanel(togglePanel, "Fullscreen", config.Fullscreen, val => config.Fullscreen = val);

            Grid.SetColumn(textPanel, 0);
            Grid.SetColumn(separator, 1);
            Grid.SetColumn(togglePanel, 2);

            grid.Children.Add(textPanel);
            grid.Children.Add(separator);
            grid.Children.Add(togglePanel);

            ConfigDetailsPanel.Children.Add(grid);
        }

        private void AddDetailToPanel(Grid panel, string label, string value, int rowIndex)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 8, 0, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = $"{label}",
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(labelBlock, 0);

            var valueBlock = new TextBlock
            {
                Text = value,
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Application.Current?.TryFindResource("AccentColor") as Brush,
            };

            Grid.SetColumn(valueBlock, 1);

            row.Children.Add(labelBlock);
            row.Children.Add(valueBlock);

            Grid.SetRow(row, rowIndex);

            panel.Children.Add(row);
        }

        private void AddToggleDetailToPanel(StackPanel panel, string label, bool value, Action<bool> onChanged)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // fixed label width
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // toggle width

            var text = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(text, 0);

            var toggle = new ToggleButton
            {
                IsChecked = value,
                Style = (Style)FindResource("ToggleSwitchStyle"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsThreeState = false
            };
            Grid.SetColumn(toggle, 1);

            toggle.Click += async (_, _) =>
            {
                onChanged(toggle.IsChecked == true);
                await SaveQuick();
            };

            row.Children.Add(text);
            row.Children.Add(toggle);

            panel.Children.Add(row);
        }

        private async Task SaveQuick()
        {
            try
            {
                var cfg = selectedConfig;
                if (cfg == null) return;

                // persist configs quickly
                await Task.Run(() => configManager.SaveConfigs(configs));

                logger.Log($"SaveQuick: config id={cfg.Id} name={cfg.Name}", "DEBUG");
            }
            catch (Exception ex)
            {
                logger.Log($"SaveQuick failed: {ex.Message}", "ERROR");
            }
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedConfig == null)
            {
                _toastService.Show("Please select a server.", ToastService.ToastType.Warning, "Warning");
                return;
            }

            LaunchButton.IsEnabled = false;

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
            configs ??= EndpointPresets.ToDictionary(
                    p => Guid.NewGuid().ToString(),
                    p => new ServerConfig
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = p.Name,
                        Mode = "Online",
                        Address = p.Address
                    });

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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CacheDirTextBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectFolderPath(CacheDirTextBox, "Select Cache Directory");
        }

    }

}
