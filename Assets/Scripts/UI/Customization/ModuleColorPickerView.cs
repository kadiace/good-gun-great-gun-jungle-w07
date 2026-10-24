using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 탄색 모듈 색상 선택 창의 저작 참조·입력 이벤트·표시 적용을 담당하는 View다.
    // 열림 여부와 색 계산은 Presenter가 결정하고, View는 받은 HSV 값만 표시한다.
    public sealed class ModuleColorPickerView : MonoBehaviour
    {
        private const string VIEW_NAME = "ModuleColorPickerView";
        private const int SQUARE_RESOLUTION = 64;
        private const int HUE_RESOLUTION = 128;

        [SerializeField] private GameObject m_panel;
        [SerializeField] private RawImage m_square;
        [SerializeField] private RawImage m_hue;
        [SerializeField] private RectTransform m_squareMarker;
        [SerializeField] private RectTransform m_hueMarker;
        [SerializeField] private Image m_preview;
        [SerializeField] private Button m_confirm;
        [SerializeField] private Button m_cancel;

        private Texture2D m_squareTexture;
        private Texture2D m_hueTexture;
        private ColorPickArea m_squareArea;
        private ColorPickArea m_hueArea;
        private float m_renderedHue = -1f;
        private bool m_isInitialized;
        private bool m_hasLoggedReferenceError;

        public event Action<Vector2> SquarePicked;
        public event Action<Vector2> HuePicked;
        public event Action ConfirmRequested;
        public event Action CancelRequested;

        public bool IsInitialized => m_isInitialized;
        public bool IsOpen => m_panel != null && m_panel.activeSelf;

        // 브리지가 읽은 실효 참조를 옮겨 받는다.
        public void Configure(GameObject _panel, RawImage _square, RawImage _hue, RectTransform _squareMarker,
            RectTransform _hueMarker, Image _preview, Button _confirm, Button _cancel)
        {
            m_panel = _panel;
            m_square = _square;
            m_hue = _hue;
            m_squareMarker = _squareMarker;
            m_hueMarker = _hueMarker;
            m_preview = _preview;
            m_confirm = _confirm;
            m_cancel = _cancel;
        }

        // 색상표 텍스처를 만들고 포인터·버튼 입력을 연결한다. 중복 호출에 안전하다.
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
                name = "Module Color Spectrum",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            m_hueTexture = new Texture2D(1, HUE_RESOLUTION, TextureFormat.RGBA32, false)
            {
                name = "Module Hue Spectrum",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            m_square.texture = m_squareTexture;
            m_hue.texture = m_hueTexture;
            UpdateHueTexture();

            m_squareArea = m_square.GetComponent<ColorPickArea>();
            m_hueArea = m_hue.GetComponent<ColorPickArea>();
            m_squareArea.Picked += OnSquarePicked;
            m_hueArea.Picked += OnHuePicked;

            m_confirm.onClick.AddListener(OnConfirmClicked);
            m_cancel.onClick.AddListener(OnCancelClicked);
            SetOpen(false);
            m_isInitialized = true;
            Render(0f, 1f, 1f);
            return true;
        }

        // 버튼·포인터 리스너와 생성한 텍스처를 해제한다. 중복 호출에 안전하다.
        public void Dispose()
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_confirm.onClick.RemoveListener(OnConfirmClicked);
            m_cancel.onClick.RemoveListener(OnCancelClicked);
            if (m_squareArea != null)
            {
                m_squareArea.Picked -= OnSquarePicked;
            }

            if (m_hueArea != null)
            {
                m_hueArea.Picked -= OnHuePicked;
            }

            DestroyTexture(ref m_squareTexture);
            DestroyTexture(ref m_hueTexture);
            m_renderedHue = -1f;
            m_isInitialized = false;
        }

        public void SetOpen(bool _open)
        {
            if (m_panel != null && m_panel.activeSelf != _open)
            {
                m_panel.SetActive(_open);
            }
        }

        // Presenter가 계산한 HSV 값을 미리보기·마커·색상표에 적용한다.
        public void Render(float _hue, float _saturation, float _value)
        {
            if (!m_isInitialized)
            {
                return;
            }

            m_preview.color = Color.HSVToRGB(_hue, _saturation, _value);
            m_squareMarker.anchorMin = m_squareMarker.anchorMax = new Vector2(_saturation, _value);
            m_hueMarker.anchorMin = m_hueMarker.anchorMax = new Vector2(0.5f, _hue);
            if (!Mathf.Approximately(_hue, m_renderedHue))
            {
                UpdateSquareTexture(_hue);
                m_renderedHue = _hue;
            }
        }

        private void Awake()
        {
            if (!m_isInitialized && m_panel != null)
            {
                m_panel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnSquarePicked(Vector2 _point)
        {
            SquarePicked?.Invoke(_point);
        }

        private void OnHuePicked(Vector2 _point)
        {
            HuePicked?.Invoke(_point);
        }

        private void OnConfirmClicked()
        {
            ConfirmRequested?.Invoke();
        }

        private void OnCancelClicked()
        {
            CancelRequested?.Invoke();
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
                pixels[y] = Color.HSVToRGB((float)y / (height - 1), 1f, 1f);
            }

            m_hueTexture.SetPixels(pixels);
            m_hueTexture.Apply(false, false);
        }

        private bool ValidateReferences()
        {
            bool areasValid = m_square != null && m_hue != null &&
                m_square.GetComponent<ColorPickArea>() != null &&
                m_hue.GetComponent<ColorPickArea>() != null;
            if (m_panel != null && areasValid && m_squareMarker != null && m_hueMarker != null &&
                m_preview != null && m_confirm != null && m_cancel != null)
            {
                return true;
            }

            if (!m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                List<string> missing = new List<string>();
                if (m_panel == null)
                {
                    missing.Add(nameof(m_panel));
                }

                if (!areasValid)
                {
                    missing.Add(nameof(m_square) + "/" + nameof(m_hue) + "(ColorPickArea)");
                }

                if (m_squareMarker == null)
                {
                    missing.Add(nameof(m_squareMarker));
                }

                if (m_hueMarker == null)
                {
                    missing.Add(nameof(m_hueMarker));
                }

                if (m_preview == null)
                {
                    missing.Add(nameof(m_preview));
                }

                if (m_confirm == null)
                {
                    missing.Add(nameof(m_confirm));
                }

                if (m_cancel == null)
                {
                    missing.Add(nameof(m_cancel));
                }

                Debug.LogError($"[{VIEW_NAME}] Initialize 실패: 누락/불일치 " + string.Join(", ", missing));
            }

            return false;
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
