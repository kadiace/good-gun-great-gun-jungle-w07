using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

// 초기 보상을 다시 산정하지 않는다. 요청 ID별 계약 정액과 일차당 보상을 직접 편집한다.
public sealed class CustomerRequestRewardsWindow : EditorWindow
{
    private const string Destination = "Assets/Resources/CustomerRequestData.json";
    private CustomerRequestData draft;
    private Vector2 scroll;
    private bool dirty;

    [MenuItem("Tools/Weapon Workshop/Tables/Customer Request Rewards")]
    private static void Open() => GetWindow<CustomerRequestRewardsWindow>("주문 보상 표");

    private void OnEnable() => LoadTable();

    private void LoadTable()
    {
        TableManager.ReloadAfterTableEdit();
        draft = JsonUtility.FromJson<CustomerRequestData>(JsonUtility.ToJson(TableManager.Instance.CustomerRequest));
        dirty = false;
        hasUnsavedChanges = false;
        saveChangesMessage = "주문 보상 표 변경을 저장하시겠습니까?";
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("일반 요청은 고정액, 테스트는 생성 일차 × 일차당 보상입니다. " +
            "두 값을 함께 지정하면 합산합니다. 저장된 계약 금액은 바뀌지 않으며, 새 주문부터 적용됩니다. " +
            "최소 제작비 × 1.5 초기값은 자동 재계산하지 않습니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!dirty))
                if (GUILayout.Button("JSON에 저장")) SaveChanges();
            if (GUILayout.Button("다시 읽기") && (!dirty || EditorUtility.DisplayDialog("다시 읽기",
                "저장하지 않은 보상 변경을 버리고 다시 읽으시겠습니까?", "다시 읽기", "취소"))) LoadTable();
        }
        if (draft == null) return;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var row in draft.requests ?? Array.Empty<CustomerRequestData.Request>())
        {
            if (row == null) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(row.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(row.id, EditorStyles.miniLabel);
                EditorGUI.BeginChangeCheck();
                int reward = Mathf.Max(0, EditorGUILayout.IntField("고정 보상 ($)", row.reward));
                int perDay = Mathf.Max(0, EditorGUILayout.IntField("생성 일차당 보상 ($)", row.rewardPerDay));
                if (EditorGUI.EndChangeCheck())
                {
                    row.reward = reward; row.rewardPerDay = perDay;
                    dirty = hasUnsavedChanges = true;
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    public override void SaveChanges()
    {
        if (!dirty || draft == null) return;
        try
        {
            // 다른 도구가 바꾼 조건·태그는 최신 표에서 보존하고 보상 두 필드만 반영한다.
            TableManager.ReloadAfterTableEdit();
            var latest = JsonUtility.FromJson<CustomerRequestData>(JsonUtility.ToJson(TableManager.Instance.CustomerRequest));
            foreach (var row in draft.requests ?? Array.Empty<CustomerRequestData.Request>())
            {
                if (row == null) continue;
                var target = latest.FindRequest(row.id);
                if (target == null) throw new InvalidOperationException("표에서 요청이 삭제되었습니다. 다시 읽어 주세요: " + row.id);
                target.reward = row.reward; target.rewardPerDay = row.rewardPerDay;
            }
            File.WriteAllText(Destination, JsonUtility.ToJson(latest, true) + "\n", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Destination);
            TableManager.ReloadAfterTableEdit();
            dirty = hasUnsavedChanges = false;
            ShowNotification(new GUIContent("주문 보상 표 저장 완료"));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowNotification(new GUIContent("저장 실패: " + exception.Message));
        }
    }

    public override void DiscardChanges() => LoadTable();
}
