using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    // 기기별 로그 서버 설정. 패키지의 AutoBootstrap(AfterSceneLoad)이 초기화를 자동 처리하므로
    // 여기서는 서버 주소만 기기에 따라 지정한다. 나머지는 패키지 기본값(localhost, ApiKey dev-log-key)을 쓴다.
    // IP는 DHCP라 바뀌면 이 값을 갱신한다.
    internal static class LogKitBootstrap
    {
        private const string BuildPcServerUrl = "http://172.21.10.67:5000/api/logs";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ConfigureServer()
        {
            if (SystemInfo.deviceName == "DESKTOP-0JP1TBE")
                GameLogger.Configure(developmentServerUrl: BuildPcServerUrl, releaseServerUrl: BuildPcServerUrl);
        }
    }
}
