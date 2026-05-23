using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace fflauncher.Models
{
    public class ServerConfig : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _name;
        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private string _mode;
        [JsonPropertyName("mode")]
        public string Mode 
        { 
            get => _mode;
            set 
            { 
                if (_mode != value)
                {
                    _mode = value;
                    OnPropertyChanged(nameof(Mode));
                }
            }
        }

        [JsonPropertyName("server")]
        public string ServerPath { get; set; }

        [JsonPropertyName("client")]
        public string ClientPath { get; set; }

        [JsonPropertyName("cache_dir")]
        public string CacheDir { get; set; }

        private string _address;
        [JsonPropertyName("address")]
        public string Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged(nameof(Address));
                }
            }
        }
        private string _endpoint;
        [JsonPropertyName("endpoint")]
        public string Endpoint
        {
            get => _endpoint;
            set
            {
                if (_endpoint != value)
                {
                    _endpoint = value;
                    OnPropertyChanged(nameof(Endpoint));
                }
            }
        }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("log_file")]
        public string LogFile { get; set; }

        [JsonPropertyName("verbose")]
        public bool Verbose { get; set; }

        [JsonPropertyName("dxvk_hud")]
        public bool DxvkHud { get; set; }

        [JsonPropertyName("fps_limit")]
        public string FpsLimit { get; set; }

        [JsonPropertyName("graphics_api")]
        public string GraphicsApi { get; set; }

        [JsonPropertyName("fullscreen")]
        public bool Fullscreen { get; set; }

        [JsonPropertyName("image")]
        public string ImagePath { get; set; }

        [JsonIgnore]
        public bool IsAddNew { get; internal set; }

        [JsonPropertyName("default")]
        public bool IsDefault { get; set; }

        private ImageSource? _image;
        [JsonIgnore]
        public ImageSource? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    OnPropertyChanged(nameof(Image));
                }
            }
        }
    }

}
