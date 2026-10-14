using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    public sealed class RaidHealthUI : MonoBehaviour
    {
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Image fill;
        [SerializeField, Min(.01f)] private float changeDuration = .25f;
        [Header("Low Health Vignette")]
        [SerializeField, Range(.01f, .99f)] private float lowHealthThreshold = .3f;
        [SerializeField, Range(0f, 1f)] private float lowHealthMaxOpacity = .28f;
        [SerializeField, Min(.01f)] private float lowHealthFadeDuration = .22f;
        [SerializeField, Min(0f)] private float vignetteGradientStart = .8f;
        [SerializeField, Min(.01f)] private float vignetteGradientEnd = 1.12f;
        private RaidPlayerHealth health;
        private float displayed, startValue, target, elapsed;
        private RawImage vignette;
        private Texture2D vignetteTexture;
        private float vignetteOpacity, vignetteStartOpacity, vignetteTargetOpacity, vignetteElapsed;
        private bool initialized;
        private void Awake()
        {
            CreateVignette();
            Hide();
        }
        private void Start()
        {
            var player = uiManager != null ? uiManager.Player : null;
            health = player != null ? player.GetComponent<RaidPlayerHealth>() : null;
            if (health != null) health.Changed += Refresh;
            Refresh();
        }
        private void Refresh()
        {
            bool visible = isActiveAndEnabled && health != null;
            if (panel != null) panel.SetActive(visible);
            if (!visible)
            {
                SetVignetteTarget(0f);
                return;
            }
            float next = Mathf.Clamp01((float)health.CurrentHealth / Mathf.Max(1, health.MaximumHealth));
            SetVignetteForHealth(next);
            if (!initialized) { displayed = next; initialized = true; }
            startValue = displayed; target = next; elapsed = 0f;
            Draw();
        }
        private void LateUpdate()
        {
            UpdateVignette();
            if (!initialized) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / changeDuration);
            displayed = Mathf.Lerp(startValue, target, Mathf.SmoothStep(0f, 1f, t));
            Draw();
        }
        private void Draw()
        {
            if (fill != null) fill.fillAmount = displayed;
        }
        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
            initialized = false;
            vignetteOpacity = vignetteStartOpacity = vignetteTargetOpacity = vignetteElapsed = 0f;
            if (vignette != null)
            {
                vignette.color = Color.clear;
                vignette.enabled = false;
            }
        }
        private void OnEnable() { if (health != null) Refresh(); }
        private void OnDisable() => Hide();
        private void OnDestroy()
        {
            if (health != null) health.Changed -= Refresh;
            if (vignetteTexture != null) Destroy(vignetteTexture);
        }

        private void CreateVignette()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;

            var overlay = new GameObject("LowHealthVignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overlay.layer = canvas.gameObject.layer;
            var rect = (RectTransform)overlay.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            vignette = overlay.GetComponent<RawImage>();
            vignetteTexture = CreateVignetteTexture();
            vignette.texture = vignetteTexture;
            vignette.raycastTarget = false;
            vignette.color = Color.clear;
            vignette.enabled = false;
        }

        private Texture2D CreateVignetteTexture()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "LowHealthVignetteGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            float start = Mathf.Min(vignetteGradientStart, vignetteGradientEnd - .01f);
            float end = Mathf.Max(start + .01f, vignetteGradientEnd);
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    float gradient = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(gradient * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void SetVignetteForHealth(float healthFraction)
        {
            float next = 0f;
            if (healthFraction < lowHealthThreshold)
            {
                float severity = 1f - healthFraction / Mathf.Max(.01f, lowHealthThreshold);
                next = lowHealthMaxOpacity * Mathf.SmoothStep(0f, 1f, severity);
            }
            SetVignetteTarget(next);
        }

        private void SetVignetteTarget(float next)
        {
            if (vignette == null || Mathf.Approximately(next, vignetteTargetOpacity)) return;
            vignetteStartOpacity = vignetteOpacity;
            vignetteTargetOpacity = next;
            vignetteElapsed = 0f;
            if (next > 0f) vignette.enabled = true;
        }

        private void UpdateVignette()
        {
            if (vignette == null || Mathf.Approximately(vignetteOpacity, vignetteTargetOpacity)) return;
            vignetteElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(vignetteElapsed / Mathf.Max(.01f, lowHealthFadeDuration));
            vignetteOpacity = Mathf.Lerp(vignetteStartOpacity, vignetteTargetOpacity, Mathf.SmoothStep(0f, 1f, t));
            var color = Color.red;
            color.a = vignetteOpacity;
            vignette.color = color;
            if (t >= 1f)
            {
                vignetteOpacity = vignetteTargetOpacity;
                if (vignetteOpacity <= 0f) vignette.enabled = false;
            }
        }
    }
}
