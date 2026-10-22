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

        /// <summary>선택 순서에 해당하는 씬 이름을 반환한다. 배열 저장 순서에는 의존하지 않는다.</summary>
        public static string SceneAt(this RaidMapData table, int index)
        {
            if (table == null || table.regions == null || index < 0) return null;
            var region = Array.Find(table.regions, item => item != null && item.selectionOrder == index + 1);
            return region?.scene;
        }

        public static RaidMapData.Region Find(this RaidMapData table, string scene)
        {
            if (table == null || table.regions == null || string.IsNullOrEmpty(scene)) return null;
            var exact = Array.Find(table.regions, region => region != null && region.scene == scene);
            return exact ?? Array.Find(table.regions, region => region?.sceneAliases != null &&
                Array.IndexOf(region.sceneAliases, scene) >= 0);
        }

        /// <summary>표의 상속 행을 따라 실제 보상·비용·출몰 설정을 반환한다.</summary>
        public static RaidMapData.Region Settings(this RaidMapData.Region region, RaidMapData table = null)
        {
            table ??= TableManager.Instance.RaidMap;
            var current = region;
            var visited = new HashSet<string>();
            for (int depth = 0; current != null && !string.IsNullOrEmpty(current.inheritsFrom) && depth < 16; depth++)
            {
                if (!visited.Add(current.scene)) return null;
                current = table.Find(current.inheritsFrom);
            }
            return current != null && string.IsNullOrEmpty(current.inheritsFrom) ? current : null;
        }

        /// <summary>상속 행에서 진행·음악·기존 보상 해금에 사용할 기준 씬을 반환한다.</summary>
        public static string CanonicalScene(this RaidMapData.Region region, RaidMapData table = null) =>
            region.Settings(table)?.scene;

        /// <summary>입장 비용. 행이 비었거나 값이 음수면 0으로 본다.</summary>
        public static int EntryCost(this RaidMapData.Region region)
        {
            var settings = region.Settings();
            return settings != null && settings.entryCost > 0 ? settings.entryCost : 0;
        }

        /// <summary>지갑이 이 구역의 입장 비용을 낼 수 있는지 본다.</summary>
        public static bool CanAffordEntry(this RaidMapData.Region region, PlayerWallet wallet) =>
            wallet != null && wallet.CanAfford(region.EntryCost());

        /// <summary>보상 모듈 코드 목록. 행이나 보상이 비었으면 빈 배열이다.</summary>
        public static string[] ModuleCodes(this RaidMapData.Region region) =>
            region.Settings()?.reward?.moduleCodes ?? Array.Empty<string>();

        /// <summary>습격 보상으로 지정된 모듈만 초기 잠금 대상이다. 그 외 모듈은 기본 해금한다.</summary>
        public static bool RequiresUnlock(this RaidMapData table, string moduleCode)
        {
            if (table?.regions == null || string.IsNullOrEmpty(moduleCode)) return false;
            foreach (var region in table.regions)
                if (region?.reward?.moduleCodes != null && Array.IndexOf(region.reward.moduleCodes, moduleCode) >= 0)
                    return true;
            return false;
        }

        // 상속 행은 자기 reward가 없어 0이다. 클리어 기록이 실제 씬별로 남으므로 상속하면 보너스가 중복된다.
        public static float FirstClearSaleBonus(this RaidMapData.Region region) =>
            region?.reward != null ? Mathf.Max(0f, region.reward.firstClearSaleBonus) : 0f;

        /// <summary>지도 정보 패널용 설명. 모듈 획득 여부와 상자 수, 출몰 적을 행에서 계산한다.</summary>
        public static string Describe(this RaidMapData.Region region, ModuleCatalog catalog, PlayerModuleUnlocks unlocks, bool cleared)
        {
            if (region == null) return "이 구역의 정보가 아직 등록되지 않았습니다.";
            var settings = region.Settings();
            if (settings == null) return "이 구역의 설정을 찾을 수 없습니다.";
            var text = new StringBuilder(region.name).AppendLine();
            if (!cleared && region.FirstClearSaleBonus() > 0f)
                text.AppendLine().Append("<color=#F04D4D>●</color> 최초 클리어 보상: 상점 판매 시 고객 요구 조건 하나당 제작비 보너스 +")
                    .Append((region.FirstClearSaleBonus() * 100f).ToString("0.#")).AppendLine("%p");
            text.AppendLine().AppendLine("획득 가능한 모듈");
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
            int materialCrates = (settings.reward != null ? Mathf.Max(0, settings.reward.materialCrates) : 0) + acquiredCount;
            text.AppendLine().Append("모듈 상자  ").Append(remaining).AppendLine("개")
                .Append("재료 상자  ").Append(materialCrates).AppendLine("개").AppendLine().AppendLine("출몰 적");
            if (settings.tanks) text.AppendLine("• 탱크");
            if (settings.maximumReinforcedTanks > 0 || region.scene == "NightRaid_Millitary_base") text.AppendLine("• 강화 탱크");
            if (settings.helicopters) text.AppendLine("• 헬기");
            if (!settings.tanks && !settings.helicopters && settings.maximumReinforcedTanks <= 0) text.AppendLine("• 없음");
            int cost = settings.EntryCost();
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
                if (region != null && string.IsNullOrEmpty(region.inheritsFrom) && Array.IndexOf(region.ModuleCodes(), code) >= 0)
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
            var orders = new HashSet<int>();
            foreach (RaidMapData.Region region in table.regions)
            {
                if (region == null) { Debug.LogWarning($"스테이지 테이블 {resourcePath}에 빈 항목이 있습니다."); continue; }
                if (string.IsNullOrEmpty(region.scene)) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 씬 이름이 없습니다.");
                else if (!scenes.Add(region.scene)) Debug.LogWarning($"스테이지 테이블 {resourcePath}에서 씬 이름 {region.scene}이 중복됩니다.");
                if (region.selectionOrder <= 0 || !orders.Add(region.selectionOrder))
                    Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 선택 순서가 비었거나 중복됩니다.");
                if (region.sceneAliases != null)
                    foreach (string alias in region.sceneAliases)
                        if (string.IsNullOrEmpty(alias) || !scenes.Add(alias))
                            Debug.LogWarning($"스테이지 테이블 {resourcePath}의 씬 별칭 '{alias}'이 비거나 중복됩니다.");
                if (string.IsNullOrEmpty(region.name)) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 {region.scene} 항목에 표시 이름이 없습니다.");
                var settings = region.Settings(table);
                if (settings == null) { Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 설정 상속 대상을 찾을 수 없습니다."); continue; }
                if (region.entryCost < 0) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 입장 비용이 음수입니다.");
                if (settings.reward == null) { Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 보상(reward)이 없습니다."); settings.reward = new RaidMapData.Reward(); }
                if (settings.reward.moduleCodes == null) settings.reward.moduleCodes = Array.Empty<string>();
                if (settings.reward.dollarsPerMaterialPickup < 0) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 재화 1회당 달러가 음수입니다.");
                if (settings.reward.firstClearSaleBonus < 0f) Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 최초 클리어 판매 보너스가 음수입니다.");
                if (settings.reward.materialPerCrate <= 0 && (settings.reward.materialCrates > 0 || settings.reward.moduleCodes.Length > 0))
                    Debug.LogWarning($"스테이지 테이블 {resourcePath}의 '{region.name}' 항목에 상자당 재료(materialPerCrate)가 0 이하입니다.");
            }
        }
    }
}
