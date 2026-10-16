using System;

namespace WeaponWorkshop
{
    // 곡 식별자는 파일 이름과 분리한다. None은 타이틀의 무음 상태다.
    // 곡을 다시 추가해도 기존 식별자 값을 유지해 프리팹 연결을 보존한다.
    public enum BgmId
    {
        None = 0, DayFirst = 1, Night = 2, DaySecond = 3, DayThird = 4, DayFourth = 5,
        DayLast = 7, Raining = 8, DayAttack1 = 9, DayAttack2 = 10, DayAttack0 = 11,
        // 곡 이름과 별개인 조건 식별자다. 조건별 음원은 SoundManager 인스펙터에서 연결한다.
        Raid01Travel = 20, Raid01FirstWall = 21, Raid01Clear = 22,
        Raid02Travel = 23, Raid02FirstWall = 24, Raid02Clear = 25,
        Raid03Travel = 26, Raid03FirstWall = 27, Raid03Clear = 28,
        Raid04Travel = 29, Raid04FirstWall = 30, Raid04Clear = 31
    }

    // Resources/SoundData.json의 순수 직렬화 자료. 조회와 재생 로직은 별도 클래스가 담당한다.
    [Serializable]
    public sealed class SoundData
    {
        [Serializable]
        public sealed class Track
        {
            public string id;
            // 실제 AudioClip 연결은 SoundManager 인스펙터가 담당한다.
            public string label;
            public int unlockRaid;
            // Main의 곡 선택 목록에 노출할지 여부. 습격 전용곡은 선택 목록에 넣지 않는다.
            public bool selectableInMain;
        }

        [Serializable]
        public sealed class Raid
        {
            public string scene;
            public int number;
        }

        public Track[] tracks;
        public Raid[] raids;
    }
}
