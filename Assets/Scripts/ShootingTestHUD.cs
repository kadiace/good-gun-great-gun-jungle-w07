using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(UIManager))]
    public sealed class ShootingTestHUD : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        private RectTransform panel;
        private TextMeshProUGUI title, timer, details, retry;
        private float completedAt;
        public ShootingTestController Controller { get; private set; }
        private void Awake() { Build(); Controller = null; panel.gameObject.SetActive(false); }
        private void Build()
        {
            if (panel != null) return;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            panel = new GameObject("ShootingTestHUD", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, 1f);
            panel.anchoredPosition = new Vector2(0f, -64f);
            panel.sizeDelta = new Vector2(700f, 340f);
            var background = panel.GetComponent<Image>();
            background.color = new Color(.025f, .04f, .065f, .88f); background.raycastTarget = false;
            title = Label("Title", 16f, 25f, 30f);
            timer = Label("Timer", 49f, 27f, 32f);
            details = Label("Details", 89f, 17f, 185f);
            retry = Label("Retry", 282f, 18f, 44f);
            retry.color = new Color(1f, .85f, .25f);
            panel.gameObject.SetActive(false);
        }
        private TextMeshProUGUI Label(string labelName, float top, float size, float height)
        {
            var label = new GameObject(labelName, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(panel, false); label.font = font; label.fontSize = size + 4f;
            label.alignment = TextAlignmentOptions.TopLeft; label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(-32f, height); rect.anchoredPosition = new Vector2(0f, -top);
            return label;
        }
        public void Bind(ShootingTestController controller) { Build(); Controller = controller; completedAt = -1f; }
        public void Unbind(ShootingTestController controller)
        {
            if (Controller != controller) return;
            Controller = null; if (panel != null) panel.gameObject.SetActive(false);
        }
        public bool CloseRetry()
        {
            if (Controller == null || Controller.State != ShootingTestController.TestState.Failed) return false;
            Controller.Cancel(); return true;
        }
        public void CancelTest() => Controller?.Cancel();
        private void LateUpdate()
        {
            bool visible = Controller != null && Controller.Definition != null &&
                Controller.State != ShootingTestController.TestState.Idle;
            if (visible && Controller.State == ShootingTestController.TestState.Passed)
            {
                if (completedAt < 0f) completedAt = Time.unscaledTime;
                if (Time.unscaledTime - completedAt > 4f) { Controller.Cancel(); visible = false; }
            }
            if (panel == null) return;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            title.text = Controller.Definition.DisplayName;
            bool failed = Controller.State == ShootingTestController.TestState.Failed;
            timer.text = (failed ? "다음 시도 " : "남은 시간 ") + Controller.RemainingSeconds.ToString("0.0") + "초";
            timer.color = !failed && Controller.RemainingSeconds <= 5f ? new Color(1f, .35f, .25f) : Color.white;
            details.text = Controller.StatusText;
            if (Controller.NonForwardMuzzles > 0)
                details.text += "\n* 현재 일부 총구가 전방을 향해 있지 않습니다 (" + Controller.NonForwardMuzzles + "개).";
            retry.text = failed ? (Controller.CanRetryWithWeapon ? "F: 같은 테스트 즉시 재시도 · Esc: 종료" :
                Controller.CanRetryNearby ? "테스트할 총을 손에 든 뒤 F: 재시도 · Esc: 종료" : "테이블 근처로 돌아와 F: 재시도 · Esc: 종료")
                : "모든 총구를 전방으로 정렬하세요. 붉은 보호 표적은 파괴하면 실패합니다.";
        }
        private void OnDisable() { CancelTest(); if (panel != null) panel.gameObject.SetActive(false); }
    }
}
