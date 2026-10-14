using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    // LogKit 부트스트랩. 씬에 컴포넌트를 배치하지 않아도 어떤 씬을 실행하든 로그 수집이 켜진다.
    internal static class LogKitBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GameLogger.Init();
            GameLogger.Info("System", "로그 세션 시작");
        }
    }
}
