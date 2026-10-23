using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 공방 모듈 즐겨찾기(숫자키 1~9,0)와 TAB 모듈 창. 상태(선택·즐겨찾기·창 열림)는 CubeBuilder가 보유한다.
    // 목록 드래그로 등록하며, 즐겨찾기끼리의 맞바꿈은 TAB 창이 닫혀 있어도 가능.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ModulePaletteUI : MonoBehaviour
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
        }
        [SerializeField] private TMPro.TMP_FontAsset font;
        [SerializeField, Tooltip("기존 1~3 모듈 표시. 즐겨찾기 표시로 대체되어 시작 시 끈다.")]
        private GameObject legacyModuleLegend;
        [SerializeField] private Vector2 slotSize = new Vector2(110f, 84f);
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
        [SerializeField] private Button paletteToggleButton;
        [SerializeField] private TMPro.TextMeshProUGUI paletteToggleLabel;
        [SerializeField, Range(1, 6)] private int paletteColumns = 6;
        [Header("Module List Layout")]
        [SerializeField, Tooltip("부모 영역 기준 목록 너비·높이 비율. QHD 기존 크기에서 가로 16%, 세로 10% 확대한 기본값.")]
        private Vector2 paletteSizeRatio = new Vector2(.32715625f, .42062014f);
        [SerializeField, Tooltip("직접 지정하지 않으면 기본 재질 팔레트를 생성합니다.")]
        private PaintMaterialOption[] paintMaterials = System.Array.Empty<PaintMaterialOption>();

        private static readonly Color SlotColor = new Color(41f / 255f, 45f / 255f, 51f / 255f, 1f);
        private RectTransform hotbar;
        private HorizontalLayoutGroup hotbarLayout;
        private Canvas hotbarCanvas;
        private Vector2 lastHotbarSize = new Vector2(-1f, -1f);
        private float lastHotbarScale = -1f;
        private Vector2 lastHotbarPadding = new Vector2(-1f, -1f);
        private Vector2 lastHotbarTextPadding = new Vector2(-1f, -1f);
        private float lastHotbarSpacing = -1f;
        private float paletteToggleTextWidth;
        private TMPro.TextMeshProUGUI hotbarGuide;
        private const string HotbarGuideText = "퀵슬롯을 다른 번호 칸으로 드래그하면 서로 바뀝니다 · Tab: 모듈 목록";
        private RectTransform palette;
        private VerticalLayoutGroup paletteLayout;
        private LayoutElement paletteTitleLayout;
        private Vector2 paletteReferenceSize;
        private float paletteReferenceTitleHeight;
        private Vector2 lastPaletteSize = new Vector2(-1f, -1f);
        private float lastPaletteScale = -1f;
        private readonly List<PaletteCategoryLayout> paletteCategories = new List<PaletteCategoryLayout>();
        private sealed class PaletteCategoryLayout
        {
            public VerticalLayoutGroup Layout;
            public LayoutElement Heading;
            public GridLayoutGroup Grid;
            public LayoutElement GridSize;
            public int Rows;
        }
        private RectTransform dragGhost;
        private Image dragGhostSwatch;
        private TMPro.TextMeshProUGUI dragGhostLabel;
        private readonly List<ModuleSlotUI> hotbarSlots = new List<ModuleSlotUI>();
        private readonly List<ModuleSlotUI> paletteSlots = new List<ModuleSlotUI>();
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
        private ModuleSlotUI hoveredSlot;
        public int HoveredModuleId => hoveredSlot != null && hoveredSlot.isActiveAndEnabled ? hoveredSlot.ModuleId : 0;
        public void SetHoveredSlot(ModuleSlotUI slot)
        {
            if (builder == null || slot == null) return;
            hoveredSlot = slot; if (builder.PaletteOpen && slot.FavoriteIndex < 0) builder.SetHoveredPaletteModule(slot.ModuleId);
            UpdateDropTarget();
        }
        public void ClearHoveredSlot(ModuleSlotUI slot)
        {
            if (hoveredSlot != slot) return;
            ClearHover();
        }
        private void ClearHover()
        {
            hoveredSlot = null;
            builder?.SetHoveredPaletteModule(0);
            SetDropTarget(null);
        }

        public void SlotDisabled(ModuleSlotUI slot)
        {
            ClearHoveredSlot(slot);
            if (dragSource == slot) CancelDrag();
            else if (dropTarget == slot) SetDropTarget(null);
        }
        private const float StickerViewportBottomInset = 96f;
        private const float StickerViewportTopInset = 138f;
        private const float KeyringViewportBottomInset = 144f;
        private const float KeyringViewportTopInset = 112f;
        private int draggingModule;
        private ModuleSlotUI dragSource;
        private ModuleSlotUI dropTarget;
        private int dragPointerId;
        private bool dragStartedWithPalette;
        private int lastState = int.MinValue;
        private int selectedPaintMaterial = -1;
        private int paintMaterialPage;
        private bool confirmingCustomRemoval;
        private bool wasCustomMode;
        private bool customColorSelected;
        private bool customColorMode;
        private bool syncingRgbInputs;
        private Color32 customColor = new Color32(70, 170, 255, 255);
        private float customHue;
        private PaintMaterialLibrary generatedPaintMaterials;
        [SerializeField] private RectTransform customButtonRoot;
        [SerializeField] private Button customYesButton, customNoButton, backToMaterialsButton;
        [SerializeField] private RectTransform customConfirmation;
        [SerializeField] private TMPro.TextMeshProUGUI customConfirmationMessage;
        [SerializeField] private RectTransform customNavigation;
        [SerializeField] private RectTransform customNavigationContent;
        [SerializeField] private RectTransform customContentArea;
        [SerializeField] private TMPro.TextMeshProUGUI customContentPrompt;
        [SerializeField] private RectTransform paintPanel;
        [SerializeField] private RectTransform stickerPanel;
        [SerializeField] private RectTransform keyringPanel;
        [SerializeField] private RectTransform keyringGrid;
        [SerializeField] private RectTransform materialSelectionView;
        [SerializeField] private RawImage paintPreview;
        [SerializeField] private RawImage customColorPreview;
        [SerializeField] private TMPro.TextMeshProUGUI paintName;
        [SerializeField] private TMPro.TextMeshProUGUI paintTitle;
        [SerializeField] private TMPro.TextMeshProUGUI customColorHex;
        [SerializeField] private TMPro.TextMeshProUGUI paintInstructions;
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
        private Texture2D colorSpectrumTexture;
        private Texture2D hueSpectrumTexture;
        [SerializeField] private TMPro.TMP_InputField[] rgbInputs = new TMPro.TMP_InputField[3];
        private Texture2D selectedSticker;
        [SerializeField] private TMPro.TextMeshProUGUI stickerStatus;
        [SerializeField] private TMPro.TextMeshProUGUI stickerEmptyLabel;
        [SerializeField] private RectTransform customLimitToast;
        [SerializeField] private TMPro.TextMeshProUGUI customLimitMessage;
        [SerializeField] private Button keyringRemoveButton;
        [SerializeField] private TMPro.TextMeshProUGUI keyringEmptyLabel;
        [SerializeField] private TMPro.TextMeshProUGUI keyringStatus;
        [SerializeField] private TMPro.TextMeshProUGUI keyringCountLabel;
        [SerializeField] private List<CustomPanelEntry> customPanels = new List<CustomPanelEntry>();
        private CustomPanelEntry selectedCustomPanel;
        private const float CustomNavigationWidth = 168f;
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
            hotbar.gameObject.SetActive(false);
            paletteToggleButton.gameObject.SetActive(false);
            hotbarGuide.gameObject.SetActive(false);
            palette.gameObject.SetActive(false);
            dragGhost.gameObject.SetActive(false);
            CancelDrag();
            customButtonRoot.gameObject.SetActive(false);
            selectedCustomPanel = null;
            RefreshCustomNavigation();
            UpdateCustomPanelVisibility(false);
            materialSelectionView.gameObject.SetActive(true);
            customColorPicker.gameObject.SetActive(false);
            builder?.SetPaintFillMode(false);
        }

        private void OnDisable()
        {
            CancelDrag();
            ClearHover();
            confirmingCustomRemoval = false;
            wasCustomMode = false;
            customColorMode = false;
            hotbar?.gameObject.SetActive(false);
            paletteToggleButton?.gameObject.SetActive(false);
            hotbarGuide?.gameObject.SetActive(false);
            palette?.gameObject.SetActive(false);
            customButtonRoot?.gameObject.SetActive(false);
            FeedbackUI.For(this)?.CancelChoice(this);
            selectedCustomPanel = null;
            RefreshCustomNavigation();
            UpdateCustomPanelVisibility(false);
            materialSelectionView?.gameObject.SetActive(true);
            customColorPicker?.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (paletteToggleButton != null) paletteToggleButton.onClick.RemoveListener(TogglePalette);
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
            UpdateHotbarLayout();
            UpdatePaletteLayout();
            if (builder == null)
            {
                CubeBuilder[] builders = FindObjectsByType<CubeBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (builders.Length > 0) builder = builders[0];
            }
            bool active = builder != null && builder.isActiveAndEnabled &&
                          (builder.Catalog != null || builder.IsCustomMode);
            bool customMode = active && builder.IsCustomMode;
            
            bool paletteOpen = active && !customMode && builder.PaletteOpen;
            paletteToggleButton.gameObject.SetActive(active && !customMode);
            paletteToggleButton.interactable = active && !customMode && !GameManager.GameplayBlocked;
            hotbarGuide.gameObject.SetActive(active && !customMode && builder.CanEditFavorites);
            if (hotbar.gameObject.activeSelf != (active && !customMode))
                hotbar.gameObject.SetActive(active && !customMode);
            if (palette.gameObject.activeSelf != paletteOpen) palette.gameObject.SetActive(paletteOpen);
            if (customButtonRoot.gameObject.activeSelf != (active && builder.CanStartCustom))
                customButtonRoot.gameObject.SetActive(active && builder.CanStartCustom);
            if (!active)
            {
                confirmingCustomRemoval = false;
                FeedbackUI.For(this)?.CancelChoice(this);
            }
            if (customMode && !wasCustomMode)
            {
                selectedPaintMaterial = -1;
                customColorSelected = false;
                customColorMode = false;
                paintMaterialPage = 0;
                selectedSticker = null;
                selectedCustomPanel = null;
                RefreshCustomNavigation();
            }
            UpdateCustomPanelVisibility(customMode);
            wasCustomMode = customMode;
            if (draggingModule != 0)
            {
                if (!CanContinueDrag()) CancelDrag();
                else
                {
                    if (paletteOpen) dragStartedWithPalette = true;
                    UpdateDropTarget();
                }
            }
            if (!active || customMode || (!paletteOpen && hoveredSlot != null && hoveredSlot.FavoriteIndex < 0)) ClearHover();
            if (!active) return;
            if (customMode)
            {
                RefreshPaintPanel();
                RefreshStickerPanel();
                RefreshKeyringPanel();
            }
            if (builder.Catalog == null) return;
            int state = ComputeState();
            if (state == lastState) return;
            lastState = state;
            Refresh();
        }

        private void UpdateHotbarLayout()
        {
            if (hotbar == null || hotbarLayout == null) return;
            bool anchorsChanged = hotbar.anchorMin != hotbarAnchorMin || hotbar.anchorMax != hotbarAnchorMax;
            if (anchorsChanged)
            {
                hotbar.anchorMin = hotbarAnchorMin;
                hotbar.anchorMax = hotbarAnchorMax;
                hotbar.offsetMin = hotbar.offsetMax = Vector2.zero;
            }
            if (hotbarGuide != null)
            {
                Vector2 guideMin = new Vector2(paletteToggleButton != null
                    ? ((RectTransform)paletteToggleButton.transform).anchorMax.x + .01f : hotbarAnchorMin.x,
                    hotbarAnchorMax.y + .01f);
                Vector2 guideMax = new Vector2(hotbarAnchorMax.x, hotbarAnchorMax.y + .04f);
                if (hotbarGuide.rectTransform.anchorMin != guideMin || hotbarGuide.rectTransform.anchorMax != guideMax)
                {
                    hotbarGuide.rectTransform.anchorMin = guideMin;
                    hotbarGuide.rectTransform.anchorMax = guideMax;
                    hotbarGuide.rectTransform.offsetMin = hotbarGuide.rectTransform.offsetMax = Vector2.zero;
                }
            }
            float scale = hotbarCanvas != null ? Mathf.Max(.01f, hotbarCanvas.scaleFactor) : 1f;
            Vector2 size = hotbar.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            if (!anchorsChanged && size == lastHotbarSize && Mathf.Approximately(scale, lastHotbarScale) &&
                hotbarPaddingRatio == lastHotbarPadding &&
                hotbarTextPaddingRatio == lastHotbarTextPadding &&
                Mathf.Approximately(hotbarSpacingPixels, lastHotbarSpacing)) return;
            lastHotbarSize = size;
            lastHotbarScale = scale;
            lastHotbarPadding = hotbarPaddingRatio;
            lastHotbarTextPadding = hotbarTextPaddingRatio;
            lastHotbarSpacing = hotbarSpacingPixels;
            int horizontalPadding = Mathf.RoundToInt(size.x * Mathf.Clamp(hotbarPaddingRatio.x, 0f, .49f));
            int verticalPadding = Mathf.RoundToInt(size.y * Mathf.Clamp(hotbarPaddingRatio.y, 0f, .49f));
            hotbarLayout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            hotbarLayout.spacing = hotbarSpacingPixels / scale;
            UpdatePaletteToggleLayout(size, horizontalPadding);
            foreach (var slot in hotbarSlots) slot.SetContentPaddingRatio(hotbarTextPaddingRatio);
            LayoutRebuilder.MarkLayoutForRebuild(hotbar);
        }

        private void TogglePalette() => builder?.TogglePalette();

        private void BuildPaletteToggle()
        {
            if (paletteToggleButton == null)
                paletteToggleButton = CreateButton("PaletteToggleButton", transform, "모듈 선택창 [TAB]",
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, SlotColor);
            if (paletteToggleLabel == null)
                paletteToggleLabel = paletteToggleButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            paletteToggleLabel.text = "모듈 선택창 [TAB]";
            paletteToggleLabel.font = font;
            paletteToggleLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            paletteToggleLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            paletteToggleLabel.enableAutoSizing = true;
            paletteToggleLabel.fontSize = paletteToggleLabel.fontSizeMax = 24f;
            paletteToggleLabel.fontSizeMin = 16f;
            paletteToggleLabel.raycastTarget = false;
            // 기본 두 칸 너비에서 16pt로도 한 줄이 안 들어가면 필요한 만큼만 확장한다.
            paletteToggleTextWidth = Mathf.Ceil(paletteToggleLabel.GetPreferredValues(paletteToggleLabel.text).x * (16f / 24f));
            ApplyButtonColors(paletteToggleButton);
            paletteToggleButton.onClick.AddListener(TogglePalette);
            paletteToggleButton.gameObject.SetActive(false);
        }

        private void UpdatePaletteToggleLayout(Vector2 hotbarSize, int horizontalPadding)
        {
            if (paletteToggleButton == null || !(transform is RectTransform parent) || parent.rect.width <= 0f) return;
            float slotWidth = (hotbarSize.x - horizontalPadding * 2f - hotbarLayout.spacing * 9f) / 10f;
            float twoSlotsWidth = slotWidth * 2f + hotbarLayout.spacing;
            float width = Mathf.Min(hotbarSize.x, Mathf.Max(twoSlotsWidth, (paletteToggleTextWidth + 2f) / .92f));
            float left = hotbarAnchorMin.x + horizontalPadding / parent.rect.width;
            var rect = (RectTransform)paletteToggleButton.transform;
            rect.anchorMin = new Vector2(left, hotbarAnchorMax.y + .01f);
            rect.anchorMax = new Vector2(left + width / parent.rect.width, hotbarAnchorMax.y + .05f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            paletteToggleLabel.rectTransform.anchorMin = new Vector2(.04f, .08f);
            paletteToggleLabel.rectTransform.anchorMax = new Vector2(.96f, .92f);
            paletteToggleLabel.rectTransform.offsetMin = paletteToggleLabel.rectTransform.offsetMax = Vector2.zero;
        }

        private void UpdatePaletteLayout()
        {
            if (palette == null || paletteLayout == null || paletteReferenceSize.x <= 0f || paletteReferenceSize.y <= 0f) return;
            Vector2 ratio = new Vector2(Mathf.Clamp(paletteSizeRatio.x, .01f, 1f), Mathf.Clamp(paletteSizeRatio.y, .01f, 1f));
            Vector2 anchorMin = Vector2.one * .5f - ratio * .5f;
            Vector2 anchorMax = Vector2.one * .5f + ratio * .5f;
            bool anchorsChanged = palette.anchorMin != anchorMin || palette.anchorMax != anchorMax;
            if (anchorsChanged)
            {
                palette.anchorMin = anchorMin;
                palette.anchorMax = anchorMax;
                palette.offsetMin = palette.offsetMax = Vector2.zero;
            }
            Vector2 size = palette.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float canvasScale = hotbarCanvas != null ? Mathf.Max(.01f, hotbarCanvas.scaleFactor) : 1f;
            if (!anchorsChanged && size == lastPaletteSize && Mathf.Approximately(canvasScale, lastPaletteScale)) return;
            lastPaletteSize = size;
            lastPaletteScale = canvasScale;
            float scaleX = size.x / paletteReferenceSize.x;
            float scaleY = size.y / paletteReferenceSize.y;
            paletteLayout.padding = new RectOffset(Mathf.RoundToInt(16f * scaleX), Mathf.RoundToInt(16f * scaleX),
                Mathf.RoundToInt(12f * scaleY), Mathf.RoundToInt(16f * scaleY));
            paletteLayout.spacing = 10f * scaleY;
            paletteTitleLayout.preferredHeight = paletteReferenceTitleHeight * scaleY;
            foreach (var category in paletteCategories)
            {
                category.Layout.spacing = 6f * scaleY;
                category.Heading.preferredHeight = 28f * scaleY;
                category.Grid.cellSize = new Vector2(slotSize.x * scaleX, slotSize.y * scaleY);
                category.Grid.spacing = new Vector2(6f * scaleX, 6f * scaleY);
                category.GridSize.preferredHeight = category.Rows * category.Grid.cellSize.y +
                    Mathf.Max(0, category.Rows - 1) * category.Grid.spacing.y;
            }
            foreach (var slot in paletteSlots) if (slot != null) slot.SetContentPadding(5f / canvasScale);
            LayoutRebuilder.MarkLayoutForRebuild(palette);
        }

        private int ComputeState()
        {
            int state = builder.DeleteMode ? -1 : builder.SelectedSlot;
            for (int i = 0; i < builder.FavoriteCount; i++) state = state * 31 + builder.GetFavorite(i);
            for (int id = 1; id <= builder.Catalog.Count; id++)
            {
                state = state * 31 + (builder.IsModuleLocked(id) ? 1 : 0);
                state = state * 31 + builder.GetNextModuleCreditCost(id);
            }
            return state;
        }

        private void Refresh()
        {
            ModuleCatalog catalog = builder.Catalog;
            int selected = builder.DeleteMode ? 0 : builder.SelectedSlot;
            for (int i = 0; i < hotbarSlots.Count; i++)
            {
                int id = builder.GetFavorite(i);
                catalog.TryGet(id, out ModuleCatalog.Entry entry);
                hotbarSlots[i].Show(id, entry.displayName, entry.color, id == selected, builder.IsModuleLocked(id), $"${entry.data.BuyPrice} · 재료{builder.GetNextModuleCreditCost(id)}");
            }
            for (int i = 0; i < paletteSlots.Count; i++)
            {
                int id = i + 1;
                catalog.TryGet(id, out ModuleCatalog.Entry entry);
                paletteSlots[i].Show(id, entry.displayName, entry.color, id == selected, builder.IsModuleLocked(id), $"${entry.data.BuyPrice} · 재료{builder.GetNextModuleCreditCost(id)}");
            }
            UpdateDragGuide();
        }

        public void HandleClick(ModuleSlotUI slot)
        {
            if (builder == null || slot == null || draggingModule != 0) return;
            if (!TutorialManager.TryAction("select-module")) return;
            if ((!builder.PaletteOpen && slot.FavoriteIndex < 0) || builder.IsModuleLocked(slot.ModuleId)) return;
            builder.SelectModule(slot.ModuleId);
        }

        public void BeginDrag(ModuleSlotUI slot, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || draggingModule != 0) return;
            if (!CanDragFrom(slot)) { TutorialManager.Instance?.NotifyBlockedAction(); return; }
            dragSource = slot;
            draggingModule = slot.ModuleId;
            dragPointerId = eventData.pointerId;
            dragStartedWithPalette = builder.PaletteOpen;
            eventData.eligibleForClick = false;
            builder.SetFavoriteDrag(true);
            builder.Catalog.TryGet(draggingModule, out ModuleCatalog.Entry entry);
            dragGhostSwatch.color = entry.color;
            dragGhostLabel.text = entry.displayName;
            dragGhost.gameObject.SetActive(true);
            dragGhost.SetAsLastSibling();
            Drag(eventData);
            UpdateDropTarget();
            UpdateDragGuide();
        }

        public void Drag(PointerEventData eventData)
        {
            if (draggingModule == 0 || eventData.pointerId != dragPointerId) return;
            RectTransform root = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eventData.position,
                eventData.pressEventCamera, out Vector2 local))
                dragGhost.localPosition = local;
        }

        private bool CanDragFrom(ModuleSlotUI slot) => builder != null && builder.Catalog != null &&
            builder.CanEditFavorites && slot != null && slot.isActiveAndEnabled && slot.ModuleId > 0 &&
            !builder.IsModuleLocked(slot.ModuleId) && (slot.FavoriteIndex >= 0 || builder.PaletteOpen) &&
            (TutorialManager.Instance == null || TutorialManager.Instance.AllowsQuickslotSource(slot.ModuleId));

        private bool CanContinueDrag() => CanDragFrom(dragSource) &&
            (!dragStartedWithPalette || builder.PaletteOpen) &&
            (dragSource.FavoriteIndex < 0 || builder.GetFavorite(dragSource.FavoriteIndex) == draggingModule);

        private bool CanDropOn(ModuleSlotUI target) => draggingModule != 0 && CanContinueDrag() &&
            target != null && target.isActiveAndEnabled && target.FavoriteIndex >= 0 && target != dragSource &&
            (dragSource.FavoriteIndex >= 0 || target.ModuleId != draggingModule) &&
            (TutorialManager.Instance == null || TutorialManager.Instance.AllowsQuickslotChange(draggingModule, target.FavoriteIndex));

        private void UpdateDropTarget() => SetDropTarget(CanDropOn(hoveredSlot) ? hoveredSlot : null);

        private void SetDropTarget(ModuleSlotUI target)
        {
            if (dropTarget == target) return;
            dropTarget?.SetDropHighlight(false);
            dropTarget = target;
            dropTarget?.SetDropHighlight(true);
            UpdateDragGuide();
        }

        private void UpdateDragGuide()
        {
            if (hotbarGuide == null) return;
            var step = TutorialManager.Instance?.CurrentStep;
            if (step != null && step.restrictQuickslotChange && dropTarget == null)
            {
                hotbarGuide.text = step.objective;
                return;
            }
            hotbarGuide.text = draggingModule == 0 ? HotbarGuideText : dropTarget == null
                ? "교체할 퀵슬롯에 놓으세요 · 바깥에 놓으면 취소됩니다"
                : $"{(dropTarget.FavoriteIndex + 1) % 10}번 퀵슬롯에 놓아 " +
                  (dragSource.FavoriteIndex >= 0 ? "서로 교체합니다" : "등록합니다");
        }

        public void Drop(ModuleSlotUI target, PointerEventData eventData)
        {
            if (draggingModule == 0 || dragSource == null || eventData.pointerId != dragPointerId ||
                eventData.pointerDrag != dragSource.gameObject) return;
            if (!CanDropOn(target))
            {
                if (target != null && target != dragSource) TutorialManager.Instance?.NotifyBlockedAction();
                return;
            }
            if (dragSource.FavoriteIndex >= 0)
            {
                if (!builder.SwapFavorites(dragSource.FavoriteIndex, target.FavoriteIndex)) return;
            }
            else builder.SetFavorite(target.FavoriteIndex, draggingModule);
            lastState = ComputeState();
            Refresh();
            CancelDrag();
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (draggingModule != 0 && eventData.pointerId == dragPointerId) CancelDrag();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }

        private void CancelDrag()
        {
            draggingModule = 0;
            dragSource = null;
            dragStartedWithPalette = false;
            builder?.SetFavoriteDrag(false);
            SetDropTarget(null);
            UpdateDragGuide();
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        private void Build()
        {
            slotSize.y = Mathf.Max(slotSize.y, 84f);
            hotbar = CreateRect("Hotbar", transform);
            hotbar.anchorMin = hotbarAnchorMin;
            hotbar.anchorMax = hotbarAnchorMax;
            hotbar.offsetMin = hotbar.offsetMax = Vector2.zero;
            hotbarCanvas = GetComponentInParent<Canvas>();
            hotbarLayout = hotbar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hotbarLayout.childControlWidth = hotbarLayout.childControlHeight = true;
            hotbarLayout.childForceExpandWidth = hotbarLayout.childForceExpandHeight = true;
            for (int i = 0; i < 10; i++) hotbarSlots.Add(CreateSlot(hotbar, i));
            BuildPaletteToggle();
            hotbarGuide = CreateText("HotbarGuide", (RectTransform)transform, HotbarGuideText, 18);
            hotbarGuide.rectTransform.anchorMin = new Vector2(hotbarAnchorMin.x, hotbarAnchorMax.y + .01f);
            hotbarGuide.rectTransform.anchorMax = new Vector2(hotbarAnchorMax.x, hotbarAnchorMax.y + .04f);
            hotbarGuide.rectTransform.offsetMin = hotbarGuide.rectTransform.offsetMax = Vector2.zero;
            hotbarGuide.alignment = TMPro.TextAlignmentOptions.Midline;
            hotbarGuide.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            hotbarGuide.enableAutoSizing = true;
            hotbarGuide.fontSizeMin = 18f;
            hotbarGuide.fontSizeMax = 22f;
            hotbarGuide.gameObject.SetActive(false);
            UpdateHotbarLayout();

            palette = CreateRect("Palette", transform);
            palette.anchorMin = palette.anchorMax = palette.pivot = new Vector2(0.5f, 0.5f);
            palette.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
            var column = paletteLayout = palette.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(16, 16, 12, 16);
            column.spacing = 10f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            TMPro.TextMeshProUGUI title = CreateText("Title", palette,
                "모듈 · 클릭: 선택 · 마우스 올리고 1~9·0: 등록\n목록을 하단으로 드래그: 등록 · 퀵슬롯끼리 드래그: 교체", 18);
            title.alignment = TMPro.TextAlignmentOptions.Midline;
            Vector2 titlePreferred = title.GetPreferredValues(title.text);
            paletteReferenceTitleHeight = titlePreferred.y;
            title.enableAutoSizing = true;
            title.fontSizeMin = 18f;
            title.fontSizeMax = 22f;
            paletteTitleLayout = title.gameObject.AddComponent<LayoutElement>();
            paletteTitleLayout.preferredHeight = paletteReferenceTitleHeight;
            int count = builder != null && builder.Catalog != null ? builder.Catalog.Count : 0;
            for (int i = 0; i < count; i++) paletteSlots.Add(null);
            float referenceContentWidth = titlePreferred.x;
            float referenceContentHeight = titlePreferred.y + 30f;
            for (int category = 0; category < 3; category++)
            {
                RectTransform group = CreateRect("ModuleCategory" + category, palette);
                var layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 6f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.UpperLeft;
                var heading = CreateText("CategoryTitle", group,
                    category == 0 ? "제어 모듈" : category == 1 ? "기능 모듈" : "치장 모듈", 18);
                heading.rectTransform.sizeDelta = new Vector2(110f, 28f);
                heading.enableAutoSizing = true;
                heading.fontSizeMin = 18f;
                heading.fontSizeMax = 22f;
                var headingSize = heading.gameObject.AddComponent<LayoutElement>();
                headingSize.preferredHeight = 28f;
                RectTransform grid = CreateRect("Grid", group);
                var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
                gridLayout.cellSize = slotSize;
                gridLayout.spacing = new Vector2(6f, 6f);
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                var gridSize = grid.gameObject.AddComponent<LayoutElement>();
                int categoryCount = 0;
                for (int id = 1; id <= count; id++)
                {
                    builder.Catalog.TryGet(id, out ModuleCatalog.Entry entry);
                    bool decorative = entry.data != null && (entry.data.ModuleType == StatType.Scope ||
                        entry.data.ModuleType == StatType.Color ||
                        (entry.prefab != null && entry.prefab.GetComponent<TriggerModule>() != null));
                    int moduleCategory = id <= 4 ? 0 : decorative ? 2 : 1;
                    if (moduleCategory == category)
                    {
                        paletteSlots[id - 1] = CreateSlot(grid, -1);
                        categoryCount++;
                    }
                }
                gridLayout.constraintCount = Mathf.Clamp(categoryCount, 1, Mathf.Clamp(paletteColumns, 1, 6));
                int rows = Mathf.CeilToInt(categoryCount / (float)gridLayout.constraintCount);
                float gridHeight = rows * slotSize.y + Mathf.Max(0, rows - 1) * 6f;
                referenceContentWidth = Mathf.Max(referenceContentWidth,
                    gridLayout.constraintCount * slotSize.x + (gridLayout.constraintCount - 1) * 6f);
                referenceContentHeight += 28f + 6f + gridHeight;
                paletteCategories.Add(new PaletteCategoryLayout
                {
                    Layout = layout, Heading = headingSize, Grid = gridLayout, GridSize = gridSize, Rows = rows
                });
            }
            // Preserve the old content proportions inside the independently anchored panel.
            paletteReferenceSize = new Vector2(referenceContentWidth + 32f, referenceContentHeight + 28f);
            UpdatePaletteLayout();

            dragGhost = CreateRect("DragGhost", transform);
            dragGhost.sizeDelta = slotSize;
            var ghostBackground = dragGhost.gameObject.AddComponent<Image>();
            ghostBackground.color = SlotColor;
            ghostBackground.raycastTarget = false;
            dragGhostSwatch = CreateSwatch(dragGhost);
            dragGhostLabel = CreateText("Label", dragGhost, "", 16);
            Stretch(dragGhostLabel.rectTransform, 18f);
            dragGhost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            BindCustomUI();
        }

#if UNITY_EDITOR
        public void BakeCustomUI()
        {
            if (customButtonRoot != null) throw new System.InvalidOperationException("Custom UI is already authored.");
            if (font == null) font = TMPro.TMP_Settings.defaultFontAsset;
            ResolvePaintMaterials(); ResolveStickerTextures(); ResolveKeyringModels();
            BuildCustomButton(); BuildCustomWorkspace();
            BuildPaintPanel(); BuildStickerPanel(); BuildKeyringPanel();
            RefreshPaintMaterialPage(); RefreshRgbInputs(); 
            customButtonRoot.gameObject.SetActive(true);
            customNavigation.gameObject.SetActive(true); customContentArea.gameObject.SetActive(true);
            paintPanel.gameObject.SetActive(true); stickerPanel.gameObject.SetActive(false); keyringPanel.gameObject.SetActive(false);
            materialSelectionView.gameObject.SetActive(true); customColorPicker.gameObject.SetActive(false);
        }
        [ContextMenu("Refresh Custom Resource Slots (Editor)")]
        public void RefreshCustomResourceSlots()
        {
            if (Application.isPlaying || stickerButtons.Count == 0 || keyringGrid == null) return;
            ResolveStickerTextures(); ResolveKeyringModels();
            var grid = stickerButtons[0].transform.parent as RectTransform;
            foreach (var button in stickerButtons) if (button != null) DestroyImmediate(button.gameObject);
            stickerButtons.Clear(); stickerHighlights.Clear();
            foreach (var texture in stickerTextures) CreateStickerTile(grid, texture);
            BuildKeyringTiles();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
        private void BindCustomUI()
        {
            EnsurePaintAllButton();
            customButtonRoot.GetComponent<Button>().onClick.AddListener(OpenCustomConfirmation);
            previousPaintPageButton.onClick.AddListener(() => ChangePaintMaterialPage(-1));
            nextPaintPageButton.onClick.AddListener(() => ChangePaintMaterialPage(1));
            customColorModeButton.onClick.AddListener(OpenCustomColorPicker);
            paintUndoButton.onClick.AddListener(() => builder?.UndoPaint());
            paintFillButton.onClick.AddListener(() => { if (builder != null) builder.SetPaintFillMode(!builder.IsPaintFillMode); RefreshPaintPanel(); });
            customRemoveButton.onClick.AddListener(OpenCustomRemovalConfirmation);
            stickerMirrorButton.onClick.AddListener(() => { builder?.ToggleStickerHorizontalFlip(); RefreshStickerPanel(); });
            stickerSizeDownButton.onClick.AddListener(() => { builder?.AdjustStickerSize(-1); RefreshStickerPanel(); });
            stickerSizeUpButton.onClick.AddListener(() => { builder?.AdjustStickerSize(1); RefreshStickerPanel(); });
            stickerUndoButton.onClick.AddListener(() => builder?.UndoSticker());
            stickerRemoveButton.onClick.AddListener(() => { if (builder != null) builder.SetStickerEraseMode(!builder.IsStickerEraseMode); RefreshStickerPanel(); });
            keyringRemoveButton.onClick.AddListener(() => { builder?.RemoveKeyring(); RefreshKeyringPanel(); });
            applyCustomColorButton.onClick.AddListener(SelectCustomColor);
            backToMaterialsButton.onClick.AddListener(ReturnToMaterialSelection);
            foreach (var entry in customPanels) { var target = entry; entry.Button.onClick.AddListener(() => SelectCustomPanel(target)); }
            for (int i = 0; i < paintMaterialButtons.Count; i++) { int slot = i; paintMaterialButtons[i].onClick.AddListener(() => SelectPaintPaletteSlot(slot)); }
            for (int i = 0; i < rgbInputs.Length; i++) { int channel = i; rgbInputs[i].onEndEdit.AddListener(value => SetRgbChannel(channel, value)); }
            colorSpectrum.GetComponent<EventTrigger>().triggers.Clear(); hueSpectrum.GetComponent<EventTrigger>().triggers.Clear();
            AddPointerHandler(colorSpectrum, EventTriggerType.PointerDown, SelectSpectrumColor);
            AddPointerHandler(colorSpectrum, EventTriggerType.Drag, SelectSpectrumColor);
            AddPointerHandler(colorSpectrum, EventTriggerType.PointerUp, _ => ApplyCustomColorToBrush());
            AddPointerHandler(hueSpectrum, EventTriggerType.PointerDown, SelectHue);
            AddPointerHandler(hueSpectrum, EventTriggerType.Drag, SelectHue);
            AddPointerHandler(hueSpectrum, EventTriggerType.PointerUp, _ => ApplyCustomColorToBrush());
            colorSpectrumTexture = CreateColorSpectrumTexture(customHue); hueSpectrumTexture = CreateHueSpectrumTexture();
            colorSpectrum.texture = colorSpectrumTexture; hueSpectrum.texture = hueSpectrumTexture;
            for (int i = 0; i < stickerButtons.Count; i++)
            {
                var button = stickerButtons[i]; bool available = i < stickerTextures.Count;
                button.gameObject.SetActive(available); if (!available) continue;
                var texture = stickerTextures[i];
                button.GetComponentInChildren<RawImage>(true).texture = texture;
                button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = texture.name;
                button.onClick.AddListener(() => SelectStickerTexture(texture));
            }
            RebuildKeyringTiles(); RefreshPaintMaterialPage(); RefreshRgbInputs();
        }

        private void EnsurePaintAllButton()
        {
            if (paintAllButton == null && paintPanel != null)
            {
                paintAllButton = CreateButton("PaintAllFaces", paintPanel, "전체 도색", new Vector2(150f, 44f),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(154f, 14f),
                    new Color(0.22f, 0.28f, 0.33f, 1f));
            }

            if (paintAllButton != null)
                paintAllButton.onClick.AddListener(PaintAllFaces);
        }

        private void PaintAllFaces()
        {
            if (builder != null) builder.PaintAllFaces();
            RefreshPaintPanel();
        }
        private void RebuildKeyringTiles()
        {
            for (int i = 0; i < keyringModelButtons.Count; i++)
            {
                var button = keyringModelButtons[i]; bool available = i < keyringModels.Count;
                button.gameObject.SetActive(available); button.onClick.RemoveAllListeners();
                if (!available) continue;
                var model = keyringModels[i];
                button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = model.name;
                button.onClick.AddListener(() => SelectKeyringModel(model));
            }
            if (keyringEmptyLabel != null) { keyringEmptyLabel.text = GetKeyringEmptyMessage(); keyringEmptyLabel.gameObject.SetActive(keyringModels.Count == 0); }
        }
        private void BuildCustomLimitToast()
        {
            customLimitToast = CreateRect("CustomLimitToast", transform);
            customLimitToast.anchorMin = customLimitToast.anchorMax = new Vector2(0.5f, 0.5f);
            customLimitToast.pivot = new Vector2(0.5f, 0.5f);
            customLimitToast.anchoredPosition = Vector2.zero;
            customLimitToast.sizeDelta = new Vector2(560f, 100f);

            Image background = customLimitToast.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.045f, 0.045f, 0.95f);
            background.raycastTarget = false;

            customLimitMessage = CreateText("Message", customLimitToast,
                "스티커는 10개까지 붙일 수 있습니다", 26);
            UIFonts.ApplyWeight(customLimitMessage, 700);
            customLimitMessage.alignment = TMPro.TextAlignmentOptions.Midline;
            customLimitMessage.raycastTarget = false;
            customLimitMessage.rectTransform.anchorMin = Vector2.zero;
            customLimitMessage.rectTransform.anchorMax = Vector2.one;
            customLimitMessage.rectTransform.offsetMin = new Vector2(20f, 8f);
            customLimitMessage.rectTransform.offsetMax = new Vector2(-20f, -8f);
            customLimitToast.gameObject.SetActive(false);
        }

        // 레거시 테이블: Resources 폴더를 스캔해 목록을 만든다. 스티커 목록은 테이블로 승격하기 전까지 이 경로를 유지한다.
        private void ResolveStickerTextures()
        {
            stickerTextures.Clear();
            stickerTextures.AddRange(Resources.LoadAll<Texture2D>("Stickers"));
            stickerTextures.Sort((left, right) => string.Compare(left.name, right.name,
                System.StringComparison.OrdinalIgnoreCase));
        }

        // 레거시 테이블: Resources 폴더를 스캔해 목록을 만든다. 열쇠고리 목록은 테이블로 승격하기 전까지 이 경로를 유지한다.
        private void ResolveKeyringModels()
        {
            keyringModels.Clear();
            GameObject[] loadedModels = Resources.LoadAll<GameObject>("Keyrings");
            foreach (GameObject model in loadedModels)
                if (model != null && model.GetComponentInChildren<Renderer>(true) != null)
                    keyringModels.Add(model);
            keyringAssetsFoundWithoutRenderer = loadedModels.Length > 0 && keyringModels.Count == 0;
            keyringModels.Sort((left, right) => string.Compare(left.name, right.name,
                System.StringComparison.OrdinalIgnoreCase));
        }

        private string GetKeyringEmptyMessage()
        {
            return keyringAssetsFoundWithoutRenderer
                ? "FBX는 찾았지만 메시 렌더러가 없습니다. FBX의 Mesh 임포트 설정을 확인하세요."
                : "Keyrings 경로에서 FBX 모델을 찾지 못했습니다. Assets/Resources/Keyrings를 확인하세요.";
        }

        private void ResolvePaintMaterials()
        {
            activePaintMaterials.Clear();
            if (paintMaterials != null)
                foreach (PaintMaterialOption option in paintMaterials)
                    if (option != null && option.material != null) activePaintMaterials.Add(option);
            generatedPaintMaterials = activePaintMaterials.Count > 0
                ? PaintMaterialLibrary.CreateCustomColorLibrary()
                : PaintMaterialLibrary.CreateDefaults();
            if (activePaintMaterials.Count > 0) return;
            foreach (PaintMaterialOption option in generatedPaintMaterials.Options)
                activePaintMaterials.Add(option);
        }

        private void BuildCustomButton()
        {
            Button button = CreateButton("CustomButton", transform, "커스텀", new Vector2(190f, 52f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f),
                new Color(0.24f, 0.31f, 0.36f, 0.96f));
            customButtonRoot = (RectTransform)button.transform;
            button.onClick.AddListener(OpenCustomConfirmation);
        }

        private void BuildCustomConfirmation()
        {
            customConfirmation = CreateRect("CustomConfirmation", transform);
            customConfirmation.anchorMin = Vector2.zero;
            customConfirmation.anchorMax = Vector2.one;
            customConfirmation.offsetMin = Vector2.zero;
            customConfirmation.offsetMax = Vector2.zero;
            Image dim = customConfirmation.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);

            RectTransform card = CreateRect("Card", customConfirmation);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(570f, 230f);
            card.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 1f);

            customConfirmationMessage = CreateText("Message", card,
                "커스텀 제거 전까진 총기 수정이 불가합니다.\n그래도 하시겠습니까?", 22);
            customConfirmationMessage.alignment = TMPro.TextAlignmentOptions.Midline;
            customConfirmationMessage.rectTransform.anchorMin = new Vector2(0f, 0.36f);
            customConfirmationMessage.rectTransform.anchorMax = new Vector2(1f, 1f);
            customConfirmationMessage.rectTransform.offsetMin = new Vector2(20f, 0f);
            customConfirmationMessage.rectTransform.offsetMax = new Vector2(-20f, -16f);

            Button yes = CreateButton("Yes", card, "예", new Vector2(150f, 50f),
                new Vector2(0.36f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Color(0.25f, 0.46f, 0.34f, 1f));
            Button no = CreateButton("No", card, "아니오", new Vector2(150f, 50f),
                new Vector2(0.64f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Color(0.43f, 0.27f, 0.27f, 1f));
            customYesButton = yes; customNoButton = no;
            yes.onClick.AddListener(ConfirmCustom);
            no.onClick.AddListener(CancelCustomConfirmation);
        }

        private void BuildPaintPanel()
        {
            paintPanel = CreateRect("PaintPanel", customContentArea);
            StretchPanel(paintPanel);
            paintPanel.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.055f, 0.065f, 0.94f);

            paintTitle = CreateText("Title", paintPanel, "도색 - 준비된 재질", 24);
            UIFonts.ApplyWeight(paintTitle, 700);
            paintTitle.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            paintTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            paintTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            paintTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            paintTitle.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            paintTitle.rectTransform.sizeDelta = new Vector2(-32f, 40f);

            materialSelectionView = CreateRect("MaterialSelectionView", paintPanel);
            materialSelectionView.anchorMin = Vector2.zero;
            materialSelectionView.anchorMax = Vector2.one;
            materialSelectionView.offsetMin = new Vector2(0f, 56f);
            materialSelectionView.offsetMax = new Vector2(0f, -56f);

            RectTransform grid = CreateRect("MaterialGrid", materialSelectionView);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(0f, 1f);
            grid.pivot = new Vector2(0f, 1f);
            grid.anchoredPosition = new Vector2(16f, -20f);
            grid.sizeDelta = new Vector2(238f, 420f);
            grid.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.09f, 0.1f, 0.72f);

            for (int i = 0; i < PaintPalettePageSize; i++)
            {
                int slot = i;
                Button button = CreateButton("PaintMaterialSlot" + i, grid, "",
                    new Vector2(112f, 76f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2((i % 2) * 120f, -(i / 2) * 84f),
                    new Color(0.12f, 0.14f, 0.16f, 1f));
                Outline outline = button.gameObject.AddComponent<Outline>();
                outline.enabled = false;
                outline.effectDistance = new Vector2(3f, -3f);
                outline.effectColor = new Color(1f, 0.76f, 0.28f, 1f);
                paintMaterialHighlights.Add(outline);
                paintMaterialButtons.Add(button);

                RawImage swatch = CreateRect("Swatch", button.transform).gameObject.AddComponent<RawImage>();
                swatch.rectTransform.anchorMin = Vector2.zero;
                swatch.rectTransform.anchorMax = new Vector2(0f, 1f);
                swatch.rectTransform.pivot = new Vector2(0f, 0.5f);
                swatch.rectTransform.anchoredPosition = new Vector2(5f, 0f);
                swatch.rectTransform.sizeDelta = new Vector2(30f, -10f);
                swatch.raycastTarget = false;
                paintMaterialSwatches.Add(swatch);

                TMPro.TextMeshProUGUI label = CreateText("MaterialLabel", (RectTransform)button.transform, "", 11);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(38f, 1f);
                label.rectTransform.offsetMax = new Vector2(-3f, -1f);
                label.raycastTarget = false;
                label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TMPro.TextWrappingModes.Normal;
                label.overflowMode = TMPro.TextOverflowModes.Truncate;
                paintMaterialLabels.Add(label);
                button.onClick.AddListener(() => SelectPaintPaletteSlot(slot));
            }

            previousPaintPageButton = CreateButton("PreviousPaintPage", materialSelectionView, "이전",
                new Vector2(64f, 34f), Vector2.zero, Vector2.zero, new Vector2(16f, 64f),
                new Color(0.16f, 0.19f, 0.22f, 1f));
            previousPaintPageButton.onClick.AddListener(() => ChangePaintMaterialPage(-1));
            paintPageLabel = CreateText("PaintPageLabel", materialSelectionView, "", 14);
            paintPageLabel.rectTransform.anchorMin = paintPageLabel.rectTransform.anchorMax = Vector2.zero;
            paintPageLabel.rectTransform.pivot = Vector2.zero;
            paintPageLabel.rectTransform.anchoredPosition = new Vector2(86f, 64f);
            paintPageLabel.rectTransform.sizeDelta = new Vector2(82f, 34f);
            paintPageLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            nextPaintPageButton = CreateButton("NextPaintPage", materialSelectionView, "다음",
                new Vector2(64f, 34f), Vector2.zero, Vector2.zero, new Vector2(176f, 64f),
                new Color(0.16f, 0.19f, 0.22f, 1f));
            nextPaintPageButton.onClick.AddListener(() => ChangePaintMaterialPage(1));

            customColorModeButton = CreateButton("OpenCustomColor", materialSelectionView, "나만의 색상",
                new Vector2(238f, 38f), Vector2.zero, Vector2.zero, new Vector2(16f, 24f),
                new Color(0.23f, 0.29f, 0.36f, 1f));
            customColorModeButton.onClick.AddListener(OpenCustomColorPicker);
            RefreshPaintMaterialPage();

            RectTransform details = CreateRect("MaterialDetails", materialSelectionView);
            details.anchorMin = details.anchorMax = new Vector2(1f, 1f);
            details.pivot = new Vector2(1f, 1f);
            details.anchoredPosition = new Vector2(-16f, -20f);
            details.sizeDelta = new Vector2(278f, 420f);
            details.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.105f, 0.12f, 1f);

            paintPreview = CreateRect("Preview", details).gameObject.AddComponent<RawImage>();
            paintPreview.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            paintPreview.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            paintPreview.rectTransform.pivot = new Vector2(0.5f, 1f);
            paintPreview.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            paintPreview.rectTransform.sizeDelta = new Vector2(244f, 184f);
            paintPreview.color = Color.white;
            paintPreview.raycastTarget = false;

            paintName = CreateText("MaterialName", details, "재질을 선택하세요", 20);
            paintName.alignment = TMPro.TextAlignmentOptions.Midline;
            UIFonts.ApplyWeight(paintName, 700);
            paintName.rectTransform.anchorMin = new Vector2(0f, 1f);
            paintName.rectTransform.anchorMax = new Vector2(1f, 1f);
            paintName.rectTransform.pivot = new Vector2(0.5f, 1f);

            paintInstructions = CreateText("Instructions", details,
                "재질 선택 후 총기 면을 누른 채 드래그하세요.\n이미 칠한 면은 재질 변경으로 바뀌지 않습니다.", 15);
            paintInstructions.alignment = TMPro.TextAlignmentOptions.Midline;
            paintInstructions.rectTransform.anchorMin = new Vector2(0f, 0f);
            paintInstructions.rectTransform.anchorMax = new Vector2(1f, 0f);
            paintInstructions.rectTransform.pivot = new Vector2(0.5f, 0f);
            paintInstructions.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            paintInstructions.rectTransform.sizeDelta = new Vector2(-16f, 76f);

            BuildCustomColorPicker(paintPanel);

            paintUndoButton = CreateButton("UndoPaint", paintPanel, "되돌리기 (Ctrl+Z)", new Vector2(150f, 44f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 14f),
                new Color(0.22f, 0.28f, 0.33f, 1f));
            paintUndoButton.onClick.AddListener(() => builder?.UndoPaint());

            paintFillButton = CreateButton("PaintBucket", paintPanel, "물감통", new Vector2(142f, 44f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f),
                new Color(0.22f, 0.28f, 0.33f, 1f));
            paintFillButton.onClick.AddListener(() =>
            {
                if (builder != null) builder.SetPaintFillMode(!builder.IsPaintFillMode);
                RefreshPaintPanel();
            });

            paintAllButton = CreateButton("PaintAllFaces", paintPanel, "전체 도색", new Vector2(150f, 44f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(154f, 14f),
                new Color(0.22f, 0.28f, 0.33f, 1f));
            paintAllButton.interactable = true;

            paintStatus = CreateText("PaintStatus", paintPanel, "총기에서 도색할 면을 클릭하세요", 14);
            paintStatus.alignment = TMPro.TextAlignmentOptions.MidlineRight;
            paintStatus.rectTransform.anchorMin = new Vector2(0f, 0f);
            paintStatus.rectTransform.anchorMax = new Vector2(1f, 0f);
            paintStatus.rectTransform.pivot = new Vector2(1f, 0f);
            paintStatus.rectTransform.anchoredPosition = new Vector2(-16f, 66f);
            paintStatus.rectTransform.sizeDelta = new Vector2(-32f, 44f);

            RegisterCustomPanel("도색", paintPanel);
        }

        private void BuildStickerPanel()
        {
            stickerPanel = CreateRect("StickerPanel", customContentArea);
            StretchPanel(stickerPanel);
            stickerPanel.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.055f, 0.065f, 0.94f);

            TMPro.TextMeshProUGUI title = CreateText("StickerTitle", stickerPanel, "스티커", 24);
            UIFonts.ApplyWeight(title, 700);
            title.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            title.rectTransform.sizeDelta = new Vector2(-28f, 36f);

            TMPro.TextMeshProUGUI source = CreateText("StickerSource", stickerPanel,
                "PNG 위치: Assets/Resources/Stickers", 13);
            source.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            source.rectTransform.anchorMin = new Vector2(0f, 1f);
            source.rectTransform.anchorMax = new Vector2(1f, 1f);
            source.rectTransform.pivot = new Vector2(0.5f, 1f);
            source.rectTransform.anchoredPosition = new Vector2(0f, -48f);
            source.rectTransform.sizeDelta = new Vector2(-28f, 24f);

            TMPro.TextMeshProUGUI help = CreateText("StickerHelp", stickerPanel,
                "PNG를 붙인 뒤 아래 버튼으로 마지막 스티커를 조절하세요.", 13);
            help.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            help.rectTransform.anchorMin = new Vector2(0f, 1f);
            help.rectTransform.anchorMax = new Vector2(1f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(0f, -72f);
            help.rectTransform.sizeDelta = new Vector2(-28f, 22f);

            Vector2 controlAnchor = new Vector2(0.5f, 1f);
            stickerMirrorButton = CreateButton("StickerMirror", stickerPanel, "좌우 반전: 끔",
                new Vector2(156f, 32f), controlAnchor, controlAnchor, new Vector2(-82f, -98f),
                new Color(0.18f, 0.22f, 0.25f, 1f));
            stickerMirrorButtonLabel = stickerMirrorButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (stickerMirrorButtonLabel != null) stickerMirrorButtonLabel.fontSize = 18;
            stickerMirrorButton.onClick.AddListener(() =>
            {
                builder?.ToggleStickerHorizontalFlip();
                RefreshStickerPanel();
            });

            stickerSizeDownButton = CreateButton("StickerSizeDown", stickerPanel, "−",
                new Vector2(38f, 32f), controlAnchor, controlAnchor, new Vector2(16f, -98f),
                new Color(0.18f, 0.22f, 0.25f, 1f));
            stickerSizeDownButton.onClick.AddListener(() =>
            {
                builder?.AdjustStickerSize(-1);
                RefreshStickerPanel();
            });

            stickerSizeLabel = CreateText("StickerSizeValue", stickerPanel, "×1.00", 14);
            stickerSizeLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            stickerSizeLabel.rectTransform.anchorMin = controlAnchor;
            stickerSizeLabel.rectTransform.anchorMax = controlAnchor;
            stickerSizeLabel.rectTransform.pivot = controlAnchor;
            stickerSizeLabel.rectTransform.anchoredPosition = new Vector2(83f, -98f);
            stickerSizeLabel.rectTransform.sizeDelta = new Vector2(76f, 32f);

            stickerSizeUpButton = CreateButton("StickerSizeUp", stickerPanel, "+",
                new Vector2(38f, 32f), controlAnchor, controlAnchor, new Vector2(142f, -98f),
                new Color(0.18f, 0.22f, 0.25f, 1f));
            stickerSizeUpButton.onClick.AddListener(() =>
            {
                builder?.AdjustStickerSize(1);
                RefreshStickerPanel();
            });

            RectTransform viewport = CreateRect("StickerViewport", stickerPanel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(14f, StickerViewportBottomInset);
            viewport.offsetMax = new Vector2(-32f, -StickerViewportTopInset);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.075f, 0.09f, 0.1f, 0.8f);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform content = CreateRect("StickerGrid", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            GridLayoutGroup stickerGrid = content.gameObject.AddComponent<GridLayoutGroup>();
            stickerGrid.padding = new RectOffset(4, 4, 6, 6);
            stickerGrid.cellSize = new Vector2(88f, 112f);
            stickerGrid.spacing = new Vector2(8f, 8f);
            stickerGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            stickerGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            stickerGrid.childAlignment = TextAnchor.UpperLeft;
            stickerGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            stickerGrid.constraintCount = 5;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect stickerScroll = stickerPanel.gameObject.AddComponent<ScrollRect>();
            stickerScroll.enabled = false;
            stickerScroll.viewport = viewport;
            stickerScroll.content = content;
            stickerScroll.horizontal = false;
            stickerScroll.vertical = true;
            stickerScroll.movementType = ScrollRect.MovementType.Clamped;
            stickerScroll.scrollSensitivity = 32f;
            stickerScroll.verticalScrollbar = BuildStickerScrollbar();
            stickerScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            stickerScroll.verticalScrollbarSpacing = 4f;
            stickerScroll.enabled = true;

            stickerEmptyLabel = CreateText("StickerEmpty", viewport,
                "Assets/Resources/Stickers 폴더에 PNG를 넣어주세요.", 15);
            stickerEmptyLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            stickerEmptyLabel.rectTransform.anchorMin = Vector2.zero;
            stickerEmptyLabel.rectTransform.anchorMax = Vector2.one;
            stickerEmptyLabel.rectTransform.offsetMin = new Vector2(18f, 18f);
            stickerEmptyLabel.rectTransform.offsetMax = new Vector2(-18f, -18f);
            stickerEmptyLabel.gameObject.SetActive(stickerTextures.Count == 0);

            stickerUndoButton = CreateButton("UndoSticker", stickerPanel, "되돌리기 (Ctrl+Z)",
                new Vector2(150f, 44f), Vector2.zero, Vector2.zero, new Vector2(14f, 12f),
                new Color(0.22f, 0.28f, 0.33f, 1f));
            stickerUndoButton.onClick.AddListener(() => builder?.UndoSticker());

            stickerRemoveButton = CreateButton("EraseStickerMode", stickerPanel, "스티커 제거",
                new Vector2(150f, 44f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-14f, 12f), new Color(0.3f, 0.25f, 0.25f, 1f));
            stickerRemoveButton.onClick.AddListener(() =>
            {
                if (builder != null) builder.SetStickerEraseMode(!builder.IsStickerEraseMode);
                RefreshStickerPanel();
            });

            stickerStatus = CreateText("StickerStatus", stickerPanel, "PNG 스티커를 선택하세요", 13);
            stickerStatus.alignment = TMPro.TextAlignmentOptions.Midline;
            stickerStatus.rectTransform.anchorMin = new Vector2(0f, 0f);
            stickerStatus.rectTransform.anchorMax = new Vector2(1f, 0f);
            stickerStatus.rectTransform.pivot = new Vector2(0.5f, 0f);
            stickerStatus.rectTransform.anchoredPosition = new Vector2(0f, 58f);
            stickerStatus.rectTransform.sizeDelta = new Vector2(-28f, 28f);

            for (int i = 0; i < stickerTextures.Count; i++)
                CreateStickerTile(content, stickerTextures[i]);

            RegisterCustomPanel("스티커", stickerPanel);
        }

        private void BuildKeyringPanel()
        {
            keyringPanel = CreateRect("KeyringPanel", customContentArea);
            StretchPanel(keyringPanel);
            keyringPanel.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.055f, 0.065f, 0.94f);

            TMPro.TextMeshProUGUI title = CreateText("KeyringTitle", keyringPanel, "키링", 24);
            UIFonts.ApplyWeight(title, 700);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            title.rectTransform.sizeDelta = new Vector2(-28f, 36f);

            TMPro.TextMeshProUGUI source = CreateText("KeyringSource", keyringPanel,
                "FBX/모델 위치: Assets/Resources/Keyrings", 13);
            source.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            source.rectTransform.anchorMin = new Vector2(0f, 1f);
            source.rectTransform.anchorMax = new Vector2(1f, 1f);
            source.rectTransform.pivot = new Vector2(0.5f, 1f);
            source.rectTransform.anchoredPosition = new Vector2(0f, -48f);
            source.rectTransform.sizeDelta = new Vector2(-28f, 22f);

            TMPro.TextMeshProUGUI help = CreateText("KeyringHelp", keyringPanel,
                "같은 이름 PNG를 모델 색으로 적용합니다 (UV 필요).\n모델 선택 후 총 겉면을 클릭해 줄을 연결하세요.", 14);
            help.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            help.rectTransform.anchorMin = new Vector2(0f, 1f);
            help.rectTransform.anchorMax = new Vector2(1f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(0f, -72f);
            help.rectTransform.sizeDelta = new Vector2(-28f, 36f);

            RectTransform viewport = CreateRect("KeyringViewport", keyringPanel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(14f, KeyringViewportBottomInset);
            viewport.offsetMax = new Vector2(-32f, -KeyringViewportTopInset);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.075f, 0.09f, 0.1f, 0.8f);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            keyringGrid = CreateRect("KeyringGrid", viewport);
            RectTransform content = keyringGrid;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(4, 4, 6, 6);
            grid.cellSize = new Vector2(156f, 64f);
            grid.spacing = new Vector2(8f, 8f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect keyringScroll = keyringPanel.gameObject.AddComponent<ScrollRect>();
            keyringScroll.enabled = false;
            keyringScroll.viewport = viewport;
            keyringScroll.content = content;
            keyringScroll.horizontal = false;
            keyringScroll.vertical = true;
            keyringScroll.movementType = ScrollRect.MovementType.Clamped;
            keyringScroll.scrollSensitivity = 32f;
            keyringScroll.verticalScrollbar = BuildKeyringScrollbar();
            keyringScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            keyringScroll.verticalScrollbarSpacing = 4f;
            keyringScroll.enabled = true;

            keyringEmptyLabel = CreateText("KeyringEmpty", viewport,
                GetKeyringEmptyMessage(), 15);
            keyringEmptyLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            keyringEmptyLabel.rectTransform.anchorMin = Vector2.zero;
            keyringEmptyLabel.rectTransform.anchorMax = Vector2.one;
            keyringEmptyLabel.rectTransform.offsetMin = new Vector2(18f, 18f);
            keyringEmptyLabel.rectTransform.offsetMax = new Vector2(-18f, -18f);
            keyringEmptyLabel.gameObject.SetActive(keyringModels.Count == 0);

            BuildKeyringTiles();

            keyringStatus = CreateText("KeyringStatus", keyringPanel, "키링 모델을 선택하세요.", 14);
            keyringStatus.alignment = TMPro.TextAlignmentOptions.Midline;
            keyringStatus.rectTransform.anchorMin = new Vector2(0f, 0f);
            keyringStatus.rectTransform.anchorMax = new Vector2(1f, 0f);
            keyringStatus.rectTransform.pivot = new Vector2(0.5f, 0f);
            keyringStatus.rectTransform.anchoredPosition = new Vector2(0f, 112f);
            keyringStatus.rectTransform.sizeDelta = new Vector2(-28f, 24f);

            keyringCountLabel = CreateText("KeyringCount", keyringPanel, "장착된 키링: 0", 14);
            keyringCountLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            keyringCountLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            keyringCountLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            keyringCountLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            keyringCountLabel.rectTransform.anchoredPosition = new Vector2(0f, 82f);
            keyringCountLabel.rectTransform.sizeDelta = new Vector2(-28f, 24f);

            keyringRemoveButton = CreateButton("RemoveKeyring", keyringPanel,
                "키링 제거", new Vector2(180f, 42f),
                Vector2.zero, Vector2.zero, new Vector2(14f, 12f),
                new Color(0.48f, 0.22f, 0.22f, 1f));
            keyringRemoveButton.onClick.AddListener(() =>
            {
                builder?.RemoveKeyring();
                RefreshKeyringPanel();
            });

            RegisterCustomPanel("키링", keyringPanel);
        }

        private Scrollbar BuildKeyringScrollbar()
        {
            RectTransform track = CreateRect("KeyringScrollbar", keyringPanel);
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(1f, 0.5f);
            track.anchoredPosition = new Vector2(-6f,
                (KeyringViewportBottomInset - KeyringViewportTopInset) * 0.5f);
            track.sizeDelta = new Vector2(12f,
                -(KeyringViewportBottomInset + KeyringViewportTopInset));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.12f, 0.15f, 0.17f, 1f);

            RectTransform handle = CreateRect("Handle", track);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = new Vector2(2f, 2f);
            handle.offsetMax = new Vector2(-2f, -2f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.55f, 0.62f, 0.66f, 1f);

            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private Scrollbar BuildStickerScrollbar()
        {
            RectTransform track = CreateRect("StickerScrollbar", stickerPanel);
            track.anchorMin = new Vector2(1f, 0f);
            track.anchorMax = new Vector2(1f, 1f);
            track.pivot = new Vector2(1f, 0.5f);
            track.anchoredPosition = new Vector2(-6f,
                (StickerViewportBottomInset - StickerViewportTopInset) * 0.5f);
            track.sizeDelta = new Vector2(12f,
                -(StickerViewportBottomInset + StickerViewportTopInset));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.12f, 0.15f, 0.17f, 1f);

            RectTransform handle = CreateRect("Handle", track);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = new Vector2(2f, 2f);
            handle.offsetMax = new Vector2(-2f, -2f);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.55f, 0.62f, 0.66f, 1f);

            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private void BuildCustomWorkspace()
        {
            customNavigation = CreateRect("CustomNavigation", transform);
            customNavigation.pivot = new Vector2(0.5f, 0.5f);
            customNavigation.sizeDelta = new Vector2(CustomNavigationWidth, CustomPanelHeight);
            customNavigation.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.065f, 0.075f, 0.97f);

            TMPro.TextMeshProUGUI title = CreateText("CustomNavigationTitle", customNavigation, "커스텀", 20);
            UIFonts.ApplyWeight(title, 700);
            title.alignment = TMPro.TextAlignmentOptions.Midline;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            title.rectTransform.sizeDelta = new Vector2(-16f, 36f);

            RectTransform viewport = CreateRect("CustomNavigationViewport", customNavigation);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8f, 66f);
            viewport.offsetMax = new Vector2(-8f, -54f);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImage.raycastTarget = false;
            viewport.gameObject.AddComponent<RectMask2D>();

            customNavigationContent = CreateRect("CustomNavigationItems", viewport);
            customNavigationContent.anchorMin = new Vector2(0f, 1f);
            customNavigationContent.anchorMax = Vector2.one;
            customNavigationContent.pivot = new Vector2(0.5f, 1f);
            customNavigationContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = customNavigationContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            customNavigationContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect menuScroll = customNavigation.gameObject.AddComponent<ScrollRect>();
            menuScroll.viewport = viewport;
            menuScroll.content = customNavigationContent;
            menuScroll.horizontal = false;
            menuScroll.vertical = true;
            menuScroll.movementType = ScrollRect.MovementType.Clamped;
            menuScroll.scrollSensitivity = 28f;

            customRemoveButton = CreateButton("RemoveCustom", customNavigation, "커스텀 제거",
                new Vector2(CustomNavigationWidth - 16f, 42f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                new Color(0.48f, 0.22f, 0.22f, 1f));
            customRemoveButton.onClick.AddListener(OpenCustomRemovalConfirmation);
            customRemoveButton.gameObject.SetActive(false);

            customContentArea = CreateRect("CustomContentArea", transform);
            customContentArea.pivot = new Vector2(0.5f, 0.5f);
            customContentArea.sizeDelta = new Vector2(CustomPanelWidth, CustomPanelHeight);
            Image contentBackground = customContentArea.gameObject.AddComponent<Image>();
            contentBackground.color = new Color(0.045f, 0.055f, 0.065f, 0.94f);

            customContentPrompt = CreateText("CustomContentPrompt", customContentArea,
                "왼쪽 메뉴에서 기능을 선택하세요.", 20);
            customContentPrompt.alignment = TMPro.TextAlignmentOptions.Midline;
            customContentPrompt.rectTransform.anchorMin = Vector2.zero;
            customContentPrompt.rectTransform.anchorMax = Vector2.one;
            customContentPrompt.rectTransform.offsetMin = new Vector2(24f, 24f);
            customContentPrompt.rectTransform.offsetMax = new Vector2(-24f, -24f);

            foreach (CustomPanelEntry entry in customPanels)
                if (entry.Button == null) CreateCustomPanelEntry(entry);
        }

        /// <summary>Registers an additional panel in the custom menu.</summary>
        public bool RegisterCustomPanel(string label, RectTransform panel)
        {
            if (string.IsNullOrWhiteSpace(label) || panel == null) return false;

            foreach (CustomPanelEntry existing in customPanels)
                if (existing.Panel == panel) return true;

            CustomPanelEntry entry = new CustomPanelEntry
            {
                Label = label.Trim(),
                Panel = panel
            };
            customPanels.Add(entry);
            if (customNavigationContent != null && customContentArea != null)
                CreateCustomPanelEntry(entry);
            RefreshCustomNavigation();
            return true;
        }

        private void CreateCustomPanelEntry(CustomPanelEntry entry)
        {
            if (entry == null || entry.Panel == null || entry.Button != null) return;
            entry.Panel.SetParent(customContentArea, false);
            StretchPanel(entry.Panel);
            entry.Panel.gameObject.SetActive(false);
            entry.Button = CreateButton("CustomMenuItem_" + customPanels.IndexOf(entry), customNavigationContent,
                entry.Label, new Vector2(CustomNavigationWidth - 20f, 46f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Color(0.16f, 0.19f, 0.22f, 1f));
            LayoutElement layoutElement = entry.Button.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = layoutElement.preferredHeight = 46f;
            entry.Button.transition = Selectable.Transition.None;
            entry.Button.onClick.AddListener(() => SelectCustomPanel(entry));
        }

        private void SelectCustomPanel(CustomPanelEntry entry)
        {
            if (entry == null || builder == null || !builder.IsCustomMode) return;
            if (selectedCustomPanel != entry) builder.CancelKeyringPlacement();
            selectedCustomPanel = entry;
            if (entry.Panel == keyringPanel)
            {
                ResolveKeyringModels();
                RebuildKeyringTiles();
            }
            RefreshCustomNavigation();
            UpdateCustomPanelVisibility(true);
            if (entry.Panel == keyringPanel) RefreshKeyringPanel();
        }

        private void BuildKeyringTiles()
        {
            if (keyringGrid == null) return;
            for (int i = keyringGrid.childCount - 1; i >= 0; i--)
            {
                GameObject child = keyringGrid.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            keyringModelButtons.Clear();
            keyringModelHighlights.Clear();
            for (int i = 0; i < keyringModels.Count; i++)
                CreateKeyringTile(keyringGrid, keyringModels[i]);
            if (keyringEmptyLabel != null)
            {
                keyringEmptyLabel.text = GetKeyringEmptyMessage();
                keyringEmptyLabel.gameObject.SetActive(keyringModels.Count == 0);
            }
        }

        private void RefreshCustomNavigation()
        {
            for (int i = 0; i < customPanels.Count; i++)
            {
                CustomPanelEntry entry = customPanels[i];
                if (entry.Button != null && entry.Button.targetGraphic != null)
                    entry.Button.targetGraphic.color = entry == selectedCustomPanel
                        ? new Color(0.34f, 0.45f, 0.5f, 1f)
                        : new Color(0.16f, 0.19f, 0.22f, 1f);
            }
        }

        private void UpdateCustomPanelVisibility(bool customMode)
        {
            if (customNavigation != null && customNavigation.gameObject.activeSelf != customMode)
                customNavigation.gameObject.SetActive(customMode);
            if (customRemoveButton != null && customRemoveButton.gameObject.activeSelf != customMode)
                customRemoveButton.gameObject.SetActive(customMode);
            if (customContentArea != null && customContentArea.gameObject.activeSelf != customMode)
                customContentArea.gameObject.SetActive(customMode);
            if (customContentPrompt != null)
                customContentPrompt.gameObject.SetActive(customMode && selectedCustomPanel == null);

            foreach (CustomPanelEntry entry in customPanels)
                if (entry.Panel != null)
                {
                    bool showPanel = customMode && entry == selectedCustomPanel;
                    if (entry.Panel.gameObject.activeSelf != showPanel)
                        entry.Panel.gameObject.SetActive(showPanel);
                }
        }

        private void CreateStickerTile(RectTransform parent, Texture2D texture)
        {
            RectTransform tile = CreateRect("Sticker_" + texture.name, parent);
            tile.sizeDelta = new Vector2(92f, 112f);
            Image background = tile.gameObject.AddComponent<Image>();
            background.color = new Color(0.13f, 0.15f, 0.17f, 1f);
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            Outline outline = tile.gameObject.AddComponent<Outline>();
            outline.enabled = false;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.effectColor = new Color(0.55f, 0.82f, 0.48f, 1f);
            stickerButtons.Add(button);
            stickerHighlights.Add(outline);

            RawImage preview = CreateRect("Preview", tile).gameObject.AddComponent<RawImage>();
            preview.texture = texture;
            preview.raycastTarget = false;
            preview.rectTransform.anchorMin = preview.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            preview.rectTransform.pivot = new Vector2(0.5f, 1f);
            preview.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            float width = 74f;
            float height = 72f;
            float aspect = (float)texture.width / Mathf.Max(1, texture.height);
            if (aspect >= 1f) height = width / aspect;
            else width = height * aspect;
            preview.rectTransform.sizeDelta = new Vector2(width, height);

            TMPro.TextMeshProUGUI label = CreateText("Name", tile, texture.name, 11);
            label.alignment = TMPro.TextAlignmentOptions.Midline;
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.overflowMode = TMPro.TextOverflowModes.Truncate;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 3f);
            label.rectTransform.sizeDelta = new Vector2(-6f, 26f);

            button.onClick.AddListener(() => SelectStickerTexture(texture));
        }

        private void CreateKeyringTile(RectTransform parent, GameObject model)
        {
            RectTransform tile = CreateRect("Keyring_" + model.name, parent);
            tile.sizeDelta = new Vector2(156f, 64f);
            Image background = tile.gameObject.AddComponent<Image>();
            background.color = new Color(0.13f, 0.15f, 0.17f, 1f);
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            Outline outline = tile.gameObject.AddComponent<Outline>();
            outline.enabled = false;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.effectColor = new Color(0.55f, 0.82f, 0.48f, 1f);
            keyringModelButtons.Add(button);
            keyringModelHighlights.Add(outline);

            TMPro.TextMeshProUGUI label = CreateText("Name", tile, model.name, 14);
            label.alignment = TMPro.TextAlignmentOptions.Midline;
            UIFonts.ApplyWeight(label, 700);
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.overflowMode = TMPro.TextOverflowModes.Truncate;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(6f, 4f);
            label.rectTransform.offsetMax = new Vector2(-6f, -4f);

            button.onClick.AddListener(() => SelectKeyringModel(model));
        }

        private void SelectKeyringModel(GameObject model)
        {
            if (builder == null || model == null || !builder.SelectKeyringForPlacement(model)) return;
            RefreshKeyringPanel();
        }

        private void BuildCustomColorPicker(RectTransform parent)
        {
            customColorPicker = CreateRect("CustomColorPicker", parent);
            customColorPicker.anchorMin = Vector2.zero;
            customColorPicker.anchorMax = Vector2.one;
            customColorPicker.offsetMin = new Vector2(0f, 56f);
            customColorPicker.offsetMax = new Vector2(0f, -56f);
            customColorPicker.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.09f, 0.1f, 0.96f);

            TMPro.TextMeshProUGUI pickerHint = CreateText("PickerHint", customColorPicker,
                "나만의 색상 설정  ·  색상표를 클릭/드래그하거나 RGB 값을 입력하세요", 17);
            pickerHint.alignment = TMPro.TextAlignmentOptions.Midline;
            pickerHint.rectTransform.anchorMin = new Vector2(0f, 1f);
            pickerHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            pickerHint.rectTransform.pivot = new Vector2(0.5f, 1f);
            pickerHint.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            pickerHint.rectTransform.sizeDelta = new Vector2(-32f, 42f);

            colorSpectrum = CreateRect("SaturationValue", customColorPicker).gameObject.AddComponent<RawImage>();
            colorSpectrum.rectTransform.anchorMin = colorSpectrum.rectTransform.anchorMax = new Vector2(0f, 1f);
            colorSpectrum.rectTransform.pivot = new Vector2(0f, 1f);
            colorSpectrum.rectTransform.anchoredPosition = new Vector2(20f, -74f);
            colorSpectrum.rectTransform.sizeDelta = new Vector2(340f, 340f);
            colorSpectrum.raycastTarget = true;
            AddPointerHandler(colorSpectrum, EventTriggerType.PointerDown, SelectSpectrumColor);
            AddPointerHandler(colorSpectrum, EventTriggerType.Drag, SelectSpectrumColor);
            AddPointerHandler(colorSpectrum, EventTriggerType.PointerUp, _ => ApplyCustomColorToBrush());

            hueSpectrum = CreateRect("HueSpectrum", customColorPicker).gameObject.AddComponent<RawImage>();
            hueSpectrum.rectTransform.anchorMin = hueSpectrum.rectTransform.anchorMax = new Vector2(0f, 1f);
            hueSpectrum.rectTransform.pivot = new Vector2(0f, 1f);
            hueSpectrum.rectTransform.anchoredPosition = new Vector2(366f, -74f);
            hueSpectrum.rectTransform.sizeDelta = new Vector2(34f, 340f);
            hueSpectrum.raycastTarget = true;
            AddPointerHandler(hueSpectrum, EventTriggerType.PointerDown, SelectHue);
            AddPointerHandler(hueSpectrum, EventTriggerType.Drag, SelectHue);
            AddPointerHandler(hueSpectrum, EventTriggerType.PointerUp, _ => ApplyCustomColorToBrush());

            colorSpectrumTexture = CreateColorSpectrumTexture(customHue);
            hueSpectrumTexture = CreateHueSpectrumTexture();
            colorSpectrum.texture = colorSpectrumTexture;
            hueSpectrum.texture = hueSpectrumTexture;

            customColorPreview = CreateRect("CustomColorPreview", customColorPicker).gameObject.AddComponent<RawImage>();
            customColorPreview.rectTransform.anchorMin = customColorPreview.rectTransform.anchorMax = new Vector2(1f, 1f);
            customColorPreview.rectTransform.pivot = new Vector2(1f, 1f);
            customColorPreview.rectTransform.anchoredPosition = new Vector2(-26f, -76f);
            customColorPreview.rectTransform.sizeDelta = new Vector2(118f, 118f);
            customColorPreview.raycastTarget = false;

            customColorHex = CreateText("CustomColorHex", customColorPicker, "", 17);
            customColorHex.alignment = TMPro.TextAlignmentOptions.Midline;
            customColorHex.rectTransform.anchorMin = customColorHex.rectTransform.anchorMax = new Vector2(1f, 1f);
            customColorHex.rectTransform.pivot = new Vector2(1f, 1f);
            customColorHex.rectTransform.anchoredPosition = new Vector2(-16f, -220f);
            customColorHex.rectTransform.sizeDelta = new Vector2(138f, 34f);

            CreateRgbInput(customColorPicker, 0, "R", 100f);
            CreateRgbInput(customColorPicker, 1, "G", 200f);
            CreateRgbInput(customColorPicker, 2, "B", 300f);
            RefreshRgbInputs();

            applyCustomColorButton = CreateButton("ApplyCustomColor", customColorPicker, "이 색으로 선택",
                new Vector2(150f, 42f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-18f, 210f), new Color(0.25f, 0.42f, 0.32f, 1f));
            applyCustomColorButton.onClick.AddListener(SelectCustomColor);
            Button backToMaterialButton = CreateButton("BackToMaterials", customColorPicker, "재질 목록",
                new Vector2(150f, 42f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-18f, 160f), new Color(0.18f, 0.22f, 0.26f, 1f));
            backToMaterialsButton = backToMaterialButton;
            backToMaterialButton.onClick.AddListener(ReturnToMaterialSelection);
        }

        private void OpenCustomConfirmation()
        {
            if (builder == null || !builder.CanStartCustom) return;
            confirmingCustomRemoval = false;
            if (FeedbackUI.For(this)?.ShowChoice(this, "커스텀 제거 전까진 총기 수정이 불가합니다.\n그래도 하시겠습니까?", ConfirmCustom, CancelCustomConfirmation) != true) CancelCustomConfirmation();
        }

        private void OpenCustomRemovalConfirmation()
        {
            if (builder == null || !builder.IsCustomMode) return;
            confirmingCustomRemoval = true;
            if (FeedbackUI.For(this)?.ShowChoice(this, "커스텀을 제거하시겠습니까?\n제거하면 총기를 다시 수정할 수 있습니다", ConfirmCustom, CancelCustomConfirmation) != true) CancelCustomConfirmation();
        }

        private void ConfirmCustom()
        {
            bool removeCustom = confirmingCustomRemoval;
            confirmingCustomRemoval = false;
            if (removeCustom)
            {
                ConfirmCustomRemoval();
                return;
            }
            if (builder != null && builder.BeginCustomMode())
            {
                Camera listenerCamera = Camera.main;
                WorkshopSoundEffects.RaiseAssemblyCompleted(listenerCamera != null
                    ? listenerCamera.transform.position : transform.position);
                selectedPaintMaterial = -1;
                customColorSelected = false;
                customColorMode = false;
                paintMaterialPage = 0;
                selectedSticker = null;
                selectedCustomPanel = null;
                RefreshCustomNavigation();
                RefreshPaintPanel();
                RefreshStickerPanel();
                UpdateCustomPanelVisibility(true);
            }
        }

        private void ConfirmCustomRemoval()
        {
            if (builder == null || !builder.RemoveCustomAndResumeEditing()) return;
            customColorSelected = false;
            customColorMode = false;
            selectedPaintMaterial = -1;
            selectedSticker = null;
            RefreshPaintPanel();
            RefreshStickerPanel();
        }

        private void CancelCustomConfirmation()
        {
            confirmingCustomRemoval = false;
        }

        private void SelectPaintPaletteSlot(int slot)
        {
            int index = paintMaterialPage * PaintPalettePageSize + slot;
            SelectPaintMaterial(index);
        }

        private void ChangePaintMaterialPage(int delta)
        {
            int pageCount = Mathf.Max(1, (activePaintMaterials.Count + PaintPalettePageSize - 1) /
                                         PaintPalettePageSize);
            paintMaterialPage = Mathf.Clamp(paintMaterialPage + delta, 0, pageCount - 1);
            RefreshPaintPanel();
        }

        private void SelectPaintMaterial(int index)
        {
            if (builder == null || !builder.IsPaintingMode || index < 0 || index >= activePaintMaterials.Count) return;
            PaintMaterialOption option = activePaintMaterials[index];
            if (option == null || option.material == null) return;
            selectedPaintMaterial = index;
            customColorSelected = false;
            customColorMode = false;
            builder.SelectPaintMaterial(option.material);
            RefreshPaintPanel();
            RefreshStickerPanel();
        }

        private void SelectStickerTexture(Texture2D texture)
        {
            if (builder == null || texture == null || !builder.SelectSticker(texture)) return;
            selectedSticker = texture;
            RefreshStickerPanel();
            if (builder.IsPaintingMode) RefreshPaintPanel();
        }

        private void OpenCustomColorPicker()
        {
            if (builder == null || !builder.IsPaintingMode) return;
            customColorMode = true;
            RefreshPaintPanel();
        }

        private void SelectCustomColor()
        {
            if (builder == null || !builder.IsPaintingMode) return;
            selectedPaintMaterial = -1;
            customColorSelected = true;
            ApplyCustomColorToBrush();
            RefreshPaintPanel();
        }

        private void ReturnToMaterialSelection()
        {
            customColorMode = false;
            RefreshPaintPanel();
        }

        private void ApplyCustomColorToBrush()
        {
            if (!customColorSelected || builder == null || !builder.IsPaintingMode || generatedPaintMaterials == null) return;
            Material material = generatedPaintMaterials.GetCustomColorMaterial((Color)customColor);
            if (material != null) builder.SelectPaintMaterial(material);
        }

        private void CreateRgbInput(RectTransform parent, int channel, string label, float xPosition)
        {
            TMPro.TextMeshProUGUI channelLabel = CreateText(label, parent, label, 13);
            channelLabel.alignment = TMPro.TextAlignmentOptions.Midline;
            channelLabel.rectTransform.anchorMin = channelLabel.rectTransform.anchorMax = new Vector2(0f, 0f);
            channelLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            channelLabel.rectTransform.anchoredPosition = new Vector2(xPosition, 41f);
            channelLabel.rectTransform.sizeDelta = new Vector2(78f, 18f);

            RectTransform inputRect = CreateRect(label + "Value", parent);
            inputRect.anchorMin = inputRect.anchorMax = new Vector2(0f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.anchoredPosition = new Vector2(xPosition, 12f);
            inputRect.sizeDelta = new Vector2(78f, 30f);
            Image background = inputRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.16f, 0.18f, 0.2f, 1f);
            TMPro.TMP_InputField input = inputRect.gameObject.AddComponent<TMPro.TMP_InputField>();
            input.targetGraphic = background;
            input.contentType = TMPro.TMP_InputField.ContentType.IntegerNumber;
            input.characterValidation = TMPro.TMP_InputField.CharacterValidation.Integer;
            input.lineType = TMPro.TMP_InputField.LineType.SingleLine;
            input.selectionColor = new Color(0.3f, 0.48f, 0.68f, 0.8f);

            TMPro.TextMeshProUGUI value = CreateText("Text", inputRect, "0", 14);
            value.alignment = TMPro.TextAlignmentOptions.Midline;
            value.rectTransform.anchorMin = Vector2.zero;
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.offsetMin = new Vector2(3f, 1f);
            value.rectTransform.offsetMax = new Vector2(-3f, -1f);
            input.textComponent = value;
            input.textViewport = inputRect;
            input.onEndEdit.AddListener(text => SetRgbChannel(channel, text));
            rgbInputs[channel] = input;
        }

        private void SetRgbChannel(int channel, string value)
        {
            if (syncingRgbInputs || !int.TryParse(value, out int channelValue))
            {
                RefreshRgbInputs();
                return;
            }

            Color32 next = customColor;
            byte component = (byte)Mathf.Clamp(channelValue, 0, 255);
            if (channel == 0) next.r = component;
            else if (channel == 1) next.g = component;
            else next.b = component;
            SetCustomColor(next, true);
            ApplyCustomColorToBrush();
        }

        private void SelectSpectrumColor(PointerEventData eventData)
        {
            RectTransform rect = colorSpectrum.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;
            Rect bounds = rect.rect;
            float saturation = Mathf.Clamp01((local.x - bounds.xMin) / bounds.width);
            float value = Mathf.Clamp01((local.y - bounds.yMin) / bounds.height);
            SetCustomColor(Color.HSVToRGB(customHue, saturation, value), false);
        }

        private void SelectHue(PointerEventData eventData)
        {
            RectTransform rect = hueSpectrum.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;
            Rect bounds = rect.rect;
            float vertical = Mathf.Clamp01((local.y - bounds.yMin) / bounds.height);
            customHue = 1f - vertical;
            UpdateColorSpectrumTexture();
            Color.RGBToHSV((Color)customColor, out _, out float saturation, out float value);
            SetCustomColor(Color.HSVToRGB(customHue, saturation, value), false);
        }

        private void SetCustomColor(Color color, bool updateHue)
        {
            customColor = color;
            customColor.a = 255;
            if (updateHue)
            {
                Color.RGBToHSV((Color)customColor, out customHue, out _, out _);
                UpdateColorSpectrumTexture();
            }
            RefreshRgbInputs();
            RefreshPaintPanel();
        }

        private void RefreshRgbInputs()
        {
            if (rgbInputs[0] == null) return;
            syncingRgbInputs = true;
            rgbInputs[0].text = customColor.r.ToString();
            rgbInputs[1].text = customColor.g.ToString();
            rgbInputs[2].text = customColor.b.ToString();
            syncingRgbInputs = false;
        }

        private Texture2D CreateColorSpectrumTexture(float hue)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Paint Color Spectrum",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            generatedPaintUiTextures.Add(texture);
            UpdateColorSpectrumTexture(texture, hue);
            return texture;
        }

        private void UpdateColorSpectrumTexture() => UpdateColorSpectrumTexture(colorSpectrumTexture, customHue);

        private static void UpdateColorSpectrumTexture(Texture2D texture, float hue)
        {
            if (texture == null) return;
            int width = texture.width;
            int height = texture.height;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = Color.HSVToRGB(hue,
                        (float)x / (width - 1), (float)y / (height - 1));
            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }

        private Texture2D CreateHueSpectrumTexture()
        {
            const int height = 128;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                name = "Paint Hue Spectrum",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            generatedPaintUiTextures.Add(texture);
            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
                pixels[y] = Color.HSVToRGB(1f - (float)y / (height - 1), 1f, 1f);
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void AddPointerHandler(RawImage image, EventTriggerType eventType,
            System.Action<PointerEventData> handler)
        {
            EventTrigger trigger = image.GetComponent<EventTrigger>();
            if (trigger == null) trigger = image.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(data => handler((PointerEventData)data));
            trigger.triggers.Add(entry);
        }

        private void RefreshPaintMaterialPage()
        {
            int itemCount = activePaintMaterials.Count;
            int pageCount = Mathf.Max(1, (itemCount + PaintPalettePageSize - 1) / PaintPalettePageSize);
            paintMaterialPage = Mathf.Clamp(paintMaterialPage, 0, pageCount - 1);
            int firstItem = paintMaterialPage * PaintPalettePageSize;

            for (int slot = 0; slot < paintMaterialButtons.Count; slot++)
            {
                int index = firstItem + slot;
                bool visible = index < itemCount;
                Button button = paintMaterialButtons[slot];
                if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
                if (!visible) continue;

                RawImage swatch = paintMaterialSwatches[slot];
                TMPro.TextMeshProUGUI label = paintMaterialLabels[slot];
                PaintMaterialOption option = activePaintMaterials[index];
                Texture texture = GetPreviewTexture(option);
                swatch.texture = texture;
                swatch.color = texture != null
                    ? Color.white
                    : option != null && option.material != null ? option.material.color : Color.white;
                label.text = option == null
                    ? ""
                    : (string.IsNullOrEmpty(option.category)
                        ? option.displayName
                        : option.category + "\n" + option.displayName) +
                      (string.IsNullOrEmpty(option.hexColor) ? "" : "\n#" + option.hexColor);
                paintMaterialHighlights[slot].enabled = index == selectedPaintMaterial;
            }

            if (paintPageLabel != null) paintPageLabel.text = (paintMaterialPage + 1) + " / " + pageCount;
            if (previousPaintPageButton != null) previousPaintPageButton.interactable = paintMaterialPage > 0;
            if (nextPaintPageButton != null) nextPaintPageButton.interactable = paintMaterialPage < pageCount - 1;
        }

        private void RefreshPaintPanel()
        {
            if (paintTitle != null)
                paintTitle.text = customColorMode ? "도색 - 나만의 색상" : "도색 - 준비된 재질";
            if (materialSelectionView != null &&
                materialSelectionView.gameObject.activeSelf == customColorMode)
                materialSelectionView.gameObject.SetActive(!customColorMode);
            if (customColorPicker != null && customColorPicker.gameObject.activeSelf != customColorMode)
                customColorPicker.gameObject.SetActive(customColorMode);
            RefreshPaintMaterialPage();

            PaintMaterialOption selected = selectedPaintMaterial >= 0 && selectedPaintMaterial < activePaintMaterials.Count
                ? activePaintMaterials[selectedPaintMaterial] : null;
            if (paintPreview != null)
            {
                paintPreview.texture = selected != null ? GetPreviewTexture(selected) : null;
                paintPreview.color = customColorSelected ? (Color)customColor : Color.white;
            }
            if (paintName != null)
            {
                if (customColorSelected)
                {
                    paintName.text = $"나만의 색상  #{customColor.r:X2}{customColor.g:X2}{customColor.b:X2}";
                }
                else
                {
                    paintName.text = selected != null ? selected.displayName : "재질을 선택하세요";
                }
            }
            if (customColorPreview != null) customColorPreview.color = customColor;
            if (customColorHex != null)
                customColorHex.text = "#" + customColor.r.ToString("X2") +
                                      customColor.g.ToString("X2") +
                                      customColor.b.ToString("X2");
            if (customColorModeButton != null && customColorModeButton.targetGraphic != null)
                customColorModeButton.targetGraphic.color = customColorSelected
                    ? new Color(0.34f, 0.43f, 0.36f, 1f)
                    : new Color(0.23f, 0.29f, 0.36f, 1f);
            if (applyCustomColorButton != null)
            {
                TMPro.TextMeshProUGUI buttonLabel = applyCustomColorButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (buttonLabel != null)
                    buttonLabel.text = customColorSelected ? "선택한 색 다시 적용" : "이 색으로 선택";
            }
            if (paintStatus != null && builder != null)
                paintStatus.text = builder.PaintSelectionLabel + "\n현재 선택: " +
                                   (builder.IsKeyringToolActive
                                       ? (builder.SelectedKeyringModel != null
                                           ? builder.SelectedKeyringModel.name
                                           : "키링 모델")
                                       :
                                       builder.IsStickerToolActive ? builder.StickerBrushLabel : builder.PaintBrushLabel) +
                                   "  |  도구: " + (builder.IsKeyringToolActive ? "키링" :
                                       builder.IsStickerToolActive ? "스티커" : builder.IsPaintFillMode ? "물감통" : "붓");
            if (paintFillButton != null && paintFillButton.targetGraphic != null)
                paintFillButton.targetGraphic.color = builder != null && builder.IsPaintFillMode
                    ? new Color(0.32f, 0.45f, 0.3f, 1f)
                    : new Color(0.22f, 0.28f, 0.33f, 1f);
            if (paintAllButton != null)
                paintAllButton.interactable = builder != null && builder.IsPaintingMode &&
                                              !GameManager.ModalBlocked;
        }

        private void RefreshStickerPanel()
        {
            if (stickerEmptyLabel != null)
                stickerEmptyLabel.gameObject.SetActive(stickerTextures.Count == 0);
            for (int i = 0; i < stickerButtons.Count; i++)
                stickerHighlights[i].enabled = i < stickerTextures.Count && stickerTextures[i] == selectedSticker;

            if (stickerStatus != null)
            {
                if (builder != null && builder.IsStickerEraseMode)
                    stickerStatus.text = "제거할 스티커를 클릭하세요 · Ctrl+Z로 복구";
                else if (selectedSticker == null)
                    stickerStatus.text = stickerTextures.Count == 0
                        ? "PNG 스티커를 선택할 수 없습니다"
                        : "PNG 스티커를 선택하세요";
                else
                    stickerStatus.text = builder != null && builder.IsStickerToolActive
                        ? selectedSticker.name + " 선택됨 · 면을 클릭해 붙이기"
                        : selectedSticker.name + " 선택됨 · 스티커를 선택해 붙이기";
            }
            if (stickerUndoButton != null)
                stickerUndoButton.interactable = builder != null && builder.IsCustomMode;
            if (stickerRemoveButton != null)
            {
                stickerRemoveButton.interactable = builder != null && builder.IsCustomMode;
                if (stickerRemoveButton.targetGraphic != null)
                    stickerRemoveButton.targetGraphic.color = builder != null && builder.IsStickerEraseMode
                        ? new Color(0.62f, 0.28f, 0.25f, 1f)
                        : new Color(0.3f, 0.25f, 0.25f, 1f);
            }
            bool hasLastSticker = builder != null && builder.HasLastPlacedSticker;
            bool canAdjustSticker = builder != null && builder.IsCustomMode && hasLastSticker;
            bool isLastStickerFlipped = hasLastSticker && builder.IsStickerFlippedHorizontally;
            if (stickerMirrorButton != null)
            {
                stickerMirrorButton.interactable = canAdjustSticker;
                if (stickerMirrorButtonLabel != null)
                    stickerMirrorButtonLabel.text = hasLastSticker
                        ? isLastStickerFlipped ? "좌우 반전: 켬" : "좌우 반전: 끔"
                        : "스티커 없음";
                if (stickerMirrorButton.targetGraphic != null)
                    stickerMirrorButton.targetGraphic.color = isLastStickerFlipped
                        ? new Color(0.3f, 0.42f, 0.32f, 1f)
                        : new Color(0.18f, 0.22f, 0.25f, 1f);
            }
            if (stickerSizeLabel != null)
            {
                float sizeMultiplier = builder != null
                    ? builder.StickerSizeMultiplier
                    : StickerOverlay.DefaultSizeMultiplier;
                stickerSizeLabel.text = "×" + sizeMultiplier.ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            if (stickerSizeDownButton != null)
                stickerSizeDownButton.interactable = canAdjustSticker && builder.CanDecreaseStickerSize;
            if (stickerSizeUpButton != null)
                stickerSizeUpButton.interactable = canAdjustSticker && builder.CanIncreaseStickerSize;
        }

        private void RefreshKeyringPanel()
        {
            bool customMode = builder != null && builder.IsCustomMode;
            bool placing = customMode && builder.IsKeyringToolActive;
            GameObject selectedModel = builder != null ? builder.SelectedKeyringModel : null;
            if (keyringEmptyLabel != null)
                keyringEmptyLabel.gameObject.SetActive(keyringModels.Count == 0);
            for (int i = 0; i < keyringModelButtons.Count; i++)
            {
                bool selected = i < keyringModels.Count && keyringModels[i] == selectedModel;
                Button button = keyringModelButtons[i];
                button.interactable = customMode;
                if (button.targetGraphic != null)
                    button.targetGraphic.color = selected && placing
                        ? new Color(0.34f, 0.45f, 0.5f, 1f)
                        : new Color(0.13f, 0.15f, 0.17f, 1f);
                keyringModelHighlights[i].enabled = selected;
            }
            if (keyringStatus != null)
                keyringStatus.text = !customMode
                    ? "커스텀 모드에서 사용할 수 있습니다."
                    : keyringModels.Count == 0
                        ? GetKeyringEmptyMessage()
                        : placing && selectedModel != null
                            ? selectedModel.name + " 선택됨 · 총의 겉면을 클릭해 줄을 연결하세요."
                            : "키링 모델을 선택한 뒤 총의 겉면을 클릭하세요.";
            if (keyringCountLabel != null)
                keyringCountLabel.text = "장착된 키링: " + (builder != null ? builder.KeyringCount : 0);
            if (keyringRemoveButton != null)
                keyringRemoveButton.interactable = customMode && builder.KeyringCount > 0;
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

        private ModuleSlotUI CreateSlot(RectTransform parent, int favoriteIndex)
        {
            RectTransform rect = CreateRect(favoriteIndex >= 0 ? $"Favorite{favoriteIndex + 1}" : "Module", parent);
            rect.sizeDelta = slotSize;
            var background = rect.gameObject.AddComponent<Image>();
            background.color = SlotColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ApplyButtonColors(button);
            Image swatch = CreateSwatch(rect);
            TMPro.TextMeshProUGUI label = CreateText("Label", rect, "", 16);
            label.enableAutoSizing = true;
            label.fontSize = favoriteIndex >= 0 ? 24f : 20f;
            label.fontSizeMin = favoriteIndex >= 0 ? 20f : 16f;
            label.fontSizeMax = favoriteIndex >= 0 ? 24f : 20f;
            Stretch(label.rectTransform, 18f);
            label.rectTransform.offsetMin = new Vector2(18f, 28f);
            var slot = rect.gameObject.AddComponent<ModuleSlotUI>();
            slot.Setup(this, favoriteIndex, background, swatch, label);
            return slot;
        }

        private static Image CreateSwatch(RectTransform parent)
        {
            RectTransform rect = CreateRect("Color", parent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(12f, 0f);
            rect.anchoredPosition = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
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

        private static void Stretch(RectTransform rect, float leftInset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(leftInset, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
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
