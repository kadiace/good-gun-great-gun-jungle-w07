using System;

namespace WeaponWorkshop
{
    // Resources/RaidMapData.json을 그대로 옮긴 직렬화 자료다. 여기에 로직을 두지 않는다.
    // 읽기·조회는 RaidStageInfo, 로딩과 보유는 TableManager가 담당한다.
    [Serializable]
    public sealed class RaidMapData
    {
        [Serializable]
        public sealed class Region
        {
            public string scene;
            public string name;
            public int materialCrates;
            public int entryCost;
            public string[] moduleCodes;
            public bool helicopters;
            public bool tanks;
        }

        public Region[] regions;
    }
}
