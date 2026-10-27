using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 튜토리얼 버튼 강조 커버의 저작된 템플릿과 재사용 풀, 버튼 그래픽 복제·펄스 표시만 담당하는 View입니다.
    /// 어떤 버튼을 강조할지는 TutorialButtonHighlightPresenter가 결정합니다.
    /// </summary>
    public sealed class TutorialButtonHighlightView : MonoBehaviour
    {
        private const string VIEW_NAME = "TutorialButtonHighlightView";
        private const string COVER_NAME = "TutorialButtonCover";

        private Image m_cover;
        private readonly List<Image> m_covers = new List<Image>();
        private readonly Image[] m_spotlight = new Image[4];
        private readonly Image[] m_guides = new Image[4];
        private readonly Vector3[] m_corners = new Vector3[4];

        private bool m_hasAppliedInitialState;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            if (!m_isInitialized)
            {
                ApplyInitialCoverState();
            }
        }

        private void OnDisable()
        {
            HideAll();
        }

        private void OnDestroy()
        {
            Dispose();
            foreach (Image image in m_spotlight)
            {
                if (image != null)
                {
                    Destroy(image.gameObject);
                }
            }
            foreach (Image image in m_guides)
            {
                if (image != null)
                {
                    Destroy(image.gameObject);
                }
            }
        }

        /// <summary>
        /// 브리지가 보유한 커버 템플릿 참조를 Initialize 전에 전달합니다.
        /// </summary>
        public void Configure(Image _cover)
        {
            m_cover = _cover;
            ApplyInitialCoverState();
        }

        /// <summary>
        /// 커버 템플릿을 비상호작용·숨김 상태로 초기화합니다. 중복 호출에 안전합니다.
        /// </summary>
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyInitialCoverState();
            if (m_cover == null)
            {
                if (!m_hasLoggedReferenceError)
                {
                    m_hasLoggedReferenceError = true;
                    Debug.LogError(
                        "[" + VIEW_NAME + "] Initialize 실패: 필수 참조 누락 (m_cover=False)",
                        this);
                }

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

        /// <summary>
        /// 필요한 수만큼 커버를 복제하고 각 대상 버튼 그래픽 위에 맞춥니다. 남는 커버는 숨겨 루트로 돌립니다.
        /// </summary>
        public void ShowTargets(IReadOnlyList<RectTransform> _targets)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            if (_targets == null)
            {
                HideAll();
                return;
            }

            while (m_covers.Count < _targets.Count)
            {
                Image copy = Instantiate(m_cover, transform);
                copy.name = COVER_NAME;
                copy.raycastTarget = false;
                m_covers.Add(copy);
            }

            for (int i = 0; i < m_covers.Count; i++)
            {
                Image image = m_covers[i];
                if (image == null)
                {
                    continue;
                }

                bool show = i < _targets.Count;
                image.enabled = show;
                image.gameObject.SetActive(show);
                if (!show)
                {
                    image.transform.SetParent(transform, false);
                    continue;
                }

                Draw(image, _targets[i]);
            }
        }

        /// <summary>
        /// 모든 커버를 표시하지 않습니다. 계층은 건드리지 않아 버튼 비활성화 도중에도 안전합니다.
        /// </summary>
        public void HideAll()
        {
            foreach (Image image in m_covers)
            {
                if (image != null)
                {
                    image.enabled = false;
                }
            }
            HideSpotlight();
        }

        public void ShowSpotlight(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                HideSpotlight();
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                HideSpotlight();
                return;
            }

            RectTransform surface = canvas.rootCanvas.transform as RectTransform;
            if (surface == null)
            {
                HideSpotlight();
                return;
            }

            target.GetWorldCorners(m_corners);
            Vector3 first = surface.InverseTransformPoint(m_corners[0]);
            float left = first.x;
            float right = first.x;
            float bottom = first.y;
            float top = first.y;
            for (int i = 1; i < m_corners.Length; i++)
            {
                Vector3 corner = surface.InverseTransformPoint(m_corners[i]);
                left = Mathf.Min(left, corner.x);
                right = Mathf.Max(right, corner.x);
                bottom = Mathf.Min(bottom, corner.y);
                top = Mathf.Max(top, corner.y);
            }

            Rect bounds = surface.rect;
            left = Mathf.Clamp(left, bounds.xMin, bounds.xMax);
            right = Mathf.Clamp(right, bounds.xMin, bounds.xMax);
            bottom = Mathf.Clamp(bottom, bounds.yMin, bounds.yMax);
            top = Mathf.Clamp(top, bounds.yMin, bounds.yMax);

            SetSpotlightRect(0, surface, bounds.xMin, bounds.yMin, left, bounds.yMax);
            SetSpotlightRect(1, surface, right, bounds.yMin, bounds.xMax, bounds.yMax);
            SetSpotlightRect(2, surface, left, top, right, bounds.yMax);
            SetSpotlightRect(3, surface, left, bounds.yMin, right, bottom);

            const float width = 4f;
            SetGuideRect(0, surface, left, bottom, right, bottom + width);
            SetGuideRect(1, surface, left, top - width, right, top);
            SetGuideRect(2, surface, left, bottom, left + width, top);
            SetGuideRect(3, surface, right - width, bottom, right, top);
        }

        private Image GetGuide(int index, RectTransform surface)
        {
            if (m_guides[index] == null)
            {
                GameObject guide = new GameObject("TutorialSpotlightGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                guide.transform.SetParent(surface, false);
                m_guides[index] = guide.GetComponent<Image>();
                m_guides[index].color = new Color(1f, .82f, .12f, 1f);
                m_guides[index].raycastTarget = false;
                m_guides[index].rectTransform.anchorMin = m_guides[index].rectTransform.anchorMax = new Vector2(.5f, .5f);
            }

            m_guides[index].rectTransform.SetAsLastSibling();
            return m_guides[index];
        }

        private void SetGuideRect(int index, RectTransform surface, float left, float bottom, float right, float top)
        {
            Image image = GetGuide(index, surface);
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = new Vector2((left + right) * .5f, (bottom + top) * .5f) - surface.rect.center;
            rect.sizeDelta = new Vector2(right - left, top - bottom);
            image.gameObject.SetActive(right > left && top > bottom);
        }

        private void SetSpotlightRect(int index, RectTransform surface, float left, float bottom, float right, float top)
        {
            if (m_spotlight[index] == null)
            {
                GameObject panel = new GameObject("TutorialSpotlightShade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panel.transform.SetParent(surface, false);
                m_spotlight[index] = panel.GetComponent<Image>();
                m_spotlight[index].color = new Color(0f, 0f, 0f, .65f);
                m_spotlight[index].raycastTarget = false;
            }

            Image image = m_spotlight[index];
            RectTransform rect = image.rectTransform;
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2((left + right) * .5f, (bottom + top) * .5f) - surface.rect.center;
            rect.sizeDelta = new Vector2(right - left, top - bottom);
            image.gameObject.SetActive(right > left && top > bottom);
        }

        private void HideSpotlight()
        {
            foreach (Image image in m_spotlight)
            {
                if (image != null)
                {
                    image.gameObject.SetActive(false);
                }
            }
            foreach (Image image in m_guides)
            {
                if (image != null)
                {
                    image.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyInitialCoverState()
        {
            if (m_hasAppliedInitialState || m_cover == null)
            {
                return;
            }

            m_hasAppliedInitialState = true;
            if (!m_covers.Contains(m_cover))
            {
                m_covers.Add(m_cover);
            }

            m_cover.raycastTarget = false;
            m_cover.gameObject.SetActive(false);
        }

        private static void Draw(Image _image, RectTransform _target)
        {
            Selectable button = _target.GetComponent<Selectable>();
            Image shape = button != null ? button.targetGraphic as Image : _target.GetComponent<Image>();
            if (shape != null)
            {
                _target = shape.rectTransform;
            }

            RectTransform rect = _image.rectTransform;
            if (rect.parent != _target)
            {
                rect.SetParent(_target, false);
            }

            rect.SetAsLastSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            _image.sprite = shape != null ? shape.sprite : null;
            _image.type = shape != null ? shape.type : Image.Type.Simple;
            _image.preserveAspect = shape != null && shape.preserveAspect;
            _image.fillCenter = shape == null || shape.fillCenter;
            _image.pixelsPerUnitMultiplier = shape != null ? shape.pixelsPerUnitMultiplier : 1f;
            if (shape != null)
            {
                _image.fillMethod = shape.fillMethod;
                _image.fillOrigin = shape.fillOrigin;
                _image.fillAmount = shape.fillAmount;
                _image.fillClockwise = shape.fillClockwise;
            }

            _image.color = new Color(1f, .82f, .12f, .12f + .22f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI)));
        }
    }
}
