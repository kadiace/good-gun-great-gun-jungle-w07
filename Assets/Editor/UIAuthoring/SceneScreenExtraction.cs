using System;
using System.Collections.Generic;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.SceneManagement;

using Object = UnityEngine.Object;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// 씬에 직접 부착된 엔딩·은퇴 화면 루트를 Assets/Prefabs/UI/Screens/*.prefab으로 추출하고 씬에는 인스턴스만 남긴다.
    /// 참조를 스냅샷으로 보관해 변환 뒤 복원·검증하며, 하나라도 끊기면 해당 씬을 저장하지 않는다.
    /// </summary>
    public static class SceneScreenExtraction
    {
        private const string MENU_PATH = "Tools/UI/Authoring/SceneScreens/Extract";
        private const string LOG_PREFIX = "[UIAuthoring] ";
        private const string SCREEN_FOLDER = "Assets/Prefabs/UI/Screens";

        // 씬 인라인으로 확인된 화면만 대상으로 한다. TutorialSkipUI·TargetSelectionUI는
        // UIManager.prefab 인스턴스의 일부(stripped)라 추출 대상이 아니다.
        private static readonly ScreenSpec[] SPECS =
        {
            new ScreenSpec("Retire", "Assets/Scenes/Main.unity", typeof(RetireUI), "panel", ScreenAction.Extract, true),
            new ScreenSpec("EndChestEnding", "Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity",
                typeof(EndChestEndingUI), null, ScreenAction.Extract, true),
            new ScreenSpec("EndChestEnding", "Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity",
                typeof(EndChestEndingUI), null, ScreenAction.Extract, false)
        };

        private static readonly string[] RETIRE_REQUIRED_FIELDS =
        {
            "panel", "continueText", "lobbyButton", "continueButton", "dayManager"
        };

        private static readonly string[] END_CHEST_REQUIRED_FIELDS =
        {
            "chest", "whiteGroup", "endGroup", "buttonsGroup", "continueButton", "quitButton"
        };

        [MenuItem(MENU_PATH)]
        public static void Extract()
        {
            if (!EnsureSceneGuard(out string guardReason))
            {
                Fail(null, guardReason);
                return;
            }

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            StringBuilder summary = new StringBuilder();
            string currentScenePath = null;
            Scene currentScene = default;
            bool currentSceneDirty = false;
            bool failed = false;

            try
            {
                foreach (ScreenSpec spec in SPECS)
                {
                    if (!string.Equals(currentScenePath, spec.ScenePath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!FlushScene(currentScenePath, currentScene, ref currentSceneDirty))
                        {
                            failed = true;
                            break;
                        }

                        currentScenePath = spec.ScenePath;
                        currentScene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
                        currentSceneDirty = false;
                    }

                    try
                    {
                        SpecOutcome outcome = ProcessSpec(spec, currentScene);
                        if (outcome.Changed)
                        {
                            currentSceneDirty = true;
                        }

                        AppendOutcome(summary, spec, outcome);
                    }
                    catch (Exception exception)
                    {
                        Fail(spec.ScenePath, exception.Message);
                        failed = true;
                        break;
                    }
                }

                if (!failed && !FlushScene(currentScenePath, currentScene, ref currentSceneDirty))
                {
                    failed = true;
                }
            }
            catch (Exception exception)
            {
                if (!failed)
                {
                    Fail(currentScenePath, exception.Message);
                }

                failed = true;
            }
            finally
            {
                if (failed && currentScene.IsValid())
                {
                    try
                    {
                        // 검증에 실패한 씬은 저장하지 않고 버린다.
                        EditorSceneManager.CloseScene(currentScene, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(LOG_PREFIX + "SceneScreens/Extract 실패 씬 닫기 실패: " + exception.Message);
                    }
                }

                RestoreSceneSetup(originalSetup);
            }

            if (!failed)
            {
                Debug.Log(LOG_PREFIX + "OK SceneScreens/Extract " + summary.ToString().Trim());
            }
        }

        private static bool EnsureSceneGuard(out string _reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _reason = "플레이 모드에서는 실행할 수 없습니다.";
                return false;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                _reason = "프리팹 스테이지가 열려 있습니다. 프리팹 스테이지를 닫고 다시 실행하세요.";
                return false;
            }

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

        private static SpecOutcome ProcessSpec(ScreenSpec _spec, Scene _scene)
        {
            Component bridge = FindBridge(_spec.BridgeType);
            if (bridge == null)
            {
                throw new InvalidOperationException("씬에 " + _spec.BridgeType.Name + " 컴포넌트가 없습니다.");
            }

            GameObject root = ResolveScreenRoot(bridge, _spec.RootProperty);
            if (root == null)
            {
                throw new InvalidOperationException(_spec.RootProperty + " 화면 루트 참조가 비어 있습니다.");
            }

            Object correspondingRoot = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (_spec.Action == ScreenAction.ReportOnly)
            {
                if (correspondingRoot != null)
                {
                    return SpecOutcome.Skipped("이미 프리팹 내용(" +
                        AssetDatabase.GetAssetPath(correspondingRoot) + ")");
                }

                return SpecOutcome.Skipped("씬 부착이지만 이번 단위 추출 대상 아님");
            }

            string assetPath = SCREEN_FOLDER + "/" + _spec.ScreenName + ".prefab";
            bool isInstanceOfTarget = PrefabUtility.IsAnyPrefabInstanceRoot(root) &&
                string.Equals(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root), assetPath,
                    StringComparison.OrdinalIgnoreCase);
            if (isInstanceOfTarget)
            {
                return SpecOutcome.NoChange("이미 추출·연결됨");
            }

            if (correspondingRoot != null)
            {
                return SpecOutcome.Skipped("이미 프리팹 인스턴스의 일부(" +
                    AssetDatabase.GetAssetPath(correspondingRoot) + ")");
            }

            bool assetExists = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
            if (_spec.CreateAsset && assetExists)
            {
                throw new InvalidOperationException(
                    "프리팹 자산이 이미 있으나 첫 씬 인스턴스가 연결되지 않았습니다. 덮어쓰지 않습니다: " + assetPath);
            }

            if (!_spec.CreateAsset && !assetExists)
            {
                throw new InvalidOperationException(
                    "첫 씬에서 만든 프리팹 자산을 찾을 수 없습니다: " + assetPath);
            }

            EnsureScreenFolder();
            ReferenceSnapshot snapshot = CaptureReferences(root, _scene);
            ExternalConsumers consumers = CaptureExternalConsumers(_spec);

            // 프리팹 자산에는 씬 오브젝트 참조를 저장할 수 없으므로 외부 참조를 잠시 비운 뒤 변환하고,
            // 변환된 씬 인스턴스의 오버라이드로 되돌린다.
            NullOutgoingReferences(snapshot, root.transform);

            GameObject instanceRoot;
            if (_spec.CreateAsset)
            {
                // SaveAsPrefabAssetAndConnect는 저장된 프리팹 자산의 루트를 반환한다. 씬 인스턴스는 연결된 원래 오브젝트(root)다.
                GameObject savedAsset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, assetPath,
                    InteractionMode.AutomatedAction, out bool success);
                instanceRoot = root;
                if (!success || savedAsset == null || !PrefabUtility.IsAnyPrefabInstanceRoot(instanceRoot))
                {
                    throw new InvalidOperationException("프리팹 생성·연결 실패: " + assetPath);
                }
            }
            else
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                // Unity 6의 ConvertToPrefabInstance는 반환값 없이 전달한 오브젝트를 그 자리에서 인스턴스로 바꾼다.
                PrefabUtility.ConvertToPrefabInstance(root, prefabAsset,
                    CreateConvertSettings(), InteractionMode.AutomatedAction);
                instanceRoot = root;
                if (instanceRoot == null)
                {
                    throw new InvalidOperationException("프리팹 인스턴스 변환 실패: " + assetPath);
                }
            }

            string connectedPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
            if (!string.Equals(connectedPath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("연결된 프리팹 경로가 다릅니다: " + connectedPath);
            }

            RestoreAndVerifyReferences(snapshot, instanceRoot);
            VerifyRequiredBridgeFields(_spec);
            VerifyExternalConsumers(_spec, consumers);
            EditorSceneManager.MarkSceneDirty(_scene);

            return _spec.CreateAsset
                ? SpecOutcome.Extracted("생성·연결 " + snapshot.TotalCount + "개 참조 검증")
                : SpecOutcome.Converted("변환 " + snapshot.TotalCount + "개 참조 검증");
        }

        private static Component FindBridge(Type _bridgeType)
        {
            // 씬을 Single 모드로 열어 두므로 활성 씬만 탐색하면 충분하다.
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType() == _bridgeType)
                    {
                        return component;
                    }
                }
            }

            return null;
        }

        private static GameObject ResolveScreenRoot(Component _bridge, string _rootProperty)
        {
            if (string.IsNullOrEmpty(_rootProperty))
            {
                return _bridge.gameObject;
            }

            SerializedObject serializedBridge = new SerializedObject(_bridge);
            SerializedProperty property = serializedBridge.FindProperty(_rootProperty);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return null;
            }

            return property.objectReferenceValue as GameObject;
        }

        private static void EnsureScreenFolder()
        {
            if (AssetDatabase.IsValidFolder(SCREEN_FOLDER))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            {
                throw new InvalidOperationException("상위 폴더가 없습니다: Assets/Prefabs/UI");
            }

            AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Screens");
        }

        private static ConvertToPrefabInstanceSettings CreateConvertSettings()
        {
            return new ConvertToPrefabInstanceSettings
            {
                objectMatchMode = ObjectMatchMode.ByHierarchy,
                componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true,
                recordPropertyOverridesOfMatches = true,
                changeRootNameToAssetName = false
            };
        }

        private static bool FlushScene(string _scenePath, Scene _scene, ref bool _dirty)
        {
            if (!_dirty || string.IsNullOrEmpty(_scenePath) || !_scene.IsValid())
            {
                _dirty = false;
                return true;
            }

            bool saved = EditorSceneManager.SaveScene(_scene, _scenePath);
            _dirty = false;
            if (!saved)
            {
                Fail(_scenePath, "씬 저장 실패");
                return false;
            }

            return true;
        }

        private static void RestoreSceneSetup(SceneSetup[] _setup)
        {
            try
            {
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LOG_PREFIX + "SceneScreens/Extract 씬 구성 복원 실패: " + exception.Message);
            }
        }

        private static void AppendOutcome(StringBuilder _summary, ScreenSpec _spec, SpecOutcome _outcome)
        {
            if (_summary.Length > 0)
            {
                _summary.Append(' ');
            }

            _summary.Append(_spec.ScreenName);
            _summary.Append('[');
            _summary.Append(System.IO.Path.GetFileNameWithoutExtension(_spec.ScenePath));
            _summary.Append(':');
            _summary.Append(_outcome.Label);
            if (!string.IsNullOrEmpty(_outcome.Message))
            {
                _summary.Append('(');
                _summary.Append(_outcome.Message);
                _summary.Append(')');
            }

            _summary.Append(']');
        }

        private static void Fail(string _scenePath, string _reason)
        {
            string target = string.IsNullOrEmpty(_scenePath) ? "" : _scenePath + ": ";
            Debug.LogError(LOG_PREFIX + "FAIL SceneScreens/Extract " + target + _reason);
        }

        // ------------------------------------------------------------------
        // 참조 스냅샷
        // ------------------------------------------------------------------

        private sealed class ReferenceSnapshot
        {
            public readonly List<ReferenceRecord> Outgoing = new List<ReferenceRecord>();
            public readonly List<ReferenceRecord> Incoming = new List<ReferenceRecord>();

            public int TotalCount
            {
                get
                {
                    return Outgoing.Count + Incoming.Count;
                }
            }
        }

        private sealed class ReferenceRecord
        {
            // 추출 대상 내부 소유자: 루트 기준 상대 경로와 컴포넌트 종류로 변환 뒤 다시 찾는다.
            public string OwnerRelativePath;
            public string OwnerTypeName;
            public int OwnerTypeIndex;
            // 추출 대상 외부 소유자: 변환에 영향받지 않으므로 직접 보관한다.
            public Component Owner;
            public string PropertyPath;
            // 외부 대상(나가는 참조): 변환에 영향받지 않으므로 직접 보관한다.
            public Object ExternalTarget;
            // 내부 대상(들어오는 참조): 루트 기준 상대 경로와 컴포넌트 종류로 새 인스턴스에서 찾는다.
            public string TargetRelativePath;
            public string TargetTypeName;
            public int TargetTypeIndex;
        }

        private static ReferenceSnapshot CaptureReferences(GameObject _root, Scene _scene)
        {
            ReferenceSnapshot snapshot = new ReferenceSnapshot();
            Transform rootTransform = _root.transform;

            foreach (Transform transform in _root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    CaptureComponentReferences(component, rootTransform, snapshot.Outgoing, true);
                }
            }

            foreach (GameObject sceneRoot in _scene.GetRootGameObjects())
            {
                foreach (Transform transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (IsInside(transform, rootTransform))
                    {
                        continue;
                    }

                    foreach (Component component in transform.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            continue;
                        }

                        CaptureComponentReferences(component, rootTransform, snapshot.Incoming, false);
                    }
                }
            }

            return snapshot;
        }

        private static void CaptureComponentReferences(Component _component, Transform _rootTransform,
            List<ReferenceRecord> _records, bool _ownerInside)
        {
            SerializedObject serialized = new SerializedObject(_component);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    IsHierarchyProperty(property.propertyPath))
                {
                    continue;
                }

                Object target = property.objectReferenceValue;
                if (target == null || IsPersistent(target))
                {
                    continue;
                }

                Transform targetTransform = GetTransform(target);
                if (targetTransform == null)
                {
                    continue;
                }

                bool targetInside = IsInside(targetTransform, _rootTransform);
                if (_ownerInside)
                {
                    if (targetInside)
                    {
                        continue;
                    }

                    ReferenceRecord record = new ReferenceRecord
                    {
                        OwnerRelativePath = BuildRelativePath(_rootTransform, _component.transform),
                        OwnerTypeName = _component.GetType().FullName,
                        OwnerTypeIndex = IndexOfType(_component),
                        PropertyPath = property.propertyPath,
                        ExternalTarget = target
                    };
                    _records.Add(record);
                }
                else
                {
                    if (!targetInside)
                    {
                        continue;
                    }

                    ReferenceRecord record = new ReferenceRecord
                    {
                        Owner = _component,
                        PropertyPath = property.propertyPath,
                        TargetRelativePath = BuildRelativePath(_rootTransform, targetTransform),
                        TargetTypeName = target is GameObject ? null : target.GetType().FullName,
                        TargetTypeIndex = target is Component targetComponent
                            ? IndexOfType(targetComponent)
                            : -1
                    };
                    _records.Add(record);
                }
            }
        }

        private static void NullOutgoingReferences(ReferenceSnapshot _snapshot, Transform _root)
        {
            for (int i = 0; i < _snapshot.Outgoing.Count; i++)
            {
                ReferenceRecord record = _snapshot.Outgoing[i];
                Component owner = ResolveComponent(_root, record.OwnerRelativePath,
                    record.OwnerTypeName, record.OwnerTypeIndex);
                if (owner != null)
                {
                    SetObjectReference(owner, record.PropertyPath, null);
                }
            }
        }

        private static void RestoreAndVerifyReferences(ReferenceSnapshot _snapshot, GameObject _instanceRoot)
        {
            for (int i = 0; i < _snapshot.Outgoing.Count; i++)
            {
                ReferenceRecord record = _snapshot.Outgoing[i];
                Component owner = ResolveComponent(_instanceRoot.transform, record.OwnerRelativePath,
                    record.OwnerTypeName, record.OwnerTypeIndex);
                if (owner == null)
                {
                    throw new InvalidOperationException(
                        "참조 소유자를 찾지 못했습니다: " + record.OwnerRelativePath + " " + record.OwnerTypeName);
                }

                if (record.ExternalTarget == null)
                {
                    throw new InvalidOperationException("외부 참조 대상이 사라졌습니다: " + record.PropertyPath);
                }

                SetObjectReference(owner, record.PropertyPath, record.ExternalTarget);
                Object restored = GetObjectReference(owner, record.PropertyPath);
                if (restored != record.ExternalTarget)
                {
                    Component targetComponent = record.ExternalTarget as Component;
                    throw new InvalidOperationException(
                        "외부 참조 복원 실패: " + record.OwnerTypeName + "." + record.PropertyPath +
                        " (현재값=" + (restored != null ? restored.name : "null") +
                        ", 소유자 씬=" + owner.gameObject.scene.path +
                        ", 대상 씬=" + (targetComponent != null ? targetComponent.gameObject.scene.path : "?") +
                        ", 인스턴스=" + PrefabUtility.IsPartOfPrefabInstance(owner) + ")");
                }
            }

            for (int i = 0; i < _snapshot.Incoming.Count; i++)
            {
                ReferenceRecord record = _snapshot.Incoming[i];
                if (record.Owner == null)
                {
                    throw new InvalidOperationException("참조 소유자가 사라졌습니다: " + record.PropertyPath);
                }

                Object expected = ResolveTarget(_instanceRoot.transform, record.TargetRelativePath,
                    record.TargetTypeName, record.TargetTypeIndex);
                if (expected == null)
                {
                    throw new InvalidOperationException(
                        "내부 참조 대상을 새 인스턴스에서 찾지 못했습니다: " + record.TargetRelativePath);
                }

                SetObjectReference(record.Owner, record.PropertyPath, expected);
                if (GetObjectReference(record.Owner, record.PropertyPath) != expected)
                {
                    throw new InvalidOperationException(
                        "들어오는 참조 복원 실패: " + record.Owner.GetType().Name + "." + record.PropertyPath);
                }
            }
        }

        private static void VerifyRequiredBridgeFields(ScreenSpec _spec)
        {
            Component bridge = FindBridge(_spec.BridgeType);
            if (bridge == null)
            {
                throw new InvalidOperationException("변환 뒤 " + _spec.BridgeType.Name + " 컴포넌트를 찾지 못했습니다.");
            }

            string[] required = _spec.BridgeType == typeof(RetireUI)
                ? RETIRE_REQUIRED_FIELDS
                : END_CHEST_REQUIRED_FIELDS;
            SerializedObject serialized = new SerializedObject(bridge);
            foreach (string fieldName in required)
            {
                SerializedProperty property = serialized.FindProperty(fieldName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "필수 참조가 비었습니다: " + _spec.BridgeType.Name + "." + fieldName);
                }
            }
        }

        private sealed class ExternalConsumers
        {
            public int NonNullCount;
        }

        private static ExternalConsumers CaptureExternalConsumers(ScreenSpec _spec)
        {
            ExternalConsumers consumers = new ExternalConsumers();
            if (_spec.BridgeType == typeof(RetireUI))
            {
                RetireButton[] retireButtons = Object.FindObjectsByType<RetireButton>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (RetireButton retireButton in retireButtons)
                {
                    if (HasObjectReference(retireButton, "retireUI"))
                    {
                        consumers.NonNullCount++;
                    }
                }

                return consumers;
            }

            if (_spec.BridgeType == typeof(EndChestEndingUI))
            {
                EndChest[] chests = Object.FindObjectsByType<EndChest>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (EndChest chest in chests)
                {
                    if (HasObjectReference(chest, "endingUI"))
                    {
                        consumers.NonNullCount++;
                    }
                }
            }

            return consumers;
        }

        private static void VerifyExternalConsumers(ScreenSpec _spec, ExternalConsumers _before)
        {
            if (_before == null || _before.NonNullCount == 0)
            {
                return;
            }

            if (_spec.BridgeType == typeof(RetireUI))
            {
                RetireButton[] retireButtons = Object.FindObjectsByType<RetireButton>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                int nonNull = 0;
                foreach (RetireButton retireButton in retireButtons)
                {
                    if (HasObjectReference(retireButton, "retireUI"))
                    {
                        nonNull++;
                    }
                }

                if (nonNull < _before.NonNullCount)
                {
                    throw new InvalidOperationException(
                        "RetireButton.retireUI 참조가 끊겼습니다 (" + _before.NonNullCount + " -> " + nonNull + ")");
                }

                return;
            }

            if (_spec.BridgeType == typeof(EndChestEndingUI))
            {
                EndChest[] chests = Object.FindObjectsByType<EndChest>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                int nonNull = 0;
                foreach (EndChest chest in chests)
                {
                    if (HasObjectReference(chest, "endingUI"))
                    {
                        nonNull++;
                    }
                }

                if (nonNull < _before.NonNullCount)
                {
                    throw new InvalidOperationException(
                        "EndChest.endingUI 참조가 끊겼습니다 (" + _before.NonNullCount + " -> " + nonNull + ")");
                }
            }
        }

        private static bool HasObjectReference(Component _component, string _propertyName)
        {
            SerializedObject serialized = new SerializedObject(_component);
            SerializedProperty property = serialized.FindProperty(_propertyName);
            return property != null && property.objectReferenceValue != null;
        }

        private static bool IsHierarchyProperty(string _propertyPath)
        {
            return _propertyPath == "m_Father" ||
                   _propertyPath.StartsWith("m_Children", StringComparison.Ordinal);
        }

        private static bool IsPersistent(Object _target)
        {
            return EditorUtility.IsPersistent(_target) || AssetDatabase.Contains(_target);
        }

        private static Transform GetTransform(Object _target)
        {
            if (_target is GameObject gameObject)
            {
                return gameObject.transform;
            }

            if (_target is Component component)
            {
                return component.transform;
            }

            return null;
        }

        private static bool IsInside(Transform _target, Transform _root)
        {
            return _target == _root || _target.IsChildOf(_root);
        }

        private static string BuildRelativePath(Transform _root, Transform _target)
        {
            if (_target == _root)
            {
                return "";
            }

            List<int> indices = new List<int>();
            Transform current = _target;
            while (current != null && current != _root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            indices.Reverse();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < indices.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('/');
                }

                builder.Append(indices[i]);
            }

            return builder.ToString();
        }

        private static Transform ResolveTransform(Transform _root, string _relativePath)
        {
            if (string.IsNullOrEmpty(_relativePath))
            {
                return _root;
            }

            Transform current = _root;
            string[] segments = _relativePath.Split('/');
            foreach (string segment in segments)
            {
                if (!int.TryParse(segment, out int index) || index < 0 || index >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(index);
            }

            return current;
        }

        private static Component ResolveComponent(Transform _root, string _relativePath, string _typeName,
            int _typeIndex)
        {
            Transform transform = ResolveTransform(_root, _relativePath);
            return transform != null ? FindComponent(transform.gameObject, _typeName, _typeIndex) : null;
        }

        private static Object ResolveTarget(Transform _root, string _relativePath, string _typeName,
            int _typeIndex)
        {
            Transform transform = ResolveTransform(_root, _relativePath);
            if (transform == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_typeName))
            {
                return transform.gameObject;
            }

            return FindComponent(transform.gameObject, _typeName, _typeIndex);
        }

        private static Component FindComponent(GameObject _gameObject, string _typeName, int _typeIndex)
        {
            int seen = 0;
            foreach (Component component in _gameObject.GetComponents<Component>())
            {
                if (component == null || component.GetType().FullName != _typeName)
                {
                    continue;
                }

                if (seen == _typeIndex)
                {
                    return component;
                }

                seen++;
            }

            return null;
        }

        private static int IndexOfType(Component _component)
        {
            int index = 0;
            foreach (Component component in _component.gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                if (component == _component)
                {
                    return index;
                }

                if (component.GetType() == _component.GetType())
                {
                    index++;
                }
            }

            return index;
        }

        private static void SetObjectReference(Component _owner, string _propertyPath, Object _value)
        {
            SerializedObject serialized = new SerializedObject(_owner);
            SerializedProperty property = serialized.FindProperty(_propertyPath);
            if (property == null || property.objectReferenceValue == _value)
            {
                return;
            }

            property.objectReferenceValue = _value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.GetCorrespondingObjectFromSource(_owner) != null)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(_owner);
            }
        }

        private static Object GetObjectReference(Component _owner, string _propertyPath)
        {
            SerializedObject serialized = new SerializedObject(_owner);
            SerializedProperty property = serialized.FindProperty(_propertyPath);
            return property != null ? property.objectReferenceValue : null;
        }

        // ------------------------------------------------------------------
        // 스펙·결과
        // ------------------------------------------------------------------

        private enum ScreenAction
        {
            Extract,
            ReportOnly
        }

        private sealed class ScreenSpec
        {
            public readonly string ScreenName;
            public readonly string ScenePath;
            public readonly Type BridgeType;
            public readonly string RootProperty;
            public readonly ScreenAction Action;
            public readonly bool CreateAsset;

            public ScreenSpec(string _screenName, string _scenePath, Type _bridgeType, string _rootProperty,
                ScreenAction _action, bool _createAsset)
            {
                ScreenName = _screenName;
                ScenePath = _scenePath;
                BridgeType = _bridgeType;
                RootProperty = _rootProperty;
                Action = _action;
                CreateAsset = _createAsset;
            }
        }

        private sealed class SpecOutcome
        {
            public readonly bool Changed;
            public readonly string Label;
            public readonly string Message;

            private SpecOutcome(bool _changed, string _label, string _message)
            {
                Changed = _changed;
                Label = _label;
                Message = _message;
            }

            public static SpecOutcome Extracted(string _message)
            {
                return new SpecOutcome(true, "생성", _message);
            }

            public static SpecOutcome Converted(string _message)
            {
                return new SpecOutcome(true, "변환", _message);
            }

            public static SpecOutcome NoChange(string _message)
            {
                return new SpecOutcome(false, "변경 없음", _message);
            }

            public static SpecOutcome Skipped(string _message)
            {
                return new SpecOutcome(false, "건너뜀", _message);
            }
        }
    }
}
