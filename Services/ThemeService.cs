using Application = System.Windows.Application;

namespace fffrontend.Services
{
    public class ThemeService
    {
        private readonly List<string> _themes;

        public ThemeService(List<string> themes)
        {
            _themes = themes;
        }

        public void Apply(string theme)
        {
            var app = Application.Current;

            if (!string.IsNullOrEmpty(theme) && _themes.Contains(theme))
            {
                ApplySet(app, theme);
                return;
            }

            ApplySet(app, "FusionFall");
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
    }
}
