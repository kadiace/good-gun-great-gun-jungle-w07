using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 저작된 MoneyDigits 마스크·숫자 템플릿 위에서 잔액 자릿수 굴림 연출을 담당한다.
    // 숫자 열의 폭은 표시 금액에 따라 달라지므로 저작된 템플릿 1개를 풀로 재사용하고 파괴·재생성하지 않는다.
    // 입력이나 실제 잔액은 다루지 않는다.
    [DisallowMultipleComponent]
    public sealed class MoneyRollDisplay : MonoBehaviour
    {
        private const string DISPLAY_NAME = "MoneyRollDisplay";

        [SerializeField, Tooltip("숫자 열이 클리핑되는 마스크 루트(RectMask2D). 표시 Text의 자식으로 저작된다.")]
        private RectTransform m_digitRoot;
        [SerializeField, Tooltip("굴리는 현재 숫자 한 자의 템플릿 Text")]
        private TMPro.TextMeshProUGUI m_digitTemplate;
        [SerializeField, Tooltip("다음 숫자를 미리 보여 주는 템플릿 Text")]
        private TMPro.TextMeshProUGUI m_nextDigitTemplate;
        [SerializeField, Tooltip("천 단위 쉼표 템플릿 Text")]
        private TMPro.TextMeshProUGUI m_separatorTemplate;

        private sealed class Digit
        {
            public TMPro.TextMeshProUGUI current;
            public TMPro.TextMeshProUGUI next;
            public float phase;
            public float start;
            public float end;
            public float duration;
        }

        private readonly List<Digit> m_digitPool = new List<Digit>();
        private readonly List<TMPro.TextMeshProUGUI> m_separatorPool = new List<TMPro.TextMeshProUGUI>();
        private readonly List<Digit> m_activeDigits = new List<Digit>();
        private readonly List<TMPro.TextMeshProUGUI> m_activeSeparators = new List<TMPro.TextMeshProUGUI>();
        private TMPro.TextMeshProUGUI source;
        private bool rolling;
        private float elapsed;
        private float digitHeight;
        private int amount;
        private int target;
        private bool m_hasLoggedReferenceError;

        public void Initialize(TMPro.TextMeshProUGUI _text)
        {
            source = _text;
            if (source == null)
            {
                return;
            }

            if (!ValidateReferences())
            {
                return;
            }

            m_digitRoot.gameObject.SetActive(false);
        }

        public void Show(int _value)
        {
            rolling = false;
            amount = target = _value;
            if (source == null)
            {
                return;
            }

            source.enabled = true;
            source.text = _value.ToString("N0");
            if (m_digitRoot != null)
            {
                m_digitRoot.gameObject.SetActive(false);
            }
        }

        public void SetColor(Color _color)
        {
            if (source != null)
            {
                source.color = _color;
            }

            if (m_digitRoot != null)
            {
                foreach (TMPro.TextMeshProUGUI digit in m_digitRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    digit.color = _color;
                }
            }
        }

        public void RollTo(int _value, float _duration)
        {
            if (source == null || m_digitRoot == null || _duration <= 0f)
            {
                Show(_value);
                return;
            }

            // 음수의 부호는 숫자 회전 열로 처리하지 않고 실제 잔액을 그대로 표시합니다.
            if (target < 0 || _value < 0)
            {
                Show(_value);
                return;
            }

            if (_value == target)
            {
                return;
            }

            int previous = target;
            int width = Mathf.Max(previous.ToString(CultureInfo.InvariantCulture).Length,
                _value.ToString(CultureInfo.InvariantCulture).Length);
            if (rolling && m_activeDigits.Count != width)
            {
                Show(previous);
            }

            if (!rolling || m_activeDigits.Count != width)
            {
                BuildDigits(width);
            }

            string beginning = previous.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
            target = _value;
            elapsed = 0f;
            string ending = _value.ToString(CultureInfo.InvariantCulture).PadLeft(m_activeDigits.Count, '0');
            for (int i = 0; i < m_activeDigits.Count; i++)
            {
                Digit digit = m_activeDigits[i];
                int floor = Mathf.FloorToInt(digit.phase);
                int current = Wrap(-floor);
                digit.start = digit.phase;
                // Each wheel travels according to the amount changed at its decimal place.
                // Bound extra turns so large payments stay readable within the short animation.
                long place = (long)System.Math.Pow(10, m_activeDigits.Count - i - 1);
                long steps = System.Math.Abs((long)previous / place - (long)_value / place);
                if (steps > 0 || rolling)
                {
                    int extraTurns = (int)System.Math.Min(2L, steps / 10);
                    digit.end = floor + extraTurns * 10 + Wrap(current - (ending[i] - '0'));
                    if (digit.end < digit.start)
                    {
                        digit.end += 10;
                    }
                }
                else
                {
                    digit.end = digit.start;
                }

                digit.duration = _duration * (0.8f + 0.2f * (i + 1f) / m_activeDigits.Count);
            }

            source.enabled = false;
            m_digitRoot.gameObject.SetActive(true);
            rolling = true;
        }

        private void BuildDigits(int _width)
        {
            m_activeDigits.Clear();
            m_activeSeparators.Clear();
            string initial = amount.ToString(CultureInfo.InvariantCulture).PadLeft(_width, '0');
            digitHeight = Mathf.Max(1f, m_digitRoot.rect.height);
            float maximumFontSize = digitHeight * 0.75f;
            if (source.GetComponentInParent<UIManager>(true) != null)
            {
                maximumFontSize += 3f;
            }

            float fontSize = Mathf.Min(source.fontSize, maximumFontSize);
            int separators = (_width - 1) / 3;
            float unit = Mathf.Min(fontSize * 0.65f, m_digitRoot.rect.width / (_width + separators * 0.45f));
            float totalWidth = unit * (_width + separators * 0.45f);
            // 기존 MoneyText의 오른쪽 끝을 유지합니다.
            float x = m_digitRoot.rect.width * 0.5f - totalWidth;
            for (int i = 0; i < _width; i++)
            {
                Digit digit = GetPooledDigit(i);
                ApplyTextLayout(digit.current, x + unit * 0.5f, unit, fontSize);
                ApplyTextLayout(digit.next, x + unit * 0.5f, unit, fontSize);
                float phase = Wrap(-(initial[i] - '0'));
                digit.phase = phase;
                digit.start = phase;
                digit.end = phase;
                PositionDigit(digit);
                digit.current.gameObject.SetActive(true);
                digit.next.gameObject.SetActive(true);
                m_activeDigits.Add(digit);
                x += unit;
                if (i < _width - 1 && (_width - i - 1) % 3 == 0)
                {
                    TMPro.TextMeshProUGUI comma = GetPooledSeparator(m_activeSeparators.Count);
                    ApplyTextLayout(comma, x + unit * 0.225f, unit * 0.45f, fontSize);
                    comma.text = ",";
                    comma.gameObject.SetActive(true);
                    m_activeSeparators.Add(comma);
                    x += unit * 0.45f;
                }
            }

            DeactivatePoolRemainder();
        }

        private Digit GetPooledDigit(int _index)
        {
            while (m_digitPool.Count <= _index)
            {
                Digit digit = new Digit
                {
                    current = InstantiateTemplate(m_digitTemplate),
                    next = InstantiateTemplate(m_nextDigitTemplate)
                };
                m_digitPool.Add(digit);
            }

            return m_digitPool[_index];
        }

        private TMPro.TextMeshProUGUI GetPooledSeparator(int _index)
        {
            while (m_separatorPool.Count <= _index)
            {
                m_separatorPool.Add(InstantiateTemplate(m_separatorTemplate));
            }

            return m_separatorPool[_index];
        }

        private TMPro.TextMeshProUGUI InstantiateTemplate(TMPro.TextMeshProUGUI _template)
        {
            TMPro.TextMeshProUGUI text = Instantiate(_template, m_digitRoot);
            text.name = _template.name;
            return text;
        }

        private void DeactivatePoolRemainder()
        {
            for (int i = m_activeDigits.Count; i < m_digitPool.Count; i++)
            {
                m_digitPool[i].current.gameObject.SetActive(false);
                m_digitPool[i].next.gameObject.SetActive(false);
            }

            for (int i = m_activeSeparators.Count; i < m_separatorPool.Count; i++)
            {
                m_separatorPool[i].gameObject.SetActive(false);
            }
        }

        // 템플릿 복제본에도 원본 Text의 현재 값(폰트·크기·색)을 그대로 적용해 외형을 유지한다.
        private void ApplyTextLayout(TMPro.TextMeshProUGUI _text, float _x, float _width, float _fontSize)
        {
            _text.rectTransform.anchorMin = _text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _text.rectTransform.sizeDelta = new Vector2(_width, digitHeight);
            _text.rectTransform.anchoredPosition = new Vector2(_x, 0f);
            _text.font = source.font;
            _text.fontStyle = source.fontStyle;
            _text.fontWeight = source.fontWeight;
            _text.fontSize = Mathf.RoundToInt(_fontSize);
            _text.color = source.color;
            _text.alignment = TMPro.TextAlignmentOptions.Midline;
            _text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            _text.overflowMode = TMPro.TextOverflowModes.Overflow;
            _text.raycastTarget = false;
        }

        private static int Wrap(int _value)
        {
            return (_value % 10 + 10) % 10;
        }

        private void PositionDigit(Digit _digit)
        {
            int step = Mathf.FloorToInt(_digit.phase);
            float offset = _digit.phase - step;
            _digit.current.text = Wrap(-step).ToString();
            _digit.next.text = Wrap(-step - 1).ToString();
            Vector2 position = _digit.current.rectTransform.anchoredPosition;
            position.y = -offset * digitHeight;
            _digit.current.rectTransform.anchoredPosition = position;
            position.y = (1f - offset) * digitHeight;
            _digit.next.rectTransform.anchoredPosition = position;
        }

        private void Update()
        {
            if (!rolling)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            bool finished = true;
            foreach (Digit digit in m_activeDigits)
            {
                float t = Mathf.Clamp01(elapsed / digit.duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                digit.phase = Mathf.Lerp(digit.start, digit.end, eased);
                PositionDigit(digit);
                if (t < 1f)
                {
                    finished = false;
                }
            }

            if (finished)
            {
                Show(target);
            }
        }

        private void OnDisable()
        {
            Show(target);
        }

        private void OnDestroy()
        {
            if (source != null)
            {
                source.enabled = true;
            }

            // 마스크·템플릿은 프리팹에 저작된 오브젝트이므로 파괴하지 않고 숨긴다.
            if (m_digitRoot != null)
            {
                m_digitRoot.gameObject.SetActive(false);
            }
        }

        private bool ValidateReferences()
        {
            bool isValid = m_digitRoot != null && m_digitTemplate != null &&
                m_nextDigitTemplate != null && m_separatorTemplate != null &&
                m_digitRoot.IsChildOf(source.transform);
            if (!isValid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    $"[{DISPLAY_NAME}] 필수 저작 참조 누락: digitRoot={m_digitRoot != null}, " +
                    $"digitTemplate={m_digitTemplate != null}, nextDigitTemplate={m_nextDigitTemplate != null}, " +
                    $"separatorTemplate={m_separatorTemplate != null}, " +
                    $"digitRootInText={m_digitRoot != null && m_digitRoot.IsChildOf(source.transform)}. " +
                    "Tools/UI/Authoring/Settlement/Author 실행 결과를 확인하세요.", this);
            }

            return isValid;
        }
    }
}
