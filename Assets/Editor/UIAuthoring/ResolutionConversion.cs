using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WeaponWorkshop.EditorTools
{
    public static class ResolutionConversion
    {
        private const string MenuRoot = "Tools/UI/Authoring/Resolution/";
        private const string UIManagerPath = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string StartUiPath = "Assets/Prefabs/UI/StartUI_Manager.prefab";
        private const string NightRaidPath = "Assets/Prefabs/UI/NightRaidUI.prefab";
        private const string DialoguePath = "Assets/Prefabs/UI/Dialogue.prefab";
        private const string RaidTransitionPath = "Assets/Prefabs/UI/Raid/RaidTransitionCover.prefab";

        private const float TargetWidth = 1920f;
        private const float TargetHeight = 1080f;
        private const float TargetMatch = 1f;

        [MenuItem(MenuRoot + "Convert")]
        public static void Convert()
        {
            Run(false);
        }

        [MenuItem(MenuRoot + "DryRun")]
        public static void DryRun()
        {
            Run(true);
        }

        private static void Run(bool _dryRun)
        {
            string prefix = _dryRun ? "DryRun" : "Convert";
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[UIAuthoring] FAIL Resolution/{prefix} 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError($"[UIAuthoring] FAIL Resolution/{prefix} 프리팹 스테이지를 닫고 실행해야 합니다.");
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    Debug.LogError($"[UIAuthoring] FAIL Resolution/{prefix} 열린 씬이 저장되지 않았습니다: {scene.path}");
                    return;
                }
            }

            if (IsAlreadyConverted())
            {
                Debug.Log($"[UIAuthoring] SKIP Resolution/{prefix} UIManager.prefab 루트 CanvasScaler가 이미 " +
                    $"{TargetWidth}x{TargetHeight} match {TargetMatch}입니다. 변환된 상태로 판단해 아무것도 하지 않습니다.");
                return;
            }

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var report = new ChangeReport();
            try
            {
                ConvertPrefab(NightRaidPath, _dryRun, 0.75f, false, true, prefix, report);
                ConvertPrefab(DialoguePath, _dryRun, 0.75f, false, true, prefix, report);
                ConvertPrefab(RaidTransitionPath, _dryRun, null, true, false, prefix, report);
                ConvertScenes(_dryRun, prefix, report);
                ConvertPrefab(UIManagerPath, _dryRun, null, true, true, prefix, report);
                ConvertPrefab(StartUiPath, _dryRun, null, true, true, prefix, report);

                Debug.Log($"[UIAuthoring] OK Resolution/{prefix} 총 {report.Total}건" +
                    (_dryRun ? " (저장하지 않음)" : " (저장 완료)"));
                report.LogAll(prefix);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL Resolution/{prefix} {exception.Message}\n{exception}");
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }

        private static bool IsAlreadyConverted()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UIManagerPath);
            if (root == null)
            {
                return false;
            }

            try
            {
                CanvasScaler scaler = FindRootScaler(root);
                if (scaler == null)
                {
                    return false;
                }

                Vector2 resolution = scaler.referenceResolution;
                return Mathf.Approximately(resolution.x, TargetWidth) &&
                    Mathf.Approximately(resolution.y, TargetHeight) &&
                    scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                    scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                    Mathf.Approximately(scaler.matchWidthOrHeight, TargetMatch);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConvertPrefab(string _path, bool _dryRun, float? _explicitCoefficient, bool _convertScaler,
            bool _convertChildren, string _prefix, ChangeReport _report)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(_path) == null)
            {
                throw new InvalidOperationException($"프리팹을 찾지 못했습니다: {_path}");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(_path);
            if (root == null)
            {
                throw new InvalidOperationException($"프리팹을 열지 못했습니다: {_path}");
            }

            try
            {
                float coefficient = _explicitCoefficient ?? GetCoefficient(root, _path);
                int changes = 0;
                if (_convertScaler)
                {
                    changes += ConvertRootScaler(root, coefficient, _path);
                }

                if (_convertChildren)
                {
                    changes += ConvertDirectObjects(root, coefficient);
                    changes += ConvertNestedInstanceModifications(root, coefficient);
                }

                _report.Record(_path, changes, coefficient);
                if (_dryRun || changes == 0)
                {
                    return;
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, _path) == null)
                {
                    throw new InvalidOperationException($"프리팹 저장에 실패했습니다: {_path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static float GetCoefficient(GameObject _root, string _path)
        {
            CanvasScaler scaler = FindRootScaler(_root);
            if (scaler == null)
            {
                throw new InvalidOperationException($"루트 CanvasScaler가 없어 계수를 계산할 수 없습니다: {_path}");
            }

            Vector2 resolution = scaler.referenceResolution;
            if (resolution.x <= 0f)
            {
                throw new InvalidOperationException($"referenceResolution.x가 0 이하입니다: {_path}");
            }

            float coefficient = TargetWidth / resolution.x;
            float expectedY = TargetHeight / coefficient;
            if (!Mathf.Approximately(resolution.y, expectedY))
            {
                Debug.LogWarning($"[UIAuthoring] WARN Resolution {_path} 기준 해상도 {resolution.x}x{resolution.y}의 " +
                    $"세로/가로 비율이 16:9가 아닙니다. 가로 기준 계수 {coefficient}를 적용합니다.");
            }

            return coefficient;
        }

        private static int ConvertRootScaler(GameObject _root, float _coefficient, string _path)
        {
            CanvasScaler scaler = FindRootScaler(_root);
            if (scaler == null)
            {
                throw new InvalidOperationException($"루트 CanvasScaler가 없습니다: {_path}");
            }

            Canvas canvas = _root.GetComponent<Canvas>();
            if (canvas != null && !canvas.isRootCanvas)
            {
                throw new InvalidOperationException($"루트가 아닌 CanvasScaler는 변환할 수 없습니다: {_path}");
            }

            int changes = 0;
            var serialized = new SerializedObject(scaler);
            changes += SetVector2(serialized, "m_ReferenceResolution", new Vector2(TargetWidth, TargetHeight));
            changes += SetInt(serialized, "m_UiScaleMode", (int)CanvasScaler.ScaleMode.ScaleWithScreenSize);
            changes += SetInt(serialized, "m_ScreenMatchMode", (int)CanvasScaler.ScreenMatchMode.MatchWidthOrHeight);
            changes += SetFloat(serialized, "m_MatchWidthOrHeight", TargetMatch);

            SerializedProperty referencePixelsPerUnit = serialized.FindProperty("m_ReferencePixelsPerUnit");
            if (referencePixelsPerUnit != null)
            {
                float previous = referencePixelsPerUnit.floatValue;
                // 9슬라이스 테두리의 캔버스 단위 두께 = 테두리px × refPPU / spritePPU. 새 배율 1에서 이전 물리 크기(× 이전 배율)를 유지하려면 refPPU에 계수를 곱한다.
                float next = previous * _coefficient;
                if (!Mathf.Approximately(previous, next))
                {
                    referencePixelsPerUnit.floatValue = next;
                    changes++;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return changes;
        }

        private static int ConvertDirectObjects(GameObject _root, float _coefficient)
        {
            int changes = 0;
            CanvasScaler rootScaler = FindRootScaler(_root);
            Transform scope = rootScaler != null ? rootScaler.transform : _root.transform;
            Component[] components = scope.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                if (rootScaler != null && component.transform == scope && IsRootCanvasFrame(component))
                {
                    continue;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(component) &&
                    !PrefabUtility.IsAddedComponentOverride(component) &&
                    !IsInsideAddedGameObjectOverride(component.transform))
                {
                    continue;
                }

                changes += ConvertComponent(component, _coefficient);
            }

            return changes;
        }

        // 중첩 인스턴스 안에 이 프리팹이 추가한 오브젝트(와 그 자식)는 소스 에셋 값이 아니므로 직접 환산 대상이다.
        private static bool IsInsideAddedGameObjectOverride(Transform _transform)
        {
            for (Transform current = _transform; current != null; current = current.parent)
            {
                if (PrefabUtility.IsAddedGameObjectOverride(current.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        // 프리팹 루트가 아닌 자식에 스케일러가 있을 수 있다(StartUI_Manager의 Canvas). ScaleWithScreenSize인 최상위 Canvas의 스케일러를 찾는다.
        private static CanvasScaler FindRootScaler(GameObject _root)
        {
            foreach (CanvasScaler scaler in _root.GetComponentsInChildren<CanvasScaler>(true))
            {
                Canvas canvas = scaler.GetComponent<Canvas>();
                Transform parent = scaler.transform.parent;
                bool isTopCanvas = canvas != null && (parent == null || parent.GetComponentInParent<Canvas>(true) == null);
                if (isTopCanvas && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    return scaler;
                }
            }

            return null;
        }

        // 스케일러가 프리팹 루트에 없으면 그 Canvas 하위만 환산한다. 루트에 있으면 제한 없음(null).
        private static Transform GetSourceScalerScope(GameObject _instanceRoot)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(_instanceRoot);
            if (source == null)
            {
                return null;
            }

            CanvasScaler scaler = FindRootScaler(source);
            return scaler != null && scaler.transform != source.transform ? scaler.transform : null;
        }

        private static bool IsTargetInScope(UnityEngine.Object _target, Transform _scope)
        {
            if (_scope == null)
            {
                return true;
            }

            Transform transform = _target is Component component ? component.transform : (_target as GameObject)?.transform;
            return transform != null && transform.IsChildOf(_scope);
        }

        // 루트 Canvas 오브젝트에서 건너뛸 것은 Canvas 틀(RectTransform·Canvas·CanvasScaler)뿐이다. 같은 오브젝트의 화면 브리지·View는 환산 대상이다.
        private static bool IsRootCanvasFrame(Component _component)
        {
            return _component is RectTransform || _component is Canvas || _component is CanvasScaler;
        }

        // 1차 변환이 루트 오브젝트의 브리지·View 픽셀 필드를 빠뜨렸던 것을 한 번만 보정한다(UIManager, 계수 0.75).
        [MenuItem(MenuRoot + "ConvertRootComponents")]
        public static void ConvertRootComponents()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UIManagerPath);
            try
            {
                HoveringUI hovering = root.GetComponentInChildren<HoveringUI>(true);
                SerializedProperty diameter = hovering != null ? new SerializedObject(hovering).FindProperty("scanLoadingDiameter") : null;
                if (diameter == null || !Mathf.Approximately(diameter.floatValue, 90f))
                {
                    Debug.Log("[UIAuthoring] SKIP Resolution/ConvertRootComponents 이미 보정됐거나 기준 필드를 찾지 못했습니다.");
                    return;
                }

                int changes = 0;
                var converted = new List<string>();
                foreach (Component component in root.GetComponents<Component>())
                {
                    if (component == null || IsRootCanvasFrame(component))
                    {
                        continue;
                    }

                    int count = ConvertComponent(component, 0.75f);
                    if (count > 0)
                    {
                        changes += count;
                        converted.Add(component.GetType().Name + " " + count);
                    }
                }

                if (changes > 0 && PrefabUtility.SaveAsPrefabAsset(root, UIManagerPath) == null)
                {
                    throw new InvalidOperationException("프리팹 저장에 실패했습니다: " + UIManagerPath);
                }

                Debug.Log("[UIAuthoring] OK Resolution/ConvertRootComponents " + changes + "건: " + string.Join(", ", converted));
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIAuthoring] FAIL Resolution/ConvertRootComponents " + exception.Message);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int ConvertNestedInstanceModifications(GameObject _root, float _coefficient)
        {
            int changes = 0;
            Transform[] transforms = _root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform == _root.transform)
                {
                    continue;
                }

                // 바깥쪽 인스턴스의 오버라이드 목록이 안쪽 중첩 인스턴스 대상까지 포함하므로 최외곽 인스턴스만 처리한다(이중 환산 방지).
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(transform.gameObject))
                {
                    continue;
                }

                changes += ConvertInstanceModifications(transform.gameObject, _coefficient);
            }

            return changes;
        }

        private static int ConvertInstanceModifications(GameObject _instanceRoot, float _coefficient)
        {
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(_instanceRoot);
            if (modifications == null || modifications.Length == 0)
            {
                return 0;
            }

            int changes = 0;
            Transform scope = GetSourceScalerScope(_instanceRoot);
            foreach (PropertyModification modification in modifications)
            {
                if (!IsTargetInScope(modification.target, scope))
                {
                    continue;
                }

                if (TryConvertModification(modification, _coefficient))
                {
                    changes++;
                }
            }

            if (changes > 0)
            {
                PrefabUtility.SetPropertyModifications(_instanceRoot, modifications);
            }

            return changes;
        }

        private static int ConvertScenes(bool _dryRun, string _prefix, ChangeReport _report)
        {
            int total = 0;
            foreach (string scenePath in GetBuildScenePaths())
            {
                if (!System.IO.File.Exists(scenePath))
                {
                    Debug.LogWarning($"[UIAuthoring] WARN Resolution {_prefix} 빌드 씬 파일이 없어 건너뜁니다: {scenePath}");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    int changes = 0;
                    var visited = new HashSet<GameObject>();
                    foreach (GameObject rootObject in scene.GetRootGameObjects())
                    {
                        GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(rootObject);
                        if (instanceRoot == null || !visited.Add(instanceRoot))
                        {
                            continue;
                        }

                        string sourcePath = GetSourcePath(instanceRoot);
                        if (sourcePath != UIManagerPath && sourcePath != StartUiPath)
                        {
                            continue;
                        }

                        float coefficient = GetCoefficient(instanceRoot, sourcePath);
                        changes += ConvertInstanceModifications(instanceRoot, coefficient);
                        changes += ConvertSceneAddedObjects(instanceRoot, coefficient);
                    }

                    _report.RecordScene(scenePath, changes);
                    total += changes;
                    if (!_dryRun && changes > 0 && !EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException($"씬 저장에 실패했습니다: {scenePath}");
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return total;
        }

        // 씬에서 프리팹 인스턴스 아래에 직접 추가한 오브젝트·컴포넌트는 오버라이드가 아니라 씬 값이므로 따로 환산한다.
        private static int ConvertSceneAddedObjects(GameObject _instanceRoot, float _coefficient)
        {
            int changes = 0;
            var addedObjects = new HashSet<GameObject>();
            foreach (AddedGameObject added in PrefabUtility.GetAddedGameObjects(_instanceRoot))
            {
                if (added.instanceGameObject == null || !addedObjects.Add(added.instanceGameObject))
                {
                    continue;
                }

                foreach (Component component in added.instanceGameObject.GetComponentsInChildren<Component>(true))
                {
                    if (component != null)
                    {
                        changes += ConvertComponent(component, _coefficient);
                    }
                }
            }

            foreach (AddedComponent added in PrefabUtility.GetAddedComponents(_instanceRoot))
            {
                Component component = added.instanceComponent;
                if (component == null || IsInsideAddedObject(component.transform, addedObjects))
                {
                    continue;
                }

                changes += ConvertComponent(component, _coefficient);
            }

            return changes;
        }

        private static bool IsInsideAddedObject(Transform _transform, HashSet<GameObject> _addedObjects)
        {
            for (Transform current = _transform; current != null; current = current.parent)
            {
                if (_addedObjects.Contains(current.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSourcePath(GameObject _instanceRoot)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(_instanceRoot);
            return source != null ? AssetDatabase.GetAssetPath(source) : null;
        }

        private static IEnumerable<string> GetBuildScenePaths()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    yield return scene.path;
                }
            }
        }

        private static bool TryConvertModification(PropertyModification _modification, float _coefficient)
        {
            if (_modification.target == null || string.IsNullOrEmpty(_modification.propertyPath))
            {
                return false;
            }

            if (_modification.target is CanvasScaler scaler)
            {
                return TryConvertScalerModification(_modification, scaler, _coefficient);
            }

            FieldRule[] rules;
            if (!Rules.TryGetValue(_modification.target.GetType().Name, out rules))
            {
                return false;
            }

            foreach (FieldRule rule in rules)
            {
                if (!rule.Matches(_modification.propertyPath))
                {
                    continue;
                }

                if (!TryParse(_modification.value, out float value))
                {
                    return false;
                }

                if (rule.SkipNegative && value < 0f)
                {
                    return false;
                }

                float next = value * _coefficient;
                _modification.value = FormatValue(next, rule.Kind);
                return true;
            }

            return false;
        }

        private static bool MatchesAxis(string _path, string _rulePath, ValueKind _kind)
        {
            if (_path == _rulePath)
            {
                return true;
            }

            string prefix = _rulePath + ".";
            if (!_path.StartsWith(prefix, StringComparison.Ordinal) || _path.Length != prefix.Length + 1)
            {
                return false;
            }

            char axis = _path[_path.Length - 1];
            if (_kind == ValueKind.Vector2)
            {
                return axis == 'x' || axis == 'y';
            }

            if (_kind == ValueKind.Vector4)
            {
                return axis == 'x' || axis == 'y' || axis == 'z' || axis == 'w';
            }

            return false;
        }

        private static bool TryConvertScalerModification(PropertyModification _modification,
            CanvasScaler _scaler, float _coefficient)
        {
            Canvas canvas = _scaler.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas)
            {
                return false;
            }

            switch (_modification.propertyPath)
            {
                case "m_ReferenceResolution.x":
                    _modification.value = FormatValue(TargetWidth, ValueKind.Float);
                    return true;
                case "m_ReferenceResolution.y":
                    _modification.value = FormatValue(TargetHeight, ValueKind.Float);
                    return true;
                case "m_MatchWidthOrHeight":
                    _modification.value = FormatValue(TargetMatch, ValueKind.Float);
                    return true;
                case "m_UiScaleMode":
                    _modification.value = FormatValue((int)CanvasScaler.ScaleMode.ScaleWithScreenSize, ValueKind.Int);
                    return true;
                case "m_ScreenMatchMode":
                    _modification.value = FormatValue((int)CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, ValueKind.Int);
                    return true;
                case "m_ReferencePixelsPerUnit":
                    if (!TryParse(_modification.value, out float value))
                    {
                        return false;
                    }

                    _modification.value = FormatValue(value * _coefficient, ValueKind.Float);
                    return true;
                default:
                    return false;
            }
        }

        private static int ConvertComponent(Component _component, float _coefficient)
        {
            FieldRule[] rules;
            if (!Rules.TryGetValue(_component.GetType().Name, out rules))
            {
                return 0;
            }

            var serialized = new SerializedObject(_component);
            int changes = 0;
            foreach (FieldRule rule in rules)
            {
                SerializedProperty property = serialized.FindProperty(rule.Path);
                if (property == null)
                {
                    continue;
                }

                switch (rule.Kind)
                {
                    case ValueKind.Float:
                        if (property.propertyType == SerializedPropertyType.Float)
                        {
                            float value = property.floatValue;
                            if (!rule.SkipNegative || value >= 0f)
                            {
                                float next = value * _coefficient;
                                if (!Mathf.Approximately(value, next))
                                {
                                    property.floatValue = next;
                                    changes++;
                                }
                            }
                        }

                        break;
                    case ValueKind.Int:
                        if (property.propertyType == SerializedPropertyType.Integer)
                        {
                            int value = property.intValue;
                            int next = Mathf.RoundToInt(value * _coefficient);
                            if (value != next)
                            {
                                property.intValue = next;
                                changes++;
                            }
                        }

                        break;
                    case ValueKind.Vector2:
                        if (property.propertyType == SerializedPropertyType.Vector2)
                        {
                            Vector2 value = property.vector2Value;
                            if (!rule.SkipNegative || (value.x >= 0f && value.y >= 0f))
                            {
                                Vector2 next = value * _coefficient;
                                if (value != next)
                                {
                                    property.vector2Value = next;
                                    changes++;
                                }
                            }
                        }

                        break;
                    case ValueKind.Vector4:
                        if (property.propertyType == SerializedPropertyType.Vector4)
                        {
                            Vector4 value = property.vector4Value;
                            Vector4 next = value * _coefficient;
                            if (value != next)
                            {
                                property.vector4Value = next;
                                changes++;
                            }
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (changes > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return changes;
        }

        private static int SetFloat(SerializedObject _serialized, string _path, float _value)
        {
            SerializedProperty property = _serialized.FindProperty(_path);
            if (property == null || Mathf.Approximately(property.floatValue, _value))
            {
                return 0;
            }

            property.floatValue = _value;
            return 1;
        }

        private static int SetInt(SerializedObject _serialized, string _path, int _value)
        {
            SerializedProperty property = _serialized.FindProperty(_path);
            if (property == null || property.intValue == _value)
            {
                return 0;
            }

            property.intValue = _value;
            return 1;
        }

        private static int SetVector2(SerializedObject _serialized, string _path, Vector2 _value)
        {
            SerializedProperty property = _serialized.FindProperty(_path);
            if (property == null || property.vector2Value == _value)
            {
                return 0;
            }

            property.vector2Value = _value;
            return 1;
        }

        private static bool TryParse(string _value, out float _result)
        {
            return float.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out _result);
        }

        private static string FormatValue(float _value, ValueKind _kind)
        {
            if (_kind == ValueKind.Int)
            {
                return Mathf.RoundToInt(_value).ToString(CultureInfo.InvariantCulture);
            }

            return _value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static FieldRule R(string _path, ValueKind _kind, bool _skipNegative = false)
        {
            return new FieldRule(_path, _kind, _skipNegative);
        }

        private enum ValueKind
        {
            Float,
            Int,
            Vector2,
            Vector4
        }

        private sealed class FieldRule
        {
            public FieldRule(string _path, ValueKind _kind, bool _skipNegative)
            {
                Path = _path;
                Kind = _kind;
                SkipNegative = _skipNegative;
            }

            public string Path { get; }
            public ValueKind Kind { get; }
            public bool SkipNegative { get; }

            public bool Matches(string _path)
            {
                return MatchesAxis(_path, Path, Kind);
            }
        }

        private sealed class ChangeReport
        {
            private readonly List<Entry> m_entries = new List<Entry>();

            public int Total { get; private set; }

            public void Record(string _path, int _changes, float _coefficient)
            {
                m_entries.Add(new Entry(_path, _changes, _coefficient, false));
                Total += _changes;
            }

            public void RecordScene(string _path, int _changes)
            {
                m_entries.Add(new Entry(_path, _changes, 0f, true));
                Total += _changes;
            }

            public void LogAll(string _prefix)
            {
                foreach (Entry entry in m_entries)
                {
                    string kind = entry.IsScene ? "씬" : "프리팹";
                    string coefficient = entry.IsScene ? string.Empty : $" 계수 {entry.Coefficient}";
                    Debug.Log($"[UIAuthoring] {_prefix} {kind} {entry.Path}: {entry.Changes}건{coefficient}");
                }
            }

            private sealed class Entry
            {
                public Entry(string _path, int _changes, float _coefficient, bool _isScene)
                {
                    Path = _path;
                    Changes = _changes;
                    Coefficient = _coefficient;
                    IsScene = _isScene;
                }

                public string Path { get; }
                public int Changes { get; }
                public float Coefficient { get; }
                public bool IsScene { get; }
            }
        }

        private static readonly Dictionary<string, FieldRule[]> Rules =
            new Dictionary<string, FieldRule[]>(StringComparer.Ordinal)
            {
                {
                    "RectTransform", new[]
                    {
                        R("m_SizeDelta", ValueKind.Vector2),
                        R("m_AnchoredPosition", ValueKind.Vector2)
                    }
                },
                {
                    "TextMeshProUGUI", new[]
                    {
                        R("m_fontSize", ValueKind.Float),
                        R("m_fontSizeMin", ValueKind.Float),
                        R("m_fontSizeMax", ValueKind.Float),
                        R("m_margin", ValueKind.Vector4)
                    }
                },
                {
                    "TextMeshPro", new[]
                    {
                        R("m_fontSize", ValueKind.Float),
                        R("m_fontSizeMin", ValueKind.Float),
                        R("m_fontSizeMax", ValueKind.Float),
                        R("m_margin", ValueKind.Vector4)
                    }
                },
                {
                    "Text", new[]
                    {
                        R("m_FontSize", ValueKind.Int),
                        R("m_ResizeTextMinSize", ValueKind.Int),
                        R("m_ResizeTextMaxSize", ValueKind.Int)
                    }
                },
                {
                    "HorizontalLayoutGroup", new[]
                    {
                        R("m_Padding.m_Left", ValueKind.Int),
                        R("m_Padding.m_Right", ValueKind.Int),
                        R("m_Padding.m_Top", ValueKind.Int),
                        R("m_Padding.m_Bottom", ValueKind.Int),
                        R("m_Spacing", ValueKind.Float)
                    }
                },
                {
                    "VerticalLayoutGroup", new[]
                    {
                        R("m_Padding.m_Left", ValueKind.Int),
                        R("m_Padding.m_Right", ValueKind.Int),
                        R("m_Padding.m_Top", ValueKind.Int),
                        R("m_Padding.m_Bottom", ValueKind.Int),
                        R("m_Spacing", ValueKind.Float)
                    }
                },
                {
                    "GridLayoutGroup", new[]
                    {
                        R("m_Padding.m_Left", ValueKind.Int),
                        R("m_Padding.m_Right", ValueKind.Int),
                        R("m_Padding.m_Top", ValueKind.Int),
                        R("m_Padding.m_Bottom", ValueKind.Int),
                        R("m_Spacing", ValueKind.Vector2),
                        R("m_CellSize", ValueKind.Vector2)
                    }
                },
                {
                    "LayoutElement", new[]
                    {
                        R("m_MinWidth", ValueKind.Float, true),
                        R("m_MinHeight", ValueKind.Float, true),
                        R("m_PreferredWidth", ValueKind.Float, true),
                        R("m_PreferredHeight", ValueKind.Float, true)
                    }
                },
                {
                    "Outline", new[] { R("m_EffectDistance", ValueKind.Vector2) }
                },
                {
                    "Shadow", new[] { R("m_EffectDistance", ValueKind.Vector2) }
                },
                {
                    "HoveringUI", new[]
                    {
                        R("connectorWidth", ValueKind.Float),
                        R("textOffset", ValueKind.Vector2),
                        R("screenPadding", ValueKind.Float),
                        R("scanLoadingDiameter", ValueKind.Float),
                        R("scanLoadingThickness", ValueKind.Float)
                    }
                },
                {
                    "HoverTooltipView", new[]
                    {
                        R("m_connectorWidth", ValueKind.Float),
                        R("m_textOffset", ValueKind.Vector2),
                        R("m_screenPadding", ValueKind.Float),
                        R("m_scanLoadingDiameter", ValueKind.Float),
                        R("m_scanLoadingThickness", ValueKind.Float)
                    }
                },
                {
                    "DaySettlementUI", new[]
                    {
                        R("dailyRentGap", ValueKind.Float),
                        R("dailyRentWidth", ValueKind.Float)
                    }
                },
                {
                    "TutorialTargetUI", new[]
                    {
                        R("bounceHeight", ValueKind.Float),
                        R("arrowSize", ValueKind.Float)
                    }
                },
                {
                    "TutorialQuestView", new[]
                    {
                        R("m_compactWidth", ValueKind.Float)
                    }
                },
                {
                    "QuestInfoTextSizing", new[]
                    {
                        R("preferredFontSize", ValueKind.Float),
                        R("minimumFontSize", ValueKind.Float),
                        R("maximumHeight", ValueKind.Float),
                        R("bottomMargin", ValueKind.Float)
                    }
                },
                {
                    "ButtonHoverOutline", new[]
                    {
                        R("normalThickness", ValueKind.Float),
                        R("hoverThickness", ValueKind.Float)
                    }
                },
                {
                    "RotationCircleGraphic", new FieldRule[0]
                },
                {
                    "ScopeOverlayGraphic", new[]
                    {
                        R("crosshairWidth", ValueKind.Float),
                        R("reticleRadius", ValueKind.Float),
                        R("redDotRadius", ValueKind.Float)
                    }
                },
                {
                    "SpreadCrosshair", new[]
                    {
                        R("tickLength", ValueKind.Float),
                        R("tickWidth", ValueKind.Float),
                        R("minimumGap", ValueKind.Float)
                    }
                },
                {
                    "TagFlowLayout", new[]
                    {
                        R("spacing", ValueKind.Vector2),
                        R("textPadding", ValueKind.Vector2),
                        R("minimumHeight", ValueKind.Float)
                    }
                },
                {
                    "ChargeGaugeUI", new[]
                    {
                        R("offset", ValueKind.Vector2),
                        R("size", ValueKind.Vector2)
                    }
                }
            };
    }
}
