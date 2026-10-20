using UnityEngine;
namespace WeaponWorkshop
{
    /// <summary>은퇴 기준 판정·문구. 기준값은 RetirementData.json 테이블의 단일 출처이다.</summary>
    public static class RetirementInfo
    {
        public static RetirementData.Rule Rule => TableManager.Instance.Retirement?.rule;
        public static int Cost => Rule != null ? Rule.cost : int.MaxValue;
        public static void Validate(RetirementData table)
        {
            if (table == null) { Debug.LogError("은퇴 기준 테이블이 올바르지 않습니다."); return; }
            if (table.rule == null || table.rule.cost < 0 || table.rule.satisfiedSales < 0 || table.rule.lifetimeRevenue < 0)
            { Debug.LogError("은퇴 기준 테이블이 올바르지 않습니다."); table.rule = null; }
        }
        public static bool Meets(GameSaveController save, PlayerWallet wallet)
        {
            var rule = Rule;
            return rule != null && save != null && !save.IsRetired && wallet != null && wallet.CanAfford(rule.cost) &&
                ((save.RetirementLegacy && rule.legacySaveCostOnly) ||
                (save.LifetimeSatisfiedSales >= rule.satisfiedSales && save.LifetimeSaleRevenue >= rule.lifetimeRevenue));
        }
        private static string Line(string label, int value, int required, bool money = false)
        {
            string unit = money ? "$" : "";
            string text = $"{label} {unit}{value:N0}/{unit}{required:N0}";
            return value < required ? "<color=#FF6666>" + text + "</color>" : text;
        }
        public static string Describe(GameSaveController save, PlayerWallet wallet)
        {
            var rule = Rule;
            if (rule == null || save == null || wallet == null) return "은퇴 정보를 불러올 수 없습니다.";
            string text = Line("만족 판매", save.LifetimeSatisfiedSales, rule.satisfiedSales) + "\n" +
                Line("누적 매출", save.LifetimeSaleRevenue, rule.lifetimeRevenue, true) + "\n" + Line("잔액", wallet.Balance, rule.cost, true);
            return save.RetirementLegacy && rule.legacySaveCostOnly ? text + "\n레거시 세이브: 잔액 조건만 적용" : text;
        }
    }
}
