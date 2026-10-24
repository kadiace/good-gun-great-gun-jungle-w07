using System;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

namespace WeaponWorkshop.Editor
{
    /// <summary>
    /// UI Wave 12 Layout 저작 명령.
    /// 1. 기능 화면(ModulePalette, ColorPicker)을 HudLayer에서 ScreenLayer로 이동
    /// 2. 불필요한 중첩 Canvas(DaySettlementUI의 CanvasScaler·Canvas)를 제거하고 D03 필수 Canvas(PausePanel 200, YesNoUI 150)는 보존
    /// 3. 주요 조작 화면에 중앙 최대 너비(1920 기준 공간) 제한 및 CenterMaxWidth 컴포넌트 저작
    /// 대상: Assets/Prefabs/Managers/UIManager.prefab, Assets/Prefabs/UI/StartUI_Manager.prefab
    /// </summary>
    public static class LayoutAuthoring
    {
        private const string MENU_PATH = "Tools/UI/Authoring/Layout/Author";
        private const string STEP_NAME = "Layout/Author";
        private const string UI_MANAGER_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string START_UI_PATH = "Assets/Prefabs/UI/StartUI_Manager.prefab";

        private const float BASE_MAX_WIDTH = 1920f;
        private const float YES_NO_CARD_WIDTH = 500f;
        private const float YES_NO_CARD_HEIGHT = 216f;
        private const float NOTIFICATION_WIDTH = 1470f;
        private const float NOTIFICATION_HEIGHT = 48f;
        private const float NOTIFICATION_Y = 90f;

