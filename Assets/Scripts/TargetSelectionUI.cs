using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 기존 직렬화 필드와 Open·Close·GetSavePrefab API를 유지하며 표적 선택창의
    /// View·Presenter를 연결하는 브리지입니다. 씬 오버라이드가 적용된 참조를 View에 주입합니다.
    /// </summary>
    public sealed class TargetSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform grid;
        [SerializeField] private UnityEngine.UI.Button buttonTemplate;
        [SerializeField] private UnityEngine.UI.Button closeButton;
        [SerializeField] private GameObject[] targetPrefabs = new GameObject[0];
        [SerializeField] private string[] targetNames = new string[0];
        [SerializeField] private ShootingTestBinding[] timedTestLayouts = new ShootingTestBinding[0];
        [SerializeField] private GameObject customTargetPrefab;
        [SerializeField] private TargetSelectionView m_view;

        private TargetSelectionPresenter m_presenter;

        /// <summary>
        /// 표적·사격 시험 선택 브리지가 활성화되어 있고 프레젠터 초기화 및 패널이 계층 구조상 실제로 표시 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsReadingVisible => isActiveAndEnabled && m_presenter != null && panel != null && panel.activeInHierarchy;

        public int GetSavePrefabIndex(GameObject prefab)
        {
            return prefab == customTargetPrefab ? -2 : System.Array.IndexOf(targetPrefabs, prefab);
        }

        public GameObject GetSavePrefab(int slot)
        {
            return slot == -2
                ? customTargetPrefab
                : slot >= 0 && slot < targetPrefabs.Length ? targetPrefabs[slot] : null;
        }

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError(
                    "[TargetSelectionUI] Initialize 실패: m_view 참조 누락. UIManager.prefab의 TargetSelectionView 연결이 필요합니다.",
                    this);
                return;
            }

            // 씬 오버라이드가 적용된 브리지 직렬화 참조를 Initialize 전에 View에 주입한다.
            m_view.BindReferences(panel, grid, buttonTemplate, closeButton);
            m_presenter = new TargetSelectionPresenter(m_view, targetPrefabs, targetNames, timedTestLayouts,
                customTargetPrefab);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
            }
        }

        public bool Open(TargetBell source)
        {
            return m_presenter != null && m_presenter.Open(source);
        }

        public void Close()
        {
            if (m_presenter != null)
            {
                m_presenter.Close();
            }
        }

        private void LateUpdate()
        {
            if (m_presenter != null)
            {
                m_presenter.Tick();
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            if (m_presenter != null)
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }
    }
}
