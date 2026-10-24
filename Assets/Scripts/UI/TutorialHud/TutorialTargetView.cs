using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 튜토리얼 대상 표식의 저작된 참조, 시작 숨김, 월드→뷰포트 위치와 화살표·거리 문구 표시만 담당하는 View입니다.
    /// 대상·단계 선택은 TutorialTargetPresenter가 맡습니다.
    /// </summary>
    public sealed class TutorialTargetView : MonoBehaviour
    {
        private const string VIEW_NAME = "TutorialTargetView";

        private RectTransform m_panel;
        private TextMeshProUGUI m_marker;
        private Camera m_viewCamera;
        private float m_bounceHeight = 6f;
        private float m_bounceSpeed = 2f;
        private float m_arrowSize = 36f;

        private bool m_hasAppliedInitialState;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyInitialHiddenState();
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// 브리지가 보유한 실효 참조와 연출 수치를 Initialize 전에 전달합니다.
        /// </summary>
        public void Configure(RectTransform _panel, TextMeshProUGUI _marker, Camera _viewCamera,
            float _bounceHeight, float _bounceSpeed, float _arrowSize)
        {
            m_panel = _panel;
            m_marker = _marker;
            m_viewCamera = _viewCamera;
            m_bounceHeight = _bounceHeight;
            m_bounceSpeed = _bounceSpeed;
            m_arrowSize = _arrowSize;
            ApplyInitialHiddenState();
        }

        /// <summary>
        /// 필수 참조를 검증하고 시작 숨김을 적용합니다. 중복 호출에 안전합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 표시 참조를 해제합니다. 중복 호출에 안전합니다.
        /// </summary>
        public void Dispose()
        {
            m_isInitialized = false;
        }

        public void Hide()
        {
            if (m_panel != null && m_panel.gameObject.activeSelf)
            {
                m_panel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 대상의 월드 위치를 화면 가장자리 또는 대상 위쪽에 배치하고 화살표·거리 문구를 갱신합니다.
        /// </summary>
        public void ShowTarget(Component _target, string _title)
        {
            if (m_panel == null || m_marker == null || m_viewCamera == null || _target == null)
            {
                Hide();
                return;
            }

            m_panel.gameObject.SetActive(true);

            InteractionGuidePoints guide = _target.GetComponent<InteractionGuidePoints>();
            Vector3 world = _target.transform.position;
            Vector3 topWorld = world + Vector3.up;
            if (guide != null)
            {
                world = guide.Center;
                topWorld = guide.Top;
            }
            else
            {
                // 손님은 프로필에 따라 초상화 크기가 달라지므로 실제 렌더러 경계를 따른다.
                bool found = false;
                Bounds bounds = default;
                foreach (Renderer renderer in _target.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer.GetComponent<TMP_Text>() != null)
                    {
                        continue;
                    }

                    if (!found)
                    {
                        bounds = renderer.bounds;
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                if (found)
                {
                    world = bounds.center;
                    topWorld = new Vector3(world.x, bounds.max.y + .1f, world.z);
                }
            }

            Vector3 viewport = m_viewCamera.WorldToViewportPoint(world);
            Vector2 direction = new Vector2(viewport.x - .5f, viewport.y - .5f);
            bool behind = viewport.z <= 0f;
            if (behind)
            {
                direction = -direction;
                if (direction.sqrMagnitude < .001f)
                {
                    direction = Vector2.down;
                }
            }

            Vector2 half = m_panel.rect.size * .5f;
            Vector2 limit = new Vector2(Mathf.Max(1f, half.x - 116.25f), Mathf.Max(1f, half.y - 78.75f));
            Vector2 point = Vector2.Scale(direction, m_panel.rect.size);
            float edge = Mathf.Max(Mathf.Abs(point.x) / limit.x, Mathf.Abs(point.y) / limit.y);
            bool offscreen = behind || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * m_bounceSpeed)) * m_bounceHeight;
            if (offscreen)
            {
                point /= Mathf.Max(edge, .001f);
            }
            else
            {
                Vector3 top = m_viewCamera.WorldToViewportPoint(topWorld);
                point = Vector2.Scale(new Vector2(top.x - .5f, top.y - .5f), m_panel.rect.size);
                point.y += bounce;
                point.x = Mathf.Clamp(point.x, -half.x + m_arrowSize, half.x - m_arrowSize);
                point.y = Mathf.Clamp(point.y, -half.y + m_arrowSize, half.y - m_arrowSize);
            }

            m_marker.rectTransform.anchoredPosition = point;

            string arrow = offscreen
                ? (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                    ? (direction.x > 0 ? "→ " : "← ")
                    : (direction.y > 0 ? "↑ " : "↓ "))
                : "";
            if (offscreen)
            {
                float distance = Vector3.Distance(m_viewCamera.transform.position, world);
                m_marker.text = "<size=" + m_arrowSize.ToString("0") + ">" + arrow + "</size>" +
                    _title + "\n" + distance.ToString("0") + "m";
            }
            else
            {
                m_marker.text = "<size=" + m_arrowSize.ToString("0") + ">▼</size>";
            }
        }

        private void ApplyInitialHiddenState()
        {
            if (m_hasAppliedInitialState || m_panel == null)
            {
                return;
            }

            m_hasAppliedInitialState = true;
            m_panel.gameObject.SetActive(false);
        }

        private bool ValidateReferences()
        {
            bool isValid = m_panel != null && m_marker != null;
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[" + VIEW_NAME + "] Initialize 실패: 필수 참조 누락 " +
                    "(m_panel=" + (m_panel != null) + ", m_marker=" + (m_marker != null) + ")",
                    this);
            }

            return isValid;
        }
    }
}
