using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 21:9 등 울트라와이드 해상도에서 UI 콘텐츠가 가로로 과도하게 늘어나지 않도록
    /// 중앙 최대 너비(기준 공간 1920)를 제한하고 좌우 여백을 유지합니다.
    /// 16:9 이하에서는 부모 너비에 맞춰 자연스럽게 표시됩니다.
    /// </summary>
    // 에디트 모드에서 오프셋을 바꾸면 Game 뷰 비율에 따라 프리팹·씬이 변경·오버라이드되므로 실행 중에만 적용한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CenterMaxWidth : MonoBehaviour
    {
        private const float DEFAULT_MAX_WIDTH = 1920f;

        [SerializeField, Min(1f)]
        private float m_maxWidth = DEFAULT_MAX_WIDTH;

        [SerializeField]
        private bool m_applySideMargins = true;

        private RectTransform m_rectTransform;

        public float MaxWidth
        {
            get
            {
                return m_maxWidth;
            }
            set
            {
                if (!Mathf.Approximately(m_maxWidth, value))
                {
                    m_maxWidth = Mathf.Max(1f, value);
                    Apply();
                }
            }
        }

        public bool ApplySideMargins
        {
            get
            {
                return m_applySideMargins;
            }
            set
            {
                if (m_applySideMargins != value)
                {
                    m_applySideMargins = value;
                    Apply();
                }
            }
        }

        private void Awake()
        {
            EnsureRectTransform();
            Apply();
        }

        private void OnEnable()
        {
            EnsureRectTransform();
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_maxWidth < 1f)
            {
                m_maxWidth = DEFAULT_MAX_WIDTH;
            }
        }
#endif

        /// <summary>
        /// 부모 RectTransform 너비에 따라 중앙 최대 너비 제한을 적용합니다.
        /// 부모 너비가 m_maxWidth를 초과하는 경우(21:9 등) 좌우 여백을 주어 1920 폭으로 중앙 정렬합니다.
        /// </summary>
        public void Apply()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRectTransform();
            if (m_rectTransform == null)
            {
                return;
            }

            RectTransform parent = m_rectTransform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            float parentWidth = parent.rect.width;
            if (parentWidth <= 0f)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                    if (canvasRect != null)
                    {
                        parentWidth = canvasRect.rect.width;
                    }
                }
            }

            if (parentWidth <= 0f)
            {
                return;
            }

            bool isStretch = Mathf.Approximately(m_rectTransform.anchorMin.x, 0f) &&
                             Mathf.Approximately(m_rectTransform.anchorMax.x, 1f);

            if (isStretch && m_applySideMargins)
            {
                if (parentWidth > m_maxWidth)
                {
                    float margin = (parentWidth - m_maxWidth) * 0.5f;
                    Vector2 min = m_rectTransform.offsetMin;
                    Vector2 max = m_rectTransform.offsetMax;
                    if (!Mathf.Approximately(min.x, margin) || !Mathf.Approximately(max.x, -margin))
                    {
                        m_rectTransform.offsetMin = new Vector2(margin, min.y);
                        m_rectTransform.offsetMax = new Vector2(-margin, max.y);
                    }
                }
                else
                {
                    Vector2 min = m_rectTransform.offsetMin;
                    Vector2 max = m_rectTransform.offsetMax;
                    if (!Mathf.Approximately(min.x, 0f) || !Mathf.Approximately(max.x, 0f))
                    {
                        m_rectTransform.offsetMin = new Vector2(0f, min.y);
                        m_rectTransform.offsetMax = new Vector2(0f, max.y);
                    }
                }
            }
            else
            {
                if (m_rectTransform.sizeDelta.x > m_maxWidth)
                {
                    m_rectTransform.sizeDelta = new Vector2(m_maxWidth, m_rectTransform.sizeDelta.y);
                }
            }
        }

        private void EnsureRectTransform()
        {
            if (m_rectTransform == null)
            {
                m_rectTransform = GetComponent<RectTransform>();
            }
        }
    }
}
