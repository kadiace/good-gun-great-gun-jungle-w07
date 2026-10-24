using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using TMPro;

namespace WeaponWorkshop
{
    // 도색 패널의 저작 참조·입력 이벤트·표시 적용을 담당하는 View다.
    // 재질 목록/페이지/색상 선택 상태는 Presenter가 계산하고 View는 받은 값만 적용한다.
    public sealed class PaintPanelView : MonoBehaviour
    {
        private const string VIEW_NAME = "PaintPanelView";
        private const int SQUARE_RESOLUTION = 64;
        private const int HUE_RESOLUTION = 128;

        [SerializeField] private TMP_Text m_title;
        [SerializeField] private Button m_previousPage;
        [SerializeField] private Button m_nextPage;
        [SerializeField] private TMP_Text m_pageLabel;
        [SerializeField] private Button[] m_slotButtons = Array.Empty<Button>();
        [SerializeField] private RawImage[] m_slotSwatches = Array.Empty<RawImage>();
        [SerializeField] private TMP_Text[] m_slotLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Outline[] m_slotHighlights = Array.Empty<Outline>();
        [SerializeField] private Button m_customColorButton;
        [SerializeField] private GameObject m_materialSelection;
        [SerializeField] private GameObject m_customColorPicker;
        [SerializeField] private RawImage m_preview;
        [SerializeField] private TMP_Text m_materialName;
        [SerializeField] private TMP_Text m_status;
        [SerializeField] private RawImage m_customPreview;
        [SerializeField] private TMP_Text m_customHex;
        [SerializeField] private TMP_InputField[] m_rgbInputs = Array.Empty<TMP_InputField>();
        [SerializeField] private RawImage m_colorSpectrum;
        [SerializeField] private RawImage m_hueSpectrum;
        [SerializeField] private Button m_applyColor;
        [SerializeField] private TMP_Text m_applyLabel;
        [SerializeField] private Button m_back;
        [SerializeField] private Button m_undo;
        [SerializeField] private Button m_fill;
        [SerializeField] private Button m_paintAll;

        private UnityAction[] m_slotActions;
        private UnityAction<string>[] m_rgbActions;
        private ColorPickArea m_colorArea;
        private ColorPickArea m_hueArea;
        private Texture2D m_squareTexture;
        private Texture2D m_hueTexture;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action PreviousPageRequested;
        public event Action NextPageRequested;
        public event Action<int> SlotRequested;
        public event Action CustomColorModeRequested;
        public event Action ApplyColorRequested;
        public event Action BackRequested;
        public event Action UndoRequested;
        public event Action FillRequested;
        public event Action PaintAllRequested;
        public event Action<int, string> RgbSubmitted;
        public event Action<Vector2> SpectrumPicked;
        public event Action<Vector2> HuePicked;
        public event Action ColorApplyOnReleaseRequested;

        public int SlotCount => m_slotButtons != null ? m_slotButtons.Length : 0;
        public bool IsInitialized => m_isInitialized;

        // 브리지의 실효 참조를 옮겨 받는다. 씬 오버라이드가 반영된 값이 그대로 쓰인다.
        public void Configure(TMP_Text _title, Button _previousPage, Button _nextPage, TMP_Text _pageLabel,
            Button[] _slotButtons, RawImage[] _slotSwatches, TMP_Text[] _slotLabels, Outline[] _slotHighlights,
            Button _customColorButton, GameObject _materialSelection, GameObject _customColorPicker,
            RawImage _preview, TMP_Text _materialName, TMP_Text _status, RawImage _customPreview,
            TMP_Text _customHex, TMP_InputField[] _rgbInputs, RawImage _colorSpectrum, RawImage _hueSpectrum,
            Button _applyColor, TMP_Text _applyLabel, Button _back, Button _undo, Button _fill, Button _paintAll)
        {
            m_title = _title;
            m_previousPage = _previousPage;
            m_nextPage = _nextPage;
            m_pageLabel = _pageLabel;
            m_slotButtons = _slotButtons != null ? _slotButtons : Array.Empty<Button>();
            m_slotSwatches = _slotSwatches != null ? _slotSwatches : Array.Empty<RawImage>();
            m_slotLabels = _slotLabels != null ? _slotLabels : Array.Empty<TMP_Text>();
            m_slotHighlights = _slotHighlights != null ? _slotHighlights : Array.Empty<Outline>();
            m_customColorButton = _customColorButton;
            m_materialSelection = _materialSelection;
            m_customColorPicker = _customColorPicker;
            m_preview = _preview;
            m_materialName = _materialName;
            m_status = _status;
            m_customPreview = _customPreview;
            m_customHex = _customHex;
            m_rgbInputs = _rgbInputs != null ? _rgbInputs : Array.Empty<TMP_InputField>();
            m_colorSpectrum = _colorSpectrum;
            m_hueSpectrum = _hueSpectrum;
            m_applyColor = _applyColor;
            m_applyLabel = _applyLabel;
            m_back = _back;
            m_undo = _undo;
            m_fill = _fill;
            m_paintAll = _paintAll;
        }

