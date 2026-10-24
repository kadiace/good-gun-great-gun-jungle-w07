using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class DayTransitionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private CanvasGroup titleFade;
        [SerializeField, Min(.01f)] private float fadeDuration = .45f;
        [SerializeField, Min(0f)] private float titleDuration = 1f;
        [SerializeField] private DayTransitionView m_view;

        private DayTransitionPresenter m_presenter;

        public event System.Action Covered;
        public event System.Action DayTextShown;
        public event System.Action Completed;

        public bool IsTransitioning
        {
            get
            {
                return m_presenter != null && m_presenter.IsRunning;
            }
        }

        private void Awake()
        {
            if (m_view == null)
            {
                Debug.LogError(
                    "[DayTransitionUI] Awake 실패: m_view에 DayTransitionView 참조가 필요합니다.");
                return;
            }

            m_view.Configure(panel, fade, dayText, titleFade, fadeDuration, titleDuration);

            m_presenter = new DayTransitionPresenter(m_view, () => GameManager.Instance);
            m_presenter.Covered += OnPresenterCovered;
            m_presenter.DayTextShown += OnPresenterDayTextShown;
            m_presenter.Completed += OnPresenterCompleted;

            if (m_presenter.Initialize())
            {
                return;
            }

            m_presenter.Covered -= OnPresenterCovered;
            m_presenter.DayTextShown -= OnPresenterDayTextShown;
            m_presenter.Completed -= OnPresenterCompleted;
            m_presenter = null;
        }

        private void OnDisable()
        {
            m_presenter?.Cancel();
        }

        private void OnDestroy()
        {
            if (m_presenter == null)
            {
                return;
            }

            m_presenter.Covered -= OnPresenterCovered;
            m_presenter.DayTextShown -= OnPresenterDayTextShown;
            m_presenter.Completed -= OnPresenterCompleted;
            m_presenter.Dispose();
            m_presenter = null;
        }

        public bool Begin(_DayManager _day)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.Begin(_day);
        }

        public bool BeginFirstDay(_DayManager _day)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.BeginFirstDay(_day);
        }

        public bool BeginReturn(_DayManager _day)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.BeginReturn(_day);
        }

        public bool BeginSkippedNightRaid(_DayManager _day)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.BeginSkippedNightRaid(_day);
        }

        public bool BeginTutorial(System.Action _whenCovered)
        {
            if (!isActiveAndEnabled || m_presenter == null)
            {
                return false;
            }

            return m_presenter.BeginTutorial(_whenCovered);
        }

        private void OnPresenterCovered()
        {
            Covered?.Invoke();
        }

        private void OnPresenterDayTextShown()
        {
            DayTextShown?.Invoke();
        }

        private void OnPresenterCompleted()
        {
            Completed?.Invoke();
        }
    }
}
