using System;
using LogKit;
using UnityEngine;

namespace WeaponWorkshop
{
    // 은퇴·엔드 상자 엔딩 화면의 버튼 선택 로그. 두 엔딩이 같은 형식을 쓴다.
    public static class EndingLog
    {
        public const string Retire = "retire", EndChest = "end_chest";

        public static void Choice(string endingType, string choice, int day, float panelSeconds, bool firstEnding)
        {
            try
            {
                GameLogger.Data("Info", "Game", "ending_choice", new EndingChoiceLog
                {
                    endingType = endingType, choice = choice, day = day, panelSeconds = Mathf.Max(0f, panelSeconds),
                    firstEnding = firstEnding, installId = PlayerIdentity.InstallId, saveId = PlayerIdentity.SaveId
                });
            }
            catch (Exception exception) { Debug.LogWarning("엔딩 로그 기록 실패: " + exception.Message); }
        }
    }
}
