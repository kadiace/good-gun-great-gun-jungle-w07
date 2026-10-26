using System;

using UnityEngine;
using UnityEngine.InputSystem;

using TMPro;

namespace WeaponWorkshop
{
    // 기존 호출부용 대화 API를 유지하고 DialogueView·DialoguePresenter를 연결한다.
    public sealed class DialogueUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject box;
        [SerializeField] private TextMeshProUGUI line;
        [SerializeField] private TextMeshProUGUI speakerLabel;
        [SerializeField] private UnityEngine.UI.Button answerButton;
        [SerializeField] private TextMeshProUGUI answerLabel;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Presenter")]
        [SerializeField] private DialogueView m_view;

        private DialoguePresenter m_presenter;
        private bool m_hasLoggedViewError;

        public static DialogueUI Instance { get; private set; }

        public bool IsOpen => m_presenter != null ? m_presenter.IsOpen : box != null && box.activeSelf;

        /// <summary>
        /// 대화창 브리지가 활성화되어 있고 대화 상자가 계층 구조상 실제로 표시 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsReadingVisible => isActiveAndEnabled && IsOpen && box != null && box.activeInHierarchy;

        public RectTransform TutorialButton
        {
            get
            {
                if (m_view != null)
                {
                    return m_view.TutorialButton;
                }

                return answerButton != null ? answerButton.transform as RectTransform : null;
            }
        }

        private void Awake()
        {
            Instance = this;

            if (m_view == null)
            {
                m_hasLoggedViewError = true;
                Debug.LogError("[DialogueUI] Awake 실패: m_view에 DialogueView 참조가 필요합니다.", this);
                if (box != null)
                {
                    box.SetActive(false);
                }

                return;
            }

            m_view.Configure(box, line, speakerLabel, answerButton, answerLabel, inputActions);
            m_presenter = new DialoguePresenter(m_view, () => GameManager.Instance);
            m_presenter.Initialize();
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.Hide();
                return;
            }

            m_view?.Hide();
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
            m_presenter = null;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 대사 본문과 확인 버튼 문구를 지정해 표시하고 완료 콜백을 등록한다.
        /// 이미 열려 있으면 기존 화면과 콜백을 새 내용으로 교체한다.
        /// </summary>
        public bool Show(string _text, string _answer, Action _onAnswer, string _speaker = "손님", bool _confirmOnBackgroundClick = false, float _inputLockSeconds = 0f)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (m_view == null)
            {
                if (!m_hasLoggedViewError)
                {
                    m_hasLoggedViewError = true;
                    Debug.LogError("[DialogueUI] Show 실패: m_view에 DialogueView 참조가 필요합니다.", this);
                }

                return false;
            }

            return m_presenter != null && m_presenter.Show(_text, _answer, _onAnswer, _speaker, _confirmOnBackgroundClick, _inputLockSeconds);
        }

        public bool ShowChoices(string text, string[] labels, Action[] callbacks, string speaker = "손님")
        {
            return isActiveAndEnabled && m_presenter != null &&
                   m_presenter.ShowChoices(text, speaker, labels, callbacks);
        }

        /// <summary>
        /// 대사 표시를 닫고 등록된 콜백과 대화 상태를 정리한다.
        /// </summary>
        public void Hide()
        {
            m_presenter?.Hide();
        }

        /// <summary>
        /// 특정 튜토리얼 구간에서 대화창 배경 클릭 차단 블로커의 활성화 여부를 임시로 제어합니다.
        /// </summary>
        /// <param name="_active">블로커를 활성화할지 여부입니다.</param>
        public void SetClickBlockerActive(bool _active)
        {
            if (m_view != null)
            {
                m_view.SetClickBlockerActive(_active);
            }
        }
    }
}
