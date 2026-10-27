using UnityEngine;

namespace WeaponWorkshop
{
    // 크로스헤어 아래 차지 게이지 브리지. 기존 직렬화 필드·외형 값을 유지하고 저작된 ChargeGaugeView로 표시를 넘긴다.
    // 충전 상태 조회는 ChargeGaugePresenter가 맡는다.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ChargeGaugeUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField, Tooltip("저작 전용 값입니다. 런타임에는 저작된 Label 폰트를 씁니다.")]
        private TMPro.TMP_FontAsset font;
        [SerializeField, Tooltip("저작 전용 값입니다. 런타임에는 저작된 루트 위치를 씁니다.")]
        private Vector2 offset = new Vector2(0f, -27f);
        [SerializeField, Tooltip("저작 전용 값입니다. 런타임에는 저작된 칸 크기를 씁니다.")]
        private Vector2 size = new Vector2(135f, 7.5f);
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color fillColor = new Color(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color fullColor = new Color(1f, 0.6f, 0.15f, 1f);
        [SerializeField] private ChargeGaugeView m_view;

        private ChargeGaugePresenter m_presenter;
        private bool m_hasLoggedDependencyError;
        private bool m_hasInitializationFailed;

        private void Awake()
        {
            ResolveUIManager();
            EnsurePresenter();
        }

        private void OnDisable()
        {
            m_view?.Hide();
        }

        private void OnDestroy()
        {
            if (m_presenter == null)
            {
                return;
            }

            m_presenter.Dispose();
            m_presenter = null;
        }

        private void LateUpdate()
        {
            ResolveUIManager();
            if (m_presenter != null)
            {
                m_presenter.Refresh();
                return;
            }

            if (!m_hasInitializationFailed)
            {
                EnsurePresenter();
            }
        }

        private void ResolveUIManager()
        {
            if (uiManager != null)
            {
                return;
            }

            uiManager = GetComponentInParent<UIManager>(true);
            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }
        }

        private void EnsurePresenter()
        {
            if (m_presenter != null)
            {
                return;
            }

            if (m_view == null || uiManager == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        "[ChargeGaugeUI] 초기화 실패: m_view=" + (m_view != null) +
                        ", uiManager=" + (uiManager != null) +
                        ". Tools/UI/Authoring/CombatHud/Author 실행 결과를 확인하세요.", this);
                }

                return;
            }

            m_view.Configure(emptyColor, fillColor, fullColor);
            m_presenter = new ChargeGaugePresenter(m_view, uiManager);
            if (!m_presenter.Initialize())
            {
                m_presenter = null;
                m_hasInitializationFailed = true;
            }
        }
    }
}
