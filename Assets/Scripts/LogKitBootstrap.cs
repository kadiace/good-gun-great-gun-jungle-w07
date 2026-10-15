using System;
using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    // 로그 서버 주소 설정. 패키지의 AutoBootstrap(AfterSceneLoad)이 초기화를 자동 처리하므로
    // 여기서는 서버 주소만 지정한다. 나머지는 패키지 기본값(ApiKey dev-log-key 등)을 쓴다.
    // 저장소가 공개라 주소는 코드에 두지 않는다. 우선순위: OS 환경변수 LOGKIT_SERVER_URL →
    // (에디터) 프로젝트 루트 .env의 LOGKIT_SERVER_URL / (플레이어) 빌드 때 Resources에 넣은 LogKitServerUrl.
    // 어디에도 없으면 패키지 기본값(localhost)을 쓴다.
    public static class LogKitBootstrap
    {
        public const string EnvironmentKey = "LOGKIT_SERVER_URL";
        public const string ResourceName = "LogKitServerUrl";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureServer()
        {
            string url = Environment.GetEnvironmentVariable(EnvironmentKey);
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(url)) url = ReadDotEnv();
#else
            if (string.IsNullOrWhiteSpace(url)) url = Resources.Load<TextAsset>(ResourceName)?.text;
#endif
            if (string.IsNullOrWhiteSpace(url)) return;
            url = url.Trim();
            GameLogger.Configure(developmentServerUrl: url, releaseServerUrl: url);
        }

#if UNITY_EDITOR
        // 환경변수가 없으면 프로젝트 루트 .env(KEY=VALUE, # 주석)에서 읽는다. 빌드 전처리에서도 사용한다.
        public static string ReadServerUrlForEditor()
        {
            string url = Environment.GetEnvironmentVariable(EnvironmentKey);
            return string.IsNullOrWhiteSpace(url) ? ReadDotEnv() : url.Trim();
        }

        private static string ReadDotEnv()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), ".env");
            if (!System.IO.File.Exists(path)) return null;
            foreach (string raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int split = line.IndexOf('=');
                if (split <= 0 || line.Substring(0, split).Trim() != EnvironmentKey) continue;
                string value = line.Substring(split + 1).Trim().Trim('"');
                return value.Length > 0 ? value : null;
            }
            return null;
        }
#endif
    }
}
