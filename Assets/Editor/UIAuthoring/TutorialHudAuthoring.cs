using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using WeaponWorkshop;

// 튜토리얼 HUD(UIManager.prefab)의 저작 명령. 기존 브리지 컴포넌트와 같은 GameObject에
// Tutorial*View 컴포넌트를 추가하고 브리지의 m_view 참조와 건너뛰기 버튼 Navigation을 연결한다.
// 런타임 UI 계층을 새로 만들지 않으며, 멱등이라 이미 저작돼 있으면 연결만 검증·보정한다.
public static class TutorialHudAuthoring
{
    private const string PREFAB_PATH = "Assets/Prefabs/Managers/UIManager.prefab";

    [MenuItem("Tools/UI/Authoring/TutorialHud/Author")]
    public static void Author()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) == null)
        {
            Debug.LogError("[UIAuthoring] FAIL TutorialHud/Author: UIManager 프리팹을 찾을 수 없습니다. " + PREFAB_PATH);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            string summary = AuthorContents(root);
            if (PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH) == null)
            {
                Debug.LogError("[UIAuthoring] FAIL TutorialHud/Author: 프리팹 저장 실패 " + PREFAB_PATH);
                return;
            }

            Debug.Log("[UIAuthoring] OK TutorialHud/Author: " + summary);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[UIAuthoring] FAIL TutorialHud/Author: " + exception.Message + "\n" + exception.StackTrace);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string AuthorContents(GameObject _root)
    {
        TutorialQuestUI quest = FindBridge<TutorialQuestUI>(_root);
        TutorialTargetUI target = FindBridge<TutorialTargetUI>(_root);
        TutorialButtonHighlightUI highlight = FindBridge<TutorialButtonHighlightUI>(_root);
        TutorialSkipUI skip = FindBridge<TutorialSkipUI>(_root);

        bool created = false;
        TutorialQuestView questView = EnsureView<TutorialQuestView>(quest, ref created);
        TutorialTargetView targetView = EnsureView<TutorialTargetView>(target, ref created);
        TutorialButtonHighlightView highlightView = EnsureView<TutorialButtonHighlightView>(highlight, ref created);
        TutorialSkipView skipView = EnsureView<TutorialSkipView>(skip, ref created);

        SetViewReference(quest, questView);
        SetViewReference(target, targetView);
        SetViewReference(highlight, highlightView);
        SetViewReference(skip, skipView);

        // 기존 코드의 튜토리얼 목표 패널 좁은 폭(300)을 저작 값으로 고정한다.
        SerializedObject questViewSo = new SerializedObject(questView);
        SerializedProperty compactWidth = questViewSo.FindProperty("m_compactWidth");
        if (compactWidth == null)
        {
            Fail("TutorialQuestView에 m_compactWidth 직렬화 필드가 없습니다.");
        }

            compactWidth.floatValue = 225f;
        questViewSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject skipSo = new SerializedObject(skip);
        Button skipButton = skipSo.FindProperty("skipButton").objectReferenceValue as Button;
        Button tutorialButton = skipSo.FindProperty("tutorialButton").objectReferenceValue as Button;
        if (skipButton == null || tutorialButton == null)
        {
            Fail("TutorialSkipUI의 skipButton·tutorialButton 참조가 비어 있어 Navigation을 저작할 수 없습니다.");
        }

        ApplyLinearNavigation(skipButton, null, tutorialButton);
        ApplyLinearNavigation(tutorialButton, skipButton, null);

        return (created ? "신규 View 생성 포함" : "기존 저작물 검증") +
            ": TutorialQuestView·TutorialTargetView·TutorialButtonHighlightView·TutorialSkipView " +
            "브리지 m_view 연결, TutorialQuestView.compactWidth=300, " +
            "TutorialSkipUI 버튼 Explicit Navigation(비순환)";
    }

    private static T FindBridge<T>(GameObject _root) where T : Component
    {
        T bridge = _root.GetComponentInChildren<T>(true);
        if (bridge == null)
        {
            Fail("UIManager 프리팹에 " + typeof(T).Name + " 컴포넌트가 없습니다.");
        }

        return bridge;
    }

    private static T EnsureView<T>(Component _bridge, ref bool _created) where T : Component
    {
        GameObject host = _bridge.gameObject;
        T view = host.GetComponent<T>();
        if (view == null)
        {
            view = host.AddComponent<T>();
            _created = true;
        }

        return view;
    }

    private static void SetViewReference(Component _bridge, Component _view)
    {
        SerializedObject bridgeSo = new SerializedObject(_bridge);
        bridgeSo.Update();
        SerializedProperty viewProperty = bridgeSo.FindProperty("m_view");
        if (viewProperty == null)
        {
            Fail(_bridge.GetType().Name + "에 m_view 직렬화 필드가 없습니다.");
        }

        viewProperty.objectReferenceValue = _view;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 두 버튼을 한 방향으로만 잇는다. 역방향을 비워 두어 Explicit Navigation이 순환하지 않는다.
    private static void ApplyLinearNavigation(Selectable _selectable, Selectable _left, Selectable _right)
    {
        Navigation navigation = _selectable.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.wrapAround = false;
        navigation.selectOnUp = null;
        navigation.selectOnDown = null;
        navigation.selectOnLeft = _left;
        navigation.selectOnRight = _right;
        _selectable.navigation = navigation;
    }

    private static void Fail(string _reason)
    {
        throw new System.InvalidOperationException(_reason);
    }
}
