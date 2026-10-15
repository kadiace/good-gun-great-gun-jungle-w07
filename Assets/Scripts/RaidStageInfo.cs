using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WeaponWorkshop
{
    // RaidMapData 테이블을 읽는 로직. 데이터 클래스는 직렬화된 필드만 갖고, 조회와 문구 생성은 이곳에 있다.
    // 호출측은 TableManager.Instance.RaidMap으로 테이블을 받아 확장 메서드로 읽는다.
    public static class RaidStageInfo
    {
        public static int Count(this RaidMapData table) => table != null && table.regions != null ? table.regions.Length : 0;

        public static string SceneAt(this RaidMapData table, int index) =>
            table != null && table.regions != null && index >= 0 && index < table.regions.Length ? table.regions[index].scene : null;

        public static RaidMapData.Region Find(this RaidMapData table, string scene)
        {
            if (table == null || table.regions == null || string.IsNullOrEmpty(scene)) return null;
            return Array.Find(table.regions, region => region != null && region.scene == scene);
        }

        /// <summary>입장 비용. 행이 비었거나 값이 음수면 0으로 본다.</summary>
        public static int EntryCost(this RaidMapData.Region region) =>
            region != null && region.entryCost > 0 ? region.entryCost : 0;

        /// <summary>지갑이 이 구역의 입장 비용을 낼 수 있는지 본다.</summary>
        public static bool CanAffordEntry(this RaidMapData.Region region, PlayerWallet wallet) =>
            wallet != null && wallet.CanAfford(region.EntryCost());

        /// <summary>보상 모듈 코드 목록. 행이나 보상이 비었으면 빈 배열이다.</summary>
        public static string[] ModuleCodes(this RaidMapData.Region region) =>
            region?.reward?.moduleCodes ?? Array.Empty<string>();

        /// <summary>지도 정보 패널용 설명. 모듈 획득 여부와 상자 수, 출몰 적을 행에서 계산한다.</summary>
        public static string Describe(this RaidMapData.Region region, ModuleCatalog catalog, PlayerModuleUnlocks unlocks)
        {
            if (region == null) return "이 구역의 정보가 아직 등록되지 않았습니다.";
            var text = new StringBuilder(region.name).AppendLine().AppendLine("획득 가능한 모듈");
            int remaining = 0, acquiredCount = 0;
            foreach (string code in region.ModuleCodes())
                for (int id = 1; catalog != null && id <= catalog.Count; id++)
                    if (catalog.TryGet(id, out var entry) && entry.data.CodeId == code)
                    {
                        bool acquired = unlocks != null && unlocks.IsUnlocked(id);
                        if (acquired) acquiredCount++; else remaining++;
                        text.Append("• ").Append(entry.displayName).AppendLine(acquired ? "  · 획득 완료" : "  · 미획득");
                        break;
                    }
            if (region.ModuleCodes().Length == 0) text.AppendLine("• 없음");
            // 이미 해금한 모듈 자리에는 재료 상자가 대신 나온다(RaidCrateSpawner).
            int materialCrates = (region.reward != null ? Mathf.Max(0, region.reward.materialCrates) : 0) + acquiredCount;
            text.AppendLine().Append("모듈 상자  ").Append(remaining).AppendLine("개")
                .Append("재료 상자  ").Append(materialCrates).AppendLine("개").AppendLine().AppendLine("출몰 적");
            if (region.tanks) text.AppendLine("• 탱크");
            if (region.helicopters) text.AppendLine("• 헬기");
            if (!region.tanks && !region.helicopters) text.AppendLine("• 없음");
            int cost = region.EntryCost();
            text.AppendLine().Append("입장 비용  ").AppendLine(cost > 0 ? $"$ {cost:N0}" : "무료");
            return text.AppendLine().Append("지도에서 영역을 눌러 이동하세요.").ToString();
        }

        /// <summary>잠긴 모듈을 어디에서 얻는지 목록으로 만든다.</summary>
        public static string Locations(this RaidMapData table, ModuleData module)
        {
            var text = new StringBuilder();
            if (table == null || table.regions == null || module == null) return "획득 장소가 아직 등록되지 않았습니다.";
            string code = module.CodeId;
            foreach (var region in table.regions)
                if (region != null && Array.IndexOf(region.ModuleCodes(), code) >= 0)
                {
                    if (text.Length > 0) text.Append(" · ");
                    text.Append(region.name);
                }
            return text.Length > 0 ? "획득 장소: " + text : "획득 장소가 아직 등록되지 않았습니다.";
        }

        // 로드 직후 한 번 호출한다. 씬 이름이 비거나 겹치면 지도 정보와 이동이 조용히 어긋난다.
        public static void Validate(RaidMapData table, string resourcePath)
        {
            if (table.regions == null)
            {
                Debug.LogError($"테이블 {resourcePath}에 행 목록이 없습니다. 빈 테이블로 동작합니다.");
                table.regions = Array.Empty<RaidMapData.Region>();
                return;
            }
            var scenes = new HashSet<string>();
            foreach (RaidMapData.Region region in table.regions)
            {
                if (region == null) { Debug.LogWarning($"스테이지 테이블 {resourcePath}에 빈 항목이 있습니다."); continue; }
                if (string.IsNullOrEmpty(region.scene)) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 씬 이름이 없습니다.");
                else if (!scenes.Add(region.scene)) Debug.LogWarning($"스테이지 테이블 {resourcePath}에서 씬 이름 {region.scene}이 중복됩니다.");
                if (string.IsNullOrEmpty(region.name)) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 {region.scene} 항목에 표시 이름이 없습니다.");
                if (region.entryCost < 0) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 입장 비용이 음수입니다.");
                if (region.reward == null) { Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 보상(reward)이 없습니다."); region.reward = new RaidMapData.Reward(); }
                if (region.reward.moduleCodes == null) region.reward.moduleCodes = Array.Empty<string>();
                if (region.reward.materialCrates < 0) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 재료 상자 수가 음수입니다.");
                if (region.reward.materialPerCrate <= 0 && (region.reward.materialCrates > 0 || region.reward.moduleCodes.Length > 0))
                    Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 상자당 재료(materialPerCrate)가 0 이하입니다.");
            }
        }
    }
}
