using System;

namespace WeaponWorkshop
{
    // Request definitions come from TableManager. Module pricing/unlocks keep their existing API.
    public static class CustomerModuleRequirements
    {
        // 고정 손님은 주문 표의 unlockDay(기본 0)부터 받는다. 무한 모드 조합은 생성 때 손님 풀 unlockDay로 이미 거른다.
        public static bool Allows(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core, int day)
            => profile != null && profile.UnlockDay <= day && Allows(profile.RequestIds, unlocks, core);
        // 필요한 모듈 해금·사격 테스트는 요청 조건에서 계산한다.
        public static bool Allows(string[] ids, PlayerModuleUnlocks unlocks, WeaponStats core)
        {
            var table = TableManager.Instance.CustomerRequest;
            if (!table.ContainsOrder(ids)) return false;
            foreach (string id in ids)
            {
                var row = table.FindRequest(id);
                foreach (string testId in row.requiredTestIds ?? Array.Empty<string>())
                    if (!TableManager.Instance.ShootingTestTable.IsAvailable(testId)) return false;
                foreach (var condition in table.Conditions(row))
                {
                    if (condition.min <= 0f) continue;
                    switch (condition.Measure)
                    {
                        case GunMeasure.SplitTwoBlocks:
                            if (unlocks == null || !unlocks.IsUnlocked(ModuleKind.SplitTwo)) return false;
                            break;
                        case GunMeasure.SplitFourBlocks:
                            if (unlocks == null || !unlocks.IsUnlocked(ModuleKind.SplitFour)) return false;
                            break;
                        case GunMeasure.TotalModules:
                            if (unlocks == null || !unlocks.HasUnlockedStatModule()) return false;
                            break;
                        case GunMeasure.Muzzles:
                            if (condition.min > 6 && (unlocks == null ||
                                (!unlocks.IsUnlocked(ModuleKind.SplitTwo) && !unlocks.IsUnlocked(ModuleKind.SplitFour)))) return false;
                            break;
                        default:
                            if (condition.Measure != GunMeasure.PathModules && condition.min <= core.Get(condition.Stat)) break;
                            if (condition.Stat == StatType.Weight)
                            { if (unlocks == null || !unlocks.HasUnlockedStatModule()) return false; }
                            else if (unlocks == null || !ModuleCounter.TryToKind(condition.Stat, out var kind) || !unlocks.IsUnlocked(kind)) return false;
                            break;
                    }
                }
            }
            return true;
        }
        public static int CreditCost(CustomerProfile profile, PlayerModuleUnlocks unlocks, WeaponStats core) => 0;
        public static int CreditCost(string[] ids, PlayerModuleUnlocks unlocks, WeaponStats core) => 0;
    }
}
