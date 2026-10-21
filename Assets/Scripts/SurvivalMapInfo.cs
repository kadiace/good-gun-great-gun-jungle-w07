using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// SurvivalMapData 테이블의 유효성 검증 및 좌표 조회 헬퍼 확장 메서드를 제공하는 클래스입니다.
    /// </summary>
    public static class SurvivalMapInfo
    {
        /// <summary>
        /// SurvivalMapData 테이블의 필수 값과 범위를 검증합니다.
        /// </summary>
        /// <param name="data">검증할 맵 데이터 객체</param>
        public static void Validate(SurvivalMapData data)
        {
            if (data == null)
            {
                Debug.LogError("[SurvivalMapInfo] SurvivalMapData가 null입니다.");
                return;
            }

            if (data.mapWidth <= 0f || data.mapLength <= 0f)
            {
                Debug.LogError("[SurvivalMapInfo] mapWidth 또는 mapLength가 유효하지 않습니다.");
            }

            if (data.zones == null || data.zones.Count == 0)
            {
                Debug.LogWarning("[SurvivalMapInfo] 등록된 구역(zones)이 비어 있습니다.");
            }
        }

        /// <summary>
        /// 생존 모드 플레이어의 시작 스폰 위치를 Vector3로 반환합니다.
        /// </summary>
        /// <param name="data">맵 데이터 인스턴스</param>
        /// <returns>플레이어 스폰 위치</returns>
        public static Vector3 GetPlayerSpawnPosition(this SurvivalMapData data)
        {
            if (data == null) return new Vector3(180f, 1f, -420f);
            return new Vector3(data.playerSpawnX, data.playerSpawnY, data.playerSpawnZ);
        }

        /// <summary>
        /// 생존 모드 공방의 중심 좌표를 Vector3로 반환합니다.
        /// </summary>
        /// <param name="data">맵 데이터 인스턴스</param>
        /// <returns>공방 중심 위치</returns>
        public static Vector3 GetWorkshopCenterPosition(this SurvivalMapData data)
        {
            if (data == null) return new Vector3(68.5f, 0f, -438.5f);
            return new Vector3(data.workshopCenterX, data.workshopCenterY, data.workshopCenterZ);
        }
    }
}
