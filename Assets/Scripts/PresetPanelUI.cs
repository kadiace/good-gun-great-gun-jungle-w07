using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 프리셋 패널 브리지. 기존 public API·직렬화 필드·씬 오버라이드 호환을 유지하며,
    /// 실질적인 View/Presenter 연결은 Initialize 전에 씬 오버라이드가 적용된 참조를 View로 전달한다.
    /// </summary>
    public sealed class PresetPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel, choicePanel;
        [SerializeField] private UnityEngine.UI.Button[] slots;
        [SerializeField] private UnityEngine.UI.Button saveButton, loadButton, closeButton;
        [SerializeField] private TMPro.TMP_Text choiceMessage;
        [SerializeField] private PresetPanelView m_view;

        private PresetPanelPresenter m_presenter;
        private bool m_isInitialized;

        public bool IsOpen => m_view != null && m_view.IsOpen;

        // UICancelRouter 등 기존 호출부 호환. 닫기 처리는 Presenter가 소유한다.
        public void Close()
        {
            m_presenter?.Close();
        }
        public RectTransform TutorialFirstSlot => m_view != null ? m_view.TutorialFirstSlot : null;
        public RectTransform TutorialSaveButton => m_view != null ? m_view.TutorialSaveButton : null;

        private void Awake()
        {
            // 브리지가 먼저 Initialize하여 View에 씬 오버라이드 참조를 주입한다.
            // 늦게 실행되는 View.Awake가 초기 상태를 다시 덮지 않게 가드한다.
            Initialize();
        }

        private void OnDisable()
        {
            m_presenter?.HandleDisabled();
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        /// <summary>
        /// View에 씬 오버라이드 참조를 주입하고 Presenter를 초기화합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Initialize()
        {
            if (m_isInitialized)
            {
                return;
            }

            if (m_view == null)
            {
                m_view = GetComponent<PresetPanelView>();
            }

            if (m_view != null)
            {
                m_view.BindReferences(panel, choicePanel, slots, saveButton, loadButton, closeButton, choiceMessage);
            }

            m_presenter = new PresetPanelPresenter(m_view, GetComponent<UIManager>());
            m_presenter.Initialize();
            m_isInitialized = true;
        }
    }
}