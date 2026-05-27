using System.Windows;
using Brush = System.Windows.Media.Brush;

namespace fffrontend
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

        public void InitializeSize(double targetWidth)
        {
            Width = targetWidth;
            SizeToContent = SizeToContent.Height;

            Root.Width = targetWidth;

            MessageText.MaxWidth = targetWidth - Root.Padding.Left - Root.Padding.Right;
        }
    }
}
