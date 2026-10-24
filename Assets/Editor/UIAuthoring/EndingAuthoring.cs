using System;

using TMPro;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// 엔딩·은퇴 화면의 View 컴포넌트와 참조, 명시 Navigation을 저작한다.
    /// SceneScreens/Extract가 만든 화면 프리팹과 UIManager.prefab의 RaidDeathUI를 대상으로 하며,
    /// 마지막에 Main.unity의 RetireUI.m_view를 연결한다. 멱등: 이미 맞으면 저장하지 않는다.
    /// </summary>
    public static class EndingAuthoring
    {
        private const string MENU_PATH = "Tools/UI/Authoring/Ending/Author";
        private const string LOG_PREFIX = "[UIAuthoring] ";
        private const string RETIRE_PREFAB = "Assets/Prefabs/UI/Screens/Retire.prefab";
        private const string END_CHEST_PREFAB = "Assets/Prefabs/UI/Screens/EndChestEnding.prefab";
        private const string UI_MANAGER_PREFAB = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string MAIN_SCENE = "Assets/Scenes/Main.unity";

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

            if (!AssetExists(RETIRE_PREFAB) || !AssetExists(END_CHEST_PREFAB))
            {
                Fail("화면 프리팹이 없습니다. Tools/UI/Authoring/SceneScreens/Extract를 먼저 실행하세요. " +
                     "(" + RETIRE_PREFAB + ", " + END_CHEST_PREFAB + ")");
                return;
            }

            try
            {
                string retire = AuthorRetirePrefab();
                string endChest = AuthorEndChestPrefab();
                string raidDeath = AuthorRaidDeathPrefab();
                string mainScene = AuthorMainScene();

                Debug.Log(LOG_PREFIX + "OK Ending/Author " + retire + ", " + endChest + ", " +
                          raidDeath + ", " + mainScene);
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
        }

        private static string AuthorRetirePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RETIRE_PREFAB);
            try
            {
                bool changed = false;
                RetireView view = root.GetComponent<RetireView>();
                if (view == null)
                {
                    view = root.AddComponent<RetireView>();
                    changed = true;
                }

                Transform continueButtonTransform = FindChild(root.transform, "ContinueButton");
                Transform lobbyButtonTransform = FindChild(root.transform, "LobbyButton");
                if (continueButtonTransform == null || lobbyButtonTransform == null)
                {
                    throw new InvalidOperationException(
                        "Retire 프리팹에서 ContinueButton/LobbyButton을 찾지 못했습니다.");
                }

                Button continueButton = continueButtonTransform.GetComponent<Button>();
                Button lobbyButton = lobbyButtonTransform.GetComponent<Button>();
                TMP_Text continueText = continueButtonTransform.GetComponentInChildren<TMP_Text>(true);
                if (continueButton == null || lobbyButton == null || continueText == null)
                {
                    throw new InvalidOperationException(
                        "Retire 프리팹의 버튼 또는 계속 문구 참조가 비어 있습니다.");
                }

                SerializedObject viewSo = new SerializedObject(view);
                bool viewChanged = SetReference(viewSo, "m_panel", root) |
                                   SetReference(viewSo, "m_continueText", continueText) |
                                   SetReference(viewSo, "m_lobbyButton", lobbyButton) |
                                   SetReference(viewSo, "m_continueButton", continueButton);
                if (viewChanged)
                {
                    viewSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (ApplyVerticalNavigation(lobbyButton, continueButton))
                {
                    changed = true;
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
                EndChestEndingUI bridge = root.GetComponent<EndChestEndingUI>();
                if (bridge == null)
                {
                    throw new InvalidOperationException(
                        "EndChestEnding 프리팹 루트에 EndChestEndingUI 브리지가 없습니다.");
                }

                bool changed = false;
                EndChestEndingView view = root.GetComponent<EndChestEndingView>();
                if (view == null)
                {
                    view = root.AddComponent<EndChestEndingView>();
                    changed = true;
                }

                Transform white = FindChild(root.transform, "White");
                Transform endText = FindChild(root.transform, "EndText");
                Transform buttons = FindChild(root.transform, "Buttons");
                if (white == null || endText == null || buttons == null)
                {
                    throw new InvalidOperationException(
                        "EndChestEnding 프리팹에서 White/EndText/Buttons를 찾지 못했습니다.");
                }

                Button continueButton = FindComponent<Button>(buttons, "ContinueButton");
                Button quitButton = FindComponent<Button>(buttons, "QuitButton");
                CanvasGroup whiteGroup = white.GetComponent<CanvasGroup>();
                CanvasGroup endGroup = endText.GetComponent<CanvasGroup>();
                CanvasGroup buttonsGroup = buttons.GetComponent<CanvasGroup>();
                if (continueButton == null || quitButton == null || whiteGroup == null ||
                    endGroup == null || buttonsGroup == null)
                {
                    throw new InvalidOperationException(
                        "EndChestEnding 프리팹의 CanvasGroup 또는 버튼 참조가 비어 있습니다.");
                }

                SerializedObject viewSo = new SerializedObject(view);
                bool viewChanged = SetReference(viewSo, "m_whiteGroup", whiteGroup) |
                                   SetReference(viewSo, "m_endGroup", endGroup) |
                                   SetReference(viewSo, "m_buttonsGroup", buttonsGroup) |
                                   SetReference(viewSo, "m_continueButton", continueButton) |
                                   SetReference(viewSo, "m_quitButton", quitButton);
                if (viewChanged)
                {
                    viewSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                SerializedObject bridgeSo = new SerializedObject(bridge);
                if (SetReference(bridgeSo, "m_view", view))
                {
                    bridgeSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (ApplyHorizontalNavigation(continueButton, quitButton))
                {
                    changed = true;
                }

                if (changed)
                {
                    SavePrefab(root, END_CHEST_PREFAB);
                }

                return "EndChestEnding" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorRaidDeathPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UI_MANAGER_PREFAB);
            try
            {
                RaidDeathUI bridge = FindComponentInChildren<RaidDeathUI>(root);
                if (bridge == null)
                {
                    throw new InvalidOperationException("UIManager 프리팹에 RaidDeathUI가 없습니다.");
                }

                SerializedObject bridgeSo = new SerializedObject(bridge);
                GameObject panel = ReadReference(bridgeSo, "panel") as GameObject;
                Button returnButton = ReadReference(bridgeSo, "returnButton") as Button;
                if (panel == null || returnButton == null)
                {
                    throw new InvalidOperationException(
                        "RaidDeathUI의 panel/returnButton 참조가 비어 있습니다.");
                }

                bool changed = false;
                GameObject host = bridge.gameObject;
                RaidDeathView view = host.GetComponent<RaidDeathView>();
                if (view == null)
                {
                    view = host.AddComponent<RaidDeathView>();
                    changed = true;
                }

                SerializedObject viewSo = new SerializedObject(view);
                bool viewChanged = SetReference(viewSo, "m_panel", panel) |
                                   SetReference(viewSo, "m_returnButton", returnButton);
                if (viewChanged)
                {
                    viewSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (SetReference(bridgeSo, "m_view", view))
                {
                    bridgeSo.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (ApplySingleNavigation(returnButton))
                {
                    changed = true;
                }

                if (changed)
                {
                    SavePrefab(root, UI_MANAGER_PREFAB);
                }

                return "RaidDeath" + (changed ? "(변경)" : "(변경 없음)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string AuthorMainScene()
        {
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            bool changed = false;
            bool saved = false;
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(MAIN_SCENE, OpenSceneMode.Single);
                RetireUI bridge = Object.FindFirstObjectByType<RetireUI>(FindObjectsInactive.Include);
                if (bridge == null)
                {
                    throw new InvalidOperationException("Main.unity에서 RetireUI를 찾지 못했습니다.");
                }

                SerializedObject bridgeSo = new SerializedObject(bridge);
                GameObject panel = ReadReference(bridgeSo, "panel") as GameObject;
                if (panel == null)
                {
                    throw new InvalidOperationException("RetireUI.panel 참조가 비어 있습니다.");
                }

                string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel);
                if (!string.Equals(instancePath, RETIRE_PREFAB, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "RetireUI.panel이 추출된 Retire 프리팹 인스턴스가 아닙니다: " + instancePath);
                }

                RetireView view = panel.GetComponent<RetireView>();
                if (view == null)
                {
                    throw new InvalidOperationException(
                        "Retire 프리팹 인스턴스에 RetireView가 없습니다. Ending/Author를 다시 실행하세요.");
                }

                if (SetReference(bridgeSo, "m_view", view))
                {
                    bridgeSo.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.GetCorrespondingObjectFromSource(bridge) != null)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(bridge);
                    }

                    changed = true;
                }

                if (ReadReference(new SerializedObject(bridge), "m_view") != view)
                {
                    throw new InvalidOperationException("RetireUI.m_view 연결 검증에 실패했습니다.");
                }

                if (changed)
                {
                    if (!EditorSceneManager.SaveScene(scene, MAIN_SCENE))
                    {
                        throw new InvalidOperationException("Main.unity 저장 실패");
                    }

                    saved = true;
                }

                return "Main.unity(RetireUI.m_view " + (changed ? "연결" : "변경 없음") + ")";
            }
            finally
            {
                if (changed && !saved && scene.IsValid())
                {
                    try
                    {
                        // 검증·저장에 실패한 씬은 변경을 남기지 않는다.
                        EditorSceneManager.CloseScene(scene, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(LOG_PREFIX + "Ending/Author 실패 씬 닫기 실패: " + exception.Message);
                    }
                }

                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(LOG_PREFIX + "Ending/Author 씬 구성 복원 실패: " + exception.Message);
                }
            }
        }

        private static void SavePrefab(GameObject _root, string _path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(_root, _path) == null)
            {
                throw new InvalidOperationException("프리팹 저장 실패: " + _path);
            }
        }

        private static bool SetReference(SerializedObject _serialized, string _propertyName, Object _value)
        {
            SerializedProperty property = _serialized.FindProperty(_propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾지 못했습니다: " + _propertyName);
            }

            if (property.objectReferenceValue == _value)
            {
                return false;
            }

            property.objectReferenceValue = _value;
            return true;
        }

        private static Object ReadReference(SerializedObject _serialized, string _propertyName)
        {
            SerializedProperty property = _serialized.FindProperty(_propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾지 못했습니다: " + _propertyName);
            }

            return property.objectReferenceValue;
        }

        private static bool ApplyVerticalNavigation(Selectable _top, Selectable _bottom)
        {
            bool changed = ApplyExplicitNavigation(_top, null, _bottom, null, null);
            changed |= ApplyExplicitNavigation(_bottom, _top, null, null, null);
            return changed;
        }

        private static bool ApplyHorizontalNavigation(Selectable _left, Selectable _right)
        {
            bool changed = ApplyExplicitNavigation(_left, null, null, null, _right);
            changed |= ApplyExplicitNavigation(_right, null, null, _left, null);
            return changed;
        }

        private static bool ApplySingleNavigation(Selectable _selectable)
        {
            return ApplyExplicitNavigation(_selectable, null, null, null, null);
        }

        private static bool ApplyExplicitNavigation(Selectable _selectable, Selectable _up, Selectable _down,
            Selectable _left, Selectable _right)
        {
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
            Transform child = _parent != null ? _parent.Find(_name) : null;
            if (child == null)
            {
                throw new InvalidOperationException(
                    "자식 오브젝트를 찾지 못했습니다: " + (_parent != null ? _parent.name : "(null)") + "/" + _name);
            }

            return child;
        }

        private static T FindComponent<T>(Transform _parent, string _name) where T : Component
        {
            Transform child = FindChild(_parent, _name);
            T component = child.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    "컴포넌트를 찾지 못했습니다: " + child.name + " (" + typeof(T).Name + ")");
            }

            return component;
        }

        private static T FindComponentInChildren<T>(GameObject _root) where T : Component
        {
            return _root.GetComponentInChildren<T>(true);
        }

        private static void Fail(string _reason)
        {
            Debug.LogError(LOG_PREFIX + "FAIL Ending/Author " + _reason);
        }
    }
}