        // 색상표 텍스처를 만들고 버튼·포인터 입력을 연결한다. 중복 호출에 안전하다.
        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            m_squareTexture = new Texture2D(SQUARE_RESOLUTION, SQUARE_RESOLUTION, TextureFormat.RGBA32, false)
            {
                name = "Paint Color Spectrum",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            m_hueTexture = new Texture2D(1, HUE_RESOLUTION, TextureFormat.RGBA32, false)
            {
                name = "Paint Hue Spectrum",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            m_colorSpectrum.texture = m_squareTexture;
            m_hueSpectrum.texture = m_hueTexture;
            UpdateSquareTexture(0f);
            UpdateHueTexture();

            m_colorArea = m_colorSpectrum.GetComponent<ColorPickArea>();
            m_hueArea = m_hueSpectrum.GetComponent<ColorPickArea>();
            m_colorArea.Picked += OnSpectrumPicked;
            m_colorArea.Released += OnColorReleased;
            m_hueArea.Picked += OnHuePicked;
            m_hueArea.Released += OnColorReleased;

            m_previousPage.onClick.AddListener(OnPreviousPage);
            m_nextPage.onClick.AddListener(OnNextPage);
            m_customColorButton.onClick.AddListener(OnCustomColorMode);
            m_applyColor.onClick.AddListener(OnApplyColor);
            m_back.onClick.AddListener(OnBack);
            m_undo.onClick.AddListener(OnUndo);
            m_fill.onClick.AddListener(OnFill);
            m_paintAll.onClick.AddListener(OnPaintAll);

            m_slotActions = new UnityAction[m_slotButtons.Length];
            for (int i = 0; i < m_slotButtons.Length; i++)
            {
                int slot = i;
                UnityAction action = () => SlotRequested?.Invoke(slot);
                m_slotActions[i] = action;
                m_slotButtons[i].onClick.AddListener(action);
            }

            m_rgbActions = new UnityAction<string>[m_rgbInputs.Length];
            for (int i = 0; i < m_rgbInputs.Length; i++)
            {
                int channel = i;
                UnityAction<string> action = _value => RgbSubmitted?.Invoke(channel, _value);
                m_rgbActions[i] = action;
                m_rgbInputs[i].onEndEdit.AddListener(action);
            }

            SetActive(m_materialSelection, true);
            SetActive(m_customColorPicker, false);
            m_isInitialized = true;
            return true;
        }

        // 버튼·포인터 리스너와 생성한 텍스처를 해제한다.
        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_previousPage.onClick.RemoveListener(OnPreviousPage);
            m_nextPage.onClick.RemoveListener(OnNextPage);
            m_customColorButton.onClick.RemoveListener(OnCustomColorMode);
            m_applyColor.onClick.RemoveListener(OnApplyColor);
            m_back.onClick.RemoveListener(OnBack);
            m_undo.onClick.RemoveListener(OnUndo);
            m_fill.onClick.RemoveListener(OnFill);
            m_paintAll.onClick.RemoveListener(OnPaintAll);
            for (int i = 0; i < m_slotActions.Length; i++)
            {
                if (m_slotButtons[i] != null && m_slotActions[i] != null)
                {
                    m_slotButtons[i].onClick.RemoveListener(m_slotActions[i]);
                }
            }

            for (int i = 0; i < m_rgbActions.Length; i++)
            {
                if (m_rgbInputs[i] != null && m_rgbActions[i] != null)
                {
                    m_rgbInputs[i].onEndEdit.RemoveListener(m_rgbActions[i]);
                }
            }

            if (m_colorArea != null)
            {
                m_colorArea.Picked -= OnSpectrumPicked;
                m_colorArea.Released -= OnColorReleased;
            }

            if (m_hueArea != null)
            {
                m_hueArea.Picked -= OnHuePicked;
                m_hueArea.Released -= OnColorReleased;
            }

            DestroyTexture(ref m_squareTexture);
            DestroyTexture(ref m_hueTexture);
            m_slotActions = null;
            m_rgbActions = null;
            m_isInitialized = false;
        }

