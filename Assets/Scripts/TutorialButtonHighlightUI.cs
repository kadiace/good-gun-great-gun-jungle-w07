using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Reusable, non-interactive covers follow the actual button graphics without changing their colors.
    public sealed class TutorialButtonHighlightUI : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image cover;
        private readonly List<UnityEngine.UI.Image> covers = new List<UnityEngine.UI.Image>();
        private readonly List<RectTransform> targets = new List<RectTransform>();
        private void Awake()
        {
            if (cover == null) return;
            covers.Add(cover);
            cover.raycastTarget = false;
            cover.gameObject.SetActive(false);
        }
        /// <summary>
        /// 하이라이트할 UI 요소를 목록에 추가합니다.
        /// </summary>
        /// <param name="target">하이라이트할 UI RectTransform입니다.</param>
        private void Add(RectTransform target)
        {
            if (target != null && target.gameObject.activeInHierarchy && !targets.Contains(target)) targets.Add(target);
        }
        /// <summary>
        /// 매 프레임 튜토리얼 진행 상태와 안내 가이드에 따라 하이라이트할 대상 버튼들을 갱신하고 커버를 표시합니다.
        /// </summary>
        private void LateUpdate()
        {
            if (cover == null) return;
            targets.Clear();
            var manager = TutorialManager.Instance;
            var step = manager != null ? manager.CurrentStep : null;
            if (manager != null && manager.IsTutorialActive)
            {
                var selection = GetComponentInChildren<TutorialSkipUI>(true);
                var dialogue = GetComponentInChildren<DialogueUI>(true);
                var feedback = GetComponentInChildren<FeedbackUI>(true);
                var recipe = GetComponentInChildren<WeaponRecipeUI>(true);
                var presets = GetComponentInChildren<PresetPanelUI>(true);
                if (feedback != null && feedback.IsChoiceOpen && !feedback.IsChoiceInputSuspended &&
                    step != null && step.buttonGuide == TutorialButtonGuide.LoanConfirm)
                    Add(feedback.TutorialConfirmButton);
                else if ((selection != null && selection.IsOpen) || (dialogue != null && dialogue.IsOpen) ||
                    (feedback != null && feedback.IsChoiceOpen))
                {
                    // Ordinary choices and confirmation dialogue are not guided action buttons.
                }
                else
                {
                    if (presets != null && presets.IsOpen && step != null && step.buttonGuide == TutorialButtonGuide.PresetFirst &&
                        manager.RaidPresetSaveRequested && presets.SelectedSlot == 0)
                        Add(presets.TutorialSaveButton);
                    else if (presets != null && presets.IsOpen && step != null && step.buttonGuide == TutorialButtonGuide.PresetSecond && presets.SelectedSlot == 1)
                        Add(presets.TutorialLoadButton);
                    else if (step != null)
                    {
                        if (step.buttonGuide == TutorialButtonGuide.Recipe && manager.IsEditingTutorialWorkbench &&
                            !GameManager.GameplayBlocked && recipe != null && !recipe.IsOpen)
                            Add(recipe != null ? recipe.TutorialButton : null);
                        if (step.buttonGuide == TutorialButtonGuide.PresetFirst && manager.IsEditingTutorialWorkbench && !GameManager.GameplayBlocked)
                            Add(manager.RaidPresetSaveRequested && presets != null ? presets.TutorialFirstSlot : null);
                        if (step.buttonGuide == TutorialButtonGuide.PresetSecond && manager.IsEditingTutorialWorkbench && !GameManager.GameplayBlocked)
                            Add(presets != null ? presets.TutorialSecondSlot : null);
                        if (step.buttonGuide == TutorialButtonGuide.RaidHouse)
                        {
                            var raid = GetComponentInChildren<NightRaidUI>(true);
                            Add(raid != null ? raid.TutorialHouseButton : null);
                        }
                    }

                    if (step != null && step.keepRecipeHighlight && manager.IsEditingTutorialWorkbench &&
                        !GameManager.GameplayBlocked && recipe != null && !recipe.IsOpen &&
                        !IsWorkbenchWeaponTagSatisfied(manager, step))
                        Add(recipe.TutorialButton);
                }
            }
            while (covers.Count < targets.Count)
            {
                var copy = Instantiate(cover, transform);
                copy.name = "TutorialButtonCover";
                copy.raycastTarget = false;
                covers.Add(copy);
            }
            for (int i = 0; i < covers.Count; i++)
            {
                var image = covers[i];
                bool show = i < targets.Count;
                image.enabled = show;
                image.gameObject.SetActive(show);
                if (!show) { image.transform.SetParent(transform, false); continue; }
                Draw(image, targets[i]);
            }
        }
        /// <summary>
        /// 작업대 무기가 현재 튜토리얼 단계에서 요구하는 무기 태그를 만족하는지 확인합니다.
        /// </summary>
        /// <param name="manager">튜토리얼 매니저 인스턴스입니다.</param>
        /// <param name="step">요구 태그가 정의된 튜토리얼 단계입니다.</param>
        /// <returns>무기가 요구 태그 조건을 충족하면 true를 반환합니다.</returns>
        private static bool IsWorkbenchWeaponTagSatisfied(TutorialManager manager, TutorialStep step)
        {
            if (manager == null || step == null || string.IsNullOrEmpty(step.requiredTagId)) return false;
            var workbench = manager.TutorialWorkbench;
            var weapon = workbench != null ? workbench.CurrentWeapon : null;
            if (weapon == null) return false;
            var table = TableManager.Instance != null ? TableManager.Instance.CustomerRequest : null;
            return table != null && table.MeetsTag(step.requiredTagId, new CustomerRequestInfo.WeaponSnapshot(weapon));
        }
        private static void Draw(UnityEngine.UI.Image image, RectTransform target)
        {
            var button = target.GetComponent<UnityEngine.UI.Selectable>();
            var shape = button != null ? button.targetGraphic as UnityEngine.UI.Image : target.GetComponent<UnityEngine.UI.Image>();
            if (shape != null) target = shape.rectTransform;
            var rect = image.rectTransform;
            if (rect.parent != target) rect.SetParent(target, false);
            rect.SetAsLastSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            image.sprite = shape != null ? shape.sprite : null;
            image.type = shape != null ? shape.type : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = shape != null && shape.preserveAspect;
            image.fillCenter = shape == null || shape.fillCenter;
            image.pixelsPerUnitMultiplier = shape != null ? shape.pixelsPerUnitMultiplier : 1f;
            if (shape != null)
            {
                image.fillMethod = shape.fillMethod; image.fillOrigin = shape.fillOrigin;
                image.fillAmount = shape.fillAmount; image.fillClockwise = shape.fillClockwise;
            }
            image.color = new Color(1f, .82f, .12f, .12f + .22f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI)));
        }
        private void OnDisable()
        {
            foreach (var image in covers)
                // A button ancestor can be mid-deactivation here; do not change its hierarchy.
                if (image != null) image.enabled = false;
        }
    }
}