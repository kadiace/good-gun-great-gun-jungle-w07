using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// 모듈 팔레트의 저작된 참조(퀵슬롯·목록·드래그 고스트)와 표시·레이아웃만 담당하는 View입니다.
    /// 선택·즐겨찾기·드래그 판정은 ModulePalettePresenter가 소유하며, 이 클래스는 도메인 상태를 갖지 않습니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class ModulePaletteView : MonoBehaviour
    {
        public const string HotbarGuideText =
            "퀵슬롯을 다른 번호 칸으로 드래그하면 서로 바뀝니다 · Tab: 모듈 목록";
        public const string PaletteTitleText =
            "모듈 · 클릭: 선택 · 마우스 올리고 1~9·0: 등록\n목록을 하단으로 드래그: 등록 · 퀵슬롯끼리 드래그: 교체";

        [Serializable]
        public struct LayoutConfig
        {
            public Vector2 slotSize;
            public Vector2 hotbarAnchorMin;
            public Vector2 hotbarAnchorMax;
            public Vector2 hotbarPaddingRatio;
            public Vector2 hotbarTextPaddingRatio;
            public float hotbarSpacingPixels;
            public Vector2 paletteSizeRatio;
            public int paletteColumns;
        }

        [Serializable]
        public sealed class PaletteCategory
        {
            public VerticalLayoutGroup layout;
            public LayoutElement heading;
            public GridLayoutGroup grid;
            public LayoutElement gridSize;
        }

        [Header("Hotbar")]
        [SerializeField] private RectTransform m_hotbar;
        [SerializeField] private HorizontalLayoutGroup m_hotbarLayout;
        [SerializeField] private TMPro.TextMeshProUGUI m_hotbarGuide;
        // 퀵슬롯 위 "모듈 선택창 [TAB]" 버튼(마우스로 목록 열기·닫기).
        [SerializeField] private UnityEngine.UI.Button m_paletteToggleButton;
        [SerializeField] private ModuleSlotUI[] m_hotbarSlots;

        [Header("Palette")]
        [SerializeField] private RectTransform m_palette;
        [SerializeField] private VerticalLayoutGroup m_paletteLayout;
        [SerializeField] private TMPro.TextMeshProUGUI m_paletteTitle;
        [SerializeField] private LayoutElement m_paletteTitleLayout;
        [SerializeField] private PaletteCategory[] m_categories;
        [SerializeField, Tooltip("저작 시 계산한 목록 내용 기준 크기. 배율 계산에만 쓴다.")]
        private Vector2 m_paletteReferenceSize;
        [SerializeField, Tooltip("저작 시 계산한 제목 높이. 배율 계산에만 쓴다.")]
        private float m_paletteReferenceTitleHeight;

        [Header("Module List")]
        [SerializeField] private ModuleSlotUI m_slotTemplate;

        [Header("Drag Ghost")]
        [SerializeField] private RectTransform m_dragGhost;
        [SerializeField] private Image m_dragGhostSwatch;
        [SerializeField] private TMPro.TextMeshProUGUI m_dragGhostLabel;

        private readonly List<ModuleSlotUI> m_paletteSlots = new List<ModuleSlotUI>();
        private readonly List<ModuleSlotUI> m_palettePool = new List<ModuleSlotUI>();
        private readonly Dictionary<int, ModuleSlotUI> m_paletteSlotById = new Dictionary<int, ModuleSlotUI>();
        private List<ModuleSlotUI>[] m_categorySlots;
        private int[] m_categoryCounts;
        private LayoutConfig m_layoutConfig;
        private float m_paletteContentPadding = 5f;
        private bool m_isInitialized;
        private bool m_hasAppliedLayout;
        private bool m_hasLoggedReferenceError;

        public RectTransform Root => (RectTransform)transform;

        public IReadOnlyList<ModuleSlotUI> HotbarSlots => m_hotbarSlots;

        public IReadOnlyList<ModuleSlotUI> PaletteSlots => m_paletteSlots;

        public bool IsInitialized => m_isInitialized;

        public event Action<ModuleSlotUI> SlotHovered;
        public event Action<ModuleSlotUI> SlotUnhovered;
        public event Action<ModuleSlotUI> SlotClicked;
        public event Action<ModuleSlotUI> SlotDisabledEvent;
        public event Action<ModuleSlotUI, PointerEventData> SlotDragBegan;
        public event Action<PointerEventData> SlotDragged;
        public event Action<ModuleSlotUI, PointerEventData> SlotDropped;
        public event Action<PointerEventData> SlotDragEnded;
        public event Action PaletteToggleClicked;

        public void NotifySlotHovered(ModuleSlotUI _slot) => SlotHovered?.Invoke(_slot);

        public void NotifySlotUnhovered(ModuleSlotUI _slot) => SlotUnhovered?.Invoke(_slot);

        public void NotifySlotClicked(ModuleSlotUI _slot) => SlotClicked?.Invoke(_slot);

        public void NotifySlotDisabled(ModuleSlotUI _slot) => SlotDisabledEvent?.Invoke(_slot);

        public void NotifySlotDragBegan(ModuleSlotUI _slot, PointerEventData _eventData) =>
            SlotDragBegan?.Invoke(_slot, _eventData);

        public void NotifySlotDragged(PointerEventData _eventData) => SlotDragged?.Invoke(_eventData);

        public void NotifySlotDropped(ModuleSlotUI _slot, PointerEventData _eventData) =>
            SlotDropped?.Invoke(_slot, _eventData);

        public void NotifySlotDragEnded(PointerEventData _eventData) => SlotDragEnded?.Invoke(_eventData);

        /// <summary>
        /// 저작된 참조를 검증하고 브리지의 레이아웃 설정을 적용한 뒤 모든 모듈 UI를 숨깁니다.
        /// 중복 호출은 초기화된 상태를 유지합니다.
        /// </summary>
        public bool Initialize(LayoutConfig _layout)
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            m_layoutConfig = _layout;
            BuildCategoryState();
            if (m_paletteToggleButton != null)
            {
                m_paletteToggleButton.onClick.AddListener(OnPaletteToggleClicked);
            }

            m_isInitialized = true;
            ApplyLayout();
            ApplyNavigation();
            HideAll();
            return true;
        }

        /// <summary>
        /// 목록 카테고리별 모듈 ID를 받아 저작된 템플릿을 재사용해 슬롯을 배치합니다.
        /// 같은 카테고리 그리드 안에서만 풀을 재사용하며 슬롯을 새로 만들지 않습니다.
        /// </summary>
        public void BuildPaletteSlots(int[][] _moduleIdsByCategory)
        {
            if (!m_isInitialized || _moduleIdsByCategory == null || m_categories == null || m_slotTemplate == null)
            {
                return;
            }

            int total = 0;
            for (int i = 0; i < _moduleIdsByCategory.Length; i++)
            {
                total += _moduleIdsByCategory[i] != null ? _moduleIdsByCategory[i].Length : 0;
            }

            EnsurePool(total);
            for (int i = 0; i < m_paletteSlots.Count; i++)
            {
                if (m_paletteSlots[i] != null)
                {
                    m_paletteSlots[i].gameObject.SetActive(false);
                }
            }

            m_paletteSlots.Clear();
            m_paletteSlotById.Clear();
            for (int i = 0; i < m_categorySlots.Length; i++)
            {
                m_categorySlots[i].Clear();
                m_categoryCounts[i] = 0;
            }

            int poolIndex = 0;
            int categoryCount = Mathf.Min(_moduleIdsByCategory.Length, m_categories.Length);
            for (int category = 0; category < categoryCount; category++)
            {
                int[] ids = _moduleIdsByCategory[category];
                int count = ids != null ? ids.Length : 0;
                PaletteCategory target = m_categories[category];
                int columns = Mathf.Clamp(count, 1, Mathf.Clamp(m_layoutConfig.paletteColumns, 1, 6));
                target.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                target.grid.constraintCount = columns;
                m_categoryCounts[category] = count;
                for (int i = 0; i < count; i++)
                {
                    ModuleSlotUI slot = m_palettePool[poolIndex++];
                    slot.transform.SetParent(target.grid.transform, false);
                    slot.SetContentPadding(m_paletteContentPadding);
                    slot.gameObject.SetActive(true);
                    m_paletteSlots.Add(slot);
                    m_categorySlots[category].Add(slot);
                    m_paletteSlotById[ids[i]] = slot;
                }
            }

            ApplyPaletteLayout();
            ApplyNavigation();
        }

        public ModuleSlotUI PaletteSlot(int _moduleId)
        {
            return m_paletteSlotById.TryGetValue(_moduleId, out ModuleSlotUI slot) ? slot : null;
        }

        /// <summary>
        /// 브리지의 레이아웃 설정을 현재 RectTransform 크기에 맞춰 다시 적용합니다.
        /// 창 크기가 실제로 바뀔 때만 OnRectTransformDimensionsChange에서 호출됩니다.
        /// </summary>
        public void ApplyLayout()
        {
            if (!m_isInitialized)
            {
                return;
            }

            ApplyAnchors();
            ApplyHotbarLayout();
            ApplyPaletteLayout();
            m_hasAppliedLayout = HasValidSizes();
        }

        /// <summary>
        /// 첫 프레임 전에는 RectTransform 크기가 아직 확정되지 않을 수 있어, 유효한 크기를 얻을 때까지만
        /// 다시 적용합니다. 이후에는 창 크기 변경 콜백에서만 재계산합니다.
        /// </summary>
        public void ApplyPendingLayout()
        {
            if (!m_hasAppliedLayout)
            {
                ApplyLayout();
            }
        }

        public void SetHotbarVisible(bool _visible)
        {
            bool wasVisible = m_hotbar != null && m_hotbar.gameObject.activeSelf;
            SetVisible(m_hotbar != null ? m_hotbar.gameObject : null, _visible);
            if (_visible && !wasVisible)
            {
                ApplyHotbarContentPadding();
            }
        }

        public void SetGuideVisible(bool _visible)
        {
            SetVisible(m_hotbarGuide != null ? m_hotbarGuide.gameObject : null, _visible);
        }

        public void SetPaletteVisible(bool _visible)
        {
            bool wasVisible = m_palette != null && m_palette.gameObject.activeSelf;
            SetVisible(m_palette != null ? m_palette.gameObject : null, _visible);
            if (_visible && !wasVisible)
            {
                ApplyPaletteSlotPadding();
            }
        }

        public void SetGuideText(string _text)
        {
            if (m_hotbarGuide != null && m_hotbarGuide.text != _text)
            {
                m_hotbarGuide.text = _text;
            }
        }

        public void ShowDragGhost(Color _color, string _label, Vector2 _localPosition)
        {
            if (m_dragGhost == null)
            {
                return;
            }

            if (m_dragGhostSwatch != null)
            {
                m_dragGhostSwatch.color = _color;
            }

            if (m_dragGhostLabel != null)
            {
                m_dragGhostLabel.text = _label;
            }

            m_dragGhost.gameObject.SetActive(true);
            m_dragGhost.SetAsLastSibling();
            m_dragGhost.localPosition = _localPosition;
        }

        public void MoveDragGhost(Vector2 _localPosition)
        {
            if (m_dragGhost != null)
            {
                m_dragGhost.localPosition = _localPosition;
            }
        }

        public void HideDragGhost()
        {
            if (m_dragGhost != null)
            {
                m_dragGhost.gameObject.SetActive(false);
            }
        }

        public void SetPaletteToggleState(bool _visible, bool _interactable)
        {
            if (m_paletteToggleButton == null)
            {
                return;
            }

            SetVisible(m_paletteToggleButton.gameObject, _visible);
            if (m_paletteToggleButton.interactable != _interactable)
            {
                m_paletteToggleButton.interactable = _interactable;
            }
        }

        private void OnPaletteToggleClicked()
        {
            PaletteToggleClicked?.Invoke();
        }

        private void OnDestroy()
        {
            if (m_paletteToggleButton != null)
            {
                m_paletteToggleButton.onClick.RemoveListener(OnPaletteToggleClicked);
            }
        }

        public void HideAll()
        {
            SetPaletteToggleState(false, false);
            SetHotbarVisible(false);
            SetGuideVisible(false);
            SetPaletteVisible(false);
            HideDragGhost();
        }

        private void Awake()
        {
            if (!m_isInitialized)
            {
                HideAll();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
        }

        private void BuildCategoryState()
        {
            int count = m_categories != null ? m_categories.Length : 0;
            m_categoryCounts = new int[count];
            m_categorySlots = new List<ModuleSlotUI>[count];
            for (int i = 0; i < count; i++)
            {
                m_categorySlots[i] = new List<ModuleSlotUI>();
            }
        }

        private void EnsurePool(int _count)
        {
            while (m_palettePool.Count < _count)
            {
                ModuleSlotUI slot = Instantiate(m_slotTemplate, m_slotTemplate.transform.parent);
                slot.name = "ModuleSlot" + m_palettePool.Count;
                slot.gameObject.SetActive(false);
                m_palettePool.Add(slot);
            }
        }

        private void ApplyAnchors()
        {
            ApplyAnchors(m_hotbar, m_layoutConfig.hotbarAnchorMin, m_layoutConfig.hotbarAnchorMax);

            if (m_hotbarGuide != null)
            {
                // 모듈 선택창 버튼이 있으면 안내 문구는 버튼 오른쪽에서 시작한다.
                float guideLeft = m_paletteToggleButton != null
                    ? ((RectTransform)m_paletteToggleButton.transform).anchorMax.x + .01f
                    : m_layoutConfig.hotbarAnchorMin.x;
                Vector2 guideMin = new Vector2(guideLeft, m_layoutConfig.hotbarAnchorMax.y + .01f);
                Vector2 guideMax = new Vector2(m_layoutConfig.hotbarAnchorMax.x, m_layoutConfig.hotbarAnchorMax.y + .04f);
                ApplyAnchors(m_hotbarGuide.rectTransform, guideMin, guideMax);
            }

            Vector2 ratio = new Vector2(Mathf.Clamp(m_layoutConfig.paletteSizeRatio.x, .01f, 1f),
                Mathf.Clamp(m_layoutConfig.paletteSizeRatio.y, .01f, 1f));
            Vector2 min = Vector2.one * .5f - ratio * .5f;
            Vector2 max = Vector2.one * .5f + ratio * .5f;
            ApplyAnchors(m_palette, min, max);
            if (m_palette != null)
            {
                m_palette.pivot = new Vector2(.5f, .5f);
            }

            if (m_dragGhost != null)
            {
                m_dragGhost.sizeDelta = m_layoutConfig.slotSize;
            }
        }

        private static void ApplyAnchors(RectTransform _rect, Vector2 _min, Vector2 _max)
        {
            if (_rect == null)
            {
                return;
            }

            _rect.anchorMin = _min;
            _rect.anchorMax = _max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        private void ApplyHotbarLayout()
        {
            if (m_hotbar == null)
            {
                return;
            }

            float scale = GetCanvasScale();
            Vector2 size = m_hotbar.rect.size;
            if (m_hotbarLayout != null && size.x > 0f && size.y > 0f)
            {
                int horizontalPadding = Mathf.RoundToInt(size.x * Mathf.Clamp(m_layoutConfig.hotbarPaddingRatio.x, 0f, .49f));
                int verticalPadding = Mathf.RoundToInt(size.y * Mathf.Clamp(m_layoutConfig.hotbarPaddingRatio.y, 0f, .49f));
                m_hotbarLayout.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
                m_hotbarLayout.spacing = m_layoutConfig.hotbarSpacingPixels / scale;
            }

            ApplyHotbarContentPadding();
        }

        private void ApplyHotbarContentPadding()
        {
            if (m_hotbarSlots == null)
            {
                return;
            }

            for (int i = 0; i < m_hotbarSlots.Length; i++)
            {
                if (m_hotbarSlots[i] != null)
                {
                    m_hotbarSlots[i].SetContentPaddingRatio(m_layoutConfig.hotbarTextPaddingRatio);
                }
            }
        }

        private void ApplyPaletteSlotPadding()
        {
            for (int i = 0; i < m_paletteSlots.Count; i++)
            {
                if (m_paletteSlots[i] != null)
                {
                    m_paletteSlots[i].SetContentPadding(m_paletteContentPadding);
                }
            }
        }

        private bool HasValidSizes()
        {
            return m_hotbar != null && m_hotbar.rect.size.x > 0f && m_hotbar.rect.size.y > 0f &&
                m_palette != null && m_palette.rect.size.x > 0f && m_palette.rect.size.y > 0f;
        }

        private void ApplyPaletteLayout()
        {
            if (m_palette == null || m_paletteLayout == null || m_paletteTitleLayout == null ||
                m_paletteReferenceSize.x <= 0f || m_paletteReferenceSize.y <= 0f)
            {
                return;
            }

            Vector2 size = m_palette.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return;
            }

            float scale = GetCanvasScale();
            float scaleX = size.x / m_paletteReferenceSize.x;
            float scaleY = size.y / m_paletteReferenceSize.y;
            m_paletteLayout.padding = new RectOffset(Mathf.RoundToInt(12f * scaleX), Mathf.RoundToInt(12f * scaleX),
                Mathf.RoundToInt(9f * scaleY), Mathf.RoundToInt(12f * scaleY));
            m_paletteLayout.spacing = 7.5f * scaleY;
            m_paletteTitleLayout.preferredHeight = m_paletteReferenceTitleHeight * scaleY;
            m_paletteContentPadding = 5f / scale;

            if (m_categories != null)
            {
                int count = Mathf.Min(m_categories.Length, m_categoryCounts != null ? m_categoryCounts.Length : 0);
                for (int i = 0; i < count; i++)
                {
                    PaletteCategory category = m_categories[i];
                    if (category == null || category.layout == null || category.heading == null ||
                        category.grid == null || category.gridSize == null)
                    {
                        continue;
                    }

                    category.layout.spacing = 4.5f * scaleY;
                    category.heading.preferredHeight = 21f * scaleY;
                    category.grid.cellSize = new Vector2(m_layoutConfig.slotSize.x * scaleX,
                        m_layoutConfig.slotSize.y * scaleY);
                    category.grid.spacing = new Vector2(4.5f * scaleX, 4.5f * scaleY);
                    int columns = Mathf.Max(1, category.grid.constraintCount);
                    int rows = Mathf.CeilToInt(m_categoryCounts[i] / (float)columns);
                    category.gridSize.preferredHeight = rows * category.grid.cellSize.y +
                        Mathf.Max(0, rows - 1) * category.grid.spacing.y;
                }
            }

            ApplyPaletteSlotPadding();
            LayoutRebuilder.MarkLayoutForRebuild(m_palette);
        }

        private float GetCanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? Mathf.Max(.01f, canvas.scaleFactor) : 1f;
        }

        private void ApplyNavigation()
        {
            ApplyRowNavigation(m_hotbarSlots);

            if (m_categories == null || m_categorySlots == null)
            {
                return;
            }

            for (int category = 0; category < m_categories.Length && category < m_categorySlots.Length; category++)
            {
                PaletteCategory target = m_categories[category];
                List<ModuleSlotUI> slots = m_categorySlots[category];
                if (target == null || target.grid == null || slots == null || slots.Count == 0)
                {
                    continue;
                }

                int columns = Mathf.Max(1, target.grid.constraintCount);
                int rows = Mathf.CeilToInt(slots.Count / (float)columns);
                for (int i = 0; i < slots.Count; i++)
                {
                    int row = i / columns;
                    int column = i % columns;
                    Selectable up = row > 0 ? GetSelectable(slots[i - columns]) : null;
                    Selectable down = row < rows - 1 && i + columns < slots.Count
                        ? GetSelectable(slots[i + columns])
                        : null;
                    Selectable left = column > 0 ? GetSelectable(slots[i - 1]) : null;
                    Selectable right = column < columns - 1 && i + 1 < slots.Count
                        ? GetSelectable(slots[i + 1])
                        : null;
                    SetExplicitNavigation(GetSelectable(slots[i]), up, down, left, right);
                }
            }
        }

        private static void ApplyRowNavigation(ModuleSlotUI[] _slots)
        {
            if (_slots == null)
            {
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                Selectable left = i > 0 ? GetSelectable(_slots[i - 1]) : null;
                Selectable right = i < _slots.Length - 1 ? GetSelectable(_slots[i + 1]) : null;
                SetExplicitNavigation(GetSelectable(_slots[i]), null, null, left, right);
            }
        }

        private static Selectable GetSelectable(ModuleSlotUI _slot)
        {
            return _slot != null ? _slot.Button : null;
        }

        private static void SetExplicitNavigation(Selectable _selectable, Selectable _up, Selectable _down,
            Selectable _left, Selectable _right)
        {
            if (_selectable == null)
            {
                return;
            }

            Navigation navigation = _selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = _up;
            navigation.selectOnDown = _down;
            navigation.selectOnLeft = _left;
            navigation.selectOnRight = _right;
            _selectable.navigation = navigation;
        }

        private static void SetVisible(GameObject _target, bool _visible)
        {
            if (_target != null && _target.activeSelf != _visible)
            {
                _target.SetActive(_visible);
            }
        }

        private bool ValidateReferences()
        {
            bool valid = m_hotbar != null && m_hotbarLayout != null && m_hotbarGuide != null &&
                m_hotbarSlots != null && m_hotbarSlots.Length > 0 &&
                m_palette != null && m_paletteLayout != null && m_paletteTitle != null &&
                m_paletteTitleLayout != null && m_categories != null && m_categories.Length > 0 &&
                m_slotTemplate != null && m_dragGhost != null && m_dragGhostSwatch != null &&
                m_dragGhostLabel != null && m_paletteReferenceSize.x > 0f && m_paletteReferenceSize.y > 0f &&
                m_paletteReferenceTitleHeight > 0f;

            if (valid)
            {
                for (int i = 0; i < m_categories.Length; i++)
                {
                    PaletteCategory category = m_categories[i];
                    if (category == null || category.layout == null || category.heading == null ||
                        category.grid == null || category.gridSize == null)
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (!valid && !m_hasLoggedReferenceError)
            {
                m_hasLoggedReferenceError = true;
                Debug.LogError(
                    "[ModulePaletteView] 초기화 실패: 저작된 참조가 누락되었습니다. " +
                    "Tools/UI/Authoring/ModulePalette/Author를 실행하세요.",
                    this);
            }

            return valid;
        }
    }
}
