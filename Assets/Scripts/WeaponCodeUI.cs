using System;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 무기 코드 공유 브리지. 기존 public API·직렬화 필드·씬 오버라이드 호환을 유지하며,
    /// 실질적인 View/Presenter 연결은 Initialize 전에 씬 오버라이드가 적용된 참조를 View로 전달한다.
    /// </summary>
    public sealed class WeaponCodeUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton, closeButton, copyButton, importButton;
        [SerializeField] private TMPro.TMP_InputField codeInput;
        [SerializeField] private WeaponCodeView m_view;

        private WeaponCodePresenter m_presenter;
        private bool m_isInitialized;

        public bool IsOpen => m_view != null && m_view.IsOpen;

        // UICancelRouter 등 기존 호출부 호환. 닫기 처리는 Presenter가 소유한다.
        public void Close()
        {
            m_presenter?.Close();
        }

        private void Awake()
        {
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
                m_view = GetComponent<WeaponCodeView>();
            }

            if (m_view != null)
            {
                m_view.BindReferences(panel, openButton, closeButton, copyButton, importButton, codeInput);
            }

            m_presenter = new WeaponCodePresenter(m_view, GetComponentInParent<UIManager>(true), this);
            m_presenter.Initialize();
            m_isInitialized = true;
        }
    }
}