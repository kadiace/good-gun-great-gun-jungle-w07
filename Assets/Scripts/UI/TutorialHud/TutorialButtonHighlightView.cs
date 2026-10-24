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
