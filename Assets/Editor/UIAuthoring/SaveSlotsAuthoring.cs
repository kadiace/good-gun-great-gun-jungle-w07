using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// 저장 슬롯 패널 저작 명령입니다. 기존 SaveSlotsUI가 런타임에 만들던 자동저장 카드(AutoSaveSlot)와
    /// 삭제 버튼 4개(DeleteSlot1..3·DeleteAutoSave)를 UIManager.prefab(일시정지 저장)과
    /// StartUI_Manager.prefab(인트로 불러오기)에 고정 슬롯으로 만들고, SaveSlotCardView·SaveSlotsView를
    /// 추가해 브리지 직렬화 참조와 연결합니다.
    /// 멱등: 같은 이름의 오브젝트가 이미 있으면 새로 만들지 않고 연결만 검증·보정하며,
    /// 기존 오브젝트를 삭제·재생성해 fileID를 바꾸지 않습니다.
    /// Intro.unity가 StartUI_Manager 프리팹 인스턴스에 추가한 DeleteSlot1..3(씬 오브젝트)은 이 명령이 건드리지 않습니다.
    /// </summary>
    public static class SaveSlotsAuthoring
    {
        private const string MenuItemPath = "Tools/UI/Authoring/SaveSlots/Author";
        private const string UIManagerPrefabPath = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string StartUiPrefabPath = "Assets/Prefabs/UI/StartUI_Manager.prefab";

        // 1920×1080 기준 UIManager 값. 카드 255x172.5, 삭제 행 중심은 카드 중심에서 -116.25, 높이 36.
        private const float CardWidth = 255f;
        private const float CardHeight = 172.5f;
        private const float DeleteRowCenterY = -116.25f;
        private const float DeleteHeight = 36f;
        // StartUI_Manager는 1600×900 기준 저작값에 1.2를 곱한다.
        private const float StartUIDeleteRowCenterY = -186f;
        private const float StartUIDeleteHeight = 57.6f;

        [MenuItem(MenuItemPath)]
        public static void Author()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[UIAuthoring] FAIL SaveSlots/Author 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            bool uiManagerDone = AuthorPrefab(UIManagerPrefabPath, AuthorUIManagerPrefab);
            bool startUiDone = AuthorPrefab(StartUiPrefabPath, AuthorStartUiPrefab);
            if (uiManagerDone && startUiDone)
            {
                Debug.Log(
                    "[UIAuthoring] OK SaveSlots/Author " +
                    "UIManager.prefab(HLG 폭·높이 제어 전환, AutoSaveSlot, DeleteSlot1..3·DeleteAutoSave, SaveSlotsView 연결) " +
                    "StartUI_Manager.prefab(슬롯 최종 위치 고정, AutoSaveSlot, DeleteSlot1..3, DeleteAutoSave 이동, SaveSlotsView 연결)");
            }
            else
            {
                Debug.LogError("[UIAuthoring] FAIL SaveSlots/Author 일부 프리팹 저작에 실패해 저장하지 않았습니다(위 로그 참조).");
            }
        }

        private static bool AuthorPrefab(string path, System.Func<GameObject, string> author)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string error = author(root);
                if (error != null)
                {
                    Debug.LogError($"[UIAuthoring] FAIL SaveSlots/Author {path}: {error}");
                    return false;
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    Debug.LogError($"[UIAuthoring] FAIL SaveSlots/Author {path}: 프리팹 저장 실패");
                    return false;
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // UIManager.prefab: 가로 레이아웃 그룹이 카드 폭을 제어하게 바꾸고 카드 4장과 삭제 버튼 4개를 저작한다.
        private static string AuthorUIManagerPrefab(GameObject root)
        {
            SaveSlotsUI bridge = root.GetComponentInChildren<SaveSlotsUI>(true);
            if (bridge == null)
            {
                return "SaveSlotsUI 컴포넌트를 찾지 못했습니다";
            }

            SerializedObject bridgeSo = new SerializedObject(bridge);
            Button[] slotButtons = ReadButtonArray(bridgeSo, "slotButtons");
            if (!HasSlots(slotButtons))
            {
                return "slotButtons[0..2] 참조가 비어 있습니다";
            }

            TextMeshProUGUI[] slotTexts = ReadTextArray(bridgeSo, "slotTexts");
            if (!HasTexts(slotTexts))
            {
                return "slotTexts[0..2] 참조가 비어 있습니다";
            }

            Transform cardsParent = slotButtons[0].transform.parent;
            HorizontalLayoutGroup layout = cardsParent != null ? cardsParent.GetComponent<HorizontalLayoutGroup>() : null;
            if (layout == null)
            {
                return "슬롯 부모에 HorizontalLayoutGroup이 없습니다";
            }

            TMP_FontAsset labelFont = slotTexts[0].font;

            // 카드 3장에 카드 View를 붙인다.
            foreach (Button slot in slotButtons)
            {
                AttachCardView(slot);
            }

            // 자동저장 카드: 기존 EnsureAutoSaveSlot이 Slot1 복제로 만들던 것과 같은 구성을 고정 슬롯으로 만든다.
            Button autoSaveButton = GetOrCreateAutoSaveCardFromTemplate(cardsParent, slotButtons[0], null);
            AttachCardView(autoSaveButton);
            TextMeshProUGUI autoSaveText = autoSaveButton.GetComponentInChildren<TextMeshProUGUI>(true);

            // 삭제 버튼 4개: 기존 CreateDeleteButton과 같은 이름·크기·색·폰트(폰트 크기 21, UIManager 하위)로 만든다.
            Button[] deleteButtons = new Button[slotButtons.Length];
            for (int i = 0; i < slotButtons.Length; i++)
            {
                deleteButtons[i] = GetOrCreateDeleteButtonChild(slotButtons[i].transform, $"DeleteSlot{i + 1}", labelFont, 21f);
            }

            Button autoSaveDeleteButton = GetOrCreateDeleteButtonChild(autoSaveButton.transform, "DeleteAutoSave", labelFont, 21f);

            // 카드 폭·높이의 단일 권한을 레이아웃 그룹으로 정리한다. 기존 코드가 매 프레임 계산하던 폭과 같게
            // LayoutElement preferred(255x172.5)를 쓰고 ChildControl 플래그를 켠다.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            EditorUtility.SetDirty(layout);
            foreach (Button slot in slotButtons)
            {
                ApplyCardLayoutElement(slot.transform);
            }

            ApplyCardLayoutElement(autoSaveButton.transform);

            // 브리지 직렬화 참조를 채운다.
            bridgeSo.FindProperty("autoSaveButton").objectReferenceValue = autoSaveButton;
            bridgeSo.FindProperty("autoSaveText").objectReferenceValue = autoSaveText;
            WriteButtonArray(bridgeSo, "deleteButtons", deleteButtons);
            bridgeSo.FindProperty("autoSaveDeleteButton").objectReferenceValue = autoSaveDeleteButton;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            WireView(bridge, bridgeSo, autoSaveButton, autoSaveText, slotButtons, slotTexts, deleteButtons, autoSaveDeleteButton);
            return null;
        }

        // StartUI_Manager.prefab: 수동 위치(레이아웃 그룹 없음)를 런타임 밀기 결과로 고정하고 카드·삭제 버튼을 저작한다.
        private static string AuthorStartUiPrefab(GameObject root)
        {
            SaveSlotsUI bridge = root.GetComponentInChildren<SaveSlotsUI>(true);
            if (bridge == null)
            {
                return "SaveSlotsUI 컴포넌트를 찾지 못했습니다";
            }

            SerializedObject bridgeSo = new SerializedObject(bridge);
            GameObject panel = bridgeSo.FindProperty("panel").objectReferenceValue as GameObject;
            if (panel == null)
            {
                return "panel 참조가 비어 있습니다";
            }

            Button[] slotButtons = ReadButtonArray(bridgeSo, "slotButtons");
            if (!HasSlots(slotButtons))
            {
                return "slotButtons[0..2] 참조가 비어 있습니다";
            }

            TextMeshProUGUI[] slotTexts = ReadTextArray(bridgeSo, "slotTexts");
            if (!HasTexts(slotTexts))
            {
                return "slotTexts[0..2] 참조가 비어 있습니다";
            }

            // 런타임 EnsureAutoSaveSlot이 밀던 결과(자동저장 -666, 슬롯 -222/222/666, y 36)를 고정한다.
            // 예상 밖의 위치면 씬·프리팹 수동 배치가 섞였다는 뜻이므로 저장하지 않고 중단한다.
            Vector2[] finalPositions = { new Vector2(-222f, 36f), new Vector2(222f, 36f), new Vector2(666f, 36f) };
            Vector2[] preShiftPositions = { new Vector2(-444f, 36f), new Vector2(0f, 36f), new Vector2(444f, 36f) };
            for (int i = 0; i < slotButtons.Length; i++)
            {
                RectTransform rect = slotButtons[i].transform as RectTransform;
                if (rect == null)
                {
                    return $"슬롯 {i + 1}에 RectTransform이 없습니다";
                }

                if (Approximately(rect.anchoredPosition, preShiftPositions[i]))
                {
                    rect.anchoredPosition = finalPositions[i];
                    EditorUtility.SetDirty(rect);
                }
                else if (!Approximately(rect.anchoredPosition, finalPositions[i]))
                {
                    return $"슬롯 {i + 1} 위치 {rect.anchoredPosition}이 예상값({preShiftPositions[i]} 또는 {finalPositions[i]})과 다릅니다";
                }
            }

            foreach (Button slot in slotButtons)
            {
                AttachCardView(slot);
            }

            Button autoSaveButton = GetOrCreateAutoSaveCardFromTemplate(slotButtons[0].transform.parent, slotButtons[0], new Vector2(-666f, 36f));
            AttachCardView(autoSaveButton);
            TextMeshProUGUI autoSaveText = autoSaveButton.GetComponentInChildren<TextMeshProUGUI>(true);

            // 삭제 버튼은 이 프리팹에 이미 저작된 DeleteAutoSaveButton(레거시 Text 라벨)을 원본으로 복제해 카드 하위에 둔다.
            Button autoSaveDeleteButton = bridgeSo.FindProperty("autoSaveDeleteButton").objectReferenceValue as Button;
            if (autoSaveDeleteButton == null)
            {
                return "autoSaveDeleteButton 참조가 비어 있습니다";
            }

            MoveDeleteUnderCard(autoSaveDeleteButton, autoSaveButton.transform);
            Button[] deleteButtons = new Button[slotButtons.Length];
            for (int i = 0; i < slotButtons.Length; i++)
            {
                deleteButtons[i] = GetOrCreateDeleteButtonClone(autoSaveDeleteButton, slotButtons[i].transform, $"DeleteSlot{i + 1}");
            }

            bridgeSo.FindProperty("autoSaveButton").objectReferenceValue = autoSaveButton;
            bridgeSo.FindProperty("autoSaveText").objectReferenceValue = autoSaveText;
            WriteButtonArray(bridgeSo, "deleteButtons", deleteButtons);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            WireView(bridge, bridgeSo, autoSaveButton, autoSaveText, slotButtons, slotTexts, deleteButtons, autoSaveDeleteButton);
            return null;
        }

        /// <summary>브리지와 같은 오브젝트의 SaveSlotsView를 만들거나 찾아 실효 참조를 연결한다.</summary>
        private static void WireView(SaveSlotsUI bridge, SerializedObject bridgeSo, Button autoSaveButton,
            TextMeshProUGUI autoSaveText, Button[] slotButtons, TextMeshProUGUI[] slotTexts,
            Button[] deleteButtons, Button autoSaveDeleteButton)
        {
            SaveSlotsView view = bridge.GetComponent<SaveSlotsView>();
            if (view == null)
            {
                view = bridge.gameObject.AddComponent<SaveSlotsView>();
            }

            SerializedObject viewSo = new SerializedObject(view);
            CopyReference(bridgeSo, "panel", viewSo, "m_panel");
            CopyReference(bridgeSo, "mainPanel", viewSo, "m_mainPanel");
            CopyReference(bridgeSo, "openSaveSlotsButton", viewSo, "m_openButton");
            CopyReference(bridgeSo, "title", viewSo, "m_title");
            viewSo.FindProperty("m_autoSaveButton").objectReferenceValue = autoSaveButton;
            viewSo.FindProperty("m_autoSaveText").objectReferenceValue = autoSaveText;
            WriteButtonArray(viewSo, "m_slotButtons", slotButtons);
            WriteTextArray(viewSo, "m_slotTexts", slotTexts);
            WriteButtonArray(viewSo, "m_deleteButtons", deleteButtons);
            viewSo.FindProperty("m_autoSaveDeleteButton").objectReferenceValue = autoSaveDeleteButton;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        /// <summary>카드 버튼에 SaveSlotCardView를 붙이고 버튼·라벨 참조를 연결한다(멱등).</summary>
        private static void AttachCardView(Button cardButton)
        {
            SaveSlotCardView card = cardButton.GetComponent<SaveSlotCardView>();
            if (card == null)
            {
                card = cardButton.gameObject.AddComponent<SaveSlotCardView>();
            }

            SerializedObject so = new SerializedObject(card);
            so.FindProperty("m_button").objectReferenceValue = cardButton;
            so.FindProperty("m_label").objectReferenceValue = cardButton.GetComponentInChildren<TextMeshProUGUI>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(card);
        }

        /// <summary>
        /// 자동저장 카드를 템플릿 슬롯 복제로 만든다. 복제 원본이 남긴 persistent 호출(SelectSlot)은
        /// 기존 EnsureAutoSaveSlot이 리스너를 끄던 것과 같은 목적으로 지운다.
        /// manualPosition이 null이면(레이아웃 그룹 부모) 위치를 건드리지 않고, 아니면 그 위치에 둔다.
        /// </summary>
        private static Button GetOrCreateAutoSaveCardFromTemplate(Transform cardsParent, Button template, Vector2? manualPosition)
        {
            Transform existing = cardsParent.Find("AutoSaveSlot");
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            GameObject card = Object.Instantiate(template.gameObject, cardsParent);
            card.name = "AutoSaveSlot";
            Button button = card.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                EditorUtility.SetDirty(button);
            }

            TextMeshProUGUI label = card.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "자동 저장\n자동 저장 기록 없음";
                EditorUtility.SetDirty(label);
            }

            if (manualPosition.HasValue && card.transform is RectTransform rect)
            {
                rect.anchoredPosition = manualPosition.Value;
                EditorUtility.SetDirty(rect);
            }

            card.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
            EditorUtility.SetDirty(card);
            return button;
        }

        /// <summary>
        /// UIManager.prefab용 삭제 버튼을 카드 하위에 만든다. 기존 CreateDeleteButton 런타임 결과와 같은
        /// 색·크기·폰트·내비게이션(없음)을 쓰고, 카드 폭을 따라가는 스트레치 앵커로 레이아웃 계산을 대체한다.
        /// </summary>
        private static Button GetOrCreateDeleteButtonChild(Transform card, string objectName, TMP_FontAsset font, float fontSize)
        {
            Transform existing = card.Find(objectName);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = card.gameObject.layer;
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.SetParent(card, false);
            ApplyDeleteRowAnchors(rect, DeleteRowCenterY, DeleteHeight);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.48f, 0.12f, 0.13f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = new Color(0.48f, 0.12f, 0.13f, 1f),
                highlightedColor = new Color(0.68f, 0.16f, 0.17f, 1f),
                pressedColor = new Color(0.32f, 0.08f, 0.09f, 1f),
                selectedColor = new Color(0.56f, 0.14f, 0.15f, 1f),
                disabledColor = new Color(0.25f, 0.22f, 0.22f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };
            button.onClick = new Button.ButtonClickedEvent();

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.layer = buttonObject.layer;
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = "삭제";
            label.fontSize = fontSize;
            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            label.color = Color.white;
            label.raycastTarget = false;

            buttonObject.SetActive(false);
            EditorUtility.SetDirty(button);
            return button;
        }

        /// <summary>
        /// StartUI_Manager.prefab용 삭제 버튼을 기존 저작물(DeleteAutoSaveButton) 복제로 만든다.
        /// 색·라벨 스타일이 원본과 같게 유지되며 위치는 카드 폭을 따라가는 스트레치 앵커로 바꿔 둔다.
        /// </summary>
        private static Button GetOrCreateDeleteButtonClone(Button source, Transform card, string objectName)
        {
            Transform existing = card.Find(objectName);
            if (existing != null)
            {
                return existing.GetComponent<Button>();
            }

            GameObject clone = Object.Instantiate(source.gameObject, card);
            clone.name = objectName;
            if (clone.transform is RectTransform rect)
            {
                ApplyDeleteRowAnchors(rect, StartUIDeleteRowCenterY, StartUIDeleteHeight);
                EditorUtility.SetDirty(rect);
            }

            Button button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                EditorUtility.SetDirty(button);
            }

            clone.SetActive(false);
            EditorUtility.SetDirty(clone);
            return button;
        }

        /// <summary>기존 DeleteAutoSaveButton(패널 절대 위치)을 자동저장 카드 하위로 옮겨 카드와 함께 움직이게 한다.</summary>
        private static void MoveDeleteUnderCard(Button deleteButton, Transform card)
        {
            if (deleteButton.transform.parent == card)
            {
                return;
            }

            deleteButton.transform.SetParent(card, false);
            if (deleteButton.transform is RectTransform rect)
            {
                ApplyDeleteRowAnchors(rect, StartUIDeleteRowCenterY, StartUIDeleteHeight);
                EditorUtility.SetDirty(rect);
            }

            EditorUtility.SetDirty(deleteButton);
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }

        /// <summary>삭제 버튼 앵커: 카드 폭 전체 스트레치, 세로 위치·높이는 호출자가 넘긴다.</summary>
        private static void ApplyDeleteRowAnchors(RectTransform rect, float centerY, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, centerY - height * 0.5f);
            rect.offsetMax = new Vector2(0f, centerY + height * 0.5f);
        }

        /// <summary>카드의 LayoutElement preferred를 레이아웃 그룹이 쓰는 값(255x172.5)으로 맞춘다.</summary>
        private static void ApplyCardLayoutElement(Transform card)
        {
            LayoutElement element = card.GetComponent<LayoutElement>();
            if (element == null)
            {
                return;
            }

            if (!Mathf.Approximately(element.preferredWidth, CardWidth))
            {
                element.preferredWidth = CardWidth;
            }

            if (!Mathf.Approximately(element.preferredHeight, CardHeight))
            {
                element.preferredHeight = CardHeight;
            }

            EditorUtility.SetDirty(element);
        }

        private static void CopyReference(SerializedObject source, string sourceProperty, SerializedObject target, string targetProperty)
        {
            SerializedProperty from = source.FindProperty(sourceProperty);
            if (from != null && from.objectReferenceValue != null)
            {
                target.FindProperty(targetProperty).objectReferenceValue = from.objectReferenceValue;
            }
        }

        private static bool HasSlots(Button[] slots)
        {
            return slots != null && slots.Length >= 3 && slots[0] != null && slots[1] != null && slots[2] != null;
        }

        private static bool HasTexts(TextMeshProUGUI[] texts)
        {
            return texts != null && texts.Length >= 3 && texts[0] != null && texts[1] != null && texts[2] != null;
        }

        private static Button[] ReadButtonArray(SerializedObject so, string property)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null || !prop.isArray)
            {
                return null;
            }

            Button[] result = new Button[prop.arraySize];
            for (int i = 0; i < prop.arraySize; i++)
            {
                result[i] = prop.GetArrayElementAtIndex(i).objectReferenceValue as Button;
            }

            return result;
        }

        private static TextMeshProUGUI[] ReadTextArray(SerializedObject so, string property)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null || !prop.isArray)
            {
                return null;
            }

            TextMeshProUGUI[] result = new TextMeshProUGUI[prop.arraySize];
            for (int i = 0; i < prop.arraySize; i++)
            {
                result[i] = prop.GetArrayElementAtIndex(i).objectReferenceValue as TextMeshProUGUI;
            }

            return result;
        }

        private static void WriteButtonArray(SerializedObject so, string property, Button[] buttons)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null || !prop.isArray)
            {
                return;
            }

            prop.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }
        }

        private static void WriteTextArray(SerializedObject so, string property, TextMeshProUGUI[] texts)
        {
            SerializedProperty prop = so.FindProperty(property);
            if (prop == null || !prop.isArray)
            {
                return;
            }

            prop.arraySize = texts.Length;
            for (int i = 0; i < texts.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
            }
        }
    }
}
