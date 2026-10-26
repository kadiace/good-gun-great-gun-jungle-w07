using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class ShopBusinessTimerUI : MonoBehaviour
    {
        private Slider progress;
        private TMP_Text remainingText;
        private GameObject display;
        private _DayManager dayManager;

        private void Awake()
        {
            dayManager = FindFirstObjectByType<_DayManager>();
            if (dayManager == null) return;
            CreateDisplay();
            SetVisible(false);
        }

        private void CreateDisplay()
        {
            display = new GameObject("ShopBusinessTimer", typeof(RectTransform));
            var rect = display.GetComponent<RectTransform>();
            // 팝업·전환 화면 아래에 그리도록 HUD 레이어에 둔다. 무기 이름·프리셋 패널은 이 높이만큼 아래에 있다.
            UIRoot uiRoot = GetComponent<UIRoot>();
            rect.SetParent(uiRoot != null && uiRoot.HudLayer != null ? uiRoot.HudLayer : transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            rect.sizeDelta = new Vector2(260f, 30f);

            var bar = new GameObject("RemainingTime", typeof(RectTransform), typeof(Image), typeof(Slider));
            var barRect = bar.GetComponent<RectTransform>();
            barRect.SetParent(rect, false);
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 6f);
            bar.GetComponent<Image>().color = new Color(0.11f, 0.13f, 0.15f, 0.85f);
            bar.GetComponent<Image>().raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(barRect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);
            fill.GetComponent<Image>().color = new Color(0.91f, 0.72f, 0.34f);
            fill.GetComponent<Image>().raycastTarget = false;
            progress = bar.GetComponent<Slider>();
            progress.fillRect = fillRect;
            progress.direction = Slider.Direction.LeftToRight;
            progress.interactable = false;
            progress.value = 1f;

            var label = new GameObject("TimeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(0f, 22f);
            remainingText = label.GetComponent<TextMeshProUGUI>();
            remainingText.text = "03:00";
            remainingText.fontSize = 18f;
            remainingText.color = Color.white;
            remainingText.alignment = TextAlignmentOptions.Center;
            remainingText.raycastTarget = false;
        }

        private void Update()
        {
            bool visible = dayManager != null && dayManager.BusinessRunning;
            SetVisible(visible);
            if (!visible) return;
            float remaining = dayManager.BusinessRemaining;
            if (progress != null) progress.value = remaining / dayManager.BusinessDuration;
            int seconds = Mathf.CeilToInt(remaining);
            if (remainingText != null) remainingText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        }

        private void SetVisible(bool visible)
        {
            if (display != null && display.activeSelf != visible) display.SetActive(visible);
        }

        private void OnDisable() => SetVisible(false);
    }
}
