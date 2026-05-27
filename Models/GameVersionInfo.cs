using System.Collections.ObjectModel;

namespace fffrontend.Models
{
    public class GameVersionInfo
    {
        public string Uuid { get; set; } = string.Empty;
        public string AssetUrl { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentUuid { get; set; }
        public bool Hidden { get; set; }
        public string MainFileUrl { get; set; } = string.Empty;

        public static readonly ObservableCollection<GameVersionInfo> DefaultVersions = new()
        {
            new GameVersionInfo
            {
                Uuid = "6543a2bb-d154-4087-b9ee-3c8aa778580a",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20111013",
                Name = "beta-20111013",
                ParentUuid = "bc988161-b425-4b50-88c6-4c9947cb73ea",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20111013/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "bc988161-b425-4b50-88c6-4c9947cb73ea",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110912",
                Name = "beta-20110912",
                ParentUuid = "d1ccb15f-bbfe-4cf7-9c25-84d0a0f759da",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110912/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "d1ccb15f-bbfe-4cf7-9c25-84d0a0f759da",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110818",
                Name = "beta-20110818",
                ParentUuid = "f7e6c55c-5dfc-404f-b0d5-14f3a1fa791c",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110818/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "f7e6c55c-5dfc-404f-b0d5-14f3a1fa791c",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110725",
                Name = "beta-20110725",
                ParentUuid = "b9d9c8ee-0a73-4d4c-b5e3-fa1d25213870",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110725/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "b9d9c8ee-0a73-4d4c-b5e3-fa1d25213870",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110523",
                Name = "beta-20110523",
                ParentUuid = "232f6405-9eec-4cbc-a4d7-e1003446474a",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110523/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "232f6405-9eec-4cbc-a4d7-e1003446474a",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110424",
                Name = "beta-20110424",
                ParentUuid = "7ce68356-872f-427d-9935-581f24d93bff",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110424/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "7ce68356-872f-427d-9935-581f24d93bff",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110330",
                Name = "beta-20110330",
                ParentUuid = "1bb7cda3-c463-4856-b8bc-6918fc06f057",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110330/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "1bb7cda3-c463-4856-b8bc-6918fc06f057",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110314",
                Name = "beta-20110314",
                ParentUuid = "ff73017d-dea2-4a72-a23d-066944460c67",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110314/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "ff73017d-dea2-4a72-a23d-066944460c67",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110213",
                Name = "beta-20110213",
                ParentUuid = "f0d3541f-152d-42ef-816f-63272c392a2c",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20110213/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "f0d3541f-152d-42ef-816f-63272c392a2c",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101123",
                Name = "beta-20101123",
                ParentUuid = "bf5e00c8-29e8-4541-97f6-15ea26509cdd",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101123/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "bf5e00c8-29e8-4541-97f6-15ea26509cdd",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101028",
                Name = "beta-20101028",
                ParentUuid = "61b8e04a-4c75-4f41-b0a8-65eb9ba9ca60",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101028/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "61b8e04a-4c75-4f41-b0a8-65eb9ba9ca60",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101011",
                Name = "beta-20101011",
                ParentUuid = "2a2be828-bd05-42a6-9b33-b837f2766773",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101011/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "2a2be828-bd05-42a6-9b33-b837f2766773",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101003",
                Name = "beta-20101003",
                ParentUuid = "86c57a2c-ce46-4429-b5b5-3936fda8e7e9",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20101003/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "86c57a2c-ce46-4429-b5b5-3936fda8e7e9",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100909",
                Name = "beta-20100909",
                ParentUuid = "ec1bd459-9f07-4c2e-9073-99cd7d2062be",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100909/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "ec1bd459-9f07-4c2e-9073-99cd7d2062be",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100728",
                Name = "beta-20100728",
                ParentUuid = "72a5c13a-bdcf-4b23-acc7-c95d9e9c72bb",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100728/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "72a5c13a-bdcf-4b23-acc7-c95d9e9c72bb",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100711",
                Name = "beta-20100711",
                ParentUuid = "967240a1-8047-4de3-a070-06276faf58c3",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100711/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "967240a1-8047-4de3-a070-06276faf58c3",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100616",
                Name = "beta-20100616",
                ParentUuid = "83797b94-f137-4637-b376-38c9b6ed70b2",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100616/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "83797b94-f137-4637-b376-38c9b6ed70b2",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100604",
                Name = "beta-20100604",
                ParentUuid = "c5981846-a3e0-4598-a6a4-1b0eaf9dba2c",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100604/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "c5981846-a3e0-4598-a6a4-1b0eaf9dba2c",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100524",
                Name = "beta-20100524",
                ParentUuid = "23bbdbe5-8639-4f13-8dee-1e9ff72102ac",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100524/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "23bbdbe5-8639-4f13-8dee-1e9ff72102ac",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100502",
                Name = "beta-20100502",
                ParentUuid = "7d79b3d1-1e20-4d25-ba03-f2d4a6cf8cf5",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100502/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "7d79b3d1-1e20-4d25-ba03-f2d4a6cf8cf5",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100413",
                Name = "beta-20100413",
                ParentUuid = "908a5fec-5723-4c4a-af51-b268b8904776",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100413/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "908a5fec-5723-4c4a-af51-b268b8904776",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100322",
                Name = "beta-20100322",
                ParentUuid = "b3d94727-906d-4d94-ab8b-2360bf15cca1",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100322/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "b3d94727-906d-4d94-ab8b-2360bf15cca1",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100307",
                Name = "beta-20100307",
                ParentUuid = "3d18aab5-95a5-49cb-8cde-ddcf809ad623",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100307/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "3d18aab5-95a5-49cb-8cde-ddcf809ad623",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100207",
                Name = "beta-20100207",
                ParentUuid = "793ed605-9a36-490c-95e1-6d1df8d7568b",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100207/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "793ed605-9a36-490c-95e1-6d1df8d7568b",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100119",
                Name = "beta-20100119",
                ParentUuid = "ec8063b2-54d4-4ee1-8d9e-381f5babd420",
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100119/main.unity3d"
            },
            new GameVersionInfo
            {
                Uuid = "ec8063b2-54d4-4ee1-8d9e-381f5babd420",
                AssetUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100104",
                Name = "beta-20100104",
                ParentUuid = null,
                Hidden = false,
                MainFileUrl = "https://cdn.dexlabs.systems/ff/big/beta-20100104/main.unity3d"
            }
        };
    }
}
