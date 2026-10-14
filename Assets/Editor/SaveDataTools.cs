using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

internal static class SaveDataTools
{
    private const string MenuRoot = "Tools/Weapon Workshop/Save Data/";

    [MenuItem(MenuRoot + "Delete Slot 1")]
    private static void DeleteSlot1() => Delete(0);
    [MenuItem(MenuRoot + "Delete Slot 2")]
    private static void DeleteSlot2() => Delete(1);
    [MenuItem(MenuRoot + "Delete Slot 3")]
    private static void DeleteSlot3() => Delete(2);
    [MenuItem(MenuRoot + "Delete All Slots", false, 20)]
    private static void DeleteAll() => Delete(-1);

    [MenuItem(MenuRoot + "Delete Slot 1", true)]
    [MenuItem(MenuRoot + "Delete Slot 2", true)]
    [MenuItem(MenuRoot + "Delete Slot 3", true)]
    [MenuItem(MenuRoot + "Delete All Slots", true)]
    private static bool CanDelete() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static void Delete(int slot)
    {
        if (!CanDelete()) return;
        string directory = Path.Combine(Application.persistentDataPath, "Saves");
        string target = slot < 0 ? "모든 저장 데이터(슬롯·자동 저장·습격 복구)" : $"저장 슬롯 {slot + 1}";
        if (!EditorUtility.DisplayDialog("세이브 데이터 삭제",
                $"{target}을 삭제할까요? 삭제한 데이터는 복구할 수 없습니다.\n\n{directory}", "삭제", "취소")) return;

        try
        {
            if (slot < 0) GameSaveFiles.DeleteAllSaves();
            else GameSaveFiles.Delete(slot);
            GameSaveFiles.PendingLoad = null;
            EditorUtility.DisplayDialog("세이브 데이터 삭제", $"{target} 삭제 완료.", "확인");
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("세이브 데이터 삭제 실패",
                $"일부 파일이 삭제되지 않았을 수 있습니다.\n{e.Message}", "확인");
        }
    }
}
