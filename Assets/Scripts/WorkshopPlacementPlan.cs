using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 순서가 있는 경로를 가상 조립에 적용한다. 실제 설치·구매는 CubeBuilder의 단일 트랜잭션이다.
    internal sealed class WorkshopPlacementPlan
    {
        internal struct Part
        {
            public Vector3Int cell, direction, feed;
            public int slot, dollars, credits;
        }
        public readonly List<Part> Parts = new();
        public bool Valid { get; private set; }
        public string Error { get; private set; }
        public int Dollars { get; private set; }
        public int Credits { get; private set; }
        public int CornerExtensions { get; private set; }
        public Vector3Int? InvalidCell { get; private set; }

        public void Build(CubeAssembly assembly, IReadOnlyList<Vector3Int> path, int slot,
            Vector3Int normal, Vector3Int horizontal, PlayerWallet wallet, System.Func<int, bool> isLocked)
        {
            Parts.Clear(); Valid = false; Error = ""; InvalidCell = null; Dollars = Credits = 0;
            CornerExtensions = 0;
            if (assembly == null || assembly.GetPrefab(slot) == null) { Error = "모듈을 선택하세요"; return; }
            var state = new CubePlacementState(assembly);
            var prefabBlock = assembly.GetPrefab(slot).GetComponent<PlacedBlock>();
            bool sight = prefabBlock != null && prefabBlock.IsSight;
            bool trigger = assembly.IsTriggerModuleSlot(slot);
            bool followsFeed = slot != 1 && !sight && !trigger;
            int shotgunCount = WeaponPurchasePrices.InstalledShotgunCount(assembly);
            long dollars = 0, credits = 0;
            for (int i = 0; i < path.Count; i++)
            {
                var cell = path[i];
                // 기존 모듈을 출발점/경유점으로 사용할 수 있지만 덮어쓰거나 과금하지 않는다.
                if (state.Occupied(cell)) continue;
                InvalidCell = cell;
                if (!state.TryGetNeighbor(cell, out var feed))
                { Error = "인접한 모듈이 없습니다"; return; }
                if (followsFeed && !state.TryGetFeed(cell, out feed))
                { Error = "이 셀에 에너지를 공급하는 총구가 없습니다"; return; }
                Vector3Int desired = i + 1 < path.Count ? path[i + 1] - cell :
                    (CubePlacementState.Dot(feed, normal) == 0 ? feed : horizontal);
                if (desired.sqrMagnitude != 1) desired = horizontal;
                // 명시적인 다음 경로가 입력 진행 방향에 수직이면 그 셀을 연장으로 바꾼다.
                // 출발 셀도 실제 공급면을 기준으로 판단하므로 다른 층에서 들어오는 꺾임을 포함한다.
                // 마지막 셀의 임의 출력 선택, 기본 블럭, 스코프/트리거는 이 변환의 대상이 아니다.
                bool corner = followsFeed && i + 1 < path.Count && CubePlacementState.Dot(feed, desired) == 0;
                int partSlot = corner ? 2 : slot;
                var partPrefab = assembly.GetPrefab(partSlot);
                if (partPrefab == null || (isLocked != null && isLocked(partSlot)))
                { Error = "배치할 모듈 또는 꺾임용 연장 모듈이 해금되지 않았습니다"; return; }
                bool partSight = !corner && sight;
                bool partTrigger = !corner && trigger;
                if (!state.CanAttach(cell, partTrigger))
                { Error = "인접 연결 또는 설치 개수 조건을 만족하지 않습니다"; return; }
                if (i + 1 == path.Count && partSlot == 3 && !state.CanOrient(cell, partSlot, desired, feed, partSight))
                    foreach (var candidate in CubePlacementState.Directions)
                        if (CubePlacementState.Dot(candidate, normal) == 0 &&
                            !state.Occupied(cell + candidate) && !state.Occupied(cell - candidate) &&
                            state.CanOrient(cell, partSlot, candidate, feed, partSight)) { desired = candidate; break; }
                if (!state.CanOrient(cell, partSlot, desired, feed, partSight))
                { Error = "경로의 출력 방향이 기존 출력과 충돌합니다"; return; }
                // 연장/2분할은 기존 2D 방향 선택과 같은 빈 출력면 조건을 사용한다.
                if ((partSlot == 2 || partSlot == 3) && (state.Occupied(cell + desired) ||
                    (partSlot == 3 && state.Occupied(cell - desired))))
                { Error = "출력 방향이 점유되어 있습니다"; return; }
                if (partSlot == 4 && i + 1 < path.Count &&
                    System.Array.IndexOf(CubePlacementState.Outputs(partSlot, desired, feed, false), path[i + 1] - cell) < 0)
                { Error = "4방향 분할의 출력 평면으로 경로를 이어주세요"; return; }
                if (TutorialManager.Instance != null && !TutorialManager.Instance.AllowsPlacement(assembly, cell, partSlot, desired))
                { Error = "현재 안내 단계에서 허용되지 않는 배치입니다"; return; }
                var data = assembly.GetModuleData(partSlot);
                int price = data != null ? Mathf.Max(0, data.BuyPrice) : WeaponPurchasePrices.Module;
                int material = WeaponPurchasePrices.ModuleCredits(data, shotgunCount);
                dollars += price; credits += material;
                if (dollars > int.MaxValue || credits > int.MaxValue)
                { Error = "배치 비용이 허용 범위를 초과합니다"; return; }
                Parts.Add(new Part { cell = cell, slot = partSlot, direction = desired, feed = feed, dollars = price, credits = material });
                state.Add(cell, partPrefab, partSlot, desired, feed, partSight, partTrigger);
                if (corner && slot != 2) CornerExtensions++;
                if (data != null && data.ModuleType == StatType.Shotgun) shotgunCount++;
            }
            Dollars = (int)dollars; Credits = (int)credits; InvalidCell = null;
            if (Parts.Count == 0) { Error = "새로 설치할 셀이 없습니다"; return; }
            if (wallet == null || !wallet.CanAfford(Dollars) || !wallet.CanAffordModuleCredits(Credits))
            { Error = "설치 비용 또는 모듈 재료가 부족합니다"; return; }
            Valid = true;
        }
    }
}
