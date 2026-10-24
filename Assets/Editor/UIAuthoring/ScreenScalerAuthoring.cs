using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

namespace WeaponWorkshop.EditorTools
{
    /// <summary>
    /// UI Wave 12 Scene 단위 저작 도구: 남은 스케일러 및 비율 앵커 정리.
    /// 1. Assets/Prefabs/UI/Screens/EndChestEnding.prefab의 CanvasScaler를 1920x1080 match 1로 맞춘다.
    ///    (1920x1080 16:9에서는 배율이 동일하므로 값 환산 불필요, referencePixelsPerUnit 100 유지)
    /// 2. Assets/Prefabs/UI/StartUI_Manager.prefab의 YesNoUI Card 앵커 및 CenterMaxWidth 설정 상태를 검증/보존한다.
    /// - 멱등성 보장 (2회 실행 시 변경 0건)
    /// </summary>
    public static class ScreenScalerAuthoring
    {
        private const string MENU_PATH = "Tools/UI/Authoring/ScreenScaler/Author";
        private const string STEP_NAME = "ScreenScaler/Author";

        private const string END_CHEST_PREFAB_PATH = "Assets/Prefabs/UI/Screens/EndChestEnding.prefab";
        private const string START_UI_PREFAB_PATH = "Assets/Prefabs/UI/StartUI_Manager.prefab";

        private const float TARGET_WIDTH = 1920f;
        private const float TARGET_HEIGHT = 1080f;
        private const float TARGET_MATCH = 1f;

        [MenuItem(MENU_PATH)]
        public static void Author()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": 플레이 모드에서는 실행할 수 없습니다.");
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": 프리팹 스테이지가 열려 있습니다. 프리팹 스테이지를 닫고 다시 실행하세요.");
                return;
            }

            var summaries = new List<string>();

            try
            {
                bool endChestOk = AuthorEndChestEnding(summaries);
                if (!endChestOk)
                {
                    return;
                }

                bool startUiOk = VerifyStartUIYesNo(summaries);
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

        private static bool AuthorEndChestEnding(List<string> _summaries)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(END_CHEST_PREFAB_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": EndChestEnding 프리팹을 찾을 수 없습니다: " + END_CHEST_PREFAB_PATH);
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(END_CHEST_PREFAB_PATH);
            try
            {
                CanvasScaler scaler = root.GetComponentInChildren<CanvasScaler>(true);
                if (scaler == null)
                {
                    Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": EndChestEnding 프리팹에서 CanvasScaler 컴포넌트를 찾지 못했습니다.");
                    return false;
                }

                bool changed = false;
                var serialized = new SerializedObject(scaler);

                SerializedProperty modeProp = serialized.FindProperty("m_UiScaleMode");
                if (modeProp != null && modeProp.intValue != (int)CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    modeProp.intValue = (int)CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    changed = true;
                }

                SerializedProperty screenMatchProp = serialized.FindProperty("m_ScreenMatchMode");
                if (screenMatchProp != null && screenMatchProp.intValue != (int)CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                {
                    screenMatchProp.intValue = (int)CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    changed = true;
                }

                SerializedProperty resProp = serialized.FindProperty("m_ReferenceResolution");
                if (resProp != null)
                {
                    Vector2 currentRes = resProp.vector2Value;
                    if (!Mathf.Approximately(currentRes.x, TARGET_WIDTH) || !Mathf.Approximately(currentRes.y, TARGET_HEIGHT))
                    {
                        resProp.vector2Value = new Vector2(TARGET_WIDTH, TARGET_HEIGHT);
                        changed = true;
                    }
                }

                SerializedProperty matchProp = serialized.FindProperty("m_MatchWidthOrHeight");
                if (matchProp != null && !Mathf.Approximately(matchProp.floatValue, TARGET_MATCH))
                {
                    matchProp.floatValue = TARGET_MATCH;
                    changed = true;
                }

                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, END_CHEST_PREFAB_PATH);
                    if (saved == null)
                    {
                        Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": EndChestEnding 프리팹 저장 실패: " + END_CHEST_PREFAB_PATH);
                        return false;
                    }

                    _summaries.Add("EndChestEnding CanvasScaler (1920x1080 match 1 설정 완료)");
                }
                else
                {
                    _summaries.Add("EndChestEnding CanvasScaler (이미 match 1 설정됨, 변경 0건)");
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool VerifyStartUIYesNo(List<string> _summaries)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(START_UI_PREFAB_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL " + STEP_NAME + ": StartUI_Manager 프리팹을 찾을 수 없습니다: " + START_UI_PREFAB_PATH);
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(START_UI_PREFAB_PATH);
            try
            {
                Transform yesNoTransform = root.transform.Find("YesNoUI");
                if (yesNoTransform == null)
                {
                    Debug.LogWarning("[UIAuthoring] WARN " + STEP_NAME + ": StartUI_Manager에서 YesNoUI를 찾지 못했습니다.");
                    _summaries.Add("StartUI YesNoUI (미발견)");
                    return true;
                }

                Transform cardTransform = yesNoTransform.Find("Card");
                if (cardTransform != null && cardTransform is RectTransform cardRect)
                {
                    Vector2 anchorMin = cardRect.anchorMin;
                    Vector2 anchorMax = cardRect.anchorMax;
                    bool isCentered = Mathf.Approximately(anchorMin.x, 0.5f) &&
                                      Mathf.Approximately(anchorMin.y, 0.5f) &&
                                      Mathf.Approximately(anchorMax.x, 0.5f) &&
                                      Mathf.Approximately(anchorMax.y, 0.5f);

                    CenterMaxWidth centerMaxWidth = cardTransform.GetComponent<CenterMaxWidth>();
                    bool hasCenterMax = centerMaxWidth != null;

                    _summaries.Add("StartUI YesNoUI/Card (중앙고정앵커: " + (isCentered ? "확인" : "상이") +
                                  ", CenterMaxWidth: " + (hasCenterMax ? "확인" : "없음") + ")");
                }
                else
                {
                    _summaries.Add("StartUI YesNoUI/Card (Card 미발견)");
                }

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
