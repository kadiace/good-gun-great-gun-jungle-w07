using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    // Authored NotificationText and YesNoUI are shared by all callers on this canvas.
    public sealed class FeedbackUI : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI notificationText;
        [SerializeField] private CanvasGroup notificationGroup;
        [SerializeField] private float enterDuration = .18f, holdDuration = 2f, exitDuration = .4f;
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private TMPro.TextMeshProUGUI choiceMessage;
        [SerializeField] private UnityEngine.UI.Button yesButton, noButton;
        [SerializeField] private InputActionAsset inputActions;
        private InputActionMap controls;
        private Action yes, no;
        private object owner;
        private float elapsed = -1f;
        private int openedFrame;
        private bool confirmRequested;
        private bool choiceInputSuspended;
        private RectTransform yesButtonRect;
        private Vector2 yesAnchorMin, yesAnchorMax;
        public bool IsChoiceOpen => owner != null;
        public bool IsChoiceInputSuspended => choiceInputSuspended;
        public RectTransform TutorialConfirmButton => yesButton != null && yesButton.interactable ? yesButton.transform as RectTransform : null;

        public static FeedbackUI For(Component caller)
        {
            var local = caller != null ? caller.GetComponentInParent<FeedbackUI>(true) : null;
            if (local != null) return local;
            return GameManager.Instance != null ? GameManager.Instance.UI?.Feedback : null;
        }
        private void Awake()
        {
            yesButtonRect = yesButton != null ? yesButton.transform as RectTransform : null;
            if (yesButtonRect != null)
            { yesAnchorMin = yesButtonRect.anchorMin; yesAnchorMax = yesButtonRect.anchorMax; }
            HideNotification();
            if (choicePanel != null) choicePanel.SetActive(false);
            if (yesButton != null) yesButton.onClick.AddListener(Confirm);
            if (noButton != null) noButton.onClick.AddListener(Cancel);
            if (inputActions != null)
            {
                controls = inputActions.FindActionMap("Workshop", true).Clone();
                controls.FindAction("Interact", true).performed += _ =>
                { if (IsChoiceOpen && !choiceInputSuspended && Time.frameCount > openedFrame) confirmRequested = true; };
            }
        }
        private void OnEnable() => controls?.Enable();
        private void OnDisable() { controls?.Disable(); Cancel(); HideNotification(); }
        private void OnDestroy()
        {
            controls?.Dispose();
            if (yesButton != null) yesButton.onClick.RemoveListener(Confirm);
            if (noButton != null) noButton.onClick.RemoveListener(Cancel);
        }
        public void ShowNotification(string message)
        {
            if (notificationText == null || notificationGroup == null) return;
            if (string.IsNullOrEmpty(message)) { HideNotification(); return; }
            notificationText.text = message;
            elapsed = 0f;
            notificationGroup.alpha = 0f;
            notificationGroup.transform.localScale = Vector3.one * .92f;
            notificationGroup.gameObject.SetActive(true);
            notificationGroup.transform.SetAsLastSibling();
        }
        private void HideNotification()
        {
            elapsed = -1f;
            if (notificationGroup == null) return;
            notificationGroup.alpha = 0f;
            notificationGroup.blocksRaycasts = notificationGroup.interactable = false;
            notificationGroup.transform.localScale = Vector3.one;
            notificationGroup.gameObject.SetActive(false);
        }
        public bool ShowChoice(object requester, string message, Action onYes, Action onNo = null,
            string yesLabel = "예", string noLabel = "아니오", bool canConfirm = true)
        {
            if (!isActiveAndEnabled || IsChoiceOpen || requester == null || choicePanel == null || choiceMessage == null || yesButton == null || noButton == null) return false;
            owner = requester; yes = onYes; no = onNo;
            choiceInputSuspended = false;
            openedFrame = Time.frameCount; confirmRequested = false;
            choiceMessage.text = message;
            yesButton.interactable = canConfirm;
            RestoreYesButtonPosition();
            noButton.gameObject.SetActive(true);
            yesButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = yesLabel;
            noButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = noLabel;
            choicePanel.SetActive(true);
            choicePanel.transform.SetAsLastSibling();
            GameManager.Instance?.RefreshInputState();
            return true;
        }
        public void SuspendChoiceInput()
        {
            if (!IsChoiceOpen) return;
            choiceInputSuspended = true;
            confirmRequested = false;
            if (yesButton != null) yesButton.interactable = false;
            if (noButton != null) noButton.interactable = false;
        }
        public void ResumeChoiceInput()
        {
            if (!IsChoiceOpen || !choiceInputSuspended) return;
            choiceInputSuspended = false;
            openedFrame = Time.frameCount;
            if (yesButton != null) yesButton.interactable = true;
            if (noButton != null) noButton.interactable = true;
        }
        /// <summary>
        /// 확인 문구와 단일 버튼 안내를 공용 선택창으로 표시한다.
        /// requester, message, buttonLabel을 사용하며 표시 성공 여부를 반환한다.
        /// </summary>
        public bool ShowAcknowledgement(object requester, string message, string buttonLabel = "확인했습니다")
        {
            if (!ShowChoice(requester, message, null, null, buttonLabel)) return false;

            noButton.gameObject.SetActive(false);
            if (yesButtonRect != null)
            {
                float anchorWidth = yesAnchorMax.x - yesAnchorMin.x;
                yesButtonRect.anchorMin = new Vector2(.5f - anchorWidth * .5f, yesAnchorMin.y);
                yesButtonRect.anchorMax = new Vector2(.5f + anchorWidth * .5f, yesAnchorMax.y);
            }

            return true;
        }
        public void CancelChoice(object requester) { if (ReferenceEquals(owner, requester)) Cancel(); }
        public void Cancel() => Complete(false);
        private void Confirm() { if (!choiceInputSuspended && yesButton != null && yesButton.interactable) Complete(true); }
        private void Complete(bool accepted)
        {
            if (!IsChoiceOpen) return;
            Action callback = accepted ? yes : no;
            owner = null; yes = no = null; confirmRequested = false;
            choiceInputSuspended = false;
            if (choicePanel != null) choicePanel.SetActive(false);
            RestoreYesButtonPosition();
            if (noButton != null) noButton.gameObject.SetActive(true);
            callback?.Invoke();
            GameManager.Instance?.RefreshInputState();
        }
        private void RestoreYesButtonPosition()
        {
            if (yesButtonRect == null) return;
            yesButtonRect.anchorMin = yesAnchorMin;
            yesButtonRect.anchorMax = yesAnchorMax;
        }
        private void LateUpdate()
        {
            if (confirmRequested) { confirmRequested = false; Confirm(); }
            if (elapsed < 0f || notificationGroup == null) return;
            elapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(.01f, enterDuration), exit = Mathf.Max(.01f, exitDuration);
            if (elapsed < enter)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / enter);
                notificationGroup.alpha = t;
                notificationGroup.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, t);
            }
            else if (elapsed < enter + holdDuration)
            { notificationGroup.alpha = 1f; notificationGroup.transform.localScale = Vector3.one; }
            else if (elapsed < enter + holdDuration + exit)
                notificationGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - enter - holdDuration) / exit);
            else HideNotification();
        }
    }
}
