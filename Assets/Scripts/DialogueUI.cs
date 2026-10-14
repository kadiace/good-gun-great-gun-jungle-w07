using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    // 항상 활성인 오브젝트에 붙이고, box만 켜고 끕니다.
    public sealed class DialogueUI : MonoBehaviour
    {
        [SerializeField] private GameObject box;
        [SerializeField] private TMPro.TextMeshProUGUI line;
        [SerializeField] private TMPro.TextMeshProUGUI speakerLabel;
        [SerializeField] private Button answerButton;
        [SerializeField] private TMPro.TextMeshProUGUI answerLabel;
        private System.Action answered;
        [SerializeField] private InputActionAsset inputActions;
        private InputActionMap controls;
        private int openedFrame;
        private bool answerRequested;
        public static DialogueUI Instance { get; private set; }
        public bool IsOpen => box != null && box.activeSelf;
        public RectTransform TutorialButton => answerButton != null ? answerButton.transform as RectTransform : null;

        private void Awake()
        {
            Instance = this;
            if (box != null) box.SetActive(false);
            answerButton?.onClick.AddListener(() => { if (!IsTransitioning) answered?.Invoke(); });
            if (inputActions != null)
            {
                controls = inputActions.FindActionMap("Workshop", true).Clone();
                controls.FindAction("Interact", true).performed += _ =>
                {
                    if (IsOpen && !IsTransitioning && Time.frameCount > openedFrame && answerButton != null &&
                        answerButton.isActiveAndEnabled && answerButton.interactable)
                        answerRequested = true;
                };
            }
        }
        private void OnEnable() => controls?.Enable();
        private bool IsTransitioning
        {
            get
            {
                var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
                var transition = ui != null ? ui.GetComponent<DayTransitionUI>() : null;
                return transition != null && transition.IsTransitioning;
            }
        }
        private void LateUpdate()
        {
            if (!answerRequested) return;
            answerRequested = false;
            if (IsOpen && answerButton != null && answerButton.isActiveAndEnabled && answerButton.interactable)
                answerButton.onClick.Invoke();
        }
        private void OnDisable() { controls?.Disable(); Hide(); }
        private void OnDestroy() { controls?.Dispose(); if (Instance == this) Instance = null; }

        public bool Show(string text, string answer, System.Action onAnswer, string speaker = "손님")
        {
            if (box == null || line == null || answerButton == null || answerLabel == null)
            {
                Debug.LogError("DialogueUI의 Box/Line/Answer/Label 연결을 확인하세요.", this);
                return false;
            }
            line.text = text;
            if (speakerLabel != null) speakerLabel.text = speaker;
            answerLabel.text = answer;
            answered = onAnswer;
            answerRequested = false;
            openedFrame = Time.frameCount;
            box.SetActive(true);
            // Raise the dialogue's canvas-level branch above panels such as settlement.
            var canvas = box.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Transform branch = box.transform;
                while (branch.parent != null && branch.parent != canvas.transform) branch = branch.parent;
                if (branch != canvas.transform) branch.SetAsLastSibling();
            }
            GameManager.Instance?.SetDialogue(true);
            return true;
        }

        public void Hide()
        {
            answered = null;
            answerRequested = false;
            bool wasOpen = IsOpen;
            box?.SetActive(false);
            if (wasOpen) GameManager.Instance?.SetDialogue(false);
        }
    }
}
