using System;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

namespace WeaponWorkshop.Editor
{
    // 전투 HUD(조준 허브·차지 게이지)의 View 컴포넌트와 ChargeGauge 표시 계층을 UIManager.prefab에 저작하는 명령.
    // 실행해도 기존 오브젝트를 지우거나 다시 만들지 않고, 이름으로 있는 것을 재사용해 참조만 연결·보정한다.
    public static class CombatHudAuthoring
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string STEP = "CombatHud/Author";
        private const string DISPLAY_NAME = "Display";
        private const string SEGMENTS_NAME = "Segments";
        private const string TEMPLATE_NAME = "SegmentTemplate";
        private const string FILL_NAME = "Fill";
        private const string LABEL_NAME = "Label";

        [MenuItem("Tools/UI/Authoring/CombatHud/Author")]
        public static void Author()
        {
            GameObject root = null;
            try
            {
                root = LoadPrefab(PREFAB_PATH);
                AimUI aimBridge = RequireSingleDescendantComponent<AimUI>(root.transform);
                ChargeGaugeUI gaugeBridge = RequireSingleDescendantComponent<ChargeGaugeUI>(root.transform);
                AimHudView aimView = EnsureComponent<AimHudView>(aimBridge.gameObject);
                ChargeGaugeView gaugeView = EnsureComponent<ChargeGaugeView>(gaugeBridge.gameObject);

                ConnectAimHudView(aimBridge, aimView);
                ConnectChargeGaugeView(gaugeBridge, gaugeView);

                SavePrefabOrThrow(root, PREFAB_PATH);
                Debug.Log($"[UIAuthoring] OK {STEP} AimHudView·ChargeGaugeView 참조 연결 완료");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UIAuthoring] FAIL {STEP} {exception.Message}");
            }
            finally
            {
                UnloadPrefab(root);
            }
        }

        private static void ConnectAimHudView(AimUI _bridge, AimHudView _view)
        {
            GameObject dot = RequireObjectReference<GameObject>(_bridge, "dot");
            SpreadCrosshair crosshair = RequireObjectReference<SpreadCrosshair>(_bridge, "crosshair");
            HomingLockGraphic homingLockMarker = RequireObjectReference<HomingLockGraphic>(_bridge, "homingLockMarker");
            HomingRetentionRing homingRetentionRing = RequireObjectReference<HomingRetentionRing>(_bridge, "homingRetentionRing");
            DamageDirectionIndicator damageDirectionIndicator =
                RequireObjectReference<DamageDirectionIndicator>(_bridge, "damageDirectionIndicator");

            SerializedObject viewData = new SerializedObject(_view);
            SetObject(viewData, "m_dot", dot);
            SetObject(viewData, "m_crosshair", crosshair);
            SetObject(viewData, "m_homingLockMarker", homingLockMarker);
            SetObject(viewData, "m_homingRetentionRing", homingRetentionRing);
            SetObject(viewData, "m_damageIndicator", damageDirectionIndicator);
            viewData.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject bridgeData = new SerializedObject(_bridge);
            SetObject(bridgeData, "m_view", _view);
            bridgeData.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConnectChargeGaugeView(ChargeGaugeUI _bridge, ChargeGaugeView _view)
        {
            SerializedObject bridgeData = new SerializedObject(_bridge);
            Vector2 offset = RequireProperty(bridgeData, "offset").vector2Value;
            Vector2 size = RequireProperty(bridgeData, "size").vector2Value;
            Color emptyColor = RequireProperty(bridgeData, "emptyColor").colorValue;
            Color fillColor = RequireProperty(bridgeData, "fillColor").colorValue;
            TMP_FontAsset labelFont = ResolveLabelFont(RequireProperty(bridgeData, "font").objectReferenceValue as TMP_FontAsset);

            EnsureChargeGaugeHierarchy(_bridge.transform, offset, size, emptyColor, fillColor, labelFont,
                out GameObject display, out GameObject segmentTemplate, out TMP_Text label);

            SerializedObject viewData = new SerializedObject(_view);
            SetObject(viewData, "m_root", display);
            SetObject(viewData, "m_segmentTemplate", segmentTemplate);
            SetObject(viewData, "m_label", label);
            viewData.ApplyModifiedPropertiesWithoutUndo();

            SetObject(bridgeData, "m_view", _view);
            bridgeData.ApplyModifiedPropertiesWithoutUndo();
        }

        // 기존 런타임 Build/RebuildSegments와 같은 이름·크기·라벨 값으로 표시 계층을 만든다. 이미 있으면 그대로 쓴다.
        private static void EnsureChargeGaugeHierarchy(Transform _bridge, Vector2 _offset, Vector2 _size,
            Color _emptyColor, Color _fillColor, TMP_FontAsset _labelFont,
            out GameObject _display, out GameObject _segmentTemplate, out TMP_Text _label)
        {
            Transform displayTransform = RequireChild(_bridge, _bridge.transform, DISPLAY_NAME);
            RectTransform displayRect = (RectTransform)displayTransform;
            displayRect.anchorMin = new Vector2(0.5f, 0.5f);
            displayRect.anchorMax = new Vector2(0.5f, 0.5f);
            displayRect.pivot = new Vector2(0.5f, 1f);
            displayRect.anchoredPosition = _offset;
            displayRect.sizeDelta = new Vector2(_size.x, _size.y + 16.5f);
            _display = displayTransform.gameObject;
            _display.SetActive(true);

            Transform segmentsTransform = RequireChild(_bridge, displayTransform, SEGMENTS_NAME);
            RectTransform segmentsRect = (RectTransform)segmentsTransform;
            segmentsRect.anchorMin = new Vector2(0f, 1f);
            segmentsRect.anchorMax = new Vector2(1f, 1f);
            segmentsRect.pivot = new Vector2(0.5f, 1f);
            segmentsRect.sizeDelta = new Vector2(0f, _size.y);
            segmentsRect.anchoredPosition = Vector2.zero;
            HorizontalLayoutGroup row = EnsureComponent<HorizontalLayoutGroup>(segmentsTransform.gameObject);
            row.spacing = 2.25f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            Transform templateTransform = RequireChild(_bridge, segmentsTransform, TEMPLATE_NAME);
            _segmentTemplate = templateTransform.gameObject;
            Image templateBackground = EnsureComponent<Image>(_segmentTemplate);
            templateBackground.color = _emptyColor;
            templateBackground.raycastTarget = false;

            Transform fillTransform = RequireChild(_bridge, templateTransform, FILL_NAME);
            RectTransform fillRect = (RectTransform)fillTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fill = EnsureComponent<Image>(fillTransform.gameObject);
            fill.color = _fillColor;
            fill.raycastTarget = false;

            Transform labelTransform = RequireChild(_bridge, displayTransform, LABEL_NAME);
            RectTransform labelRect = (RectTransform)labelTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 13.5f);
            labelRect.anchoredPosition = Vector2.zero;
            _label = EnsureComponent<TextMeshProUGUI>(labelTransform.gameObject);
            _label.font = _labelFont;
            _label.fontSize = 13.5f;
            _label.alignment = TextAlignmentOptions.Top;
            _label.raycastTarget = false;

            // 칸 수가 변하는 행은 저작된 템플릿 하나를 재사용하므로, 템플릿은 비활성으로 두고 레이아웃에서 제외한다.
            _segmentTemplate.SetActive(false);
        }

        private static TMP_FontAsset ResolveLabelFont(TMP_FontAsset _bridgeFont)
        {
            if (_bridgeFont != null)
            {
                return _bridgeFont;
            }

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null)
            {
                throw new InvalidOperationException("ChargeGaugeUI.font과 TMP_Settings 기본 폰트가 모두 비어 있습니다.");
            }

            return defaultFont;
        }

        // 이미 이름이 같은 자식이 있으면 재사용한다. 대상이 여러 개면 이름이 중복되므로 저장 전에 실패로 돌린다.
        private static Transform RequireChild(Transform _bridge, Transform _parent, string _name)
        {
            if (_parent.name == _bridge.name)
            {
                Transform direct = _parent.Find(_name);
                if (direct != null)
                {
                    return direct;
                }
            }
            else if (_parent.Find(_name) != null)
            {
                Transform existing = _parent.Find(_name);
                if (existing.parent != _parent)
                {
                    throw new InvalidOperationException($"'{_name}' 자식이 예상 위치가 아닙니다.");
                }

                return existing;
            }

            return CreateChild(_parent, _name);
        }

        private static Transform CreateChild(Transform _parent, string _name)
        {
            GameObject child = new GameObject(_name, typeof(RectTransform));
            child.layer = _parent.gameObject.layer;
            child.transform.SetParent(_parent, false);
            return child.transform;
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

        private static T RequireObjectReference<T>(MonoBehaviour _target, string _name) where T : UnityEngine.Object
        {
            SerializedProperty property = RequireProperty(new SerializedObject(_target), _name);
            UnityEngine.Object value = property.objectReferenceValue;
            if (value == null)
            {
                throw new InvalidOperationException($"'{_target.name}'의 직렬화 필드 '{_name}'이 비어 있습니다.");
            }

            if (!(value is T typed))
            {
                throw new InvalidOperationException(
                    $"'{_target.name}'의 직렬화 필드 '{_name}'이 {typeof(T).Name}이 아닙니다.");
            }

            return typed;
        }

        private static T EnsureComponent<T>(GameObject _target) where T : Component
        {
            T component = _target.GetComponent<T>();
            return component != null ? component : _target.AddComponent<T>();
        }

        // SaveAsPrefabAsset은 실패해도 예외 없이 null을 반환하므로 저장 실패를 OK로 기록하지 않게 확인한다.
        private static void SavePrefabOrThrow(GameObject _root, string _path)
        {
            if (PrefabUtility.SaveAsPrefabAsset(_root, _path) == null)
            {
                throw new InvalidOperationException("프리팹 저장 실패: " + _path);
            }
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

        private static void SetObject(SerializedObject _serialized, string _name, UnityEngine.Object _value)
        {
            SerializedProperty property = RequireProperty(_serialized, _name);
            property.objectReferenceValue = _value;
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
