using Microsoft.Win32;
using System.Collections;
using System.IO;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace fflauncher
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

            //Task.Run(() =>
            //{
            //    // Retrieve and log all environment variables
            //    IDictionary envVars = Environment.GetEnvironmentVariables();
            //    var envLog = "\n\tEnvironment Variables:";

            //    foreach (DictionaryEntry entry in envVars)
            //    {
            //        envLog += $"\n\t\t{entry.Key}={entry.Value}";
            //    }

            //    logger.Log(envLog);
            //} );

            MainWindow mainWindow = new MainWindow(logger);
            mainWindow.Show();
        }
    }

}
