using UnityEngine;

namespace WeaponWorkshop
{
    // 피격 방향 표시의 피격 구독·시점 이력·표시 시간 계산을 담당한다.
    // 호를 그리는 일은 DamageDirectionIndicator가 맡고, 이 클래스는 방향과 남은 시간만 전달한다.
    public sealed class DamageDirectionPresenter
    {
        private const string SCREEN_NAME = "DamageDirectionPresenter";
        private const float DIRECTION_LOOKBACK_SECONDS = 0.5f;
        private const int VIEW_HISTORY_CAPACITY = 128;

        private struct ViewSample
        {
            public float Time;
            public Quaternion Rotation;
        }

        private readonly DamageDirectionIndicator m_indicator;
        private readonly ViewSample[] m_viewHistory = new ViewSample[VIEW_HISTORY_CAPACITY];

        private UIManager m_uiManager;
        private RaidPlayerHealth m_health;
        private Quaternion m_referenceViewRotation = Quaternion.identity;
        private Vector3 m_incomingDirectionInReferenceView;
        private Vector2 m_screenDirection = Vector2.up;
        private float m_remaining;
        private int m_viewHistoryCount;
        private int m_nextViewSample;
        private bool m_isSubscribed;
        private bool m_hasLoggedDependencyError;

        public DamageDirectionPresenter(DamageDirectionIndicator _indicator)
        {
            m_indicator = _indicator;
        }

        public bool Initialize(UIManager _uiManager)
        {
            if (m_indicator == null)
            {
                if (!m_hasLoggedDependencyError)
                {
                    m_hasLoggedDependencyError = true;
                    Debug.LogError(
                        $"[{SCREEN_NAME}] Initialize 실패: DamageDirectionIndicator 참조가 없습니다. " +
                        "AimHudView 참조 연결을 확인하세요.");
                }

                return false;
            }

            m_indicator.Initialize(_uiManager);
            m_uiManager = m_indicator.Owner;
            ClearIndicator();
            SyncHealthSubscription();
            return true;
        }

        public void Dispose()
        {
            Unsubscribe();
            m_indicator?.ClearIndicator();
            m_uiManager = null;
            m_viewHistoryCount = 0;
            m_nextViewSample = 0;
        }

        public void Suspend()
        {
            Unsubscribe();
            ClearIndicator();
        }

        public void Refresh()
        {
            if (m_indicator == null)
            {
                return;
            }

            RecordViewSample();
            SyncHealthSubscription();
            if (m_remaining <= 0f)
            {
                return;
            }

            if (GameManager.GameplayBlocked || m_health == null || m_health.IsDead)
            {
                ClearIndicator();
                return;
            }

            m_remaining -= Time.unscaledDeltaTime;
            if (m_remaining <= 0f)
            {
                ClearIndicator();
                return;
            }

            UpdateScreenDirection();
            m_indicator.SetIndicator(m_screenDirection, m_remaining);
        }

        private RaidPlayerHealth ResolveHealth()
        {
            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? player.GetComponent<RaidPlayerHealth>() : null;
        }

        private void SyncHealthSubscription()
        {
            RaidPlayerHealth nextHealth = ResolveHealth();
            if (nextHealth != m_health)
            {
                Unsubscribe();
                m_health = nextHealth;
            }

            bool shouldSubscribe = m_health != null && m_indicator != null && m_indicator.isActiveAndEnabled;
            if (shouldSubscribe && !m_isSubscribed)
            {
                m_health.HitReceived += OnHitReceived;
                m_isSubscribed = true;
            }
            else if (!shouldSubscribe && m_isSubscribed)
            {
                Unsubscribe();
            }
        }

        private void Unsubscribe()
        {
            if (m_health != null && m_isSubscribed)
            {
                m_health.HitReceived -= OnHitReceived;
            }

            m_isSubscribed = false;
        }

        private void OnHitReceived(Vector3 _sourceDirection)
        {
            if (!m_indicator.isActiveAndEnabled || GameManager.GameplayBlocked || m_uiManager == null)
            {
                return;
            }

            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null)
            {
                return;
            }

            Transform view = player.ViewPoint;
            bool hasReference = TryGetReferenceView(out ViewSample reference);
            m_referenceViewRotation = hasReference ? reference.Rotation : view != null ? view.rotation : player.transform.rotation;
            if (_sourceDirection.sqrMagnitude < 0.001f)
            {
                _sourceDirection = -player.transform.forward;
            }

            m_incomingDirectionInReferenceView = Quaternion.Inverse(m_referenceViewRotation) * _sourceDirection.normalized;
            m_remaining = m_indicator.DisplayDuration;
            UpdateScreenDirection();
            m_indicator.SetIndicator(m_screenDirection, m_remaining);
        }

        private void UpdateScreenDirection()
        {
            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            Transform view = player != null ? player.ViewPoint : null;
            if (view == null || m_incomingDirectionInReferenceView.sqrMagnitude < 0.001f)
            {
                m_screenDirection = Vector2.up;
                return;
            }

            Vector3 worldDirection = m_referenceViewRotation * m_incomingDirectionInReferenceView;
            Vector3 localDirection = view.InverseTransformDirection(worldDirection);
            Vector2 projected = new Vector2(localDirection.x, localDirection.y);
            if (projected.sqrMagnitude < 0.001f)
            {
                projected = localDirection.z < 0f ? Vector2.down : Vector2.up;
            }

            m_screenDirection = projected.normalized;
        }

        private void ClearIndicator()
        {
            m_remaining = 0f;
            m_referenceViewRotation = Quaternion.identity;
            m_incomingDirectionInReferenceView = Vector3.zero;
            m_screenDirection = Vector2.up;
            m_indicator?.ClearIndicator();
        }

        private void RecordViewSample()
        {
            FirstPersonPlayer player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            Transform view = player != null ? player.ViewPoint : null;
            if (view == null)
            {
                return;
            }

            ViewSample sample = new ViewSample
            {
                Time = Time.unscaledTime,
                Rotation = view.rotation
            };
            int latest = (m_nextViewSample - 1 + VIEW_HISTORY_CAPACITY) % VIEW_HISTORY_CAPACITY;
            if (m_viewHistoryCount > 0 && sample.Time - m_viewHistory[latest].Time < 0.001f)
            {
                m_viewHistory[latest] = sample;
                return;
            }

            m_viewHistory[m_nextViewSample] = sample;
            m_nextViewSample = (m_nextViewSample + 1) % VIEW_HISTORY_CAPACITY;
            m_viewHistoryCount = Mathf.Min(m_viewHistoryCount + 1, VIEW_HISTORY_CAPACITY);
        }

        private bool TryGetReferenceView(out ViewSample _reference)
        {
            _reference = default;
            if (m_viewHistoryCount == 0)
            {
                return false;
            }

            float targetTime = Time.unscaledTime - DIRECTION_LOOKBACK_SECONDS;
            float closestDifference = float.PositiveInfinity;
            int oldest = (m_nextViewSample - m_viewHistoryCount + VIEW_HISTORY_CAPACITY) % VIEW_HISTORY_CAPACITY;
            for (int i = 0; i < m_viewHistoryCount; i++)
            {
                ViewSample sample = m_viewHistory[(oldest + i) % VIEW_HISTORY_CAPACITY];
                float difference = Mathf.Abs(sample.Time - targetTime);
                if (difference >= closestDifference)
                {
                    continue;
                }

                closestDifference = difference;
                _reference = sample;
            }

            return true;
        }
    }
}
