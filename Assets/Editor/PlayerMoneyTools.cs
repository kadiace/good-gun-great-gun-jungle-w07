using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

internal static class PlayerMoneyTools
{
    private const string MenuRoot = "Tools/Weapon Workshop/Add Money/";

    [MenuItem(MenuRoot + "1000")]
    private static void Add1000() => AddMoney(1000);

    [MenuItem(MenuRoot + "10000")]
    private static void Add10000() => AddMoney(10000);

    [MenuItem(MenuRoot + "100000")]
    private static void Add100000() => AddMoney(100000);

    private static void AddMoney(int amount)
    {
        if (!CanAddMoney()) return;

        FirstPersonPlayer[] players = Object.FindObjectsByType<FirstPersonPlayer>(FindObjectsSortMode.None);
        if (players.Length != 1)
        {
            Debug.LogWarning("돈을 추가하려면 실행 중인 씬에 활성 플레이어가 한 명 있어야 합니다.");
            return;
        }

        PlayerWallet wallet = PlayerWallet.ForPlayer(players[0]);
        if (wallet == null || !wallet.isActiveAndEnabled) return;
        wallet.Credit(amount);
        Debug.Log($"플레이어 소지금 +${amount:N0} (현재 ${wallet.Balance:N0})");
    }

    [MenuItem(MenuRoot + "1000", true)]
    [MenuItem(MenuRoot + "10000", true)]
    [MenuItem(MenuRoot + "100000", true)]
    private static bool CanAddMoney() => EditorApplication.isPlaying;
}
