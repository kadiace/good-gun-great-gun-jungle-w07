using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

internal static class PlayerModuleCreditTools
{
    private const string MenuPath = "Tools/Weapon Workshop/Add 50 Module Materials";

    [MenuItem(MenuPath)]
    private static void AddMaterials()
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
        wallet.CreditModuleCredits(50);
        Debug.Log($"플레이어 모듈 재화 +50 (현재 {wallet.ModuleCredits:N0})");
    }

    [MenuItem(MenuPath, true)]
    private static bool CanAddMaterials() => EditorApplication.isPlaying;
}
