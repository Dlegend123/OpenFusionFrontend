using fflauncher.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace fflauncher.Services
{
    public class ImageCacheService
    {
        private class CachedImage
        {
            public ImageSource Image { get; set; }
            public DateTime LastWriteTime { get; set; }
            public long Length { get; set; }
        }

        private readonly Dictionary<string, CachedImage> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);

        public async Task LoadImageAsync(ServerConfig cfg, Logger logger, bool force = false)
        {
            if (cfg == null || cfg.IsAddNew) return;

            var path = cfg.ImagePath?.Trim();
            if (string.IsNullOrEmpty(path)) return;

            if (!_loading.Add(path) && !force)
                return;

            try
            {
                var fileInfo = new FileInfo(path);
                var key = $"{path}|{fileInfo.LastWriteTimeUtc.Ticks}|{fileInfo.Length}";

                if (_cache.TryGetValue(key, out var cached))
                {
                    cfg.Image = cached.Image;
                    return;
                }

                ImageSource? bmp = null;

                await Task.Run(() =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var frame = BitmapFrame.Create(stream, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.OnLoad);
                    frame.Freeze();
                    bmp = frame;
                });

                if (bmp != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _cache[key] = new CachedImage
                        {
                            Image = bmp,
                            LastWriteTime = fileInfo.LastWriteTimeUtc,
                            Length = fileInfo.Length
                        };

                        cfg.Image = bmp;
                    });
                }
            }
            finally
            {
                _loading.Remove(path);
            }
        }
    }
}
