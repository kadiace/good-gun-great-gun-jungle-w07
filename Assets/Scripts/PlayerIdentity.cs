using System;
using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    // 로그용 익명 식별자. installId는 기기에 한 번 만들어 두고, saveId는 새 게임마다 만들어 세이브에 함께 저장한다.
    // 세션(게임 실행 1회)을 넘어 같은 기기·같은 진행을 묶는 데만 쓴다. 개인 정보는 담지 않는다.
    public static class PlayerIdentity
    {
        private const string InstallKey = "LogKit.InstallId";
        private static string installId, saveId;
        private static bool sessionLogged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { installId = saveId = null; sessionLogged = false; }

        public static string InstallId
        {
            get
            {
                if (!string.IsNullOrEmpty(installId)) return installId;
                try
                {
                    installId = PlayerPrefs.GetString(InstallKey, "");
                    if (string.IsNullOrEmpty(installId))
                    {
                        installId = NewId();
                        PlayerPrefs.SetString(InstallKey, installId);
                        PlayerPrefs.Save();
                    }
                }
                catch (Exception) { installId = NewId(); }
                return installId;
            }
        }

        // 시작 메뉴를 거치지 않은 실행(에디터 직접 재생 등)은 첫 조회 때 새 진행으로 본다.
        public static string SaveId => string.IsNullOrEmpty(saveId) ? saveId = NewId() : saveId;
        public static void BeginNewSave() => saveId = NewId();
        // 예전 세이브처럼 값이 없으면 새로 만들고, 다음 저장 때 세이브에 기록된다.
        public static void Adopt(string id) => saveId = string.IsNullOrEmpty(id) ? NewId() : id;

        // 게임 실행마다 한 번. LogKit 자동 초기화 뒤(첫 씬 Start)에 호출한다.
        public static void LogSessionStart()
        {
            if (sessionLogged) return;
            sessionLogged = true;
            GameLogger.Data("Info", "System", "session_started", new SessionStartLog { installId = InstallId });
        }

        private static string NewId() => Guid.NewGuid().ToString("N");
    }
}
