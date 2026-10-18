using UnityEngine;
using UnityEngine.Serialization;

using TMPro;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class JungleEndChestNotice : MonoBehaviour
    {
        private const int MAX_DISPLAY_COUNT = 5;
        private const float DISPLAY_DURATION = 2f;
        private const float COOLDOWN_DURATION = 3f;

        [FormerlySerializedAs("chest"), SerializeField] private EndChest _chest;
        [SerializeField] private TMP_FontAsset _fontAsset;

        private Canvas _noticeCanvas;
        private CanvasGroup _noticeGroup;
        private GraphicRaycaster _noticeRaycaster;
        private float _displayElapsed;
        private float _cooldownRemaining;
        private int _displayCount;
        private bool _displaying;

        private void Awake()
        {
            CreateNoticeCanvas();
            Hide(false);
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.unscaledDeltaTime);
            if (!_displaying) return;
            if (_chest == null || !_chest.CanShowNotice()) { Hide(true); return; }
            _displayElapsed += Time.unscaledDeltaTime;
            if (_displayElapsed >= DISPLAY_DURATION) Hide(true);
        }

        /// <summary>Resets the notice count and timers when a new raid scene instance begins.</summary>
        public void ResetForRaid()
        {
            _displayCount = 0;
            _displayElapsed = 0f;
            _cooldownRemaining = 0f;
            Hide(false);
        }

        /// <summary>Shows the fixed notice once for an eligible hit without extending an active display.</summary>
        public void ShowBlockedHit()
        {
            if (_displaying || _displayCount >= MAX_DISPLAY_COUNT || _cooldownRemaining > 0f ||
                _chest == null || !_chest.CanShowNotice()) return;
            _displayCount++;
            _displayElapsed = 0f;
            _displaying = true;
            _noticeCanvas.gameObject.SetActive(true);
            _noticeCanvas.enabled = true;
            _noticeRaycaster.enabled = false;
            _noticeGroup.alpha = 1f;
        }

        /// <summary>Hides the notice when the chest opens or its current hit state no longer qualifies.</summary>
        public void Dismiss() => Hide(true);

        /// <summary>Creates the backgroundless notice and sets its explicit hidden start state.</summary>
        private void CreateNoticeCanvas()
        {
            GameObject canvasObject = new GameObject("JungleEndChestNotice", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            _noticeCanvas = canvasObject.GetComponent<Canvas>();
            _noticeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _noticeCanvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _noticeGroup = canvasObject.GetComponent<CanvasGroup>();
            _noticeRaycaster = canvasObject.GetComponent<GraphicRaycaster>();
            RectTransform rect = canvasObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, 72f);
            textRect.sizeDelta = new Vector2(1400f, 52f);
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = _fontAsset;
            text.fontSize = 24f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.text = "우주의 힘을 담은 코어가 총알을 막아내고있습니다...";
            text.raycastTarget = false;
            canvasObject.SetActive(false);
        }

        /// <summary>Hides the notice and optionally starts its three-second cooldown.</summary>
        /// <param name="startCooldown">True when the message ends and its cooldown should begin.</param>
        private void Hide(bool startCooldown)
        {
            _displaying = false;
            _displayElapsed = 0f;
            if (_noticeGroup != null) _noticeGroup.alpha = 0f;
            if (_noticeRaycaster != null) _noticeRaycaster.enabled = false;
            if (_noticeCanvas != null)
            {
                _noticeCanvas.enabled = false;
                _noticeCanvas.gameObject.SetActive(false);
            }
            if (startCooldown) _cooldownRemaining = COOLDOWN_DURATION;
        }
    }
}
