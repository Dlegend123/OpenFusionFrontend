using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Brush = System.Windows.Media.Brush;

namespace fflauncher
{
    /// <summary>
    /// Interaction logic for ToastWindow.xaml
    /// </summary>
    public partial class ToastWindow : Window
    {
        public ToastWindow(string message, string title, Brush background, Brush foreground)
        {
            InitializeComponent();

            MessageText.Text = message;
            MessageText.Foreground = foreground;

            if (!string.IsNullOrEmpty(title))
            {
                TitleText.Text = title;
                TitleText.Visibility = Visibility.Visible;
                TitleText.Foreground = foreground;
            }

            Root.Background = background;
        }

        public void InitializeSize(double targetWidth, double targetHeight)
        {
            Width = targetWidth;
            Height = targetHeight;
            Root.Width = targetWidth;
            Root.Height = targetHeight;
            MessageText.MaxWidth = targetWidth - Root.Padding.Left - Root.Padding.Right;
        }
    }
}
