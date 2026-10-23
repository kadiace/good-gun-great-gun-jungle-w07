using System;
using UnityEditor;
using UnityEngine;

namespace WeaponWorkshop.Editor
{
    // 월드 상호작용 화면(UIManager.prefab)의 저작 명령이다.
    // 이미 있는 오브젝트는 삭제·재생성하지 않고, 없는 View 컴포넌트만 추가하고 실효 참조를 연결한다.
    internal static class WorldInteractionAuthoring
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";
        private const string STEP = "WorldInteraction/Author";

        [MenuItem("Tools/UI/Authoring/WorldInteraction/Author")]
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
                ModuleUnlockNotice notice = root.GetComponentInChildren<ModuleUnlockNotice>(true);
                if (notice == null)
                {
                    throw new InvalidOperationException("ModuleUnlockNotice missing from UI prefab.");
                }

                changed |= AuthorModuleUnlockNotice(notice);

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
            Debug.Log("[UIAuthoring] OK " + STEP + ": ModuleUnlockNoticeView 연결 완료.");
        }

        private static bool AuthorModuleUnlockNotice(ModuleUnlockNotice _bridge)
        {
            bool changed = false;
            ModuleUnlockNoticeView view = _bridge.GetComponent<ModuleUnlockNoticeView>();
            if (view == null)
            {
                view = _bridge.gameObject.AddComponent<ModuleUnlockNoticeView>();
                changed = true;
            }

            SerializedObject bridgeSo = new SerializedObject(_bridge);
            bridgeSo.Update();
            UIManager uiManager = ReadObject<UIManager>(bridgeSo, "uiManager");
            changed |= SetObject(bridgeSo, "m_view", view);
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            if (view != null)
            {
                SerializedObject viewSo = new SerializedObject(view);
                viewSo.Update();
                changed |= SetObject(viewSo, "m_uiManager", uiManager);
                viewSo.ApplyModifiedPropertiesWithoutUndo();
            }

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
