using System;
using System.Text;
using UnityEngine;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class RaidMapData
    {
        [Serializable]
        public sealed class Region
        {
            public string scene;
            public string name;
            public int materialCrates;
            public string[] moduleCodes;
            public bool helicopters;
            public bool tanks;

            public string Describe(ModuleCatalog catalog, PlayerModuleUnlocks unlocks)
            {
                var text = new StringBuilder(name).AppendLine().AppendLine("획득 가능한 모듈");
                int remaining = 0;
                foreach (string code in moduleCodes)
                    for (int id = 1; catalog != null && id <= catalog.Count; id++)
                        if (catalog.TryGet(id, out var entry) && entry.data.CodeId == code)
                        {
                            bool acquired = unlocks != null && unlocks.IsUnlocked(id);
                            if (!acquired) remaining++;
                            text.Append("• ").Append(entry.displayName).AppendLine(acquired ? "  · 획득 완료" : "  · 미획득");
                            break;
                        }
                if (moduleCodes.Length == 0) text.AppendLine("• 없음");
                text.AppendLine().Append("모듈 상자  ").Append(remaining).AppendLine("개")
                    .Append("재료 상자  ").Append(materialCrates).AppendLine("개").AppendLine().AppendLine("출몰 적");
                if (tanks) text.AppendLine("• 탱크");
                if (helicopters) text.AppendLine("• 헬기");
                if (!tanks && !helicopters) text.AppendLine("• 없음");
                return text.AppendLine().Append("지도에서 영역을 눌러 이동하세요.").ToString();
            }
        }
        public Region[] regions;
        private static RaidMapData cached;
        public static RaidMapData Current
        {
            get
            {
                if (cached == null)
                {
                    var asset = Resources.Load<TextAsset>("RaidMapData");
                    cached = asset != null ? JsonUtility.FromJson<RaidMapData>(asset.text) : new RaidMapData { regions = Array.Empty<Region>() };
                }
                return cached;
            }
        }
        public Region Find(string scene) => Array.Find(regions, region => region.scene == scene);
        public string Locations(ModuleData module)
        {
            var text = new StringBuilder();
            foreach (var region in regions)
                if (Array.IndexOf(region.moduleCodes, module.CodeId) >= 0)
                { if (text.Length > 0) text.Append(" · "); text.Append(region.name); }
            return text.Length > 0 ? "획득 장소: " + text : "획득 장소가 아직 등록되지 않았습니다.";
        }
    }
}
