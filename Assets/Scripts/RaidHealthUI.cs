using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 체력 바와 저체력 비네트의 표시 상태·보간만 담당한다. 체력 값은 RaidHealthPresenter가 전달한다.
    public sealed class RaidHealthUI : MonoBehaviour
    {
        private static readonly Color VIGNETTE_COLOR = Color.red;
        private const int VIGNETTE_TEXTURE_SIZE = 256;

        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Image fill;
        [SerializeField, Min(.01f)] private float changeDuration = .25f;
        [Header("Low Health Vignette")]
        [SerializeField] private RawImage m_lowHealthVignette;
        [SerializeField, Range(.01f, .99f)] private float lowHealthThreshold = .3f;
        [SerializeField, Range(0f, 1f)] private float lowHealthMaxOpacity = .28f;
        [SerializeField, Min(.01f)] private float lowHealthFadeDuration = .22f;
        [SerializeField, Min(0f)] private float vignetteGradientStart = .8f;
        [SerializeField, Min(.01f)] private float vignetteGradientEnd = 1.12f;

        private RaidHealthPresenter m_presenter;
        private Texture2D m_vignetteTexture;
        private float m_vignetteOpacity;
        private float m_vignetteStartOpacity;
        private float m_vignetteTargetOpacity;
        private float m_vignetteElapsed;
        private float m_displayed;
        private float m_startValue;
        private float m_target;
        private float m_elapsed;
        private bool m_initialized;
        private bool m_hasLoggedVignetteError;

        private void Awake()
        {
            InitializeVignette();
            HideDisplay();
        }

        private void Start()
        {
            m_presenter = new RaidHealthPresenter(this, uiManager);
            if (!m_presenter.Initialize())
            {
                m_presenter.Dispose();
                m_presenter = null;
            }
        }

        private void OnEnable()
        {
            m_presenter?.Refresh();
        }

        private void OnDisable()
        {
            HideDisplay();
        }

        private void LateUpdate()
        {
            UpdateVignette();
            if (!m_initialized)
            {
                return;
            }

            m_elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_elapsed / changeDuration);
            m_displayed = Mathf.Lerp(m_startValue, m_target, Mathf.SmoothStep(0f, 1f, t));
            Draw();
        }

        private void OnDestroy()
        {
            m_presenter?.Dispose();
            m_presenter = null;
            if (m_lowHealthVignette != null)
            {
                m_lowHealthVignette.texture = null;
            }

            if (m_vignetteTexture != null)
            {
                Destroy(m_vignetteTexture);
                m_vignetteTexture = null;
            }
        }

        public void ShowHealth(float _current, float _maximum)
        {
            if (panel != null)
            {
                panel.SetActive(true);
            }

            float next = Mathf.Clamp01(_current / Mathf.Max(1f, _maximum));
            ApplyVignette(next);
            if (!m_initialized)
            {
                m_displayed = next;
                m_initialized = true;
            }

            m_startValue = m_displayed;
            m_target = next;
            m_elapsed = 0f;
            Draw();
        }

        public void HideDisplay()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }

            m_initialized = false;
            ClearVignette();
        }

        private void InitializeVignette()
        {
            if (m_lowHealthVignette == null)
            {
                if (!m_hasLoggedVignetteError)
                {
                    m_hasLoggedVignetteError = true;
                    Debug.LogError(
                        "[RaidHealthUI] 비네트 초기화 실패: m_lowHealthVignette 참조가 없습니다. " +
                        "Tools/UI/Authoring/Raid/Vignette를 실행해 UIManager 프리팹을 저작하세요.", this);
                }

                return;
            }

            m_vignetteTexture = CreateVignetteTexture();
            m_lowHealthVignette.texture = m_vignetteTexture;
            m_lowHealthVignette.raycastTarget = false;
        }

        private Texture2D CreateVignetteTexture()
        {
            Texture2D texture = new Texture2D(VIGNETTE_TEXTURE_SIZE, VIGNETTE_TEXTURE_SIZE,
                TextureFormat.RGBA32, false, true)
            {
                name = "LowHealthVignetteGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[VIGNETTE_TEXTURE_SIZE * VIGNETTE_TEXTURE_SIZE];
            float start = Mathf.Min(vignetteGradientStart, vignetteGradientEnd - .01f);
            float end = Mathf.Max(start + .01f, vignetteGradientEnd);
            for (int y = 0; y < VIGNETTE_TEXTURE_SIZE; y++)
            {
                float ny = (y / (float)(VIGNETTE_TEXTURE_SIZE - 1)) * 2f - 1f;
                for (int x = 0; x < VIGNETTE_TEXTURE_SIZE; x++)
                {
                    float nx = (x / (float)(VIGNETTE_TEXTURE_SIZE - 1)) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float gradient = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, radius));
                    pixels[y * VIGNETTE_TEXTURE_SIZE + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(gradient * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void ApplyVignette(float _healthFraction)
        {
            float next = 0f;
            if (_healthFraction < lowHealthThreshold)
            {
                float severity = 1f - _healthFraction / Mathf.Max(.01f, lowHealthThreshold);
                next = lowHealthMaxOpacity * Mathf.SmoothStep(0f, 1f, severity);
            }

            SetVignetteTarget(next);
        }

        private void SetVignetteTarget(float _opacity)
        {
            if (m_lowHealthVignette == null || Mathf.Approximately(_opacity, m_vignetteTargetOpacity))
            {
                return;
            }

            m_vignetteStartOpacity = m_vignetteOpacity;
            m_vignetteTargetOpacity = _opacity;
            m_vignetteElapsed = 0f;
            if (_opacity > 0f)
            {
                m_lowHealthVignette.enabled = true;
            }
        }

        private void UpdateVignette()
        {
            if (m_lowHealthVignette == null || Mathf.Approximately(m_vignetteOpacity, m_vignetteTargetOpacity))
            {
                return;
            }

            m_vignetteElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(m_vignetteElapsed / Mathf.Max(.01f, lowHealthFadeDuration));
            m_vignetteOpacity = Mathf.Lerp(m_vignetteStartOpacity, m_vignetteTargetOpacity,
                Mathf.SmoothStep(0f, 1f, t));
            Color color = VIGNETTE_COLOR;
            color.a = m_vignetteOpacity;
            m_lowHealthVignette.color = color;
            if (t >= 1f)
            {
                m_vignetteOpacity = m_vignetteTargetOpacity;
                if (m_vignetteOpacity <= 0f)
                {
                    m_lowHealthVignette.enabled = false;
                }
            }
        }

        private void ClearVignette()
        {
            m_vignetteOpacity = 0f;
            m_vignetteStartOpacity = 0f;
            m_vignetteTargetOpacity = 0f;
            m_vignetteElapsed = 0f;
            if (m_lowHealthVignette != null)
            {
                m_lowHealthVignette.color = Color.clear;
                m_lowHealthVignette.enabled = false;
            }
        }

        private void Draw()
        {
            if (fill != null)
            {
                fill.fillAmount = m_displayed;
            }
        }
    }
}
