using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    // 튜토리얼 대상 표식의 기존 연결점(브리지). 직렬화 필드와 씬 오버라이드 호환을 유지한다.
    // 표시는 TutorialTargetView, 대상 선택은 TutorialTargetPresenter가 담당하며 실행 시 원본 참조는 이 컴포넌트가 전달한다.
    public sealed class TutorialTargetUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI marker;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float bounceHeight = 6f;
        [SerializeField, Min(0f)] private float bounceSpeed = 2f;
        [SerializeField, Min(1f)] private float arrowSize = 36f;
        [SerializeField] private TutorialTargetView m_view;

        private TutorialTargetPresenter m_presenter;

        private void Awake()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (m_view == null)
            {
                Debug.LogError(
                    "[TutorialTargetUI] Awake 실패: m_view에 TutorialTargetView 참조가 필요합니다. " +
                    "Tools/UI/Authoring/TutorialHud/Author 실행 결과를 확인하세요.",
                    this);
                HidePanel();
                return;
            }

            m_view.Configure(panel, marker, viewCamera, bounceHeight, bounceSpeed, arrowSize);
            m_presenter = new TutorialTargetPresenter(m_view);
            if (m_presenter.Initialize())
            {
                return;
            }

            m_presenter = null;
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        private void OnDisable()
        {
            if (m_presenter != null)
            {
                m_presenter.OnDisabled();
                return;
            }

            HidePanel();
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

        private void HidePanel()
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }
    }
}
