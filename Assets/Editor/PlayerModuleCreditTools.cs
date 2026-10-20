using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

internal static class PlayerModuleCreditTools
{
    private const string MenuRoot = "Tools/Weapon Workshop/Add Module Materials/";

    [MenuItem(MenuRoot + "50")]
    private static void Add50() => AddMaterials(50);

    [MenuItem(MenuRoot + "500")]
    private static void Add500() => AddMaterials(500);

    [MenuItem(MenuRoot + "5000")]
    private static void Add5000() => AddMaterials(5000);

    private static void AddMaterials(int amount)
    {
        if (!EditorApplication.isPlaying) return;

        FirstPersonPlayer[] players = Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None);
        if (players.Length != 1)
        {
            Debug.LogWarning("모듈 재화를 추가하려면 실행 중인 씬에 활성 플레이어가 한 명 있어야 합니다.");
            return;
        }

        PlayerWallet wallet = PlayerWallet.ForPlayer(players[0]);
        if (wallet == null || !wallet.isActiveAndEnabled) return;
        wallet.CreditModuleCredits(amount);
        Debug.Log($"플레이어 모듈 재화 +{amount:N0} (현재 {wallet.ModuleCredits:N0})");
    }

    [MenuItem(MenuRoot + "50", true)]
    [MenuItem(MenuRoot + "500", true)]
    [MenuItem(MenuRoot + "5000", true)]
    private static bool CanAddMaterials() => EditorApplication.isPlaying;
}
