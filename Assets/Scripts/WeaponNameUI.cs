using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    /// <summary>
    /// 기존 UIManager·WorkshopSession 경로와 무기 이름 화면(WeaponNameView/WeaponNamePresenter)을 연결하는 브리지입니다.
    /// 기존 직렬화 필드와 public RefreshName·CancelEditing API를 유지하며, 씬 오버라이드가 적용된 참조를 View에 주입합니다.
    /// </summary>
    public sealed class WeaponNameUI : MonoBehaviour
    {
        [SerializeField] private WeaponNameView m_view;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button confirm;

        private UIManager m_uiManager;
        private WeaponNamePresenter m_presenter;

        private void Awake()
        {
            m_uiManager = GetComponent<UIManager>();
            if (m_view == null)
            {
                Debug.LogError(
                    "[WeaponNameUI] Initialize 실패: m_view 참조 누락. UIManager.prefab의 WeaponNameView 컴포넌트 연결이 필요합니다.",
                    this);
                return;
            }

            // 씬 오버라이드가 적용된 브리지 직렬화 참조를 Initialize 전에 View에 주입한다.
            m_view.BindReferences(panel, input, confirm);
            m_presenter = new WeaponNamePresenter(m_view, m_uiManager);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        private void LateUpdate()
        {
            if (m_presenter == null)
            {
                return;
            }

            WorkshopSession session = ResolveSession();
            EquipableWeapon current = session != null && session.IsEditing ? session.ActiveWorkbench.CurrentWeapon : null;
            m_presenter.UpdateTarget(current);
        }

        public void RefreshName()
        {
            if (m_presenter != null)
            {
                m_presenter.RefreshName();
            }
        }

        public bool CancelEditing()
        {
            return m_presenter != null && m_presenter.CancelEditing();
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.HandleDisabled();
            }
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        /// <summary>
        /// 기존 경로와 같은 세션 탐색(UIManager.Player의 WorkshopSession)을 유지합니다.
        /// </summary>
        private WorkshopSession ResolveSession()
        {
            FirstPersonPlayer player = m_uiManager != null ? m_uiManager.Player : null;
            return player != null ? player.GetComponent<WorkshopSession>() : null;
        }
    }
}