        [MenuItem(MENU_PATH)]
        public static void Author()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": 에디터(Edit) 모드에서만 실행할 수 있습니다.");
                return;
            }

            var summaries = new List<string>();

            try
            {
                bool uiManagerOk = AuthorUIManager(summaries);
                if (!uiManagerOk)
                {
                    return;
                }

                bool startUiOk = AuthorStartUI(summaries);
                if (!startUiOk)
                {
                    return;
                }

                Debug.Log("[UIAuthoring] OK " + STEP_NAME + ": " + string.Join("; ", summaries));
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": 예외 발생 " + exception.Message + "\n" + exception.StackTrace);
            }
        }

        private static bool AuthorUIManager(List<string> _summaries)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(UI_MANAGER_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": UIManager 프리팹을 찾을 수 없습니다: " + UI_MANAGER_PATH);
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(UI_MANAGER_PATH);
            try
            {
                var changes = new List<string>();

                Transform screenLayer = FindScreenLayer(root);
                if (screenLayer == null)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": UIManager에서 ScreenLayer를 찾을 수 없습니다.");
                    return false;
                }

                // 1. HudLayer -> ScreenLayer 이동 (기능 상호작용 화면)
                AuthorLayerHierarchy(root, screenLayer, changes);

                // 2. 중첩 Canvas 검토 및 정리 (DaySettlementUI 스케일러·캔버스 제거, PausePanel·YesNoUI D03 순서 유지)
                AuthorNestedCanvases(root, changes);

                // 3. 중앙 최대 너비 1920 제한 (비율 앵커 제거, CenterMaxWidth 부착)
                AuthorCenterMaxWidth(root, changes);

                // 프리팹 저장
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, UI_MANAGER_PATH);
                if (saved == null)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": UIManager 프리팹 저장 실패: " + UI_MANAGER_PATH);
                    return false;
                }

                string summary = changes.Count > 0
                    ? "UIManager (" + string.Join(", ", changes) + ")"
                    : "UIManager (기존 저작 상태 유지, 변경 0건)";
                _summaries.Add(summary);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AuthorStartUI(List<string> _summaries)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(START_UI_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": StartUI_Manager 프리팹을 찾을 수 없습니다: " + START_UI_PATH);
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(START_UI_PATH);
            try
            {
                var changes = new List<string>();

                // 1. LoadSlotsPanel 중앙 최대 너비 1920
                Transform loadSlots = root.transform.Find("Canvas/LoadSlotsPanel");
                if (loadSlots != null)
                {
                    if (EnsureCenterMaxWidth(loadSlots.gameObject, BASE_MAX_WIDTH, true))
                    {
                        changes.Add("LoadSlotsPanel CenterMaxWidth(1920)");
                    }
                }

                // 2. YesNoUI/Card 중앙 최대 너비 570
                Transform yesNoCard = root.transform.Find("YesNoUI/Card");
                if (yesNoCard != null)
                {
                    if (EnsureCenterMaxWidth(yesNoCard.gameObject, 570f, false))
                    {
                        changes.Add("YesNoUI/Card CenterMaxWidth(570)");
                    }
                }

                // 3. NotificationText 중앙 최대 너비 640
                Transform notif = root.transform.Find("NotificationText");
                if (notif != null)
                {
                    if (EnsureCenterMaxWidth(notif.gameObject, 640f, false))
                    {
                        changes.Add("NotificationText CenterMaxWidth(640)");
                    }
                }

                // 4. 중첩 Canvas 검증 (YesNoUI order 150 유지)
                Transform yesNoUI = root.transform.Find("YesNoUI");
                if (yesNoUI != null)
                {
                    Canvas yesNoCanvas = yesNoUI.GetComponent<Canvas>();
                    if (yesNoCanvas != null)
                    {
                        if (!yesNoCanvas.overrideSorting || yesNoCanvas.sortingOrder != 150)
                        {
                            yesNoCanvas.overrideSorting = true;
                            yesNoCanvas.sortingOrder = 150;
                            EditorUtility.SetDirty(yesNoCanvas);
                            changes.Add("YesNoUI Canvas(order 150) 보정");
                        }
                    }
                }

                // 프리팹 저장
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, START_UI_PATH);
                if (saved == null)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": StartUI_Manager 프리팹 저장 실패: " + START_UI_PATH);
                    return false;
                }

                string summary = changes.Count > 0
                    ? "StartUI (" + string.Join(", ", changes) + ")"
                    : "StartUI (기존 저작 상태 유지, 변경 0건)";
                _summaries.Add(summary);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AuthorLayerHierarchy(GameObject _root, Transform _screenLayer, List<string> _changes)
        {
            // ModulePalette: 기능 화면을 HudLayer에서 ScreenLayer로 이동
            ModulePaletteUI palette = _root.GetComponentInChildren<ModulePaletteUI>(true);
            if (palette != null)
            {
                Transform paletteTransform = palette.transform;
                if (paletteTransform.parent != _screenLayer)
                {
                    paletteTransform.SetParent(_screenLayer, false);
                    EditorUtility.SetDirty(paletteTransform);
                    _changes.Add("ModulePalette: HudLayer -> ScreenLayer 이동");
                }
            }

            // ColorPicker: 탄색 모듈 선택 창을 HudLayer에서 ScreenLayer로 이동
            ColorPickerUI picker = _root.GetComponentInChildren<ColorPickerUI>(true);
            if (picker != null)
            {
                Transform pickerTransform = picker.transform;
                if (pickerTransform.parent != _screenLayer)
                {
                    pickerTransform.SetParent(_screenLayer, false);
                    EditorUtility.SetDirty(pickerTransform);
                    _changes.Add("ColorPicker: HudLayer -> ScreenLayer 이동");
                }
            }

            // ScreenLayer 내 그리기 순서 보장: ColorPicker가 열렸을 때 ModulePalette 위에 그려지도록
            if (palette != null && picker != null)
            {
                int paletteIndex = palette.transform.GetSiblingIndex();
                int pickerIndex = picker.transform.GetSiblingIndex();
                if (pickerIndex < paletteIndex)
                {
                    picker.transform.SetSiblingIndex(paletteIndex + 1);
                    EditorUtility.SetDirty(picker.transform);
                    _changes.Add("ColorPicker 순서를 ModulePalette 뒤로 배치");
                }
            }
        }

        private static void AuthorNestedCanvases(GameObject _root, List<string> _changes)
        {
            // DaySettlementUI: 불필요한 중첩 CanvasScaler 및 Canvas(order 100) 제거
            // 루트가 아닌 CanvasScaler는 무시되며, Canvas order 100은 PopupLayer 요소(PresetChoice, TutorialSkip, NotificationText)보다
            // 위에 그려지는 역전 문제를 일으켰음. 제거하여 ScreenLayer 계층 순서 렌더링으로 통일.
            DaySettlementUI settlement = _root.GetComponentInChildren<DaySettlementUI>(true);
            if (settlement != null)
            {
                CanvasScaler scaler = settlement.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    UnityEngine.Object.DestroyImmediate(scaler, true);
                    EditorUtility.SetDirty(settlement.gameObject);
                    _changes.Add("DaySettlementUI: CanvasScaler 제거");
                }

                GraphicRaycaster raycaster = settlement.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                {
                    UnityEngine.Object.DestroyImmediate(raycaster, true);
                    EditorUtility.SetDirty(settlement.gameObject);
                    _changes.Add("DaySettlementUI: GraphicRaycaster 제거");
                }

                Canvas canvas = settlement.GetComponent<Canvas>();
                if (canvas != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvas, true);
                    EditorUtility.SetDirty(settlement.gameObject);
                    _changes.Add("DaySettlementUI: Canvas(order 100) 제거");
                }
            }

            // PausePanel: Canvas(order 200) 유지 (D03 계약: 대화 150 위에 Pause 200이 표시되어야 함)
            Transform pausePanel = FindChildRecursive(_root.transform, "PausePanel");
            if (pausePanel != null)
            {
                Canvas pauseCanvas = pausePanel.GetComponent<Canvas>();
                if (pauseCanvas != null)
                {
                    if (!pauseCanvas.overrideSorting || pauseCanvas.sortingOrder != 200)
                    {
                        pauseCanvas.overrideSorting = true;
                        pauseCanvas.sortingOrder = 200;
                        EditorUtility.SetDirty(pauseCanvas);
                        _changes.Add("PausePanel: Canvas order 200 보정");
                    }
                }
            }

            // YesNoUI: Canvas(order 150) 유지 (D03 계약: Pause 중 300으로 상향, 대출 안내 대화 151과 중첩 계약)
            Transform yesNoUI = FindChildRecursive(_root.transform, "YesNoUI");
            if (yesNoUI != null)
            {
                Canvas yesNoCanvas = yesNoUI.GetComponent<Canvas>();
                if (yesNoCanvas != null)
                {
                    if (!yesNoCanvas.overrideSorting || yesNoCanvas.sortingOrder != 150)
                    {
                        yesNoCanvas.overrideSorting = true;
                        yesNoCanvas.sortingOrder = 150;
                        EditorUtility.SetDirty(yesNoCanvas);
                        _changes.Add("YesNoUI: Canvas order 150 보정");
                    }
                }
            }
        }

        private static void AuthorCenterMaxWidth(GameObject _root, List<string> _changes)
        {
            // 1. ModulePalette: 21:9에서 Hotbar(0.08~0.92)·Palette 비율 크기가 왜곡되지 않도록 루트에 CenterMaxWidth 적용
            ModulePaletteUI palette = _root.GetComponentInChildren<ModulePaletteUI>(true);
            if (palette != null)
            {
                if (EnsureCenterMaxWidth(palette.gameObject, BASE_MAX_WIDTH, true))
                {
                    _changes.Add("ModulePalette CenterMaxWidth(1920)");
                }
            }

            // 2. TutorialSkipPanel/Card: 21:9에서 내부 버튼·메시지(0.3~0.7, 0.4~0.6)가 늘어나지 않도록 Card에 CenterMaxWidth 적용
            Transform tutorialSkip = FindChildRecursive(_root.transform, "TutorialSkipPanel");
            if (tutorialSkip != null)
            {
                Transform card = tutorialSkip.Find("Card");
                if (card != null)
                {
                    if (EnsureCenterMaxWidth(card.gameObject, BASE_MAX_WIDTH, true))
                    {
                        _changes.Add("TutorialSkipPanel/Card CenterMaxWidth(1920)");
                    }
                }
            }

            // 3. YesNoUI/Card: 기존 비율 앵커(0.37~0.63)를 중앙 고정 크기(500x216)로 변환하고 CenterMaxWidth 적용
            Transform yesNoUI = FindChildRecursive(_root.transform, "YesNoUI");
            if (yesNoUI != null)
            {
                Transform card = yesNoUI.Find("Card");
                if (card != null)
                {
                    RectTransform cardRt = card as RectTransform;
                    if (cardRt != null)
                    {
                        bool isProportional = !Mathf.Approximately(cardRt.anchorMin.x, 0.5f) ||
                                              !Mathf.Approximately(cardRt.anchorMax.x, 0.5f);
                        if (isProportional)
                        {
                            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                            cardRt.sizeDelta = new Vector2(YES_NO_CARD_WIDTH, YES_NO_CARD_HEIGHT);
                            cardRt.anchoredPosition = Vector2.zero;
                            EditorUtility.SetDirty(cardRt);
                            _changes.Add("YesNoUI/Card 비율 앵커 -> 중앙 고정(500x216) 전환");
                        }
                    }

                    if (EnsureCenterMaxWidth(card.gameObject, YES_NO_CARD_WIDTH, false))
                    {
                        _changes.Add("YesNoUI/Card CenterMaxWidth(500)");
                    }
                }
            }

            // 4. NotificationText: stretch(-450)를 중앙 고정 크기(1470x48)로 변환하고 CenterMaxWidth 적용
            Transform notif = FindChildRecursive(_root.transform, "NotificationText");
            if (notif != null && notif.parent != null && notif.parent.name == "PopupLayer")
            {
                RectTransform notifRt = notif as RectTransform;
                if (notifRt != null)
                {
                    bool isStretch = Mathf.Approximately(notifRt.anchorMin.x, 0f) &&
                                     Mathf.Approximately(notifRt.anchorMax.x, 1f);
                    if (isStretch)
                    {
                        notifRt.anchorMin = new Vector2(0.5f, 0f);
                        notifRt.anchorMax = new Vector2(0.5f, 0f);
                        notifRt.sizeDelta = new Vector2(NOTIFICATION_WIDTH, NOTIFICATION_HEIGHT);
                        notifRt.anchoredPosition = new Vector2(0f, NOTIFICATION_Y);
                        EditorUtility.SetDirty(notifRt);
                        _changes.Add("NotificationText stretch -> 중앙 고정(1470x48) 전환");
                    }
                }

                if (EnsureCenterMaxWidth(notif.gameObject, NOTIFICATION_WIDTH, false))
                {
                    _changes.Add("NotificationText CenterMaxWidth(1470)");
                }
            }

            // 5. 기타 주요 조작 화면의 카드·창에 CenterMaxWidth 적용 (기존 크기 유지 및 1920 상한 보장)
            // PausePanel 하위
            Transform pausePanel = FindChildRecursive(_root.transform, "PausePanel");
            if (pausePanel != null)
            {
                Transform mainPanel = pausePanel.Find("MainPanel");
                if (mainPanel != null && EnsureCenterMaxWidth(mainPanel.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("MainPanel CenterMaxWidth(1920)");
                }

                Transform soundSettings = pausePanel.Find("SoundSettingsPanel");
                if (soundSettings != null && EnsureCenterMaxWidth(soundSettings.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("SoundSettingsPanel CenterMaxWidth(1920)");
                }

                Transform saveSlots = pausePanel.Find("SaveSlotsPanel");
                if (saveSlots != null && EnsureCenterMaxWidth(saveSlots.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("SaveSlotsPanel CenterMaxWidth(1920)");
                }
            }

            // DaySettlementUI 하위 정산 카드
            Transform settlementCard = FindChildRecursive(_root.transform, "SettlementPanel");
            if (settlementCard != null)
            {
                Transform card = settlementCard.Find("Card");
                if (card != null && EnsureCenterMaxWidth(card.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("Settlement Card CenterMaxWidth(1920)");
                }
            }

            // TargetSelection 하위 Window
            Transform targetSelection = FindChildRecursive(_root.transform, "TargetSelection");
            if (targetSelection != null)
            {
                Transform window = targetSelection.Find("Window");
                if (window != null && EnsureCenterMaxWidth(window.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("TargetSelection/Window CenterMaxWidth(1920)");
                }
            }

            // WeaponCodePanel 하위 Card
            Transform weaponCode = FindChildRecursive(_root.transform, "WeaponCodePanel");
            if (weaponCode != null)
            {
                Transform card = weaponCode.Find("Card");
                if (card != null && EnsureCenterMaxWidth(card.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("WeaponCodePanel/Card CenterMaxWidth(1920)");
                }
            }

            // PresetChoicePanel 하위 Card
            Transform presetChoice = FindChildRecursive(_root.transform, "PresetChoicePanel");
            if (presetChoice != null)
            {
                Transform card = presetChoice.Find("Card");
                if (card != null && EnsureCenterMaxWidth(card.gameObject, BASE_MAX_WIDTH, false))
                {
                    _changes.Add("PresetChoicePanel/Card CenterMaxWidth(1920)");
                }
            }
        }

        private static bool EnsureCenterMaxWidth(GameObject _go, float _maxWidth, bool _applySideMargins)
        {
            if (_go == null)
            {
                return false;
            }

            bool added = false;
            CenterMaxWidth comp = _go.GetComponent<CenterMaxWidth>();
            if (comp == null)
            {
                comp = _go.AddComponent<CenterMaxWidth>();
                added = true;
            }

            SerializedObject so = new SerializedObject(comp);
            SerializedProperty maxWidthProp = so.FindProperty("m_maxWidth");
            SerializedProperty sideMarginsProp = so.FindProperty("m_applySideMargins");

            bool modified = added;
            if (maxWidthProp != null && !Mathf.Approximately(maxWidthProp.floatValue, _maxWidth))
            {
                maxWidthProp.floatValue = _maxWidth;
                modified = true;
            }

            if (sideMarginsProp != null && sideMarginsProp.boolValue != _applySideMargins)
            {
                sideMarginsProp.boolValue = _applySideMargins;
                modified = true;
            }

            if (modified)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_go);
            }

            return added;
        }

        private static Transform FindScreenLayer(GameObject _root)
        {
            UIRoot uiRoot = _root.GetComponentInChildren<UIRoot>(true);
            if (uiRoot != null && uiRoot.ScreenLayer != null)
            {
                return uiRoot.ScreenLayer;
            }

            return FindChildRecursive(_root.transform, "ScreenLayer");
        }

        private static Transform FindChildRecursive(Transform _parent, string _name)
        {
            if (_parent == null)
            {
                return null;
            }

            if (_parent.name == _name)
            {
                return _parent;
            }

            for (int i = 0; i < _parent.childCount; i++)
            {
                Transform found = FindChildRecursive(_parent.GetChild(i), _name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
