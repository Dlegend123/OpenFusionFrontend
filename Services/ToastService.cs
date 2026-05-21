using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;
namespace fflauncher.Services
{
    public class ToastService
    {
        public enum ToastType
        {
            Info,
            Success,
            Warning,
            Error
        }
        public static readonly Brush ToastInfoBackground = Brushes.DimGray;
        public static readonly Brush ToastSuccessBackground = CreateFrozenBrush(Color.FromRgb(56, 162, 116));
        public static readonly Brush ToastWarningBackground = CreateFrozenBrush(Color.FromRgb(222, 176, 18));
        public static readonly Brush ToastErrorBackground = CreateFrozenBrush(Color.FromRgb(215, 86, 58));
        public static readonly Brush ToastForeground = Brushes.White;

        public static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        public void Show(string message, ToastType type, string title = "")
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
                    background = Application.Current?.TryFindResource("AccentColor") as Brush ?? ToastSuccessBackground;
                    break;

                default:
                    background = Application.Current?.TryFindResource("AccentColor") as Brush ?? ToastInfoBackground;
                    break;
            }

            var toast = new ToastWindow(message, title, background, ToastForeground);
            toast.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                toast.Close();
            };
            timer.Start();
        }

    }
}
