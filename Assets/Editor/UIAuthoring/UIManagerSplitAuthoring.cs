using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// UIManager.prefab 분할 저작 명령.
    /// RelocateBridges: 루트에 몰려 있던 화면 브리지·View를 레이어 아래 화면 묶음(UIScreenRoot)으로 옮긴다.
    /// ExtractScreens: 레이어 바로 아래 오브젝트를 각각 중첩 프리팹으로 추출한다.
    /// RemapScenes: 두 단계에서 바뀐 fileID 대응표로 씬의 UIManager 오버라이드·참조를 다시 연결한다.
    /// 각 단계는 전후 참조를 대조해 어긋난 참조 수를 로그로 남긴다.
    /// </summary>
    public static class UIManagerSplitAuthoring
    {
        private const string MENU_RELOCATE = "Tools/UI/Authoring/Split/RelocateBridges";
        private const string MENU_EXTRACT = "Tools/UI/Authoring/Split/ExtractScreens";
        private const string MENU_REMAP = "Tools/UI/Authoring/Split/RemapScenes";
        private const string STEP_RELOCATE = "Split/RelocateBridges";
        private const string STEP_EXTRACT = "Split/ExtractScreens";
        private const string STEP_REMAP = "Split/RemapScenes";

        private const string RELOCATE_MAP_PATH = "Library/UIManagerSplit_Relocate.map";
        private const string EXTRACT_MAP_PATH = "Library/UIManagerSplit_Extract.map";
        private const string SCENES_ROOT = "Assets/Scenes";
        private const string UI_MANAGER_COMPONENT_FILE_ID = "3162838370691866602";
        private const string PROPERTY_SEPARATOR = "::";

        private static readonly string[] S_LAYERS =
        {
            UIAuthoringPaths.HUD_LAYER,
            UIAuthoringPaths.SCREEN_LAYER,
            UIAuthoringPaths.POPUP_LAYER,
            UIAuthoringPaths.TRANSITION_LAYER,
        };

        private sealed class ScreenSpec
        {
            public readonly string Name;
            public readonly string Layer;
            public readonly string SourceParent;
            public readonly string[] Elements;
            public readonly string[] Moves;

            public ScreenSpec(string _name, string _layer, string _sourceParent, string[] _elements, params string[] _moves)
            {
                Name = _name;
                Layer = _layer;
                SourceParent = _sourceParent;
                Elements = _elements;
                Moves = _moves;
            }
        }

        // 화면 묶음 이름, 놓일 레이어, 감쌀 오브젝트의 현재 부모(null이면 루트), 감쌀 오브젝트, 루트에서 옮길 컴포넌트.
        private static readonly ScreenSpec[] S_SCREENS =
        {
            new ScreenSpec("WorkshopDirectionMarksScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "RotationCircle" }),
            new ScreenSpec("WeaponInfoScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "WeaponInfo" },
                "WorkshopTagsUI", "WorkshopTagsView", "WeaponRecipeUI", "RecipeView"),
            new ScreenSpec("WeaponNameScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "WeaponNamePanel" },
                "WeaponNameUI", "WeaponNameView"),
            new ScreenSpec("PresetPanelScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "PresetPanel" },
                "PresetPanelUI", "PresetPanelView"),
            new ScreenSpec("QuestInfoScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "QuestInfoPanel" },
                "TutorialQuestUI", "TutorialQuestView", "CustomerRequirementsUI", "CustomerRequirementsView"),
            new ScreenSpec("HoverTooltipScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "HoveringUI" },
                "HoveringUI", "HoverTooltipView"),
            new ScreenSpec("TutorialTargetScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "TutorialTargetPanel" },
                "TutorialTargetUI", "TutorialTargetView"),
            new ScreenSpec("TutorialButtonHighlightScreen", UIAuthoringPaths.HUD_LAYER, UIAuthoringPaths.HUD_LAYER, new[] { "TutorialButtonCover" },
                "TutorialButtonHighlightUI", "TutorialButtonHighlightView"),
            new ScreenSpec("ShootingTestScreen", UIAuthoringPaths.HUD_LAYER, null, new[] { "ShootingTestHUD" },
                "ShootingTestHUD", "ShootingTestHudView"),
            new ScreenSpec("TargetSelectionScreen", UIAuthoringPaths.SCREEN_LAYER, UIAuthoringPaths.SCREEN_LAYER, new[] { "TargetSelection" },
                "TargetSelectionUI", "TargetSelectionView"),
            new ScreenSpec("WeaponCodeScreen", UIAuthoringPaths.SCREEN_LAYER, UIAuthoringPaths.SCREEN_LAYER, new[] { "WeaponCodePanel" },
                "WeaponCodeUI", "WeaponCodeView"),
            new ScreenSpec("PauseScreen", UIAuthoringPaths.SCREEN_LAYER, UIAuthoringPaths.SCREEN_LAYER, new[] { "PausePanel" },
                "PauseView", "SoundSettingsUI", "SoundSettingsView"),
            new ScreenSpec("TutorialSkipScreen", UIAuthoringPaths.POPUP_LAYER, UIAuthoringPaths.POPUP_LAYER, new[] { "TutorialSkipPanel" },
                "TutorialSkipUI", "TutorialSkipView"),
            new ScreenSpec("FeedbackScreen", UIAuthoringPaths.POPUP_LAYER, UIAuthoringPaths.POPUP_LAYER, new[] { "YesNoUI", "NotificationText" },
                "FeedbackUI", "FeedbackChoiceView", "FeedbackNotificationView",
                "ModuleUnlockNotice", "ModuleUnlockNoticeView", "StructuralFailureWarning", "StructuralFailureWarningView"),
            new ScreenSpec("DayTransitionScreen", UIAuthoringPaths.TRANSITION_LAYER, UIAuthoringPaths.TRANSITION_LAYER, new[] { "DayTransitionPanel" },
                "DayTransitionUI", "DayTransitionView"),
        };

        // 정산 화면이 들고 있던 재화 HUD 값을 CurrencyHudView 필드로 옮긴다.
        private static readonly string[,] S_CURRENCY_FIELDS =
        {
            { "moneyDisplay", "m_moneyDisplay" },
            { "moneyText", "m_moneyText" },
            { "moduleCreditDisplay", "m_moduleCreditDisplay" },
            { "moduleCreditText", "m_moduleCreditText" },
            { "moneyCountDuration", "m_moneyCountDuration" },
            { "raidLossRollDuration", "m_raidLossRollDuration" },
            { "insufficientFundsDuration", "m_insufficientFundsDuration" },
            { "insufficientFundsPulses", "m_insufficientFundsPulses" },
            { "insufficientFundsScale", "m_insufficientFundsScale" },
        };

        [MenuItem(MENU_RELOCATE)]
        public static void RelocateBridges()
        {
            if (!CheckRunnableEnvironment(STEP_RELOCATE))
            {
                return;
            }

            string path = UIAuthoringPaths.UI_MANAGER_PREFAB;
            string prefabText = File.ReadAllText(path);
            Dictionary<string, long> idsBefore = SnapshotIds(path);
            Dictionary<long, string> keyById = InvertIds(idsBefore);

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Dictionary<string, string> keyMap;
            Dictionary<string, string> refsBefore;
            int createdScreens = 0;
            int movedComponents = 0;
            int addedComponents = 0;
            int removedObjects = 0;
            try
            {
                Dictionary<Object, string> keysBefore = BuildKeys(root);
                Dictionary<string, Object> objectByKey = keysBefore.ToDictionary(_pair => _pair.Value, _pair => _pair.Key);
                refsBefore = SnapshotRefs(keysBefore);

                var moved = new List<KeyValuePair<Component, Component>>();
                foreach (ScreenSpec spec in S_SCREENS)
                {
                    Transform wrapper = EnsureScreenRoot(root.transform, spec, ref createdScreens);
                    if (wrapper == null)
                    {
                        return;
                    }

                    foreach (string typeName in spec.Moves)
                    {
                        Type type = ResolveComponentType(typeName);
                        if (type == null)
                        {
                            Debug.LogError("[UIAuthoring] FAIL " + STEP_RELOCATE + ": 타입을 찾지 못했습니다: " + typeName);
                            return;
                        }

                        Component source = root.GetComponent(type);
                        if (source == null)
                        {
                            continue;
                        }

                        Component destination = wrapper.GetComponent(type);
                        if (destination == null)
                        {
                            destination = wrapper.gameObject.AddComponent(type);
                        }

                        EditorUtility.CopySerialized(source, destination);
                        moved.Add(new KeyValuePair<Component, Component>(source, destination));
                    }
                }

                addedComponents += AddWorkshopDirectionMarks(root.transform, prefabText, keyById, objectByKey);
                addedComponents += AddPauseUI(root.transform);
                addedComponents += AddCurrencyHud(root.transform);

                var redirect = moved.ToDictionary(_pair => (Object)_pair.Key, _pair => (Object)_pair.Value);
                RedirectReferences(root, redirect);

                var movedKeys = new Dictionary<string, Component>();
                foreach (KeyValuePair<Component, Component> pair in moved)
                {
                    movedKeys[keysBefore[pair.Key]] = pair.Value;
                    Object.DestroyImmediate(pair.Key);
                    movedComponents++;
                }

                removedObjects = RemoveSeparators(root.transform, refsBefore, keysBefore);
                if (createdScreens + moved.Count + addedComponents + removedObjects == 0)
                {
                    // 이미 적용된 상태면 저장도, 대응표 덮어쓰기도 하지 않는다.
                    Debug.Log("[UIAuthoring] OK " + STEP_RELOCATE + ": 변경 0건");
                    return;
                }

                Dictionary<Object, string> keysAfter = BuildKeys(root);
                keyMap = new Dictionary<string, string>();
                foreach (KeyValuePair<Object, string> pair in keysBefore)
                {
                    if (pair.Key != null && keysAfter.TryGetValue(pair.Key, out string newKey))
                    {
                        keyMap[pair.Value] = newKey;
                    }
                }

                foreach (KeyValuePair<string, Component> pair in movedKeys)
                {
                    keyMap[pair.Key] = keysAfter[pair.Value];
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_RELOCATE + ": 프리팹 저장 실패: " + path);
                    return;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            int mapped = WriteIdMap(RELOCATE_MAP_PATH, idsBefore, SnapshotIds(path), keyMap);
            int mismatches = VerifyRefs(path, refsBefore, keyMap, STEP_RELOCATE);
            Debug.Log("[UIAuthoring] OK " + STEP_RELOCATE + ": 화면 묶음 생성 " + createdScreens + ", 컴포넌트 이동 " + movedComponents +
                      ", 컴포넌트 추가 " + addedComponents + ", 구분용 빈 오브젝트 삭제 " + removedObjects +
                      ", fileID 대응 " + mapped + "건, 어긋난 참조 " + mismatches + "건");
        }

        [MenuItem(MENU_EXTRACT)]
        public static void ExtractScreens()
        {
            if (!CheckRunnableEnvironment(STEP_EXTRACT))
            {
                return;
            }

            string path = UIAuthoringPaths.UI_MANAGER_PREFAB;
            Dictionary<string, long> idsBefore = SnapshotIds(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Dictionary<string, string> refsBefore;
            Dictionary<string, string> keyMap;
            var extracted = new List<string>();
            try
            {
                Dictionary<Object, string> keysBefore = BuildKeys(root);
                refsBefore = SnapshotRefs(keysBefore);
                keyMap = keysBefore.Values.ToDictionary(_key => _key, _key => _key);

                foreach (string layerName in S_LAYERS)
                {
                    Transform layer = root.transform.Find(layerName);
                    if (layer == null)
                    {
                        Debug.LogError("[UIAuthoring] FAIL " + STEP_EXTRACT + ": 레이어를 찾지 못했습니다: " + layerName);
                        return;
                    }

                    EnsureFolder(UIAuthoringPaths.FolderForLayer(layerName));
                    foreach (Transform child in layer.Cast<Transform>().ToList())
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                        {
                            continue;
                        }

                        string prefabPath = UIAuthoringPaths.ScreenPrefab(layerName, child.name);
                        if (File.Exists(prefabPath))
                        {
                            Debug.LogError("[UIAuthoring] FAIL " + STEP_EXTRACT + ": 같은 경로에 프리팹이 이미 있습니다: " + prefabPath);
                            return;
                        }

                        PrefabUtility.SaveAsPrefabAssetAndConnect(child.gameObject, prefabPath, InteractionMode.AutomatedAction, out bool created);
                        if (!created)
                        {
                            Debug.LogError("[UIAuthoring] FAIL " + STEP_EXTRACT + ": 중첩 프리팹 생성 실패: " + prefabPath);
                            return;
                        }

                        extracted.Add(prefabPath);
                    }
                }

                if (extracted.Count == 0)
                {
                    Debug.Log("[UIAuthoring] OK " + STEP_EXTRACT + ": 변경 0건");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_EXTRACT + ": 프리팹 저장 실패: " + path);
                    return;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            int mapped = WriteIdMap(EXTRACT_MAP_PATH, idsBefore, SnapshotIds(path), keyMap);
            int mismatches = VerifyRefs(path, refsBefore, keyMap, STEP_EXTRACT);
            Debug.Log("[UIAuthoring] OK " + STEP_EXTRACT + ": 중첩 프리팹 " + extracted.Count + "개 추출, fileID 대응 " + mapped +
                      "건, 어긋난 참조 " + mismatches + "건 -> " + string.Join(", ", extracted.Select(Path.GetFileNameWithoutExtension)));
        }

        [MenuItem(MENU_REMAP)]
        public static void RemapScenes()
        {
            if (!CheckRunnableEnvironment(STEP_REMAP))
            {
                return;
            }

            Dictionary<long, long> relocate = ReadIdMap(RELOCATE_MAP_PATH);
            Dictionary<long, long> extract = ReadIdMap(EXTRACT_MAP_PATH);
            if (relocate == null || extract == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_REMAP + ": 대응표가 없습니다. RelocateBridges와 ExtractScreens를 먼저 실행하세요.");
                return;
            }

            // 옛 fileID -> 이동 단계 -> 추출 단계 순으로 합성한다.
            var composed = new Dictionary<long, long>();
            foreach (KeyValuePair<long, long> pair in relocate)
            {
                composed[pair.Key] = extract.TryGetValue(pair.Value, out long next) ? next : pair.Value;
            }

            foreach (KeyValuePair<long, long> pair in extract)
            {
                if (!composed.ContainsKey(pair.Key))
                {
                    composed[pair.Key] = pair.Value;
                }
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            string guid = AssetDatabase.AssetPathToGUID(UIAuthoringPaths.UI_MANAGER_PREFAB);
            var pattern = new Regex(@"\{fileID: (-?\d+), guid: " + guid + @", type: 3\}");
            var summaries = new List<string>();
            int total = 0;
            foreach (string scenePath in Directory.GetFiles(SCENES_ROOT, "*.unity", SearchOption.AllDirectories)
                         .Select(_path => _path.Replace('\\', '/')).OrderBy(_path => _path, StringComparer.Ordinal))
            {
                string text = File.ReadAllText(scenePath);
                if (!text.Contains(guid))
                {
                    continue;
                }

                int count = 0;
                string replaced = pattern.Replace(text, _match =>
                {
                    long oldId = long.Parse(_match.Groups[1].Value);
                    if (!composed.TryGetValue(oldId, out long newId))
                    {
                        return _match.Value;
                    }

                    count++;
                    return "{fileID: " + newId + ", guid: " + guid + ", type: 3}";
                });

                if (count > 0)
                {
                    File.WriteAllText(scenePath, replaced);
                    AssetDatabase.ImportAsset(scenePath);
                }

                total += count;
                summaries.Add(Path.GetFileName(scenePath) + "(" + count + ")");
            }

            Debug.Log("[UIAuthoring] OK " + STEP_REMAP + ": 대응표 " + composed.Count + "건, 씬 참조 " + total + "건 재연결 -> " +
                      string.Join(", ", summaries));
        }

        private static Transform EnsureScreenRoot(Transform _root, ScreenSpec _spec, ref int _created)
        {
            Transform layer = _root.Find(_spec.Layer);
            if (layer == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_RELOCATE + ": 레이어를 찾지 못했습니다: " + _spec.Layer);
                return null;
            }

            Transform existing = layer.Find(_spec.Name);
            if (existing != null)
            {
                return existing;
            }

            Transform sourceParent = _spec.SourceParent != null ? _root.Find(_spec.SourceParent) : _root;
            var elements = new List<Transform>();
            foreach (string elementName in _spec.Elements)
            {
                Transform element = sourceParent != null ? sourceParent.Find(elementName) : null;
                if (element == null)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_RELOCATE + ": 화면 오브젝트를 찾지 못했습니다: " + _spec.SourceParent + "/" + elementName);
                    return null;
                }

                elements.Add(element);
            }

            elements.Sort((_a, _b) => _a.GetSiblingIndex().CompareTo(_b.GetSiblingIndex()));
            int index = sourceParent == layer ? elements[0].GetSiblingIndex() : layer.childCount;

            var screen = new GameObject(_spec.Name, typeof(RectTransform));
            screen.layer = elements[0].gameObject.layer;
            var rect = (RectTransform)screen.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetSiblingIndex(index);
            screen.AddComponent<UIScreenRoot>();

            foreach (Transform element in elements)
            {
                element.SetParent(rect, false);
            }

            _created++;
            return rect;
        }

        private static int AddWorkshopDirectionMarks(Transform _root, string _prefabText, Dictionary<long, string> _keyById,
            Dictionary<string, Object> _objectByKey)
        {
            Transform screen = _root.Find(UIAuthoringPaths.HUD_LAYER + "/WorkshopDirectionMarksScreen");
            if (screen == null || screen.GetComponent<WorkshopDirectionMarksUI>() != null)
            {
                return 0;
            }

            // 지운 UIManager 필드 값은 직렬화 API로 읽을 수 없어 프리팹 원문에서 읽는다.
            Match block = Regex.Match(_prefabText, @"--- !u!114 &" + UI_MANAGER_COMPONENT_FILE_ID + @"\n(.*?)(?=\n--- |\z)", RegexOptions.Singleline);
            Object rotationCircle = ResolveYamlReference(block, "rotationCircle", _keyById, _objectByKey);
            Object frontMark = ResolveYamlReference(block, "frontMark", _keyById, _objectByKey);
            Object upMark = ResolveYamlReference(block, "upMark", _keyById, _objectByKey);
            float backThreshold = ReadYamlFloat(block, "directionBackThreshold", 0.05f);
            float radius = ReadYamlFloat(block, "rotationCircleRadius", 0.5f);

            var view = screen.gameObject.AddComponent<WorkshopDirectionMarksView>();
            var bridge = screen.gameObject.AddComponent<WorkshopDirectionMarksUI>();
            foreach (Object target in new Object[] { view, bridge })
            {
                var serialized = new SerializedObject(target);
                serialized.FindProperty("rotationCircle").objectReferenceValue = rotationCircle;
                serialized.FindProperty("frontMark").objectReferenceValue = frontMark;
                serialized.FindProperty("upMark").objectReferenceValue = upMark;
                serialized.FindProperty("directionBackThreshold").floatValue = backThreshold;
                serialized.FindProperty("rotationCircleRadius").floatValue = radius;
                SerializedProperty viewProperty = serialized.FindProperty("m_view");
                if (viewProperty != null)
                {
                    viewProperty.objectReferenceValue = view;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return 2;
        }

        private static int AddPauseUI(Transform _root)
        {
            Transform screen = _root.Find(UIAuthoringPaths.SCREEN_LAYER + "/PauseScreen");
            if (screen == null || screen.GetComponent<PauseUI>() != null)
            {
                return 0;
            }

            var pause = screen.gameObject.AddComponent<PauseUI>();
            var serialized = new SerializedObject(pause);
            serialized.FindProperty("m_view").objectReferenceValue = screen.GetComponent<PauseView>();
            serialized.FindProperty("m_uiRoot").objectReferenceValue = _root.GetComponent<UIRoot>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        private static int AddCurrencyHud(Transform _root)
        {
            Transform group = _root.Find(UIAuthoringPaths.HUD_LAYER + "/CurrencyGroup");
            if (group == null || group.GetComponent<CurrencyHudView>() != null)
            {
                return 0;
            }

            var hud = group.gameObject.AddComponent<CurrencyHudView>();
            DaySettlementUI settlement = _root.GetComponentInChildren<DaySettlementUI>(true);
            if (settlement != null)
            {
                var source = new SerializedObject(settlement);
                var destination = new SerializedObject(hud);
                for (int i = 0; i < S_CURRENCY_FIELDS.GetLength(0); i++)
                {
                    CopyCompatibleValue(source.FindProperty(S_CURRENCY_FIELDS[i, 0]), destination.FindProperty(S_CURRENCY_FIELDS[i, 1]), typeof(CurrencyHudView));
                }

                destination.ApplyModifiedPropertiesWithoutUndo();
            }

            return 1;
        }

        private static void CopyCompatibleValue(SerializedProperty _source, SerializedProperty _destination, Type _destinationType)
        {
            if (_source == null || _destination == null)
            {
                return;
            }

            switch (_destination.propertyType)
            {
                case SerializedPropertyType.Float:
                    _destination.floatValue = _source.propertyType == SerializedPropertyType.Integer ? _source.intValue : _source.floatValue;
                    return;
                case SerializedPropertyType.Integer:
                    _destination.intValue = _source.propertyType == SerializedPropertyType.Float ? Mathf.RoundToInt(_source.floatValue) : _source.intValue;
                    return;
                case SerializedPropertyType.ObjectReference:
                    FieldInfo field = _destinationType.GetField(_destination.name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    Object value = _source.objectReferenceValue;
                    if (field != null && value != null && !field.FieldType.IsInstanceOfType(value))
                    {
                        GameObject owner = value is Component component ? component.gameObject : value as GameObject;
                        value = owner == null ? null : field.FieldType == typeof(GameObject) ? owner : owner.GetComponent(field.FieldType);
                    }

                    _destination.objectReferenceValue = value;
                    return;
            }
        }

        private static Object ResolveYamlReference(Match _block, string _field, Dictionary<long, string> _keyById, Dictionary<string, Object> _objectByKey)
        {
            if (!_block.Success)
            {
                return null;
            }

            Match match = Regex.Match(_block.Value, @"\n  " + _field + @": \{fileID: (-?\d+)\}");
            if (!match.Success || !_keyById.TryGetValue(long.Parse(match.Groups[1].Value), out string key))
            {
                return null;
            }

            return _objectByKey.TryGetValue(key, out Object value) ? value : null;
        }

        private static float ReadYamlFloat(Match _block, string _field, float _fallback)
        {
            if (!_block.Success)
            {
                return _fallback;
            }

            Match match = Regex.Match(_block.Value, @"\n  " + _field + @": ([-\d.eE]+)");
            return match.Success && float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : _fallback;
        }

        // 루트 직속의 "DimUI---", "HoverUI---" 구분용 빈 오브젝트를 참조가 없을 때만 지운다.
        private static int RemoveSeparators(Transform _root, Dictionary<string, string> _refs, Dictionary<Object, string> _keys)
        {
            int removed = 0;
            foreach (Transform child in _root.Cast<Transform>().ToList())
            {
                if (!child.name.EndsWith("---", StringComparison.Ordinal) || child.childCount > 0)
                {
                    continue;
                }

                string prefix = _keys[child.gameObject].Replace("|GO", string.Empty);
                if (_refs.Values.Any(_value => _value.StartsWith(prefix + "|", StringComparison.Ordinal)))
                {
                    Debug.LogWarning("[UIAuthoring] " + STEP_RELOCATE + ": 참조가 있어 남김: " + child.name);
                    continue;
                }

                Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            return removed;
        }

        private static void RedirectReferences(GameObject _root, Dictionary<Object, Object> _redirect)
        {
            foreach (Component component in _root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || _redirect.ContainsKey(component))
                {
                    continue;
                }

                var serialized = new SerializedObject(component);
                SerializedProperty iterator = serialized.GetIterator();
                bool changed = false;
                while (iterator.Next(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                        iterator.objectReferenceValue != null &&
                        _redirect.TryGetValue(iterator.objectReferenceValue, out Object target))
                    {
                        iterator.objectReferenceValue = target;
                        changed = true;
                    }
                }

                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>오브젝트마다 경로(같은 이름 형제 순번 포함)와 타입 순번으로 된 안정 키를 만든다.</summary>
        private static Dictionary<Object, string> BuildKeys(GameObject _root)
        {
            var keys = new Dictionary<Object, string>();
            foreach (Transform transform in _root.GetComponentsInChildren<Transform>(true))
            {
                string path = PathKey(transform, _root.transform);
                keys[transform.gameObject] = path + "|GO";
                var counts = new Dictionary<string, int>();
                foreach (Component component in transform.GetComponents<Component>())
                {
                    string typeName = component != null ? component.GetType().FullName : "Missing";
                    counts.TryGetValue(typeName, out int ordinal);
                    counts[typeName] = ordinal + 1;
                    if (component != null)
                    {
                        keys[component] = path + "|" + typeName + "#" + ordinal;
                    }
                }
            }

            return keys;
        }

        private static string PathKey(Transform _transform, Transform _root)
        {
            var segments = new List<string>();
            for (Transform current = _transform; current != null && current != _root; current = current.parent)
            {
                int ordinal = 0;
                Transform parent = current.parent;
                for (int i = 0; parent != null && i < current.GetSiblingIndex(); i++)
                {
                    if (parent.GetChild(i).name == current.name)
                    {
                        ordinal++;
                    }
                }

                segments.Add(current.name + "#" + ordinal);
            }

            segments.Add("~");
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static Dictionary<string, long> SnapshotIds(string _assetPath)
        {
            var ids = new Dictionary<string, long>();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
            foreach (KeyValuePair<Object, string> pair in BuildKeys(asset))
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Key, out string _, out long localId) && localId != 0)
                {
                    ids[pair.Value] = localId;
                }
            }

            return ids;
        }

        private static Dictionary<long, string> InvertIds(Dictionary<string, long> _ids)
        {
            var inverted = new Dictionary<long, string>();
            foreach (KeyValuePair<string, long> pair in _ids)
            {
                inverted[pair.Value] = pair.Key;
            }

            return inverted;
        }

        private static Dictionary<string, string> SnapshotRefs(Dictionary<Object, string> _keys)
        {
            var refs = new Dictionary<string, string>();
            foreach (KeyValuePair<Object, string> pair in _keys)
            {
                if (!(pair.Key is Component component) || component is Transform)
                {
                    continue;
                }

                var serialized = new SerializedObject(component);
                SerializedProperty iterator = serialized.GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                        iterator.propertyPath == "m_GameObject" || iterator.propertyPath == "m_Script" ||
                        iterator.propertyPath.StartsWith("m_CorrespondingSourceObject", StringComparison.Ordinal) ||
                        iterator.propertyPath.StartsWith("m_PrefabInstance", StringComparison.Ordinal) ||
                        iterator.propertyPath.StartsWith("m_PrefabAsset", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Object value = iterator.objectReferenceValue;
                    string target;
                    if (value == null)
                    {
                        target = "null";
                    }
                    else if (!_keys.TryGetValue(value, out target))
                    {
                        target = "EXT:" + AssetDatabase.GetAssetPath(value) + ":" + value.name + ":" + value.GetType().Name;
                    }

                    refs[pair.Value + PROPERTY_SEPARATOR + iterator.propertyPath] = target;
                }
            }

            return refs;
        }

        private static int VerifyRefs(string _assetPath, Dictionary<string, string> _before, Dictionary<string, string> _keyMap, string _step)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(_assetPath);
            try
            {
                Dictionary<string, string> after = SnapshotRefs(BuildKeys(root));
                var problems = new List<string>();
                foreach (KeyValuePair<string, string> pair in _before)
                {
                    int split = pair.Key.IndexOf(PROPERTY_SEPARATOR, StringComparison.Ordinal);
                    string owner = pair.Key.Substring(0, split);
                    string property = pair.Key.Substring(split);
                    if (!_keyMap.TryGetValue(owner, out string newOwner))
                    {
                        problems.Add("소유자 없음 " + pair.Key);
                        continue;
                    }

                    string expected = pair.Value;
                    if (_keyMap.TryGetValue(expected, out string mapped))
                    {
                        expected = mapped;
                    }

                    if (!after.TryGetValue(newOwner + property, out string actual))
                    {
                        problems.Add("속성 없음 " + newOwner + property);
                    }
                    else if (actual != expected)
                    {
                        problems.Add(newOwner + property + " : " + expected + " -> " + actual);
                    }
                }

                foreach (string problem in problems.Take(60))
                {
                    Debug.LogWarning("[UIAuthoring] " + _step + " 참조 대조: " + problem);
                }

                return problems.Count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int WriteIdMap(string _mapPath, Dictionary<string, long> _before, Dictionary<string, long> _after, Dictionary<string, string> _keyMap)
        {
            var builder = new StringBuilder();
            int count = 0;
            foreach (KeyValuePair<string, string> pair in _keyMap)
            {
                if (_before.TryGetValue(pair.Key, out long oldId) && _after.TryGetValue(pair.Value, out long newId) && oldId != newId)
                {
                    builder.Append(oldId).Append('\t').Append(newId).Append('\t').Append(pair.Key).Append('\n');
                    count++;
                }
            }

            File.WriteAllText(_mapPath, builder.ToString());
            return count;
        }

        private static Dictionary<long, long> ReadIdMap(string _mapPath)
        {
            if (!File.Exists(_mapPath))
            {
                return null;
            }

            var map = new Dictionary<long, long>();
            foreach (string line in File.ReadAllLines(_mapPath))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2 && long.TryParse(parts[0], out long oldId) && long.TryParse(parts[1], out long newId))
                {
                    map[oldId] = newId;
                }
            }

            return map;
        }

        private static Type ResolveComponentType(string _name)
        {
            Type[] matches = TypeCache.GetTypesDerivedFrom<Component>().Where(_type => _type.Name == _name).ToArray();
            return matches.FirstOrDefault(_type => _type.Namespace == "WeaponWorkshop") ?? (matches.Length == 1 ? matches[0] : null);
        }

        private static void EnsureFolder(string _folder)
        {
            if (AssetDatabase.IsValidFolder(_folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(_folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(_folder));
        }

        private static bool CheckRunnableEnvironment(string _stepName)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIAuthoring] FAIL " + _stepName + ": 플레이 모드에서는 실행할 수 없습니다.");
                return false;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + _stepName + ": 프리팹 스테이지가 열려 있습니다.");
                return false;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + _stepName + ": 열린 씬에 저장하지 않은 변경이 있습니다.");
                    return false;
                }
            }

            return true;
        }
    }
}
