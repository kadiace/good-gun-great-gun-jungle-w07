using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 상점 판매 목록과 판매가 계산.
    // 판매가 = 모듈 구매가 합 × 판매 배율 × 태그 배율 합(태그가 없으면 noTagMultiplier).
    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "Weapon Workshop/Shop Catalog")]
    public sealed class ShopCatalog : ScriptableObject
    {
        [SerializeField] private ModuleData[] modules = new ModuleData[0];
        [SerializeField, Min(0f)] private float sellRatio = 0.5f;
        [SerializeField, Min(0f), Tooltip("총 종류 태그가 하나도 없을 때 쓰는 배율.")]
        private float noTagMultiplier = 1f;

        public IReadOnlyList<ModuleData> Modules => modules;
        public float SellRatio => sellRatio;

        public float GetTagMultiplier(IEnumerable<WeaponTypeData> tags)
        {
            float total = 0f;
            bool any = false;
            foreach (WeaponTypeData tag in tags)
            {
                if (tag == null) continue;
                total += tag.SellMultiplier;
                any = true;
            }
            return any ? total : noTagMultiplier;
        }

        public int GetSellPrice(IEnumerable<ModuleData> installedModules, IEnumerable<WeaponTypeData> tags)
        {
            int buyTotal = 0;
            foreach (ModuleData module in installedModules)
                if (module != null) buyTotal += module.BuyPrice;
            return Mathf.RoundToInt(buyTotal * sellRatio * GetTagMultiplier(tags));
        }

        public int GetSellPrice(StatCollector weapon)
            => weapon != null ? GetSellPrice(weapon.Modules, weapon.MatchedTypes) : 0;
    }
}
