using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 공방 3D 방향 마크(전면·상단)와 회전 원의 직렬화 참조 보관, 가시성 및 불투명도 갱신,
    /// 캔버스 좌표계 배치 등 그래픽 표시만 담당하는 View입니다.
    /// 도메인 객체나 세션 계산을 직접 소유하지 않습니다.
    /// </summary>
    public sealed class WorkshopDirectionMarksView : MonoBehaviour
    {
        private const float DEFAULT_FRONT_OPACITY = 1f;

        [SerializeField] private RotationCircleGraphic rotationCircle;
        [SerializeField] private RectTransform frontMark;
        [SerializeField] private RectTransform upMark;
        [SerializeField, Range(0f, 1f), Tooltip("이 값보다 확실히 뒤를 향할 때만 표시를 흐리게 합니다.")]
        private float directionBackThreshold = .05f;
        [SerializeField, Range(0f, 1f)] private float rotationCircleRadius = .5f;

        private Graphic[] m_frontGraphics;
        private float[] m_frontAlphas;
        private Graphic[] m_upGraphics;
        private float[] m_upAlphas;

        public RectTransform FrontMark => frontMark;
        public RectTransform UpMark => upMark;
        public Graphic[] FrontGraphics => m_frontGraphics;
        public float[] FrontAlphas => m_frontAlphas;
        public Graphic[] UpGraphics => m_upGraphics;
        public float[] UpAlphas => m_upAlphas;
        public float DirectionBackThreshold => directionBackThreshold;
        public float RotationCircleRadius => rotationCircleRadius;

        public void Configure(
            RotationCircleGraphic _rotationCircle,
            RectTransform _frontMark,
            RectTransform _upMark,
            float _directionBackThreshold,
            float _rotationCircleRadius)
        {
            if (_rotationCircle != null) rotationCircle = _rotationCircle;
            if (_frontMark != null) frontMark = _frontMark;
            if (_upMark != null) upMark = _upMark;
            if (_directionBackThreshold > 0f) directionBackThreshold = _directionBackThreshold;
            if (_rotationCircleRadius > 0f) rotationCircleRadius = _rotationCircleRadius;
            CacheGraphics();
        }

        public void Initialize()
        {
            CacheGraphics();
            // 정면 문구는 공용 화살표의 '장착 정면'만 사용한다. 상단 축 UI와 방향 아이콘은 유지한다.
            if (frontMark != null)
                foreach (TMP_Text label in frontMark.GetComponentsInChildren<TMP_Text>(true))
                    label.gameObject.SetActive(false);
            SetFrontOpacity(DEFAULT_FRONT_OPACITY);
        }

        public float GetRotationRadius(Rect _viewport)
        {
            return Mathf.Min(_viewport.width, _viewport.height) * rotationCircleRadius;
        }

        public void SetWorkshopUI(bool _editing, bool _is2D)
        {
            EnsureGraphicsCached();
            if (upMark != null)
            {
                upMark.gameObject.SetActive(_editing);
            }

            if (rotationCircle != null)
            {
                rotationCircle.gameObject.SetActive(_editing && !_is2D);
            }

            if (frontMark != null)
            {
                frontMark.gameObject.SetActive(_editing);
            }

            if (!_editing)
            {
                SetFrontOpacity(DEFAULT_FRONT_OPACITY);
            }
        }

        public void SetRotationCircleActive(bool _active)
        {
            if (rotationCircle != null && rotationCircle.gameObject.activeSelf != _active)
            {
                rotationCircle.gameObject.SetActive(_active);
            }
        }

        public void SetRotationCircleGeometry(Rect _viewport, float _radius)
        {
            if (rotationCircle != null)
            {
                rotationCircle.SetGeometry(_viewport, _radius);
            }
        }

        public void SetFrontOpacity(float _opacity)
        {
            if (m_frontGraphics == null)
            {
                return;
            }

            for (int i = 0; i < m_frontGraphics.Length; i++)
            {
                if (m_frontGraphics[i] == null)
                {
                    continue;
                }

                Color color = m_frontGraphics[i].color;
                color.a = m_frontAlphas[i] * _opacity;
                m_frontGraphics[i].color = color;
            }
        }

        public void HideMarks()
        {
            if (frontMark != null && frontMark.gameObject.activeSelf)
            {
                frontMark.gameObject.SetActive(false);
            }

            if (upMark != null && upMark.gameObject.activeSelf)
            {
                upMark.gameObject.SetActive(false);
            }
        }

        public void UpdateDirectionMark(
            RectTransform _mark,
            Vector3 _facePoint,
            Camera _view,
            Graphic[] _graphics,
            float[] _alphas,
            float _opacity)
        {
            if (_mark == null || _view == null)
            {
                return;
            }

            Vector3 screenPoint = _view.WorldToScreenPoint(_facePoint);
            bool visible = screenPoint.z > _view.nearClipPlane;
            _mark.gameObject.SetActive(visible);

            if (_graphics != null && _alphas != null)
            {
                for (int i = 0; i < _graphics.Length; i++)
                {
                    if (_graphics[i] == null)
                    {
                        continue;
                    }

                    Color color = _graphics[i].color;
                    color.a = _alphas[i] * _opacity;
                    _graphics[i].color = color;
                }
            }

            if (!visible)
            {
                return;
            }

            if (_mark.parent is RectTransform parent)
            {
                Canvas canvas = _mark.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 position))
                {
                    _mark.localPosition = new Vector3(position.x, position.y, 0f);
                }
            }
        }

        private void CacheGraphics()
        {
            if (frontMark != null)
            {
                m_frontGraphics = frontMark.GetComponentsInChildren<Graphic>(true);
                m_frontAlphas = new float[m_frontGraphics.Length];
                for (int i = 0; i < m_frontGraphics.Length; i++)
                {
                    m_frontAlphas[i] = m_frontGraphics[i].color.a;
                }
            }

            if (upMark != null)
            {
                m_upGraphics = upMark.GetComponentsInChildren<Graphic>(true);
                m_upAlphas = new float[m_upGraphics.Length];
                for (int i = 0; i < m_upGraphics.Length; i++)
                {
                    m_upAlphas[i] = m_upGraphics[i].color.a;
                }
            }
        }

        private void EnsureGraphicsCached()
        {
            if (upMark != null && m_upGraphics == null)
            {
                m_upGraphics = upMark.GetComponentsInChildren<Graphic>(true);
                m_upAlphas = new float[m_upGraphics.Length];
                for (int i = 0; i < m_upGraphics.Length; i++)
                {
                    m_upAlphas[i] = m_upGraphics[i].color.a;
                }
            }

            if (frontMark != null && m_frontGraphics == null)
            {
                m_frontGraphics = frontMark.GetComponentsInChildren<Graphic>(true);
                m_frontAlphas = new float[m_frontGraphics.Length];
                for (int i = 0; i < m_frontGraphics.Length; i++)
                {
                    m_frontAlphas[i] = m_frontGraphics[i].color.a;
                }
            }
        }
    }
}
