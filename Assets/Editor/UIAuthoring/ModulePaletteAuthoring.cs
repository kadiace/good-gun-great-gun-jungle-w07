using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop.Editor
{
    /// <summary>
    /// 모듈 팔레트의 퀵슬롯·목록·드래그 고스트를 UIManager 프리팹에 저작하고 View 참조를 연결합니다.
    /// 1920×1080 기준 실효 값(슬롯 75×63, 패딩 비율)으로 저작한다.
    /// 재실행해도 기존 오브젝트를 재사용하며, 실패하면 프리팹을 저장하지 않는다.
    /// </summary>
    internal static class ModulePaletteAuthoring
    {
        private const string UIPath = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string CatalogPath = "Assets/Data/ModuleCatalog.asset";

        private static readonly Color SlotColor = new Color(41f / 255f, 45f / 255f, 51f / 255f, 1f);
        private static readonly Color SelectionBorderColor = new Color(1f, .8f, .2f, 1f);
        private static readonly Color DropBorderColor = new Color(.25f, .9f, 1f, 1f);
        private static readonly Color PriceColor = new Color(.9f, .85f, .65f);
        private static readonly Color NumberColor = new Color(.9f, .9f, .95f);

        private static readonly Vector2 SlotSize = new Vector2(75f, 63f);
        private static readonly Vector2 HotbarAnchorMin = new Vector2(.08f, .02f);
        private static readonly Vector2 HotbarAnchorMax = new Vector2(.92f, .12f);
        private static readonly Vector2 HotbarPaddingRatio = new Vector2(.01f, .03f);
        private static readonly Vector2 HotbarTextPaddingRatio = new Vector2(.05f, .1f);
        private const float HotbarSpacingPixels = 6f;
        private static readonly Vector2 PaletteSizeRatio = new Vector2(.32715625f, .42062014f);
        private const int PaletteColumns = 6;

        [MenuItem("Tools/UI/Authoring/ModulePalette/Author")]
        private static void Author()
        {
            try
            {
                Apply();
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIAuthoring] FAIL ModulePalette/Author: " + exception);
            }
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Edit mode required.");
            }

            var prefab = PrefabUtility.LoadPrefabContents(UIPath);
            try
            {
                ModulePaletteUI bridge = prefab.GetComponentInChildren<ModulePaletteUI>(true);
                if (bridge == null)
                {
                    throw new InvalidOperationException("ModulePaletteUI missing from UI prefab.");
                }

                TMP_FontAsset font = ResolveFont(bridge);
                ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(CatalogPath);
                if (catalog == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:ModuleCatalog");
                    if (guids.Length > 0)
                    {
                        catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }

                if (catalog == null)
                {
                    throw new InvalidOperationException("ModuleCatalog asset missing.");
                }

                RectTransform root = (RectTransform)bridge.transform;
                ModulePaletteView view = GetOrAdd<ModulePaletteView>(root.gameObject);
                List<int>[] groups = GroupModuleIds(catalog);
                int[] counts = { groups[0].Count, groups[1].Count, groups[2].Count };

                RectTransform hotbar = AuthorHotbar(root, view, font);
                ModuleSlotUI[] hotbarSlots = CollectHotbarSlots(hotbar);
                TMPro.TextMeshProUGUI guide = AuthorGuide(root, font);
                Button paletteToggle = AuthorPaletteToggle(root, font);

                ModulePaletteView.PaletteCategory[] categories = new ModulePaletteView.PaletteCategory[3];
                Vector2 referenceSize;
                float referenceTitleHeight;
                RectTransform palette = AuthorPalette(root, font, counts, categories, out referenceSize,
                    out referenceTitleHeight);

                RectTransform templateRect = AuthorTemplate(root, view, font);
                ModuleSlotUI template = templateRect.GetComponent<ModuleSlotUI>();
                RectTransform ghost = AuthorDragGhost(root, font, out Image ghostSwatch, out TMPro.TextMeshProUGUI ghostLabel);

                var viewData = new SerializedObject(view);
                Assign(viewData, "m_hotbar", hotbar);
                Assign(viewData, "m_hotbarLayout", hotbar.GetComponent<HorizontalLayoutGroup>());
                Assign(viewData, "m_hotbarGuide", guide);
                Assign(viewData, "m_paletteToggleButton", paletteToggle);
                SerializedProperty slotArray = viewData.FindProperty("m_hotbarSlots");
                slotArray.arraySize = hotbarSlots.Length;
                for (int i = 0; i < hotbarSlots.Length; i++)
                {
                    slotArray.GetArrayElementAtIndex(i).objectReferenceValue = hotbarSlots[i];
                }

                Assign(viewData, "m_palette", palette);
                Assign(viewData, "m_paletteLayout", palette.GetComponent<VerticalLayoutGroup>());
                Assign(viewData, "m_paletteTitle", palette.Find("Title").GetComponent<TMPro.TextMeshProUGUI>());
                Assign(viewData, "m_paletteTitleLayout", palette.Find("Title").GetComponent<LayoutElement>());
                SerializedProperty categoryArray = viewData.FindProperty("m_categories");
                categoryArray.arraySize = categories.Length;
                for (int i = 0; i < categories.Length; i++)
                {
                    SerializedProperty element = categoryArray.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("layout").objectReferenceValue = categories[i].layout;
                    element.FindPropertyRelative("heading").objectReferenceValue = categories[i].heading;
                    element.FindPropertyRelative("grid").objectReferenceValue = categories[i].grid;
                    element.FindPropertyRelative("gridSize").objectReferenceValue = categories[i].gridSize;
                }

                viewData.FindProperty("m_paletteReferenceSize").vector2Value = referenceSize;
                viewData.FindProperty("m_paletteReferenceTitleHeight").floatValue = referenceTitleHeight;
                Assign(viewData, "m_slotTemplate", template);
                Assign(viewData, "m_dragGhost", ghost);
                Assign(viewData, "m_dragGhostSwatch", ghostSwatch);
                Assign(viewData, "m_dragGhostLabel", ghostLabel);
                viewData.ApplyModifiedPropertiesWithoutUndo();

                AuthorNavigation(hotbarSlots);
                ApplyHotbarContentPadding(hotbarSlots);
                template.SetContentPadding(5f);

                if (PrefabUtility.SaveAsPrefabAsset(prefab, UIPath) == null)
                {
                    throw new InvalidOperationException("UI prefab save failed.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            AssetDatabase.ImportAsset(UIPath);
            Debug.Log("[UIAuthoring] OK ModulePalette/Author: Hotbar 10 + Palette 3 categories + ModuleSlotTemplate + DragGhost authored.");
        }

        private static TMP_FontAsset ResolveFont(ModulePaletteUI bridge)
        {
            var bridgeData = new SerializedObject(bridge);
            TMP_FontAsset font = bridgeData.FindProperty("font").objectReferenceValue as TMP_FontAsset;
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            if (font == null)
            {
                throw new InvalidOperationException("TMP font asset missing.");
            }

            return font;
        }

        private static List<int>[] GroupModuleIds(ModuleCatalog catalog)
        {
            var groups = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int id = 1; id <= catalog.Count; id++)
            {
                catalog.TryGet(id, out ModuleCatalog.Entry entry);
                bool decorative = entry.data != null && (entry.data.ModuleType == StatType.Scope ||
                    entry.data.ModuleType == StatType.Color ||
                    (entry.prefab != null && entry.prefab.GetComponent<TriggerModule>() != null));
                int category = id <= 4 ? 0 : decorative ? 2 : 1;
                groups[category].Add(id);
            }

            return groups;
        }

        private static RectTransform AuthorHotbar(RectTransform root, ModulePaletteView view, TMP_FontAsset font)
        {
            RectTransform hotbar = GetOrCreateRect(root, "Hotbar");
            hotbar.anchorMin = HotbarAnchorMin;
            hotbar.anchorMax = HotbarAnchorMax;
            hotbar.offsetMin = hotbar.offsetMax = Vector2.zero;
            var layout = GetOrAdd<HorizontalLayoutGroup>(hotbar.gameObject);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.spacing = HotbarSpacingPixels;

            Vector2 referenceResolution = GetReferenceResolution(root);
            float width = (HotbarAnchorMax.x - HotbarAnchorMin.x) * referenceResolution.x;
            float height = (HotbarAnchorMax.y - HotbarAnchorMin.y) * referenceResolution.y;
            layout.padding = new RectOffset(
                Mathf.RoundToInt(width * HotbarPaddingRatio.x), Mathf.RoundToInt(width * HotbarPaddingRatio.x),
                Mathf.RoundToInt(height * HotbarPaddingRatio.y), Mathf.RoundToInt(height * HotbarPaddingRatio.y));

            for (int i = 0; i < 10; i++)
            {
                AuthorSlot(hotbar, "Favorite" + (i + 1), view, i, font, SlotSize, 18f, 15f, 18f);
            }

            return hotbar;
        }

        private static ModuleSlotUI[] CollectHotbarSlots(RectTransform hotbar)
        {
            var slots = new ModuleSlotUI[10];
            for (int i = 0; i < slots.Length; i++)
            {
                Transform child = hotbar.Find("Favorite" + (i + 1));
                slots[i] = child != null ? child.GetComponent<ModuleSlotUI>() : null;
                if (slots[i] == null)
                {
                    throw new InvalidOperationException("Hotbar slot authoring failed: Favorite" + (i + 1));
                }
            }

            return slots;
        }

        // 퀵슬롯 위 "모듈 선택창 [TAB]" 버튼. 마우스로 모듈 목록을 열고 닫는다(글자 크기는 1920 기준).
        private static Button AuthorPaletteToggle(RectTransform root, TMP_FontAsset font)
        {
            RectTransform rect = GetOrCreateRect(root, "PaletteToggleButton");
            rect.anchorMin = new Vector2(.082f, .13f);
            rect.anchorMax = new Vector2(.247f, .17f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image background = GetOrAdd<Image>(rect.gameObject);
            background.color = Color.white;
            Button button = GetOrAdd<Button>(rect.gameObject);
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = SlotColor;
            colors.highlightedColor = colors.selectedColor = new Color32(0x3D, 0x44, 0x4F, 255);
            colors.pressedColor = new Color32(0x1C, 0x20, 0x26, 255);
            colors.disabledColor = new Color32(0x22, 0x25, 0x2B, 255);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;

            RectTransform labelRect = GetOrCreateRect(rect, "Label");
            labelRect.anchorMin = new Vector2(.04f, .08f);
            labelRect.anchorMax = new Vector2(.96f, .92f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI label = GetOrCreateText(labelRect, font);
            label.text = "모듈 선택창 [TAB]";
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSize = 18f;
            label.fontSizeMax = 18f;
            label.fontSizeMin = 12f;
            label.color = Color.white;
            label.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return button;
        }

        private static TMPro.TextMeshProUGUI AuthorGuide(RectTransform root, TMP_FontAsset font)
        {
            RectTransform rect = GetOrCreateRect(root, "HotbarGuide");
            rect.anchorMin = new Vector2(HotbarAnchorMin.x, HotbarAnchorMax.y + .01f);
            rect.anchorMax = new Vector2(HotbarAnchorMax.x, HotbarAnchorMax.y + .04f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TMPro.TextMeshProUGUI guide = GetOrCreateText(rect, font);
            guide.text = ModulePaletteView.HotbarGuideText;
            guide.alignment = TextAlignmentOptions.Midline;
            guide.textWrappingMode = TextWrappingModes.NoWrap;
            guide.fontSize = 16.5f;
            guide.enableAutoSizing = true;
            guide.fontSizeMin = 13.5f;
            guide.fontSizeMax = 16.5f;
            guide.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return guide;
        }

        private static RectTransform AuthorPalette(RectTransform root, TMP_FontAsset font, int[] counts,
            ModulePaletteView.PaletteCategory[] categories, out Vector2 referenceSize,
            out float referenceTitleHeight)
        {
            RectTransform palette = GetOrCreateRect(root, "Palette");
            palette.anchorMin = Vector2.one * .5f - PaletteSizeRatio * .5f;
            palette.anchorMax = Vector2.one * .5f + PaletteSizeRatio * .5f;
            palette.pivot = new Vector2(.5f, .5f);
            palette.offsetMin = palette.offsetMax = Vector2.zero;
            Image background = GetOrAdd<Image>(palette.gameObject);
            background.color = new Color(0f, 0f, 0f, .75f);
            var layout = GetOrAdd<VerticalLayoutGroup>(palette.gameObject);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform titleRect = GetOrCreateRect(palette, "Title");
            TMPro.TextMeshProUGUI title = GetOrCreateText(titleRect, font);
            title.text = ModulePaletteView.PaletteTitleText;
            title.fontSize = 16.5f;
            title.alignment = TextAlignmentOptions.Midline;
            title.enableAutoSizing = false;
            UIFonts.ApplyWeight(title, 800);
            Vector2 titlePreferred = title.GetPreferredValues(title.text);
            if (titlePreferred.x <= 0f || titlePreferred.y <= 0f)
            {
                titlePreferred = new Vector2(465f, 42f);
                Debug.LogWarning("[UIAuthoring] ModulePalette/Author: 제목 기본 크기를 측정하지 못해 대체 값(465×42)을 사용했습니다.");
            }

            referenceTitleHeight = titlePreferred.y;
            title.enableAutoSizing = true;
            title.fontSizeMin = 13.5f;
            title.fontSizeMax = 16.5f;
            var titleLayout = GetOrAdd<LayoutElement>(titleRect.gameObject);
            titleLayout.preferredHeight = referenceTitleHeight;

            float referenceContentWidth = titlePreferred.x;
            float referenceContentHeight = titlePreferred.y + 22.5f;
            for (int category = 0; category < 3; category++)
            {
                RectTransform group = GetOrCreateRect(palette, "ModuleCategory" + category);
                var groupLayout = GetOrAdd<VerticalLayoutGroup>(group.gameObject);
                groupLayout.childControlWidth = true;
                groupLayout.childControlHeight = true;
                groupLayout.childForceExpandWidth = true;
                groupLayout.childForceExpandHeight = false;
                groupLayout.childAlignment = TextAnchor.UpperLeft;

                RectTransform headingRect = GetOrCreateRect(group, "CategoryTitle");
                headingRect.sizeDelta = new Vector2(82.5f, 21f);
                TMPro.TextMeshProUGUI heading = GetOrCreateText(headingRect, font);
                heading.text = category == 0 ? "제어 모듈" : category == 1 ? "기능 모듈" : "치장 모듈";
                heading.fontSize = 16.5f;
                heading.enableAutoSizing = true;
                heading.fontSizeMin = 13.5f;
                heading.fontSizeMax = 16.5f;
                UIFonts.ApplyWeight(heading, 800);
                var headingLayout = GetOrAdd<LayoutElement>(headingRect.gameObject);
                headingLayout.preferredHeight = 21f;

                RectTransform grid = GetOrCreateRect(group, "Grid");
                var gridLayout = GetOrAdd<GridLayoutGroup>(grid.gameObject);
                gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = Mathf.Clamp(counts[category], 1, Mathf.Clamp(PaletteColumns, 1, 6));
                var gridSize = GetOrAdd<LayoutElement>(grid.gameObject);

                int rows = Mathf.CeilToInt(counts[category] / (float)gridLayout.constraintCount);
                float gridHeight = rows * SlotSize.y + Mathf.Max(0, rows - 1) * 4.5f;
                referenceContentWidth = Mathf.Max(referenceContentWidth,
                    gridLayout.constraintCount * SlotSize.x + (gridLayout.constraintCount - 1) * 4.5f);
                referenceContentHeight += 21f + 4.5f + gridHeight;

                categories[category] = new ModulePaletteView.PaletteCategory
                {
                    layout = groupLayout,
                    heading = headingLayout,
                    grid = gridLayout,
                    gridSize = gridSize
                };
            }

            referenceSize = new Vector2(referenceContentWidth + 24f, referenceContentHeight + 21f);
            ApplyPaletteMetrics(root, palette, layout, titleLayout, categories, counts, referenceSize,
                referenceTitleHeight);
            return palette;
        }

        private static void ApplyPaletteMetrics(RectTransform root, RectTransform palette, VerticalLayoutGroup layout,
            LayoutElement titleLayout, ModulePaletteView.PaletteCategory[] categories, int[] counts,
            Vector2 referenceSize, float referenceTitleHeight)
        {
            Vector2 referenceResolution = GetReferenceResolution(root);
            float width = PaletteSizeRatio.x * referenceResolution.x;
            float height = PaletteSizeRatio.y * referenceResolution.y;
            float scaleX = width / referenceSize.x;
            float scaleY = height / referenceSize.y;
            layout.padding = new RectOffset(Mathf.RoundToInt(12f * scaleX), Mathf.RoundToInt(12f * scaleX),
                Mathf.RoundToInt(9f * scaleY), Mathf.RoundToInt(12f * scaleY));
            layout.spacing = 7.5f * scaleY;
            titleLayout.preferredHeight = referenceTitleHeight * scaleY;

            for (int category = 0; category < categories.Length; category++)
            {
                ModulePaletteView.PaletteCategory target = categories[category];
                target.layout.spacing = 4.5f * scaleY;
                target.heading.preferredHeight = 21f * scaleY;
                target.grid.cellSize = new Vector2(SlotSize.x * scaleX, SlotSize.y * scaleY);
                target.grid.spacing = new Vector2(4.5f * scaleX, 4.5f * scaleY);
                int rows = Mathf.CeilToInt(counts[category] / (float)target.grid.constraintCount);
                target.gridSize.preferredHeight = rows * target.grid.cellSize.y +
                    Mathf.Max(0, rows - 1) * target.grid.spacing.y;
            }
        }

        private static RectTransform AuthorTemplate(RectTransform root, ModulePaletteView view, TMP_FontAsset font)
        {
            ModuleSlotUI slot = AuthorSlot(root, "ModuleSlotTemplate", view, -1, font, SlotSize, 15f, 12f, 15f);
            slot.gameObject.SetActive(false);
            return (RectTransform)slot.transform;
        }

        private static RectTransform AuthorDragGhost(RectTransform root, TMP_FontAsset font, out Image swatch,
            out TMPro.TextMeshProUGUI label)
        {
            RectTransform ghost = GetOrCreateRect(root, "DragGhost");
            ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(.5f, .5f);
            ghost.anchoredPosition = Vector2.zero;
            ghost.sizeDelta = SlotSize;
            Image background = GetOrAdd<Image>(ghost.gameObject);
            background.color = SlotColor;
            background.raycastTarget = false;
            ghost.SetAsLastSibling();

            RectTransform swatchRect = GetOrCreateRect(ghost, "Color");
            swatchRect.anchorMin = new Vector2(0f, 0f);
            swatchRect.anchorMax = new Vector2(0f, 1f);
            swatchRect.pivot = new Vector2(0f, .5f);
            swatchRect.sizeDelta = new Vector2(9f, 0f);
            swatchRect.anchoredPosition = Vector2.zero;
            swatch = GetOrAdd<Image>(swatchRect.gameObject);
            swatch.raycastTarget = false;
            swatch.color = Color.white;

            RectTransform labelRect = GetOrCreateRect(ghost, "Label");
            label = GetOrCreateText(labelRect, font);
            label.text = string.Empty;
            label.fontSize = 15f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UIFonts.ApplyWeight(label, 400);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(13.5f, 0f);
            labelRect.offsetMax = new Vector2(-3f, 0f);

            var group = GetOrAdd<CanvasGroup>(ghost.gameObject);
            group.blocksRaycasts = false;
            ghost.gameObject.SetActive(false);
            return ghost;
        }

        private static ModuleSlotUI AuthorSlot(RectTransform parent, string name, ModulePaletteView view,
            int favoriteIndex, TMP_FontAsset font, Vector2 size, float labelSize, float labelMin, float labelMax)
        {
            RectTransform rect = GetOrCreateRect(parent, name);
            rect.sizeDelta = size;
            Image background = GetOrAdd<Image>(rect.gameObject);
            background.color = Color.white;
            var button = GetOrAdd<Button>(rect.gameObject);
            button.targetGraphic = background;
            ApplyButtonColors(button);

            RectTransform swatchRect = GetOrCreateRect(rect, "Color");
            swatchRect.anchorMin = new Vector2(0f, 0f);
            swatchRect.anchorMax = new Vector2(0f, 1f);
            swatchRect.pivot = new Vector2(0f, .5f);
            swatchRect.sizeDelta = new Vector2(9f, 0f);
            swatchRect.anchoredPosition = Vector2.zero;
            Image swatch = GetOrAdd<Image>(swatchRect.gameObject);
            swatch.raycastTarget = false;
            swatch.color = Color.white;

            RectTransform labelRect = GetOrCreateRect(rect, "Label");
            TMPro.TextMeshProUGUI label = GetOrCreateText(labelRect, font);
            label.text = string.Empty;
            label.fontSize = labelSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = labelMin;
            label.fontSizeMax = labelMax;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            UIFonts.ApplyWeight(label, 700);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(13.5f, 21f);
            labelRect.offsetMax = new Vector2(-3f, 0f);

            RectTransform priceRect = GetOrCreateRect(rect, "Price");
            priceRect.anchorMin = Vector2.zero;
            priceRect.anchorMax = new Vector2(1f, 0f);
            priceRect.pivot = new Vector2(.5f, 0f);
            priceRect.anchoredPosition = new Vector2(5.25f, 3.75f);
            priceRect.sizeDelta = new Vector2(-15f, 17.25f);
            TMPro.TextMeshProUGUI price = GetOrCreateText(priceRect, font);
            price.text = string.Empty;
            price.fontSize = 13.5f;
            price.enableAutoSizing = true;
            price.fontSizeMin = 10.5f;
            price.fontSizeMax = 13.5f;
            price.textWrappingMode = TextWrappingModes.NoWrap;
            price.alignment = TextAlignmentOptions.MidlineLeft;
            price.color = PriceColor;

            ModuleSelectionBorder selection = GetOrCreateBorder(rect, "SelectionBorder", SelectionBorderColor);
            ModuleSelectionBorder drop = GetOrCreateBorder(rect, "DropBorder", DropBorderColor);

            RectTransform lockRect = GetOrCreateRect(rect, "LockOverlay");
            lockRect.anchorMin = Vector2.zero;
            lockRect.anchorMax = Vector2.one;
            lockRect.offsetMin = lockRect.offsetMax = Vector2.zero;
            Image shade = GetOrAdd<Image>(lockRect.gameObject);
            shade.color = new Color(0f, 0f, 0f, .8f);
            shade.raycastTarget = false;
            RectTransform iconRect = GetOrCreateRect(lockRect, "LockIcon");
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(30f, 30f);
            var icon = GetOrAdd<LockPictogramGraphic>(iconRect.gameObject);
            icon.color = Color.white;
            icon.raycastTarget = false;
            lockRect.gameObject.SetActive(false);

            TMPro.TextMeshProUGUI number = null;
            if (favoriteIndex >= 0)
            {
                RectTransform numberRect = GetOrCreateRect(rect, "SlotNumber");
                numberRect.anchorMin = numberRect.anchorMax = Vector2.one;
                numberRect.pivot = Vector2.one;
                number = GetOrCreateText(numberRect, font);
                number.text = ((favoriteIndex + 1) % 10).ToString();
                number.fontSize = 18f;
                number.enableAutoSizing = true;
                number.fontSizeMin = 15f;
                number.fontSizeMax = 18f;
                number.alignment = TextAlignmentOptions.TopRight;
                number.textWrappingMode = TextWrappingModes.NoWrap;
                number.color = NumberColor;
                UIFonts.ApplyWeight(number, 700);
            }

            ModuleSlotUI slot = GetOrAdd<ModuleSlotUI>(rect.gameObject);
            var data = new SerializedObject(slot);
            Assign(data, "m_view", view);
            data.FindProperty("m_favoriteIndex").intValue = favoriteIndex;
            Assign(data, "m_button", button);
            Assign(data, "m_swatch", swatch);
            Assign(data, "m_label", label);
            Assign(data, "m_priceLabel", price);
            Assign(data, "m_numberLabel", number);
            Assign(data, "m_selectionBorder", selection);
            Assign(data, "m_dropBorder", drop);
            Assign(data, "m_lockOverlay", lockRect.gameObject);
            data.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        private static ModuleSelectionBorder GetOrCreateBorder(RectTransform parent, string name, Color color)
        {
            RectTransform rect = GetOrCreateRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var border = GetOrAdd<ModuleSelectionBorder>(rect.gameObject);
            border.color = color;
            border.raycastTarget = false;
            rect.gameObject.SetActive(false);
            return border;
        }

        private static void ApplyHotbarContentPadding(ModuleSlotUI[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].SetContentPaddingRatio(HotbarTextPaddingRatio);
                }
            }
        }

        private static void AuthorNavigation(ModuleSlotUI[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Selectable left = i > 0 ? slots[i - 1].Button : null;
                Selectable right = i < slots.Length - 1 ? slots[i + 1].Button : null;
                SetExplicitNavigation(slots[i].Button, null, null, left, right);
            }
        }

        private static void SetExplicitNavigation(Selectable selectable, Selectable up, Selectable down,
            Selectable left, Selectable right)
        {
            if (selectable == null)
            {
                return;
            }

            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            selectable.navigation = navigation;
        }

        private static Vector2 GetReferenceResolution(RectTransform root)
        {
            CanvasScaler scaler = root.GetComponentInParent<CanvasScaler>();
            Vector2 resolution = scaler != null ? scaler.referenceResolution : new Vector2(1920f, 1080f);
            return resolution.x > 0f && resolution.y > 0f ? resolution : new Vector2(1920f, 1080f);
        }

        private static void ApplyButtonColors(Button button)
        {
            button.targetGraphic.color = Color.white;
            ColorBlock colors = button.colors;
            colors.normalColor = SlotColor;
            colors.highlightedColor = colors.selectedColor = new Color32(0x3D, 0x44, 0x4F, 255);
            colors.pressedColor = new Color32(0x1C, 0x20, 0x26, 255);
            colors.disabledColor = new Color32(0x22, 0x25, 0x2B, 255);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .12f;
            button.colors = colors;
        }

        private static RectTransform GetOrCreateRect(Transform parent, string name)
        {
            RectTransform existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static TMPro.TextMeshProUGUI GetOrCreateText(RectTransform rect, TMP_FontAsset font)
        {
            TMPro.TextMeshProUGUI text = rect.GetComponent<TMPro.TextMeshProUGUI>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            }

            text.font = font;
            text.fontSharedMaterial = font.material;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void Assign(SerializedObject data, string name, UnityEngine.Object value)
        {
            SerializedProperty property = data.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException("Serialized property missing: " + name);
            }

            property.objectReferenceValue = value;
        }
    }
}
