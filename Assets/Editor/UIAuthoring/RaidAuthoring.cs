using System;
using System.IO;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

namespace WeaponWorkshop.Editor
{
    // NightRaidUI 전용 지도·HUD·전환 가림막을 저작하는 일회성 명령. 실행해도 기존 오브젝트를 재생성하지 않는다.
    public static class RaidAuthoring
    {
        private const string RAID_UI_PREFAB_PATH = "Assets/Prefabs/UI/NightRaidUI.prefab";
        private const string RAID_FOLDER = "Assets/Prefabs/UI/Raid";
        private const string TRANSITION_PREFAB_PATH = "Assets/Prefabs/UI/Raid/RaidTransitionCover.prefab";
        private const string UIMANAGER_PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string VIGNETTE_NAME = "LowHealthVignette";
        private const int REGION_SLOT_COUNT = 4;

        [MenuItem("Tools/UI/Authoring/Raid/Map")]
        public static void AuthorMap()
        {
            const string step = "Raid/Map";
            GameObject root = null;
            try
            {
                EnsureRaidFolder();
                root = LoadPrefab(RAID_UI_PREFAB_PATH);
                NightRaidUI bridge = RequireNightRaidUI(root);
                RaidMapView view = EnsureComponent<RaidMapView>(root);

                Transform mapPanel = RequireDescendant(root.transform, "MapPanel");
                Transform back = RequireDescendant(mapPanel, "Back");
                Button[] buttons = new Button[REGION_SLOT_COUNT];
                TMP_Text[] labels = new TMP_Text[REGION_SLOT_COUNT];
                for (int slot = 0; slot < REGION_SLOT_COUNT; slot++)
                {
                    Transform region = RequireDescendant(mapPanel, "Region" + (slot + 1));
                    buttons[slot] = RequireComponent<Button>(region);
                    labels[slot] = RequireComponent<TMP_Text>(RequireDescendant(region, "Label"));
                    RaidRegionHoverUI hover = EnsureComponent<RaidRegionHoverUI>(region.gameObject);
                    SerializedObject hoverData = new SerializedObject(hover);
                    SetObject(hoverData, "m_view", view);
                    SetInt(hoverData, "m_regionIndex", slot);
                    hoverData.ApplyModifiedPropertiesWithoutUndo();
                }

                Transform regionInfo = RequireDescendant(mapPanel, "RegionInfo");
                string[] sceneKeys = ReadRegionSceneKeys();
                SerializedObject viewData = new SerializedObject(view);
                SetObject(viewData, "m_panel", mapPanel.gameObject);
                SetObject(viewData, "m_backButton", RequireComponent<Button>(back));
                SetObject(viewData, "m_regionInfo", RequireComponent<TMP_Text>(regionInfo));
                SetObjectArray<Button>(viewData, "m_regionButtons", buttons);
                SetObjectArray<TMP_Text>(viewData, "m_regionLabels", labels);
                SetStringArray(viewData, "m_regionSceneKeys", sceneKeys);
                viewData.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject bridgeData = new SerializedObject(bridge);
                SetObject(bridgeData, "m_mapView", view);
                bridgeData.ApplyModifiedPropertiesWithoutUndo();

                SavePrefabOrThrow(root, RAID_UI_PREFAB_PATH);
                Debug.Log($"[UIAuthoring] OK {step} RaidMapView·지역 버튼 {buttons.Length}개·호버 컴포넌트·씬 키 연결");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL {step} {exception.Message}");
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        [MenuItem("Tools/UI/Authoring/Raid/Hud")]
        public static void AuthorHud()
        {
            const string step = "Raid/Hud";
            GameObject root = null;
            try
            {
                root = LoadPrefab(RAID_UI_PREFAB_PATH);
                NightRaidUI bridge = RequireNightRaidUI(root);
                RaidHudView view = EnsureComponent<RaidHudView>(root);

                Transform extractionPanel = RequireDescendant(root.transform, "ExtractionPanel");
                Transform gaugeFill = RequireDescendant(extractionPanel, "GaugeFill");
                Transform countdown = RequireDescendant(extractionPanel, "Countdown");

                SerializedObject viewData = new SerializedObject(view);
                SetObject(viewData, "m_extractionPanel", extractionPanel.gameObject);
                SetObject(viewData, "m_extractionFill", RequireComponent<Image>(gaugeFill));
                SetObject(viewData, "m_extractionText", RequireComponent<TMP_Text>(countdown));
                viewData.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject bridgeData = new SerializedObject(bridge);
                SetObject(bridgeData, "m_hudView", view);
                bridgeData.ApplyModifiedPropertiesWithoutUndo();

                RemoveLegacyVignette(root);

                SavePrefabOrThrow(root, RAID_UI_PREFAB_PATH);
                Debug.Log($"[UIAuthoring] OK {step} RaidHudView·탈출 진행 연결");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL {step} {exception.Message}");
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        [MenuItem("Tools/UI/Authoring/Raid/Vignette")]
        public static void AuthorVignette()
        {
            const string step = "Raid/Vignette";
            GameObject root = null;
            try
            {
                root = LoadPrefab(UIMANAGER_PREFAB_PATH);
                RaidHealthUI health = RequireSingleDescendantComponent<RaidHealthUI>(root.transform);
                Canvas rootCanvas = RequireRootCanvas(health.transform);
                RawImage vignette = EnsureVignette(rootCanvas);

                SerializedObject healthData = new SerializedObject(health);
                SetObject(healthData, "m_lowHealthVignette", vignette);
                healthData.ApplyModifiedPropertiesWithoutUndo();

                SavePrefabOrThrow(root, UIMANAGER_PREFAB_PATH);
                Debug.Log($"[UIAuthoring] OK {step} {UIMANAGER_PREFAB_PATH} 루트 Canvas 첫 자식 " +
                    VIGNETTE_NAME + "·RaidHealthUI.m_lowHealthVignette 연결");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL {step} {exception.Message}");
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        [MenuItem("Tools/UI/Authoring/Raid/Transition")]
        public static void AuthorTransition()
        {
            const string step = "Raid/Transition";
            GameObject root = null;
            try
            {
                EnsureRaidFolder();
                EnsureTransitionPrefab();
                root = LoadPrefab(RAID_UI_PREFAB_PATH);
                NightRaidUI bridge = RequireNightRaidUI(root);
                GameObject cover = AssetDatabase.LoadAssetAtPath<GameObject>(TRANSITION_PREFAB_PATH);
                if (cover == null)
                {
                    throw new InvalidOperationException("전환 프리팹을 불러오지 못했습니다: " + TRANSITION_PREFAB_PATH);
                }

                SerializedObject bridgeData = new SerializedObject(bridge);
                SetObject(bridgeData, "m_transitionCoverPrefab", cover);
                bridgeData.ApplyModifiedPropertiesWithoutUndo();
                SavePrefabOrThrow(root, RAID_UI_PREFAB_PATH);
                Debug.Log($"[UIAuthoring] OK {step} {TRANSITION_PREFAB_PATH} 생성·NightRaidUI.m_transitionCoverPrefab 연결");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL {step} {exception.Message}");
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        private static void EnsureTransitionPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TRANSITION_PREFAB_PATH);
            if (prefab == null)
            {
                GameObject temporary = new GameObject("RaidTransitionCover", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RaidTransitionView));
                try
                {
                    SavePrefabOrThrow(temporary, TRANSITION_PREFAB_PATH);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(temporary);
                }

                AssetDatabase.ImportAsset(TRANSITION_PREFAB_PATH);
            }

            GameObject root = LoadPrefab(TRANSITION_PREFAB_PATH);
            try
            {
                RectTransform rootRect = EnsureComponent<RectTransform>(root);
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                Canvas canvas = EnsureComponent<Canvas>(root);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;

                CanvasScaler scaler = EnsureComponent<CanvasScaler>(root);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
                scaler.referencePixelsPerUnit = 240f; // 기존 800 기준 배율(1920/800=2.4)을 유지하는 refPPU(100 × 2.4)

                GraphicRaycaster raycaster = EnsureComponent<GraphicRaycaster>(root);
                CanvasGroup group = EnsureComponent<CanvasGroup>(root);
                group.alpha = 0f;
                group.blocksRaycasts = true;
                group.interactable = false;

                Transform blackout = root.transform.Find("Blackout");
                if (blackout == null)
                {
                    GameObject blackoutObject = new GameObject("Blackout", typeof(RectTransform), typeof(CanvasRenderer),
                        typeof(Image));
                    blackout = blackoutObject.transform;
                    blackout.SetParent(root.transform, false);
                    RectTransform blackoutRect = (RectTransform)blackout;
                    blackoutRect.anchorMin = Vector2.zero;
                    blackoutRect.anchorMax = Vector2.one;
                    blackoutRect.offsetMin = Vector2.zero;
                    blackoutRect.offsetMax = Vector2.zero;
                }

                Image image = EnsureComponent<Image>(blackout.gameObject);
                image.color = Color.black;
                image.raycastTarget = true;

                RaidTransitionView view = EnsureComponent<RaidTransitionView>(root);
                SerializedObject viewData = new SerializedObject(view);
                SetObject(viewData, "m_canvas", canvas);
                SetObject(viewData, "m_group", group);
                SetObject(viewData, "m_raycaster", raycaster);
                viewData.ApplyModifiedPropertiesWithoutUndo();

                SavePrefabOrThrow(root, TRANSITION_PREFAB_PATH);
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        private static RawImage EnsureVignette(Canvas _canvas)
        {
            Transform canvasTransform = _canvas.transform;
            Transform existing = canvasTransform.Find(VIGNETTE_NAME);
            GameObject vignetteObject;
            if (existing == null)
            {
                vignetteObject = new GameObject(VIGNETTE_NAME, typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(RawImage));
                vignetteObject.layer = _canvas.gameObject.layer;
                RectTransform rect = (RectTransform)vignetteObject.transform;
                rect.SetParent(canvasTransform, false);
                ApplyFullStretch(rect);
                rect.SetAsFirstSibling();
            }
            else
            {
                RectTransform rect = existing.GetComponent<RectTransform>();
                if (rect == null)
                {
                    throw new InvalidOperationException($"'{VIGNETTE_NAME}' 오브젝트에 RectTransform이 없습니다.");
                }

                vignetteObject = existing.gameObject;
                ApplyFullStretch(rect);
                if (existing.GetSiblingIndex() != 0)
                {
                    existing.SetAsFirstSibling();
                }
            }

            vignetteObject.SetActive(true);
            RawImage image = EnsureComponent<RawImage>(vignetteObject);
            image.texture = null;
            image.raycastTarget = false;
            image.color = Color.clear;
            image.enabled = false;
            return image;
        }

        // SaveAsPrefabAsset은 실패해도 예외 없이 null을 반환하므로 저장 실패를 OK로 기록하지 않게 확인한다.
        private static void SavePrefabOrThrow(GameObject _root, string _path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(_root, _path) == null)
            {
                throw new InvalidOperationException("프리팹 저장 실패: " + _path);
            }
        }

        private static void ApplyFullStretch(RectTransform _rect)
        {
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }

        // 이전 Hud 저작이 NightRaidUI 루트에 만들었던 비네트를 새 위치(UIManager 루트 Canvas)로 옮기며 정리한다.
        private static void RemoveLegacyVignette(GameObject _root)
        {
            Transform legacy = _root.transform.Find(VIGNETTE_NAME);
            if (legacy == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            Debug.Log("[UIAuthoring] 이전 Hud 저작의 " + VIGNETTE_NAME +
                " 오브젝트를 NightRaidUI에서 제거했습니다.");
        }

        private static string[] ReadRegionSceneKeys()
        {
            TextAsset asset = Resources.Load<TextAsset>(TableManager.RaidMapResource);
            if (asset == null)
            {
                throw new InvalidOperationException("RaidMapData 리소스를 찾지 못했습니다: " +
                    TableManager.RaidMapResource);
            }

            RaidMapData table = JsonUtility.FromJson<RaidMapData>(asset.text);
            string[] keys = new string[REGION_SLOT_COUNT];
            for (int slot = 0; slot < keys.Length; slot++)
            {
                keys[slot] = table.SceneAt(slot);
                if (string.IsNullOrEmpty(keys[slot]))
                {
                    throw new InvalidOperationException(
                        $"선택 순서 {slot + 1}에 해당하는 지역 씬이 표에 없습니다.");
                }
            }

            return keys;
        }

        private static void EnsureRaidFolder()
        {
            if (AssetDatabase.IsValidFolder(RAID_FOLDER))
            {
                return;
            }

            string parent = Path.GetDirectoryName(RAID_FOLDER)?.Replace('\\', '/');
            string folderName = Path.GetFileName(RAID_FOLDER);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("저작 폴더 경로가 올바르지 않습니다: " + RAID_FOLDER);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static GameObject LoadPrefab(string _path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(_path);
            if (root == null)
            {
                throw new InvalidOperationException("프리팹을 불러오지 못했습니다: " + _path);
            }

            return root;
        }

        private static void UnloadPrefab(GameObject _root)
        {
            if (_root != null)
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
        }

        private static NightRaidUI RequireNightRaidUI(GameObject _root)
        {
            NightRaidUI bridge = _root.GetComponent<NightRaidUI>();
            if (bridge == null)
            {
                throw new InvalidOperationException("NightRaidUI 컴포넌트를 찾지 못했습니다.");
            }

            return bridge;
        }

        private static T RequireSingleDescendantComponent<T>(Transform _root) where T : Component
        {
            T[] components = _root.GetComponentsInChildren<T>(true);
            if (components.Length == 0)
            {
                throw new InvalidOperationException($"{typeof(T).Name} 컴포넌트를 찾지 못했습니다.");
            }

            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 컴포넌트가 {components.Length}개 있습니다. 대상을 하나로 정리하세요.");
            }

            return components[0];
        }

        private static Canvas RequireRootCanvas(Transform _target)
        {
            Canvas canvas = _target.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                throw new InvalidOperationException("RaidHealthUI가 속한 Canvas를 찾지 못했습니다.");
            }

            Canvas rootCanvas = canvas.rootCanvas;
            if (rootCanvas == null)
            {
                throw new InvalidOperationException("루트 Canvas를 찾지 못했습니다.");
            }

            return rootCanvas;
        }

        private static Transform RequireDescendant(Transform _root, string _name)
        {
            Transform found = FindDescendant(_root, _name);
            if (found == null)
            {
                throw new InvalidOperationException($"'{_name}' 오브젝트를 찾지 못했습니다.");
            }

            return found;
        }

        private static Transform FindDescendant(Transform _root, string _name)
        {
            if (_root == null)
            {
                return null;
            }

            if (_root.name == _name)
            {
                return _root;
            }

            for (int i = 0; i < _root.childCount; i++)
            {
                Transform found = FindDescendant(_root.GetChild(i), _name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static T RequireComponent<T>(Transform _target) where T : Component
        {
            T component = _target.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"'{_target.name}'에 {typeof(T).Name} 컴포넌트가 없습니다.");
            }

            return component;
        }

        private static T EnsureComponent<T>(GameObject _target) where T : Component
        {
            T component = _target.GetComponent<T>();
            return component != null ? component : _target.AddComponent<T>();
        }

        private static void SetObject(SerializedObject _serialized, string _name, UnityEngine.Object _value)
        {
            SerializedProperty property = RequireProperty(_serialized, _name);
            property.objectReferenceValue = _value;
        }

        private static void SetInt(SerializedObject _serialized, string _name, int _value)
        {
            SerializedProperty property = RequireProperty(_serialized, _name);
            property.intValue = _value;
        }

        private static void SetObjectArray<T>(SerializedObject _serialized, string _name, T[] _values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = RequireProperty(_serialized, _name);
            property.arraySize = _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
            }
        }

        private static void SetStringArray(SerializedObject _serialized, string _name, string[] _values)
        {
            SerializedProperty property = RequireProperty(_serialized, _name);
            property.arraySize = _values.Length;
            for (int i = 0; i < _values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = _values[i];
            }
        }

        private static SerializedProperty RequireProperty(SerializedObject _serialized, string _name)
        {
            SerializedProperty property = _serialized.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{_serialized.targetObject.GetType().Name}에 직렬화 필드 '{_name}'가 없습니다.");
            }

            return property;
        }
    }
}
