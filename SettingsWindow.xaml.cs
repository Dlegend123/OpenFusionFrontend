using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace fflauncher
{
    public partial class SettingsWindow : Window
    {
        private readonly List<string> themeOptions = new()
{
    "dark",
    "light",
    "blue",
    "neon",
    "green",
    "sunset",
    "gray",
    "amoled"
};
        private ConfigManager configManager;
        private Dictionary<string, ServerConfig> configs;
        private ServerConfig currentConfig, currentEndpoint;
        private readonly List<EndpointPreset> endpointPresets = new();
        public static List<ServerConfig> EndpointPresets = new List<ServerConfig>
    {
        new ServerConfig
        {
            Address = "api.dexlabs.systems",
            CacheDir = "",
            ClientPath = "",
            DxvkHud = false,
            FpsLimit = "60",
            GraphicsApi = "vulkan",
            LogFile = "",
            Mode = "online",
            Name = "Public - Original",
            ServerPath = "",
        },
        new ServerConfig
        {
            Address = "api.dexlabs.systems/academy",
            CacheDir = "",
            ClientPath = "",
            DxvkHud = false,
            FpsLimit = "60",
            GraphicsApi = "vulkan",
            LogFile = "",
            Mode = "online",
            Name = "Public - Academy",
            ServerPath = "",
        }
    };

        private sealed class EndpointPreset
        {
            public string Description { get; set; } = string.Empty;
            public string Endpoint { get; set; } = string.Empty;
        }

        public SettingsWindow(ConfigManager configManager, Dictionary<string, ServerConfig> configs, ServerConfig initialConfig = null)
        {
            InitializeComponent();
            this.configManager = configManager;
            this.configs = new Dictionary<string, ServerConfig>(configs);
            ThemeDropdown.ItemsSource = themeOptions;
            ThemeDropdown.SelectedItem = configManager.GlobalTheme ?? "dark";
            if (initialConfig != null)
            {
                if (!this.configs.ContainsKey(initialConfig.Name))
                {
                    this.configs[initialConfig.Name] = initialConfig;
                }
                currentConfig = initialConfig;
            }
            LoadEndpointPresets();
            LoadServerList();
            if (currentConfig != null)
            {
                ServerDropdown.SelectedItem = currentConfig;
            }
        }

        private void LoadEndpointPresets()
        {

            try
            {

                if (EndpointPresets.Any())
                {
                    EndpointPresetDropdown.ItemsSource = EndpointPresets;
                    EndpointPresetDropdown.SelectedItem = EndpointPresets[0];
                }
            }
            catch
            {
                // Ignore preset loading failures; user can still edit settings manually.
            }
        }

        private void LoadServerList()
        {
            var list = configs.Values.ToList();
            ServerDropdown.ItemsSource = list;
            if (list.Any())
            {
                if (currentConfig != null && list.Contains(currentConfig))
                {
                    ServerDropdown.SelectedItem = currentConfig;
                }
                else
                {
                    ServerDropdown.SelectedItem = list[0];
                }
            }
            else
            {
                currentConfig = null;
            }
        }

        private void ServerDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerDropdown.SelectedItem is ServerConfig config)
            {
                currentConfig = config;
                LoadConfigToForm(config);
            }
        }

        private void EndpointPresetDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EndpointPresetDropdown.SelectedItem is ServerConfig config)
            {
                currentEndpoint = config;
            }
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

        private void SectionChanged(object sender, RoutedEventArgs e)
        {
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
            ModeOnlineRadio.IsChecked = config.Mode == "Online";
            ModeOfflineRadio.IsChecked = config.Mode == "Offline";
            ServerPathTextBox.Text = config.ServerPath;
            ClientPathTextBox.Text = config.ClientPath;
            CacheDirTextBox.Text = config.CacheDir;
            AddressTextBox.Text = config.Address;
            var matchedPreset = EndpointPresets.FirstOrDefault(p => string.Equals(p.Address, config.Address, StringComparison.OrdinalIgnoreCase));
            if (matchedPreset != null)
            {
                EndpointPresetDropdown.SelectedItem = matchedPreset;
            }
            else if (EndpointPresetDropdown.Items.Count > 0)
            {
                EndpointPresetDropdown.SelectedIndex = 0;
            }
            UsernameTextBox.Text = config.Username;
            TokenTextBox.Text = config.Token;
            LogFileTextBox.Text = config.LogFile;
            VerboseCheckBox.IsChecked = config.Verbose;
            DxvkHudCheckBox.IsChecked = config.DxvkHud;
            FpsLimitTextBox.Text = config.FpsLimit;
            GraphicsApiTextBox.Text = config.GraphicsApi;
            FullscreenCheckBox.IsChecked = config.Fullscreen;
            ImagePathTextBox.Text = config.ImagePath;
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

        private bool _settingsDragging;
        private Point _settingsStartPoint;
        private double _settingsStartOffset;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentConfig == null)
                return;

            var oldName = currentConfig.Name;

            currentConfig.Name = NameTextBox.Text;
            currentConfig.Mode = ModeOnlineRadio.IsChecked == true ? "Online" : "Offline";
            currentConfig.ServerPath = ServerPathTextBox.Text;
            currentConfig.ClientPath = ClientPathTextBox.Text;
            currentConfig.CacheDir = CacheDirTextBox.Text;
            currentConfig.Address = AddressTextBox.Text;
            currentConfig.Username = UsernameTextBox.Text;
            currentConfig.Token = TokenTextBox.Text;
            currentConfig.LogFile = LogFileTextBox.Text;
            currentConfig.Verbose = VerboseCheckBox.IsChecked == true;
            currentConfig.DxvkHud = DxvkHudCheckBox.IsChecked == true;
            currentConfig.FpsLimit = FpsLimitTextBox.Text;
            currentConfig.GraphicsApi = GraphicsApiTextBox.Text;
            currentConfig.Fullscreen = FullscreenCheckBox.IsChecked == true;
            currentConfig.ImagePath = ImagePathTextBox.Text;

            if (oldName != currentConfig.Name && configs.ContainsKey(oldName))
            {
                configs.Remove(oldName);
            }

            configs[currentConfig.Name] = currentConfig;
            configManager.GlobalTheme = (ThemeDropdown.SelectedItem as string) ?? "dark";
            try
            {
                configManager.SaveConfigs(configs);
                ApplyTheme(configManager.GlobalTheme);
                LoadServerList();
                ServerDropdown.SelectedItem = currentConfig;
                (this.Owner as MainWindow)?.LoadConfigs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImagePathTextBox_Click(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select carousel background image",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ImagePathTextBox.Text = dialog.FileName;
            }
        }

        private async void FetchToken_Click(object sender, RoutedEventArgs e)
        {
            if (currentConfig == null)
            {
                MessageBox.Show("Select a server config first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(currentConfig.Address))
            {
                MessageBox.Show("Endpoint address is required to fetch a token.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var username = UsernameTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Enter username and password to fetch a refresh token.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new EndpointClient(currentConfig.Address);
                var refreshToken = await client.GetRefreshTokenAsync(username, password);
                TokenTextBox.Text = refreshToken;
                currentConfig.Username = username;
                currentConfig.Token = refreshToken;
                configs[currentConfig.Name] = currentConfig;
                configManager.SaveConfigs(configs);
                
                MessageBox.Show("Refresh token fetched and saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch token: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            if (currentConfig == null)
            {
                MessageBox.Show("Select a server config first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(currentConfig.Address))
            {
                MessageBox.Show("Endpoint address is required to register.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var username = UsernameTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordBox.Password ?? string.Empty;
            var email = EmailTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                MessageBox.Show("Enter username, password, and email to register.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new EndpointClient(currentConfig.Address);
                var result = await client.RegisterUserAsync(username, password, email);
                MessageBox.Show(result?.Resp ?? "Registration completed.", "Register", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Registration failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsScroll_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            var source = e.OriginalSource as DependencyObject;
            if (source != null &&
                (FindParent<TextBox>(source) != null ||
FindParent<PasswordBox>(source) != null ||
FindParent<ComboBox>(source) != null ||
FindParent<Button>(source) != null ||
FindParent<ScrollBar>(source) != null ||
FindParent<CheckBox>(source) != null ||
FindParent<RadioButton>(source) != null))
            {
                return;
            }

            _settingsDragging = true;
            _settingsStartPoint = e.GetPosition(this);
            _settingsStartOffset = ServerPanel.VerticalOffset;
            ServerPanel.CaptureMouse();
        }

        private void SettingsScroll_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_settingsDragging) return;
            var current = e.GetPosition(this);
            var delta = _settingsStartPoint.Y - current.Y;
            ServerPanel.ScrollToVerticalOffset(_settingsStartOffset + delta);
        }

        private void SettingsScroll_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _settingsDragging = false;
            ServerPanel.ReleaseMouseCapture();
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
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newConfig = new ServerConfig
            {
                Name = "config:new",
                Mode = "offline",
                GraphicsApi = "vulkan",
                Fullscreen = true,
                FpsLimit = "60"
            };
            configs[newConfig.Name] = newConfig;
            LoadServerList();
            ServerDropdown.SelectedItem = newConfig.Name;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerDropdown.SelectedItem is string name)
            {
                configs.Remove(name);
                LoadServerList();
            }
        }
    }
}