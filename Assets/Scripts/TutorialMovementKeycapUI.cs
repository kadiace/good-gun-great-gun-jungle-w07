using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WeaponWorkshop
{
    /// <summary>
    /// 튜토리얼 야간 습격 도착 안내 시 달리기(Shift) 및 점프(Space) 키캡 이미지를 표시하는 UI 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialMovementKeycapUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("키캡 안내 전체를 담는 루트 패널입니다.")]
        private GameObject panel;

        [SerializeField, Tooltip("Shift 키캡을 표시할 이미지 컴포넌트입니다.")]
        private Image shiftKeyImage;

        [SerializeField, Tooltip("Space 키캡을 표시할 이미지 컴포넌트입니다.")]
        private Image spaceKeyImage;

        [SerializeField, Tooltip("Shift 동작(달리기) 설명 텍스트입니다.")]
        private TextMeshProUGUI shiftLabel;

        [SerializeField, Tooltip("Space 동작(점프) 설명 텍스트입니다.")]
        private TextMeshProUGUI spaceLabel;

        [SerializeField, Tooltip("튜토리얼 대화창 UI 참조입니다. 비어 있으면 자동으로 탐색합니다.")]
        private DialogueUI dialogueUI;

        [Header("Keycap Sprites")]
        [SerializeField, Tooltip("Shift 키캡 스프라이트입니다.")]
        private Sprite shiftSprite;

        [SerializeField, Tooltip("Space 키캡 스프라이트입니다.")]
        private Sprite spaceSprite;

        /// <summary>
        /// 컴포넌트가 깨어날 때 초기 상태를 설정하고 UI를 꺼진 상태로 초기화합니다.
        /// </summary>
        private void Awake()
        {
            ApplySprites();
            Hide();
            if (dialogueUI == null)
            {
                dialogueUI = GetComponentInChildren<DialogueUI>(true);
            }
        }

        /// <summary>
        /// 컴포넌트 활성화 시 튜토리얼 변경 이벤트를 구독하고 현재 상태에 맞춰 표시를 갱신합니다.
        /// </summary>
        private void OnEnable()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.Changed += HandleTutorialChanged;
            }

            Refresh();
        }

        /// <summary>
        /// 컴포넌트 비활성화 시 이벤트를 해제하고 패널을 끕니다.
        /// </summary>
        private void OnDisable()
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.Changed -= HandleTutorialChanged;
            }

            Hide();
        }

        /// <summary>
        /// 매 프레임 대화창 상태나 일시정지 상태 변화를 감지해 표시 여부를 갱신합니다.
        /// </summary>
        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>
        /// 튜토리얼 매니저의 상태 변경 시 호출되는 이벤트 핸들러입니다.
        /// </summary>
        private void HandleTutorialChanged()
        {
            Refresh();
        }

        /// <summary>
        /// 현재 게임 및 튜토리얼 상태를 검사하여 키캡 안내 패널의 표시/숨김 상태를 갱신합니다.
        /// </summary>
        public void Refresh()
        {
            bool show = ShouldShow();
            if (show)
            {
                Show();
            }
            else
            {
                Hide();
            }
        }

        /// <summary>
        /// 키캡 안내 패널을 화면에 표시합니다.
        /// </summary>
        public void Show()
        {
            ApplySprites();
            if (panel != null && !panel.activeSelf)
            {
                panel.SetActive(true);
            }
        }

        /// <summary>
        /// 키캡 안내 패널을 화면에서 숨깁니다.
        /// </summary>
        public void Hide()
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// 이동 키캡 안내 패널을 표시할 조건인지 판별합니다.
        /// 활성 튜토리얼 중 showMovementKeycaps가 참인 도착 안내 대화가 열려 있고, 일시정지되지 않은 상태여야 합니다.
        /// </summary>
        /// <returns>키캡 UI를 표시해야 하면 true, 숨겨야 하면 false를 반환합니다.</returns>
        private bool ShouldShow()
        {
            var tutorial = TutorialManager.Instance;
            if (tutorial == null || !tutorial.IsTutorialActive)
            {
                return false;
            }

            var step = tutorial.CurrentStep;
            if (step == null || !step.showMovementKeycaps)
            {
                return false;
            }

            var game = GameManager.Instance;
            if (game != null && game.IsPaused)
            {
                return false;
            }

            var dialogue = ResolveDialogue();
            if (dialogue == null || !dialogue.IsOpen)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 대화 UI 인스턴스를 반환하며, 없는 경우 캐시 또는 씬에서 탐색합니다.
        /// </summary>
        /// <returns>유효한 DialogueUI 인스턴스 또는 null입니다.</returns>
        private DialogueUI ResolveDialogue()
        {
            if (dialogueUI != null)
            {
                return dialogueUI;
            }

            dialogueUI = DialogueUI.Instance;
            if (dialogueUI == null)
            {
                dialogueUI = GetComponentInChildren<DialogueUI>(true);
            }

            return dialogueUI;
        }

        /// <summary>
        /// 직렬화된 키캡 스프라이트를 이미지 컴포넌트에 적용합니다.
        /// </summary>
        private void ApplySprites()
        {
            if (shiftKeyImage != null && shiftSprite != null && shiftKeyImage.sprite != shiftSprite)
            {
                shiftKeyImage.sprite = shiftSprite;
            }

            if (spaceKeyImage != null && spaceSprite != null && spaceKeyImage.sprite != spaceSprite)
            {
                spaceKeyImage.sprite = spaceSprite;
            }
        }
    }
}
