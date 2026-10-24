using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 차지 게이지의 저작된 참조, 칸 표시, 라벨만 담당한다.
    // 무기·충전 상태 조회는 ChargeGaugePresenter가 맡고, 가변 칸은 저작 템플릿을 복제해 재사용한다.
    public sealed class ChargeGaugeView : MonoBehaviour
    {
        private const string SCREEN_NAME = "ChargeGaugeView";

        [SerializeField] private GameObject m_root;
        [SerializeField] private GameObject m_segmentTemplate;
        [SerializeField] private TMP_Text m_label;

        private readonly List<Image> m_backgrounds = new List<Image>();
        private readonly List<Image> m_fills = new List<Image>();
        private Color m_emptyColor = new Color(1f, 1f, 1f, 0.15f);
        private Color m_fillColor = new Color(0.45f, 0.85f, 1f, 0.95f);
        private Color m_fullColor = new Color(1f, 0.6f, 0.15f, 1f);
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            // 브리지가 먼저 Initialize한 뒤 늦게 실행되는 Awake가 표시를 되돌리지 않게 한다.
            if (!m_isInitialized)
            {
                ApplyHiddenState();
            }
        }

        public void Configure(Color _emptyColor, Color _fillColor, Color _fullColor)
        {
            m_emptyColor = _emptyColor;
            m_fillColor = _fillColor;
            m_fullColor = _fullColor;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            ApplyHiddenState();
            if (!ValidateReferences())
            {
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        public void Dispose()
        {
            ApplyHiddenState();
            m_isInitialized = false;
        }

        public void Hide()
        {
            ApplyHiddenState();
        }

        public void SetCharge(bool _visible, float _progress, int _chargeLevel, int _maxLevel)
        {
            if (!m_isInitialized && !Initialize())
            {
                return;
            }

            if (!_visible || _maxLevel <= 0)
            {
                ApplyHiddenState();
                return;
            }

            EnsureSegments(_maxLevel);
            SetRootVisible(true);
            bool full = _chargeLevel >= _maxLevel;
            float filled = _progress * _maxLevel;
            for (int i = 0; i < m_fills.Count; i++)
            {
                float amount = Mathf.Clamp01(filled - i);
                m_fills[i].color = full
                    ? m_fullColor
                    : amount >= 1f ? m_fillColor : m_fillColor * new Color(1f, 1f, 1f, 0.6f);
                SetFillAmount(m_fills[i], amount);
            }

            if (m_label != null)
            {
                m_label.text = full ? $"차지 MAX ({_maxLevel})" : $"차지 {_chargeLevel}/{_maxLevel}";
                m_label.color = full ? m_fullColor : Color.white;
            }
        }

        private void EnsureSegments(int _count)
        {
            while (m_backgrounds.Count > _count)
            {
                int last = m_backgrounds.Count - 1;
                if (m_backgrounds[last] != null)
                {
                    // Background is the cloned segment root; its parent owns every segment and the template.
                    GameObject segment = m_backgrounds[last].gameObject;
                    segment.SetActive(false);
                    Destroy(segment);
                }

                m_backgrounds.RemoveAt(last);
                m_fills.RemoveAt(last);
            }

            while (m_backgrounds.Count < _count)
            {
                if (!AddSegment())
                {
                    return;
                }
            }
        }

        private bool AddSegment()
        {
            if (m_segmentTemplate == null)
            {
                LogReferenceError();
                return false;
            }

            GameObject clone = Instantiate(m_segmentTemplate, m_segmentTemplate.transform.parent);
            clone.name = "Segment" + (m_backgrounds.Count + 1);
            Image background = clone.GetComponent<Image>();
            Transform fillTransform = clone.transform.Find("Fill");
            Image fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (background == null || fill == null)
            {
                Destroy(clone);
                LogReferenceError();
                return false;
            }

            background.color = m_emptyColor;
            background.raycastTarget = false;
            fill.raycastTarget = false;
            clone.SetActive(true);
            m_backgrounds.Add(background);
            m_fills.Add(fill);
            return true;
        }

        // 칸 채움은 스프라이트 없이 앵커 너비로 처리한다. 마스크·머티리얼 추가 없이 저작 구조만 쓴다.
        private static void SetFillAmount(Image _fill, float _amount)
        {
            RectTransform rect = _fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Clamp01(_amount), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ApplyHiddenState()
        {
            SetRootVisible(false);
        }

        private void SetRootVisible(bool _visible)
        {
            if (m_root != null && m_root.activeSelf != _visible)
            {
                m_root.SetActive(_visible);
            }
        }

        private bool ValidateReferences()
        {
            Image templateImage = m_segmentTemplate != null ? m_segmentTemplate.GetComponent<Image>() : null;
            Image templateFill = m_segmentTemplate != null
                ? m_segmentTemplate.transform.Find("Fill")?.GetComponent<Image>()
                : null;
            bool isValid = m_root != null && m_segmentTemplate != null && m_label != null &&
                templateImage != null && templateFill != null;
            if (!isValid)
            {
                LogReferenceError();
            }

            return isValid;
        }

        private void LogReferenceError()
        {
            if (m_hasLoggedReferenceError)
            {
                return;
            }

            m_hasLoggedReferenceError = true;
            Debug.LogError(
                $"[{SCREEN_NAME}] Initialize 실패: 필수 참조 누락 " +
                $"(m_root={m_root != null}, m_segmentTemplate={m_segmentTemplate != null}, " +
                $"m_label={m_label != null}). Tools/UI/Authoring/CombatHud/Author 실행 결과를 확인하세요.", this);
        }
    }
}