        // 현재 색의 RGB 값을 입력칸에 표시한다. 입력 이벤트는 발생시키지 않는다.
        public void SetRgbInputs(Color32 _color)
        {
            if (m_rgbInputs == null || m_rgbInputs.Length < 3 || m_rgbInputs[0] == null)
            {
                return;
            }

            m_rgbInputs[0].text = _color.r.ToString();
            m_rgbInputs[1].text = _color.g.ToString();
            m_rgbInputs[2].text = _color.b.ToString();
        }

        // 색조가 바뀌면 채도/밝기 사각형 텍스처를 다시 계산한다.
        public void SetSpectrumHue(float _hue)
        {
            UpdateSquareTexture(_hue);
        }

        // Presenter가 계산한 한 프레임 표시 값을 저작된 오브젝트에 적용한다.
        public void Render(PaintPanelState _state)
        {
            if (!m_isInitialized || _state == null)
            {
                return;
            }

            SetText(m_title, _state.Title);
            SetActive(m_materialSelection, !_state.CustomColorMode);
            SetActive(m_customColorPicker, _state.CustomColorMode);
            SetText(m_pageLabel, _state.PageLabel);
            m_previousPage.interactable = _state.CanGoPrevious;
            m_nextPage.interactable = _state.CanGoNext;

            int count = Mathf.Min(m_slotButtons.Length, _state.Slots.Length);
            for (int i = 0; i < count; i++)
            {
                PaintSlotState slot = _state.Slots[i];
                SetActive(m_slotButtons[i].gameObject, slot.Visible);
                if (!slot.Visible)
                {
                    continue;
                }

                if (m_slotSwatches[i].texture != slot.SwatchTexture)
                {
                    m_slotSwatches[i].texture = slot.SwatchTexture;
                }

                m_slotSwatches[i].color = slot.SwatchColor;
                SetText(m_slotLabels[i], slot.Label);
                m_slotHighlights[i].enabled = slot.Highlighted;
            }

            if (m_preview.texture != _state.PreviewTexture)
            {
                m_preview.texture = _state.PreviewTexture;
            }

            m_preview.color = _state.PreviewColor;
            SetText(m_materialName, _state.MaterialName);
            m_customPreview.color = _state.CustomColor;
            SetText(m_customHex, _state.CustomHex);
            if (m_customColorButton.targetGraphic != null)
            {
                m_customColorButton.targetGraphic.color = _state.CustomColorButtonColor;
            }

            SetText(m_applyLabel, _state.ApplyLabel);
            SetText(m_status, _state.Status);
            if (m_fill.targetGraphic != null)
            {
                m_fill.targetGraphic.color = _state.FillColor;
            }

            m_paintAll.interactable = _state.PaintAllInteractable;
        }

        private void Awake()
        {
            if (m_isInitialized)
            {
                return;
            }

            SetActive(m_materialSelection, true);
            SetActive(m_customColorPicker, false);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnPreviousPage()
        {
            PreviousPageRequested?.Invoke();
        }

        private void OnNextPage()
        {
            NextPageRequested?.Invoke();
        }

        private void OnCustomColorMode()
        {
            CustomColorModeRequested?.Invoke();
        }

        private void OnApplyColor()
        {
            ApplyColorRequested?.Invoke();
        }

        private void OnBack()
        {
            BackRequested?.Invoke();
        }

        private void OnUndo()
        {
            UndoRequested?.Invoke();
        }

        private void OnFill()
        {
            FillRequested?.Invoke();
        }

        private void OnPaintAll()
        {
            PaintAllRequested?.Invoke();
        }

        private void OnSpectrumPicked(Vector2 _point)
        {
            SpectrumPicked?.Invoke(_point);
        }

        private void OnHuePicked(Vector2 _point)
        {
            HuePicked?.Invoke(_point);
        }

        private void OnColorReleased()
        {
            ColorApplyOnReleaseRequested?.Invoke();
        }

        private void UpdateSquareTexture(float _hue)
        {
            if (m_squareTexture == null)
            {
                return;
            }

            int size = m_squareTexture.width;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = Color.HSVToRGB(_hue, (float)x / (size - 1), (float)y / (size - 1));
                }
            }

            m_squareTexture.SetPixels(pixels);
            m_squareTexture.Apply(false, false);
        }

        private void UpdateHueTexture()
        {
            if (m_hueTexture == null)
            {
                return;
            }

            int height = m_hueTexture.height;
            Color[] pixels = new Color[height];
            for (int y = 0; y < height; y++)
            {
                pixels[y] = Color.HSVToRGB(1f - (float)y / (height - 1), 1f, 1f);
            }

            m_hueTexture.SetPixels(pixels);
            m_hueTexture.Apply(false, false);
        }

