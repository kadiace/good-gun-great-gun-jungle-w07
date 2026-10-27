using UnityEditor;
using UnityEngine;
using WeaponWorkshop;

internal static class OnboardingTools
{
    private const string MenuRoot = "Tools/Weapon Workshop/Onboarding/";

    [MenuItem(MenuRoot + "Reset Module Onboarding")]
    private static void ResetModules()
    {
        OnboardingManager.ResetModuleOnboarding();
        Debug.Log("모듈 온보딩 노출 상태를 초기화했습니다. 다음 연장·2방향·스코프·트리거 모듈 선택 또는 설치 때 다시 안내합니다.");
    }

    [MenuItem(MenuRoot + "Reset Custom Onboarding")]
    private static void ResetCustom()
    {
        OnboardingManager.ResetCustomOnboarding();
        Debug.Log("커스텀 온보딩 노출 상태를 초기화했습니다. 이미 받은 커스텀 요청이 있으면 다음 작업대 진입 때 다시 안내합니다.");
    }

    [MenuItem(MenuRoot + "Reset Module Onboarding", true)]
    [MenuItem(MenuRoot + "Reset Custom Onboarding", true)]
    [MenuItem(MenuRoot + "Reset Shooting Onboarding", true)]
    private static bool CanReset() => EditorApplication.isPlaying;

    [MenuItem(MenuRoot + "Reset Shooting Onboarding")]
    private static void ResetShooting()
    {
        OnboardingManager.ResetShootingOnboarding();
        Debug.Log("사격 온보딩 노출 상태를 초기화했습니다. 현재 받은 대상 주문이 있으면 다시 안내합니다.");
    }
}
