using System.IO;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace fffrontend
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, System.Windows.StartupEventArgs e)
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.log");
            var logger = new Logger(logPath);
            logger.Clear();

            MainWindow mainWindow = new MainWindow(logger);
            mainWindow.Show();
        }
    }

}
