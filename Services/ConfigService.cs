using fffrontend.Models;
using System.Collections.ObjectModel;

namespace fffrontend.Services
{
    public class ConfigService
    {
        public ObservableCollection<ServerConfig> BuildList(Dictionary<string, ServerConfig> configs)
        {
            var list = new ObservableCollection<ServerConfig>();
            var seen = new HashSet<string>();

            foreach (var cfg in configs.Values)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.Id))
                    continue;

                if (!seen.Add(cfg.Id))
                    continue;

                list.Add(cfg);
            }

            list.Add(new ServerConfig { Name = "+", IsAddNew = true });

            return list;
        }
    }
}
