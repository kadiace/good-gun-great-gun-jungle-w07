using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 기존 돈 TMPro.TextMeshProUGUI 위에 클리핑된 숫자 열을 생성합니다. 입력이나 실제 잔액은 다루지 않습니다.
    [DisallowMultipleComponent]
    public sealed class MoneyRollDisplay : MonoBehaviour
    {
        private sealed class Digit
        {
            public TMPro.TextMeshProUGUI current;
            public TMPro.TextMeshProUGUI next;
            public float phase;
            public float start;
            public float end;
            public float duration;
        }

        private readonly List<Digit> digits = new List<Digit>();
        private TMPro.TextMeshProUGUI source;
        private RectTransform root;
        private bool rolling;
        private float elapsed;
        private float digitHeight;
        private int amount;
        private int target;

        public void Initialize(TMPro.TextMeshProUGUI text)
        {
            source = text;
            if (source == null || root != null) return;
            var obj = new GameObject("MoneyDigits", typeof(RectTransform), typeof(RectMask2D));
            root = obj.GetComponent<RectTransform>();
            root.SetParent(source.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            obj.SetActive(false);
        }

        public void Show(int value)
        {
            rolling = false;
            amount = target = value;
            if (source == null) return;
            source.enabled = true;
            source.text = value.ToString("N0");
            if (root != null) root.gameObject.SetActive(false);
        }

        public void SetColor(Color color)
        {
            if (source != null) source.color = color;
            if (root != null)
                foreach (TMPro.TextMeshProUGUI digit in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true)) digit.color = color;
        }

        public void RollTo(int value, float duration)
        {
            if (source == null || root == null || duration <= 0f) { Show(value); return; }
            // 음수의 부호는 숫자 회전 열로 처리하지 않고 실제 잔액을 그대로 표시합니다.
            if (target < 0 || value < 0) { Show(value); return; }
            if (value == target) return;
            int previous = target;
            int width = Mathf.Max(previous.ToString(CultureInfo.InvariantCulture).Length,
                value.ToString(CultureInfo.InvariantCulture).Length);
            if (rolling && digits.Count != width) Show(previous);
            if (!rolling || digits.Count != width) BuildDigits(width);
            string beginning = previous.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
            target = value;
            elapsed = 0f;
            string ending = value.ToString(CultureInfo.InvariantCulture).PadLeft(digits.Count, '0');
            for (int i = 0; i < digits.Count; i++)
            {
                Digit digit = digits[i];
                int floor = Mathf.FloorToInt(digit.phase);
                int current = Wrap(-floor);
                digit.start = digit.phase;
                // Each wheel travels according to the amount changed at its decimal place.
                // Bound extra turns so large payments stay readable within the short animation.
                long place = (long)System.Math.Pow(10, digits.Count - i - 1);
                long steps = System.Math.Abs((long)previous / place - (long)value / place);
                if (steps > 0 || rolling)
                {
                    int extraTurns = (int)System.Math.Min(2L, steps / 10);
                    digit.end = floor + extraTurns * 10 + Wrap(current - (ending[i] - '0'));
                    if (digit.end < digit.start) digit.end += 10;
                }
                else digit.end = digit.start;
                digit.duration = duration * (0.8f + 0.2f * (i + 1f) / digits.Count);
            }
            source.enabled = false;
            root.gameObject.SetActive(true);
            rolling = true;
        }

        private void BuildDigits(int width)
        {
            foreach (Transform child in root) Destroy(child.gameObject);
            digits.Clear();
            string initial = amount.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
            digitHeight = Mathf.Max(1f, root.rect.height);
            float maximumFontSize = digitHeight * 0.75f;
            if (source.GetComponentInParent<UIManager>(true) != null) maximumFontSize += 4f;
            float fontSize = Mathf.Min(source.fontSize, maximumFontSize);
            int separators = (width - 1) / 3;
            float unit = Mathf.Min(fontSize * 0.65f, root.rect.width / (width + separators * 0.45f));
            float totalWidth = unit * (width + separators * 0.45f);
            // 기존 MoneyText의 오른쪽 끝을 유지합니다.
            float x = root.rect.width * 0.5f - totalWidth;
            for (int i = 0; i < width; i++)
            {
                TMPro.TextMeshProUGUI current = MakeText("Digit", x + unit * 0.5f, unit, fontSize);
                TMPro.TextMeshProUGUI next = MakeText("NextDigit", x + unit * 0.5f, unit, fontSize);
                float phase = Wrap(-(initial[i] - '0'));
                var digit = new Digit { current = current, next = next, phase = phase, start = phase, end = phase };
                digits.Add(digit);
                PositionDigit(digit);
                x += unit;
                if (i < width - 1 && (width - i - 1) % 3 == 0)
                {
                    TMPro.TextMeshProUGUI comma = MakeText("Separator", x + unit * 0.225f, unit * 0.45f, fontSize);
                    comma.text = ",";
                    x += unit * 0.45f;
                }
            }
        }

        private TMPro.TextMeshProUGUI MakeText(string name, float x, float width, float fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            TMPro.TextMeshProUGUI text = obj.GetComponent<TMPro.TextMeshProUGUI>();
            text.rectTransform.SetParent(root, false);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(width, digitHeight);
            text.rectTransform.anchoredPosition = new Vector2(x, 0f);
            text.font = source.font;
            text.fontStyle = source.fontStyle;
            text.fontWeight = source.fontWeight;
            text.fontSize = Mathf.RoundToInt(fontSize);
            text.color = source.color;
            text.alignment = TMPro.TextAlignmentOptions.Midline;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static int Wrap(int value) => (value % 10 + 10) % 10;

        private void PositionDigit(Digit digit)
        {
            int step = Mathf.FloorToInt(digit.phase);
            float offset = digit.phase - step;
            digit.current.text = Wrap(-step).ToString();
            digit.next.text = Wrap(-step - 1).ToString();
            Vector2 position = digit.current.rectTransform.anchoredPosition;
            position.y = -offset * digitHeight;
            digit.current.rectTransform.anchoredPosition = position;
            position.y = (1f - offset) * digitHeight;
            digit.next.rectTransform.anchoredPosition = position;
        }

        private void Update()
        {
            if (!rolling) return;
            elapsed += Time.unscaledDeltaTime;
            bool finished = true;
            foreach (Digit digit in digits)
            {
                float t = Mathf.Clamp01(elapsed / digit.duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                digit.phase = Mathf.Lerp(digit.start, digit.end, eased);
                PositionDigit(digit);
                if (t < 1f) finished = false;
            }
            if (finished) Show(target);
        }

        private void OnDisable() => Show(target);

        private void OnDestroy()
        {
            if (source != null) source.enabled = true;
            if (root != null) Destroy(root.gameObject);
        }
    }
}
