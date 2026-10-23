using System;
using System.Collections.Generic;

using TMPro;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// 전 화면 Selectable의 명시적 Navigation(Mode.Explicit, wrapAround=false)을 저작합니다.
    /// 대상: UIManager.prefab, StartUI_Manager.prefab, NightRaidUI.prefab, Dialogue.prefab,
    ///       Screens/Retire.prefab, Screens/EndChestEnding.prefab.
    /// 멱등성: 기존 설정과 일치하면 저장하지 않고 건너뜁니다.
    /// </summary>
    public static class NavigationAuthoring
    {
        private const string MENU_PATH = "Tools/UI/Authoring/Navigation/Author";
        private const string LOG_PREFIX = "[UIAuthoring] ";

        private const string UI_MANAGER_PREFAB = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string START_UI_PREFAB = "Assets/Prefabs/UI/StartUI_Manager.prefab";
        private const string NIGHT_RAID_PREFAB = "Assets/Prefabs/UI/NightRaidUI.prefab";
        private const string DIALOGUE_PREFAB = "Assets/Prefabs/UI/Dialogue.prefab";
        private const string RETIRE_PREFAB = "Assets/Prefabs/UI/Screens/Retire.prefab";
        private const string END_CHEST_PREFAB = "Assets/Prefabs/UI/Screens/EndChestEnding.prefab";

        [MenuItem(MENU_PATH)]
        public static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail("플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            if (!EnsureNoDirtyScene(out string sceneReason))
            {
                Fail(sceneReason);
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Fail("프리팹 스테이지가 열려 있습니다. 프리팹 스테이지를 닫고 다시 실행하세요.");
                return;
            }

            if (!AssetExists(UI_MANAGER_PREFAB) || !AssetExists(START_UI_PREFAB) ||
                !AssetExists(NIGHT_RAID_PREFAB) || !AssetExists(DIALOGUE_PREFAB) ||
                !AssetExists(RETIRE_PREFAB) || !AssetExists(END_CHEST_PREFAB))
            {
                Fail("필수 프리팹 자산이 누락되었습니다.");
                return;
            }

            try
            {
                string dialogue = AuthorDialoguePrefab();
                string nightRaid = AuthorNightRaidPrefab();
                string startUI = AuthorStartUIPrefab();
                string retire = AuthorRetirePrefab();
                string endChest = AuthorEndChestPrefab();
                string uiManager = AuthorUIManagerPrefab();

                Debug.Log(LOG_PREFIX + "OK Navigation/Author " + dialogue + ", " + nightRaid + ", " +
                          startUI + ", " + retire + ", " + endChest + ", " + uiManager);
            }
            catch (Exception exception)
            {
                Fail(exception.Message + "\n" + exception.StackTrace);
            }
        }

        private static string AuthorDialoguePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DIALOGUE_PREFAB);
            try
            {
                bool changed = false;
                Button answerButton = FindChildButton(root.transform, "Answer");
                if (answerButton == null)
                {
                    answerButton = root.GetComponentInChildren<Button>(true);
                }

                if (answerButton != null)
                {
                    changed |= ApplySingleNavigation(answerButton);
                }

                if (changed)
                {
                    SavePrefab(root, DIALOGUE_PREFAB);
                }

                return "Dialogue" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorRetirePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RETIRE_PREFAB);
            try
            {
                bool changed = false;
                Button continueButton = FindChildButton(root.transform, "ContinueButton");
                Button lobbyButton = FindChildButton(root.transform, "LobbyButton");

                if (lobbyButton != null && continueButton != null)
                {
                    changed |= ApplyVerticalNavigation(lobbyButton, continueButton);
                }

                if (changed)
                {
                    SavePrefab(root, RETIRE_PREFAB);
                }

                return "Retire" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorEndChestPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(END_CHEST_PREFAB);
            try
            {
                bool changed = false;
                Button continueButton = FindChildButton(root.transform, "ContinueButton");
                Button quitButton = FindChildButton(root.transform, "QuitButton");

                if (continueButton != null && quitButton != null)
                {
                    changed |= ApplyVerticalNavigation(continueButton, quitButton);
                }

                if (changed)
                {
                    SavePrefab(root, END_CHEST_PREFAB);
                }

                return "EndChest" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorNightRaidPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NIGHT_RAID_PREFAB);
            try
            {
                bool changed = false;

                Button region1 = FindChildButton(root.transform, "MapPanel/DistrictMap/Region1");
                Button region2 = FindChildButton(root.transform, "MapPanel/DistrictMap/Region2");
                Button region3 = FindChildButton(root.transform, "MapPanel/DistrictMap/Region3");
                Button region4 = FindChildButton(root.transform, "MapPanel/DistrictMap/Region4");
                // Region5는 RegionInfoPanel 아래에 있고 지도 핀으로 연결되지 않아 탐색에서 제외한다(숨김 버튼으로 선택이 이동하지 않게).
                Button region5 = null;
                Button back = FindChildButton(root.transform, "MapPanel/DistrictMap/Back");

                if (region1 != null && region2 != null && region3 != null && region4 != null)
                {
                    Selectable rightColTop = region2;
                    Selectable rightColBottom = region4;
                    Selectable farRightTop = back;
                    Selectable farRightBottom = region5;

                    changed |= ApplyExplicitNavigation(region1, null, region3, null, rightColTop);
                    changed |= ApplyExplicitNavigation(region2, null, region4, region1, farRightTop);
                    changed |= ApplyExplicitNavigation(region3, region1, null, null, region4);
                    changed |= ApplyExplicitNavigation(region4, region2, null, region3, farRightBottom != null ? farRightBottom : farRightTop);

                    if (farRightTop != null)
                    {
                        changed |= ApplyExplicitNavigation(farRightTop, null, farRightBottom, region2, null);
                    }

                    if (farRightBottom != null)
                    {
                        changed |= ApplyExplicitNavigation(farRightBottom, farRightTop, null, region4, null);
                    }
                }

                Button confirm = FindChildButton(root.transform, "RaidResultPanel/ResultContent/Confirm");
                if (confirm != null)
                {
                    changed |= ApplySingleNavigation(confirm);
                }

                if (changed)
                {
                    SavePrefab(root, NIGHT_RAID_PREFAB);
                }

                return "NightRaid" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorStartUIPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(START_UI_PREFAB);
            try
            {
                bool changed = false;

                changed |= AuthorSaveSlotsHierarchy(root.transform);
                changed |= AuthorYesNoHierarchy(root.transform);

                if (changed)
                {
                    SavePrefab(root, START_UI_PREFAB);
                }

                return "StartUI" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorUIManagerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UI_MANAGER_PREFAB);
            try
            {
                bool changed = false;

                changed |= AuthorWorkshopOrientation(root);
                changed |= AuthorWeaponName(root);
                changed |= AuthorWeaponInfoRecipe(root);
                changed |= AuthorSaveSlotsHierarchy(root.transform);
                changed |= AuthorYesNoHierarchy(root.transform);
                changed |= AuthorSoundSettings(root);
                changed |= AuthorCustomization(root);
                changed |= AuthorMouseSensitivity(root);
                changed |= AuthorTargetSelection(root);

                if (changed)
                {
                    SavePrefab(root, UI_MANAGER_PREFAB);
                }

                return "UIManager" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AuthorWorkshopOrientation(GameObject _root)
        {
            bool changed = false;
            WorkshopOrientationView view = _root.GetComponentInChildren<WorkshopOrientationView>(true);
            if (view == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(view);
            Button front = so.FindProperty("m_frontButton")?.objectReferenceValue as Button;
            Button up = so.FindProperty("m_upButton")?.objectReferenceValue as Button;
            Button right = so.FindProperty("m_rightButton")?.objectReferenceValue as Button;
            Button back = so.FindProperty("m_backButton")?.objectReferenceValue as Button;
            Button down = so.FindProperty("m_downButton")?.objectReferenceValue as Button;
            Button left = so.FindProperty("m_leftButton")?.objectReferenceValue as Button;
            Button axisMenuToggle = so.FindProperty("m_axisMenuButton")?.objectReferenceValue as Button;

            if (front != null && up != null && right != null && back != null && down != null && left != null)
            {
                changed |= ApplyExplicitNavigation(front, up, down, left, right);
                changed |= ApplyExplicitNavigation(up, null, front, left, right);
                changed |= ApplyExplicitNavigation(down, front, axisMenuToggle, left, right);
                changed |= ApplyExplicitNavigation(left, up, down, null, front);
                changed |= ApplyExplicitNavigation(right, up, down, front, null);
                changed |= ApplyExplicitNavigation(back, up, front, left, right);

                if (axisMenuToggle != null)
                {
                    Navigation nav = axisMenuToggle.navigation;
                    if (nav.selectOnUp != down || nav.mode != Navigation.Mode.Explicit || nav.wrapAround)
                    {
                        nav.mode = Navigation.Mode.Explicit;
                        nav.wrapAround = false;
                        nav.selectOnUp = down;
                        axisMenuToggle.navigation = nav;
                        changed = true;
                    }
                }
            }

            SerializedProperty menuButtonsProp = so.FindProperty("m_axisMenuButtons");
            if (menuButtonsProp != null && menuButtonsProp.arraySize == 6)
            {
                Button[] menuButtons = new Button[6];
                for (int i = 0; i < 6; i++)
                {
                    menuButtons[i] = menuButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                }

                if (menuButtons[0] != null && menuButtons[1] != null && menuButtons[2] != null &&
                    menuButtons[3] != null && menuButtons[4] != null && menuButtons[5] != null)
                {
                    Button mFront = menuButtons[0];
                    Button mUp = menuButtons[1];
                    Button mRight = menuButtons[2];
                    Button mBack = menuButtons[3];
                    Button mDown = menuButtons[4];
                    Button mLeft = menuButtons[5];

                    changed |= ApplyExplicitNavigation(mFront, null, mUp, null, mBack);
                    changed |= ApplyExplicitNavigation(mUp, mFront, mRight, null, mDown);
                    changed |= ApplyExplicitNavigation(mRight, mUp, null, null, mLeft);

                    changed |= ApplyExplicitNavigation(mBack, null, mDown, mFront, null);
                    changed |= ApplyExplicitNavigation(mDown, mBack, mLeft, mUp, null);
                    changed |= ApplyExplicitNavigation(mLeft, mDown, null, mRight, null);
                }
            }

            return changed;
        }

        private static bool AuthorWeaponName(GameObject _root)
        {
            WeaponNameView view = _root.GetComponentInChildren<WeaponNameView>(true);
            if (view == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(view);
            TMP_InputField input = so.FindProperty("m_input")?.objectReferenceValue as TMP_InputField;
            Button confirm = so.FindProperty("m_confirm")?.objectReferenceValue as Button;

            if (input != null && confirm != null)
            {
                return ApplyHorizontalNavigation(input, confirm);
            }

            return false;
        }

        private static bool AuthorWeaponInfoRecipe(GameObject _root)
        {
            RecipeView view = _root.GetComponentInChildren<RecipeView>(true);
            if (view == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(view);
            Button openButton = so.FindProperty("openButton")?.objectReferenceValue as Button;
            ScrollRect scroll = so.FindProperty("scroll")?.objectReferenceValue as ScrollRect;
            Scrollbar scrollbar = scroll != null ? scroll.verticalScrollbar : null;

            if (openButton != null && scrollbar != null)
            {
                return ApplyVerticalNavigation(openButton, scrollbar);
            }
            else if (openButton != null)
            {
                return ApplySingleNavigation(openButton);
            }

            return false;
        }

        private static bool AuthorSaveSlotsHierarchy(Transform _root)
        {
            Transform saveSlotsPanel = FindChildRecursively(_root, "SaveSlotsPanel");
            if (saveSlotsPanel == null)
            {
                return false;
            }

            Button slot1 = FindChildButton(saveSlotsPanel, "Slot1");
            Button slot2 = FindChildButton(saveSlotsPanel, "Slot2");
            Button slot3 = FindChildButton(saveSlotsPanel, "Slot3");
            Button autoSaveSlot = FindChildButton(saveSlotsPanel, "AutoSaveSlot");

            Button del1 = FindChildButton(saveSlotsPanel, "DeleteSlot1");
            Button del2 = FindChildButton(saveSlotsPanel, "DeleteSlot2");
            Button del3 = FindChildButton(saveSlotsPanel, "DeleteSlot3");
            Button delAuto = FindChildButton(saveSlotsPanel, "DeleteAutoSaveButton");

            Button close = FindChildButton(saveSlotsPanel, "CloseSlotsButton");

            if (slot1 == null || slot2 == null || slot3 == null || autoSaveSlot == null)
            {
                return false;
            }

            bool changed = false;

            changed |= ApplyExplicitNavigation(slot1, null, del1, null, slot2);
            changed |= ApplyExplicitNavigation(slot2, null, del2, slot1, slot3);
            changed |= ApplyExplicitNavigation(slot3, null, del3, slot2, autoSaveSlot);
            changed |= ApplyExplicitNavigation(autoSaveSlot, null, delAuto, slot3, null);

            if (del1 != null) changed |= ApplyExplicitNavigation(del1, slot1, close, null, del2);
            if (del2 != null) changed |= ApplyExplicitNavigation(del2, slot2, close, del1, del3);
            if (del3 != null) changed |= ApplyExplicitNavigation(del3, slot3, close, del2, delAuto);
            if (delAuto != null) changed |= ApplyExplicitNavigation(delAuto, autoSaveSlot, close, del3, null);

            if (close != null)
            {
                Selectable upTarget = del2 != null ? (Selectable)del2 : slot2;
                changed |= ApplyExplicitNavigation(close, upTarget, null, null, null);
            }

            return changed;
        }

        private static bool AuthorYesNoHierarchy(Transform _root)
        {
            Transform yesNoRoot = FindChildRecursively(_root, "YesNoUI");
            if (yesNoRoot == null)
            {
                return false;
            }

            Button yes = FindChildButton(yesNoRoot, "Yes");
            Button no = FindChildButton(yesNoRoot, "No");

            if (yes != null && no != null)
            {
                return ApplyHorizontalNavigation(yes, no);
            }

            return false;
        }

        private static bool AuthorSoundSettings(GameObject _root)
        {
            SoundSettingsView view = _root.GetComponentInChildren<SoundSettingsView>(true);
            if (view == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(view);
            Button closeButton = so.FindProperty("m_closeButton")?.objectReferenceValue as Button;
            Slider masterSlider = so.FindProperty("m_masterSlider")?.objectReferenceValue as Slider;
            Slider bgmSlider = so.FindProperty("m_bgmSlider")?.objectReferenceValue as Slider;
            Slider sfxSlider = so.FindProperty("m_sfxSlider")?.objectReferenceValue as Slider;
            Toggle muteToggle = so.FindProperty("m_muteToggle")?.objectReferenceValue as Toggle;

            List<Selectable> chain = new List<Selectable>();
            if (closeButton != null) chain.Add(closeButton);
            if (masterSlider != null) chain.Add(masterSlider);
            if (bgmSlider != null) chain.Add(bgmSlider);
            if (sfxSlider != null) chain.Add(sfxSlider);
            if (muteToggle != null) chain.Add(muteToggle);

            SerializedProperty songsProp = so.FindProperty("m_songs");
            if (songsProp != null)
            {
                for (int i = 0; i < songsProp.arraySize; i++)
                {
                    SerializedProperty songRow = songsProp.GetArrayElementAtIndex(i);
                    Button songBtn = songRow.FindPropertyRelative("button")?.objectReferenceValue as Button;
                    if (songBtn != null)
                    {
                        chain.Add(songBtn);
                    }
                }
            }

            return ApplyVerticalChain(chain.ToArray());
        }

        private static bool AuthorCustomization(GameObject _root)
        {
            CustomizationView view = _root.GetComponentInChildren<CustomizationView>(true);
            if (view == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(view);
            Button removeButton = so.FindProperty("m_removeButton")?.objectReferenceValue as Button;
            SerializedProperty panelButtonsProp = so.FindProperty("m_panelButtons");

            List<Selectable> chain = new List<Selectable>();
            if (panelButtonsProp != null)
            {
                for (int i = 0; i < panelButtonsProp.arraySize; i++)
                {
                    Button btn = panelButtonsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                    if (btn != null)
                    {
                        chain.Add(btn);
                    }
                }
            }

            if (removeButton != null)
            {
                chain.Add(removeButton);
            }

            return ApplyVerticalChain(chain.ToArray());
        }

        private static bool AuthorMouseSensitivity(GameObject _root)
        {
            Transform sensitivityTransform = FindChildRecursively(_root.transform, "MouseSensitivity");
            if (sensitivityTransform == null)
            {
                return false;
            }

            Slider slider = sensitivityTransform.GetComponentInChildren<Slider>(true);
            TMP_InputField input = sensitivityTransform.GetComponentInChildren<TMP_InputField>(true);

            if (slider != null && input != null)
            {
                return ApplyHorizontalNavigation(slider, input);
            }

            return false;
        }

        private static bool AuthorTargetSelection(GameObject _root)
        {
            TargetSelectionView view = _root.GetComponentInChildren<TargetSelectionView>(true);
            if (view == null)
            {
                return false;
            }

            SerializedObject so = new SerializedObject(view);
            Button closeButton = so.FindProperty("m_closeButton")?.objectReferenceValue as Button;
            if (closeButton != null)
            {
                return ApplySingleNavigation(closeButton);
            }

            return false;
        }

        private static bool ApplyExplicitNavigation(Selectable _selectable, Selectable _up, Selectable _down,
            Selectable _left, Selectable _right)
        {
            if (_selectable == null)
            {
                return false;
            }

            Navigation navigation = _selectable.navigation;
            if (navigation.mode == Navigation.Mode.Explicit &&
                !navigation.wrapAround &&
                navigation.selectOnUp == _up &&
                navigation.selectOnDown == _down &&
                navigation.selectOnLeft == _left &&
                navigation.selectOnRight == _right)
            {
                return false;
            }

            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = _up;
            navigation.selectOnDown = _down;
            navigation.selectOnLeft = _left;
            navigation.selectOnRight = _right;
            _selectable.navigation = navigation;
            return true;
        }

        private static bool ApplySingleNavigation(Selectable _selectable)
        {
            return ApplyExplicitNavigation(_selectable, null, null, null, null);
        }

        private static bool ApplyHorizontalNavigation(Selectable _left, Selectable _right)
        {
            bool changed = ApplyExplicitNavigation(_left, null, null, null, _right);
            changed |= ApplyExplicitNavigation(_right, null, null, _left, null);
            return changed;
        }

        private static bool ApplyVerticalNavigation(Selectable _top, Selectable _bottom)
        {
            bool changed = ApplyExplicitNavigation(_top, null, _bottom, null, null);
            changed |= ApplyExplicitNavigation(_bottom, _top, null, null, null);
            return changed;
        }

        private static bool ApplyVerticalChain(Selectable[] _chain)
        {
            if (_chain == null || _chain.Length == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < _chain.Length; i++)
            {
                Selectable current = _chain[i];
                if (current == null)
                {
                    continue;
                }

                Selectable up = (i > 0) ? _chain[i - 1] : null;
                Selectable down = (i < _chain.Length - 1) ? _chain[i + 1] : null;
                changed |= ApplyExplicitNavigation(current, up, down, null, null);
            }

            return changed;
        }

        private static void SavePrefab(GameObject _root, string _path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(_root, _path) == null)
            {
                throw new InvalidOperationException("프리팹 저장 실패: " + _path);
            }
        }

        private static bool EnsureNoDirtyScene(out string _reason)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    _reason = "열린 씬에 저장하지 않은 변경이 있습니다: " + scene.path;
                    return false;
                }
            }

            _reason = null;
            return true;
        }

        private static bool AssetExists(string _path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(_path) != null;
        }

        private static Transform FindChild(Transform _parent, string _name)
        {
            return _parent != null ? _parent.Find(_name) : null;
        }

        private static Transform FindChildRecursively(Transform _parent, string _name)
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
                Transform found = FindChildRecursively(_parent.GetChild(i), _name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Button FindChildButton(Transform _parent, string _relativePath)
        {
            Transform child = _parent != null ? _parent.Find(_relativePath) : null;
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static void Fail(string _reason)
        {
            Debug.LogError(LOG_PREFIX + "FAIL Navigation/Author " + _reason);
        }
    }
}
