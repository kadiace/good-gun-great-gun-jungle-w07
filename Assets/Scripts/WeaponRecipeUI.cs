using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class WeaponRecipeUI : MonoBehaviour
    {
        [Serializable] public sealed class RecipeEntry
        {
            public WeaponTypeData tag;
            public TMP_Text title;
            public TagPillGraphic badge;
            public TMP_Text conditions;
        }
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private ModuleCatalog moduleCatalog;
        [SerializeField] private RecipeEntry[] recipes = new RecipeEntry[0];
        public bool IsOpen => panel != null && panel.activeSelf;
        public RectTransform TutorialButton => (IsOpen ? closeButton : openButton) != null ? (IsOpen ? closeButton : openButton).transform as RectTransform : null;
        private bool ownsDialogue;
        private void Awake()
        {
            panel.SetActive(false);
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }
        public void Open()
        {
            if (!TutorialManager.ActionAllowed("recipe")) return;
            if (IsOpen || GameManager.GameplayBlocked) return;
            foreach (var recipe in recipes)
            {
                if (recipe.tag == null) continue;
                recipe.title.text = recipe.tag.DisplayName;
                recipe.badge.color = recipe.tag.TagColor;
                var text = new StringBuilder();
                var orderedConditions = new OrderedCondition[recipe.tag.Conditions.Count];
                for (int i = 0; i < orderedConditions.Length; i++)
                    orderedConditions[i] = new OrderedCondition(recipe.tag.Conditions[i], i);
                Array.Sort(orderedConditions, CompareConditions);
                foreach (var ordered in orderedConditions)
                {
                    var condition = ordered.condition;
                    string source = condition.source == WeaponTypeData.ValueSource.Stat ? "" : " 모듈 개수";
                    string comparison = condition.comparison == WeaponTypeData.Comparison.AtLeast ? "이상" :
                        condition.comparison == WeaponTypeData.Comparison.AtMost ? "이하" : "미만";
                    text.Append("• ").Append(StatName(condition.stat)).Append(source).Append(' ')
                        .Append(condition.value.ToString("0.##")).Append(' ').Append(comparison).AppendLine();
                }
                recipe.conditions.text = text.Length > 0 ? text.ToString().TrimEnd() : "조건이 설정되지 않았습니다.";
            }
            ownsDialogue = GameManager.Instance != null;
            if (ownsDialogue) GameManager.Instance.SetDialogue(true);
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f;
        }
        public void Close()
        {
            bool inspected = IsOpen;
            if (panel != null) panel.SetActive(false);
            if (ownsDialogue && GameManager.Instance != null) GameManager.Instance.SetDialogue(false);
            ownsDialogue = false;
            if (inspected && TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("recipe-inspected");
        }
        private static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Penetration: return "관통력";
                case StatType.FireRate: return "연사력";
                case StatType.Explosion: return "폭발력";
                case StatType.Ricochet: return "반사 횟수";
                case StatType.Charge: return "차지";
                case StatType.Gravity: return "중력";
                case StatType.Heat: return "발열";
                case StatType.Spin: return "회전";
                case StatType.Water: return "물";
                case StatType.Homing: return "유도";
                case StatType.Shotgun: return "산탄";
                case StatType.Weight: return "무게";
                case StatType.Scope: return "스코프";
                case StatType.Speed: return "탄속 배율";
                case StatType.Color: return "컬러";
                case StatType.Spread: return "탄퍼짐";
                default: return stat.ToString();
            }
        }

        private int CompareConditions(OrderedCondition a, OrderedCondition b)
        {
            int order = ConditionOrder(a.condition.stat).CompareTo(ConditionOrder(b.condition.stat));
            return order != 0 ? order : a.originalIndex.CompareTo(b.originalIndex);
        }

        private int ConditionOrder(StatType stat)
        {
            if (stat == StatType.Weight) return -1;
            if (moduleCatalog != null)
                for (int moduleId = 1; moduleId <= moduleCatalog.Count; moduleId++)
                    if (moduleCatalog.TryGet(moduleId, out ModuleCatalog.Entry entry) &&
                        entry.data != null && entry.data.ModuleType == stat)
                        return moduleId;
            return int.MaxValue;
        }

        private readonly struct OrderedCondition
        {
            public readonly WeaponTypeData.Condition condition;
            public readonly int originalIndex;

            public OrderedCondition(WeaponTypeData.Condition condition, int originalIndex)
            {
                this.condition = condition;
                this.originalIndex = originalIndex;
            }
        }
    }
}
