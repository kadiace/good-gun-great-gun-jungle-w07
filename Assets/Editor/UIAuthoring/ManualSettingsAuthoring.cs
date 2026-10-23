using System;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop.Editor
{
    // 매뉴얼 설정 화면(UIManager.prefab)의 저작 명령이다.
    // 이미 있는 오브젝트는 삭제·재생성하지 않고, 없는 View 컴포넌트만 추가하고 실효 참조를 연결한다.
    internal static class ManualSettingsAuthoring
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string STEP = "ManualSettings/Author";

        [MenuItem("Tools/UI/Authoring/ManualSettings/Author")]
        private static void Author()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP + ": Edit mode required.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
            if (root == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP + ": Prefab not found: " + PREFAB_PATH);
                return;
            }

            bool changed = false;
            try
            {
                MouseSensitivityUI sensitivity = root.GetComponentInChildren<MouseSensitivityUI>(true);
                if (sensitivity == null)
                {
                    throw new InvalidOperationException("MouseSensitivityUI missing from UI prefab.");
                }

                StructuralFailureWarning warning = root.GetComponentInChildren<StructuralFailureWarning>(true);
                if (warning == null)
                {
                    throw new InvalidOperationException("StructuralFailureWarning missing from UI prefab.");
                }

                changed |= AuthorMouseSensitivity(sensitivity);
                changed |= AuthorStructuralFailureWarning(warning);

                if (!changed)
                {
                    Debug.Log("[UIAuthoring] OK " + STEP + ": already authored. No save.");
                    return;
                }

                if (PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH) == null)
                {
                    throw new InvalidOperationException("Prefab save failed: " + PREFAB_PATH);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP + ": " + exception.Message);
                return;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.ImportAsset(PREFAB_PATH);
            Debug.Log("[UIAuthoring] OK " + STEP +
                ": MouseSensitivityView·StructuralFailureWarningView 연결 완료.");
        }

        private static bool AuthorMouseSensitivity(MouseSensitivityUI _bridge)
        {
            SerializedObject bridgeSo = new SerializedObject(_bridge);
            bridgeSo.Update();

            Slider slider = ReadObject<Slider>(bridgeSo, "slider");
            TMP_InputField valueInput = ReadObject<TMP_InputField>(bridgeSo, "valueInput");
            if (slider == null || valueInput == null)
            {
                throw new InvalidOperationException("MouseSensitivityUI의 slider/valueInput 참조가 비어 있습니다.");
            }

            bool changed = false;
            MouseSensitivityView view = _bridge.GetComponent<MouseSensitivityView>();
            if (view == null)
            {
                view = _bridge.gameObject.AddComponent<MouseSensitivityView>();
                changed = true;
            }

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.Update();
            changed |= SetObject(viewSo, "m_slider", slider);
            changed |= SetObject(viewSo, "m_valueInput", valueInput);
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            changed |= SetObject(bridgeSo, "m_view", view);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool AuthorStructuralFailureWarning(StructuralFailureWarning _bridge)
        {
            bool changed = false;
            StructuralFailureWarningView view = _bridge.GetComponent<StructuralFailureWarningView>();
            if (view == null)
            {
                view = _bridge.gameObject.AddComponent<StructuralFailureWarningView>();
                changed = true;
            }

            SerializedObject bridgeSo = new SerializedObject(_bridge);
            bridgeSo.Update();
            changed |= SetObject(bridgeSo, "m_view", view);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static T ReadObject<T>(SerializedObject _so, string _name) where T : UnityEngine.Object
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            return property.objectReferenceValue as T;
        }

        private static bool SetObject(SerializedObject _so, string _name, UnityEngine.Object _value)
        {
            SerializedProperty property = _so.FindProperty(_name);
            if (property == null)
            {
                throw new InvalidOperationException("직렬화 필드를 찾을 수 없습니다: " + _name);
            }

            if (property.objectReferenceValue == _value)
            {
                return false;
            }

            property.objectReferenceValue = _value;
            return true;
        }
    }
}
