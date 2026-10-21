using System;
using System.Collections.Generic;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드 360x360m 도시 및 6개 통합 구역, 스폰 지점 메타데이터 직렬화 클래스입니다.
    /// </summary>
    [Serializable]
    public sealed class SurvivalMapData
    {
        public float mapWidth;
        public float mapLength;
        public float mapMinX;
        public float mapMaxX;
        public float mapMinZ;
        public float mapMaxZ;
        public float playerSpawnX;
        public float playerSpawnY;
        public float playerSpawnZ;
        public float workshopCenterX;
        public float workshopCenterY;
        public float workshopCenterZ;
        public int lootCrateTargetCount;
        public List<ZoneEntry> zones = new List<ZoneEntry>();

        [Serializable]
        public sealed class ZoneEntry
        {
            public string name;
            public float minX;
            public float maxX;
            public float minZ;
            public float maxZ;
            public float centerX;
            public float centerZ;
        }
    }
}