        private bool ValidateReferences()
        {
            bool slotsValid = m_slotButtons != null && m_slotSwatches != null && m_slotLabels != null &&
                m_slotHighlights != null && m_slotButtons.Length > 0 &&
                m_slotButtons.Length == m_slotSwatches.Length &&
                m_slotButtons.Length == m_slotLabels.Length &&
                m_slotButtons.Length == m_slotHighlights.Length &&
                !Array.Exists(m_slotButtons, _button => _button == null) &&
                !Array.Exists(m_slotSwatches, _swatch => _swatch == null) &&
                !Array.Exists(m_slotLabels, _label => _label == null) &&
                !Array.Exists(m_slotHighlights, _highlight => _highlight == null);
            bool rgbValid = m_rgbInputs != null && m_rgbInputs.Length == 3 &&
                !Array.Exists(m_rgbInputs, _input => _input == null);
            bool spectrumValid = m_colorSpectrum != null && m_hueSpectrum != null &&
                m_colorSpectrum.GetComponent<ColorPickArea>() != null &&
                m_hueSpectrum.GetComponent<ColorPickArea>() != null;
            if (m_title != null && m_previousPage != null && m_nextPage != null && m_pageLabel != null &&
                slotsValid && m_customColorButton != null && m_materialSelection != null &&
                m_customColorPicker != null && m_preview != null && m_materialName != null &&
                m_status != null && m_customPreview != null && m_customHex != null && rgbValid &&
                spectrumValid && m_applyColor != null && m_applyLabel != null && m_back != null &&
                m_undo != null && m_fill != null && m_paintAll != null)
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                List<string> missing = new List<string>();
                if (m_title == null)
                {
                    missing.Add(nameof(m_title));
                }

                if (m_previousPage == null)
                {
                    missing.Add(nameof(m_previousPage));
                }

                if (m_nextPage == null)
                {
                    missing.Add(nameof(m_nextPage));
                }

                if (m_pageLabel == null)
                {
                    missing.Add(nameof(m_pageLabel));
                }

                if (!slotsValid)
                {
                    missing.Add(nameof(m_slotButtons) + "/" + nameof(m_slotSwatches) + "/" +
                        nameof(m_slotLabels) + "/" + nameof(m_slotHighlights));
                }

                if (m_customColorButton == null)
                {
                    missing.Add(nameof(m_customColorButton));
                }

                if (m_materialSelection == null)
                {
                    missing.Add(nameof(m_materialSelection));
                }

                if (m_customColorPicker == null)
                {
                    missing.Add(nameof(m_customColorPicker));
                }

                if (m_preview == null)
                {
                    missing.Add(nameof(m_preview));
                }

                if (m_materialName == null)
                {
                    missing.Add(nameof(m_materialName));
                }

                if (m_status == null)
                {
                    missing.Add(nameof(m_status));
                }

                if (m_customPreview == null)
                {
                    missing.Add(nameof(m_customPreview));
                }

                if (m_customHex == null)
                {
                    missing.Add(nameof(m_customHex));
                }

                if (!rgbValid)
                {
                    missing.Add(nameof(m_rgbInputs));
                }

                if (!spectrumValid)
                {
                    missing.Add(nameof(m_colorSpectrum) + "/" + nameof(m_hueSpectrum) + "(ColorPickArea)");
                }

                if (m_applyColor == null)
                {
                    missing.Add(nameof(m_applyColor));
                }

                if (m_applyLabel == null)
                {
                    missing.Add(nameof(m_applyLabel));
                }

                if (m_back == null)
                {
                    missing.Add(nameof(m_back));
                }

                if (m_undo == null)
                {
                    missing.Add(nameof(m_undo));
                }

                if (m_fill == null)
                {
                    missing.Add(nameof(m_fill));
                }

                if (m_paintAll == null)
                {
                    missing.Add(nameof(m_paintAll));
                }

                Debug.LogError($"[{VIEW_NAME}] Initialize 실패: 누락/불일치 " + string.Join(", ", missing));
            }

            return false;
        }

        private static void SetText(TMP_Text _text, string _value)
        {
            if (_text != null && _text.text != _value)
            {
                _text.text = _value;
            }
        }

        private static void SetActive(GameObject _target, bool _active)
        {
            if (_target != null && _target.activeSelf != _active)
            {
                _target.SetActive(_active);
            }
        }

        private static void DestroyTexture(ref Texture2D _texture)
        {
            if (_texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_texture);
            }
            else
            {
                DestroyImmediate(_texture);
            }

            _texture = null;
        }
    }
}
