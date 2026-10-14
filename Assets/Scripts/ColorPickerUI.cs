using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 컬러 모듈을 고르면 뜨는 색 선택 창. 오른쪽 색상 바(H) + 왼쪽 사각형(가로 진하기 S, 세로 밝기 V) + 미리보기.
    // 확인 = CubeBuilder.ConfirmModuleColor, 취소 = CancelModuleColor. 열림 여부는 CubeBuilder.ColorPickerOpen을 따른다.
    // UIManager 프리팹의 Canvas 아래에 두며, 화면 요소는 Awake에서 코드로 만든다.
    public sealed class ColorPickerUI : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 씬에서 찾음(공방 카메라의 CubeBuilder).")]
        private CubeBuilder builder;
        [SerializeField] private TMPro.TMP_FontAsset font;

        private const int SquareResolution = 64;
        private GameObject panel;
        private Texture2D squareTexture;
        private Texture2D hueTexture;
        private RectTransform squareMarker;
        private RectTransform hueMarker;
        private Image preview;
        private float hue, saturation = 1f, value = 1f;

        private void Awake()
        {
            if (builder == null) builder = FindAnyObjectByType<CubeBuilder>(FindObjectsInactive.Include);
            if (font == null) font = TMPro.TMP_Settings.defaultFontAsset;
            BuildPanel();
            panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (squareTexture != null) Destroy(squareTexture);
            if (hueTexture != null) Destroy(hueTexture);
        }

        private void Update()
        {
            bool open = builder != null && builder.ColorPickerOpen;
            if (open == panel.activeSelf) return;
            if (open)
            {
                Color.RGBToHSV(builder.ModuleColor, out hue, out saturation, out value);
                RefreshSquare();
                Refresh();
            }
            panel.SetActive(open);
        }

        private void Confirm() => builder?.ConfirmModuleColor(Color.HSVToRGB(hue, saturation, value));
        private void Cancel() => builder?.CancelModuleColor();

        private void PickSquare(Vector2 point)
        {
            saturation = point.x;
            value = point.y;
            Refresh();
        }

        private void PickHue(Vector2 point)
        {
            hue = Mathf.Min(point.y, 0.9999f);
            RefreshSquare();
            Refresh();
        }

        private void Refresh()
        {
            preview.color = Color.HSVToRGB(hue, saturation, value);
            squareMarker.anchorMin = squareMarker.anchorMax = new Vector2(saturation, value);
            hueMarker.anchorMin = hueMarker.anchorMax = new Vector2(0.5f, hue);
        }

        // The square shows every saturation/brightness of the current hue.
        private void RefreshSquare()
        {
            var pixels = new Color[SquareResolution * SquareResolution];
            for (int y = 0; y < SquareResolution; y++)
                for (int x = 0; x < SquareResolution; x++)
                    pixels[y * SquareResolution + x] = Color.HSVToRGB(hue,
                        x / (SquareResolution - 1f), y / (SquareResolution - 1f));
            squareTexture.SetPixels(pixels);
            squareTexture.Apply();
        }

        private void BuildPanel()
        {
            panel = CreateRect("ColorPickerPanel", transform, new Vector2(340f, 300f), Vector2.zero);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.95f);

            TMPro.TextMeshProUGUI title = CreateRect("Title", panel.transform, new Vector2(300f, 30f), new Vector2(0f, 128f)).AddComponent<TMPro.TextMeshProUGUI>();
            title.font = font;
            title.fontSize = 20;
            title.alignment = TMPro.TextAlignmentOptions.Midline;
            title.color = Color.white;
            title.text = "탄 색 선택";

            squareTexture = new Texture2D(SquareResolution, SquareResolution, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            GameObject square = CreateRect("SaturationValue", panel.transform, new Vector2(200f, 200f), new Vector2(-60f, 8f));
            square.AddComponent<RawImage>().texture = squareTexture;
            square.AddComponent<ColorPickArea>().Picked += PickSquare;
            squareMarker = CreateMarker(square.transform, new Vector2(10f, 10f));

            hueTexture = new Texture2D(1, 128, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 128; y++) hueTexture.SetPixel(0, y, Color.HSVToRGB(y / 127f, 1f, 1f));
            hueTexture.Apply();
            GameObject hueBar = CreateRect("Hue", panel.transform, new Vector2(24f, 200f), new Vector2(65f, 8f));
            hueBar.AddComponent<RawImage>().texture = hueTexture;
            hueBar.AddComponent<ColorPickArea>().Picked += PickHue;
            hueMarker = CreateMarker(hueBar.transform, new Vector2(32f, 4f));

            preview = CreateRect("Preview", panel.transform, new Vector2(50f, 50f), new Vector2(125f, 83f)).AddComponent<Image>();
            preview.raycastTarget = false;

            CreateButton("확인", new Vector2(-75f, -120f), Confirm);
            CreateButton("취소", new Vector2(75f, -120f), Cancel);
        }

        private RectTransform CreateMarker(Transform parent, Vector2 size)
        {
            GameObject marker = CreateRect("Marker", parent, size, Vector2.zero);
            Image image = marker.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            marker.AddComponent<Outline>().effectColor = Color.black;
            return (RectTransform)marker.transform;
        }

        private void CreateButton(string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            GameObject button = CreateRect(label, panel.transform, new Vector2(120f, 34f), position);
            button.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
            button.AddComponent<Button>().onClick.AddListener(onClick);
            TMPro.TextMeshProUGUI text = CreateRect("Label", button.transform, new Vector2(120f, 34f), Vector2.zero).AddComponent<TMPro.TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 18;
            text.alignment = TMPro.TextAlignmentOptions.Midline;
            text.color = Color.white;
            text.text = label;
            text.raycastTarget = false;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }
    }
}
