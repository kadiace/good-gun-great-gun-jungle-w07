using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 공방 모듈 즐겨찾기(숫자키 1~9,0)와 TAB 모듈 창. 상태(선택·즐겨찾기·창 열림)는 CubeBuilder가 보유한다.
    // 목록 드래그로 등록하며, 즐겨찾기끼리의 맞바꿈은 TAB 창이 닫혀 있어도 가능.
    [RequireComponent(typeof(RectTransform))]
    public sealed partial class ModulePaletteUI : MonoBehaviour
    {
        [System.Serializable] private sealed class CustomPanelEntry
        {
            public string Label;
            public RectTransform Panel;
            public Button Button;
        }

        [SerializeField] private CubeBuilder builder;
        public void Initialize(CubeBuilder playerBuilder)
        {
            builder = playerBuilder;
            enabled = true;
            gameObject.SetActive(true);
            m_presenter?.SetBuilder(playerBuilder);
        }
        [SerializeField] private TMPro.TMP_FontAsset font;
        [SerializeField, Tooltip("기존 1~3 모듈 표시. 즐겨찾기 표시로 대체되어 시작 시 끈다.")]
        private GameObject legacyModuleLegend;
        [SerializeField] private Vector2 slotSize = new Vector2(82.5f, 63f);
        [Header("Quickslot Layout")]
        [SerializeField, Tooltip("부모 영역 기준 좌측·하단 시작 비율(0~1).")]
        private Vector2 hotbarAnchorMin = new Vector2(.08f, .02f);
        [SerializeField, Tooltip("부모 영역 기준 우측·상단 끝 비율(0~1).")]
        private Vector2 hotbarAnchorMax = new Vector2(.92f, .12f);
        [SerializeField, Tooltip("패널 너비·높이 기준 외곽 여백 비율(0~1).")]
        private Vector2 hotbarPaddingRatio = new Vector2(.00232515f, .03472222f);
        [SerializeField, Tooltip("각 슬롯 너비·높이 기준 내부 텍스트 여백 비율(0~1).")]
        private Vector2 hotbarTextPaddingRatio = new Vector2(.02875767f, .04477612f);
        [SerializeField, Min(0f)] private float hotbarSpacingPixels = 6f;
        [SerializeField, Range(1, 6)] private int paletteColumns = 6;
        [Header("Module List Layout")]
        [SerializeField, Tooltip("부모 영역 기준 목록 너비·높이 비율. QHD 기존 크기에서 가로 16%, 세로 10% 확대한 기본값.")]
        private Vector2 paletteSizeRatio = new Vector2(.32715625f, .42062014f);
        [SerializeField, Tooltip("직접 지정하지 않으면 기본 재질 팔레트를 생성합니다.")]
        private PaintMaterialOption[] paintMaterials = System.Array.Empty<PaintMaterialOption>();

        private static readonly Color SlotColor = new Color(41f / 255f, 45f / 255f, 51f / 255f, 1f);
        private ModulePaletteView m_view;
        private ModulePalettePresenter m_presenter;
        private readonly List<PaintMaterialOption> activePaintMaterials = new List<PaintMaterialOption>();
        [SerializeField] private List<Outline> paintMaterialHighlights = new List<Outline>();
        [SerializeField] private List<Button> paintMaterialButtons = new List<Button>();
        [SerializeField] private List<RawImage> paintMaterialSwatches = new List<RawImage>();
        [SerializeField] private List<TMPro.TextMeshProUGUI> paintMaterialLabels = new List<TMPro.TextMeshProUGUI>();
        private readonly List<Texture2D> generatedPaintUiTextures = new List<Texture2D>();
        private readonly List<Texture2D> stickerTextures = new List<Texture2D>();
        [SerializeField] private List<Button> stickerButtons = new List<Button>();
        [SerializeField] private List<Outline> stickerHighlights = new List<Outline>();
        private readonly List<GameObject> keyringModels = new List<GameObject>();
        [SerializeField] private List<Button> keyringModelButtons = new List<Button>();
        [SerializeField] private List<Outline> keyringModelHighlights = new List<Outline>();
        private bool keyringAssetsFoundWithoutRenderer;
        private const int PaintPalettePageSize = 10;
        public int HoveredModuleId => m_presenter != null ? m_presenter.HoveredModuleId : 0;
        public void SetHoveredSlot(ModuleSlotUI slot) => m_presenter?.SetHoveredSlot(slot);
        public void ClearHoveredSlot(ModuleSlotUI slot) => m_presenter?.ClearHoveredSlot(slot);
        public void SlotDisabled(ModuleSlotUI slot) => m_presenter?.SlotDisabled(slot);
        private const float StickerViewportBottomInset = 96f;
        private const float StickerViewportTopInset = 138f;
        private const float KeyringViewportBottomInset = 144f;
        private const float KeyringViewportTopInset = 112f;
        private bool wasCustomMode;
        private PaintMaterialLibrary generatedPaintMaterials;
        [SerializeField] private RectTransform customButtonRoot;
        [SerializeField] private Button backToMaterialsButton;
        [SerializeField] private RectTransform customNavigation;
        [SerializeField] private RectTransform customNavigationContent;
        [SerializeField] private RectTransform customContentArea;
        [SerializeField] private TMPro.TextMeshProUGUI customContentPrompt;
        [SerializeField] private RectTransform paintPanel;
        [SerializeField] private RectTransform stickerPanel;
        [SerializeField] private RectTransform keyringPanel;
        [SerializeField] private RectTransform materialSelectionView;
        [SerializeField] private RawImage paintPreview;
        [SerializeField] private RawImage customColorPreview;
        [SerializeField] private TMPro.TextMeshProUGUI paintName;
        [SerializeField] private TMPro.TextMeshProUGUI paintTitle;
        [SerializeField] private TMPro.TextMeshProUGUI customColorHex;
        [SerializeField] private TMPro.TextMeshProUGUI paintStatus;
        [SerializeField] private TMPro.TextMeshProUGUI paintPageLabel;
        [SerializeField] private Button paintUndoButton;
        [SerializeField] private Button paintFillButton;
        [SerializeField] private Button paintAllButton;
        [SerializeField] private Button customRemoveButton;
        [SerializeField] private Button stickerUndoButton;
        [SerializeField] private Button stickerRemoveButton;
        [SerializeField] private Button stickerMirrorButton;
        [SerializeField] private TMPro.TextMeshProUGUI stickerMirrorButtonLabel;
        [SerializeField] private Button stickerSizeDownButton;
        [SerializeField] private TMPro.TextMeshProUGUI stickerSizeLabel;
        [SerializeField] private Button stickerSizeUpButton;
        [SerializeField] private Button previousPaintPageButton;
        [SerializeField] private Button nextPaintPageButton;
        [SerializeField] private Button customColorModeButton;
        [SerializeField] private Button applyCustomColorButton;
        [SerializeField] private RawImage colorSpectrum;
        [SerializeField] private RawImage hueSpectrum;
        [SerializeField] private RectTransform customColorPicker;
        [SerializeField] private TMPro.TMP_InputField[] rgbInputs = new TMPro.TMP_InputField[3];
        [SerializeField] private TMPro.TextMeshProUGUI stickerStatus;
        [SerializeField] private TMPro.TextMeshProUGUI stickerEmptyLabel;
        [SerializeField] private Button keyringRemoveButton;
        [SerializeField] private TMPro.TextMeshProUGUI keyringEmptyLabel;
        [SerializeField] private TMPro.TextMeshProUGUI keyringStatus;
        [SerializeField] private TMPro.TextMeshProUGUI keyringCountLabel;
        [SerializeField] private List<CustomPanelEntry> customPanels = new List<CustomPanelEntry>();
        private const float CustomNavigationWidth = 126f;
        private const float CustomNavigationGap = 16f;
        private const float CustomPanelWidth = 570f;
        private const float CustomPanelHeight = 640f;
        private const float CustomWorkspaceLeftInset = 24f;

        private void Awake()
        {
            if (builder == null)
            {
                CubeBuilder[] builders = FindObjectsByType<CubeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (builders.Length > 0) builder = builders[0];
            }
            if (legacyModuleLegend != null) legacyModuleLegend.SetActive(false);
            if (font == null) font = TMPro.TMP_Settings.defaultFontAsset;
            ResolvePaintMaterials();
            ResolveStickerTextures();
            ResolveKeyringModels();
            Build();
            m_presenter?.CancelDrag();
            if (customButtonRoot != null) customButtonRoot.gameObject.SetActive(false);
            RefreshCustomNavigation();
            UpdateCustomPanelVisibility(false);
            if (materialSelectionView != null) materialSelectionView.gameObject.SetActive(true);
            if (customColorPicker != null) customColorPicker.gameObject.SetActive(false);
            builder?.SetPaintFillMode(false);
        }

        private void OnDisable()
        {
            m_presenter?.CancelDrag();
            m_presenter?.ClearHover();
            m_view?.HideAll();
            wasCustomMode = false;
            customButtonRoot?.gameObject.SetActive(false);
            FeedbackUI.For(this)?.CancelChoice(this);
            RefreshCustomNavigation();
            UpdateCustomPanelVisibility(false);
            materialSelectionView?.gameObject.SetActive(true);
            customColorPicker?.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (m_view != null)
            {
                m_view.SlotHovered -= SetHoveredSlot;
                m_view.SlotUnhovered -= ClearHoveredSlot;
                m_view.SlotClicked -= HandleClick;
                m_view.SlotDisabledEvent -= SlotDisabled;
                m_view.SlotDragBegan -= BeginDrag;
                m_view.SlotDragged -= Drag;
                m_view.SlotDropped -= Drop;
                m_view.SlotDragEnded -= EndDrag;
            }

            m_presenter?.Dispose();
            generatedPaintMaterials?.Dispose();
            foreach (Texture2D texture in generatedPaintUiTextures)
                if (texture != null)
                {
                    if (Application.isPlaying) Destroy(texture);
                    else DestroyImmediate(texture);
                }
            generatedPaintUiTextures.Clear();
        }

        private void LateUpdate()
        {
            if (m_view == null || m_presenter == null) return;
            m_view.ApplyPendingLayout();
            if (builder == null)
            {
                CubeBuilder[] builders = FindObjectsByType<CubeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (builders.Length > 0) builder = builders[0];
                m_presenter.SetBuilder(builder);
            }
            bool active = builder != null && builder.isActiveAndEnabled &&
                          (builder.Catalog != null || builder.IsCustomMode);
            bool customMode = active && builder.IsCustomMode;
            bool paletteOpen = active && !customMode && builder.PaletteOpen;
            m_view.SetGuideVisible(active && !customMode && builder.CanEditFavorites);
            m_view.SetHotbarVisible(active && !customMode);
            m_view.SetPaletteVisible(paletteOpen);
            if (customButtonRoot != null && customButtonRoot.gameObject.activeSelf != (active && builder.CanStartCustom))
                customButtonRoot.gameObject.SetActive(active && builder.CanStartCustom);
            if (!active)
            {
                FeedbackUI.For(this)?.CancelChoice(this);
            }
            if (customMode && !wasCustomMode)
            {
                RefreshCustomNavigation();
            }
            UpdateCustomPanelVisibility(customMode);
            wasCustomMode = customMode;
            m_presenter.UpdateDrag(paletteOpen);
            m_presenter.ClearHoverIfNeeded(active, customMode, paletteOpen);
            if (!active) return;
            if (customMode)
            {
                RefreshPaintPanel();
                RefreshStickerPanel();
                RefreshKeyringPanel();
            }
            m_presenter.RefreshIfStateChanged();
        }

        public void HandleClick(ModuleSlotUI slot) => m_presenter?.HandleClick(slot);

        public void BeginDrag(ModuleSlotUI slot, PointerEventData eventData) => m_presenter?.BeginDrag(slot, eventData);

        public void Drag(PointerEventData eventData) => m_presenter?.Drag(eventData);

        public void Drop(ModuleSlotUI target, PointerEventData eventData) => m_presenter?.Drop(target, eventData);

        public void EndDrag(PointerEventData eventData) => m_presenter?.EndDrag(eventData);

        private void OnApplicationFocus(bool focused) { if (!focused) m_presenter?.CancelDrag(); }

        public bool TryCancelSlotDrag() => m_presenter != null && m_presenter.TryCancelSlotDrag();

        private void Build()
        {
            m_view = GetComponent<ModulePaletteView>();
            if (m_view == null)
            {
                Debug.LogError(
                    "[ModulePaletteUI] ModulePaletteView가 없어 모듈 팔레트를 초기화하지 못했습니다. " +
                    "Tools/UI/Authoring/ModulePalette/Author를 실행하세요.",
                    this);
                enabled = false;
                return;
            }

            var layout = new ModulePaletteView.LayoutConfig
            {
                slotSize = new Vector2(slotSize.x, Mathf.Max(slotSize.y, 63f)),
                hotbarAnchorMin = hotbarAnchorMin,
                hotbarAnchorMax = hotbarAnchorMax,
                hotbarPaddingRatio = hotbarPaddingRatio,
                hotbarTextPaddingRatio = hotbarTextPaddingRatio,
                hotbarSpacingPixels = hotbarSpacingPixels,
                paletteSizeRatio = paletteSizeRatio,
                paletteColumns = paletteColumns
            };
            if (!m_view.Initialize(layout))
            {
                m_view.HideAll();
                enabled = false;
                return;
            }

            m_presenter = new ModulePalettePresenter(m_view);
            m_presenter.Initialize(builder);
            m_view.SlotHovered += SetHoveredSlot;
            m_view.SlotUnhovered += ClearHoveredSlot;
            m_view.SlotClicked += HandleClick;
            m_view.SlotDisabledEvent += SlotDisabled;
            m_view.SlotDragBegan += BeginDrag;
            m_view.SlotDragged += Drag;
            m_view.SlotDropped += Drop;
            m_view.SlotDragEnded += EndDrag;
            BindCustomUI();
        }

        private static Texture GetPreviewTexture(PaintMaterialOption option)
        {
            if (option == null) return null;
            if (option.preview != null) return option.preview;
            return option.material != null ? option.material.mainTexture : null;
        }

        private static void ApplyButtonColors(Button button)
        {
            button.targetGraphic.color = Color.white;
            var colors = button.colors;
            colors.normalColor = SlotColor;
            colors.highlightedColor = colors.selectedColor = new Color32(0x3D, 0x44, 0x4F, 255);
            colors.pressedColor = new Color32(0x1C, 0x20, 0x26, 255);
            colors.disabledColor = new Color32(0x22, 0x25, 0x2B, 255);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 size,
            Vector2 anchor, Vector2 pivot, Vector2 position, Color background)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButtonColors(button);
            TMPro.TextMeshProUGUI text = CreateText("Label", rect, label, 18);
            text.alignment = TMPro.TextAlignmentOptions.Midline;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(4f, 2f);
            text.rectTransform.offsetMax = new Vector2(-4f, -2f);
            text.raycastTarget = false;
            return button;
        }

        private TMPro.TextMeshProUGUI CreateText(string name, RectTransform parent, string value, int size)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size + 4;
            UIFonts.ApplyWeight(text, name.Contains("Title") ? 800 : parent.GetComponent<UnityEngine.UI.Button>() != null ? 700 : 400);
            text.text = value;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchPanel(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
