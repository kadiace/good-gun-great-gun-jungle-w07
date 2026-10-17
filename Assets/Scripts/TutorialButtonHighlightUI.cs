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
        private void Add(RectTransform target)
        {
            if (target != null && target.gameObject.activeInHierarchy && !targets.Contains(target)) targets.Add(target);
        }
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
                else if (presets != null && presets.IsOpen && step != null && step.buttonGuide == TutorialButtonGuide.PresetFirst)
                    Add(presets.TutorialSaveButton);
                else if (step != null)
                {
                    if (step.buttonGuide == TutorialButtonGuide.Recipe && manager.IsEditingTutorialWorkbench &&
                        !GameManager.GameplayBlocked && recipe != null && !recipe.IsOpen)
                        Add(recipe != null ? recipe.TutorialButton : null);
                    if (step.buttonGuide == TutorialButtonGuide.PresetFirst && manager.IsEditingTutorialWorkbench && !GameManager.GameplayBlocked)
                        Add(presets != null ? presets.TutorialFirstSlot : null);
                    if (step.buttonGuide == TutorialButtonGuide.RaidHouse)
                    {
                        var raid = GetComponentInChildren<NightRaidUI>(true);
                        Add(raid != null ? raid.TutorialHouseButton : null);
                    }
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
