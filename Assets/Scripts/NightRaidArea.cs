using UnityEngine;
namespace WeaponWorkshop
{
    public sealed class NightRaidArea : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Tooltip("플레이 영역 중심(이 오브젝트 기준 로컬 좌표)")] private Vector3 mapCenter;
        [SerializeField, Tooltip("플레이 영역 가로(X)·세로(Z) 길이(m)")] private Vector2 mapSize = new Vector2(20f, 20f);
        [SerializeField, Min(4.5f), Tooltip("경계와 탱크 중심 사이 여유(m), 차체와 포신이 경계 안에 남도록 설정")]
        private float tankDropInset = 5f;
        [SerializeField, Tooltip("적 출격 후보 위치(이 오브젝트 기준 로컬 좌표). 탱크 착륙 위치로 사용")]
        private Vector3[] enemySpawnPoints;
        [SerializeField, Min(1), Tooltip("플레이어에게 가까운 후보 몇 곳 중 무작위로 출격할지")]
        private int nearbySpawnPointCount = 4;
        [SerializeField, Min(1f), Tooltip("후보 위치 위 헬기 출격 높이(m)")]
        private float helicopterSpawnHeight = 20f;
        [SerializeField, Min(0f), Tooltip("탱크가 플레이어에서 이 거리(m) 이상 떨어진 위치에 착륙(수평 거리, 후보 위치 목록과 무관)")]
        private float tankMinPlayerDistance = 15f;
        [SerializeField, Min(0f), Tooltip("탱크가 플레이어에서 이 거리(m) 이하인 위치에 착륙. 플레이 영역(경계 안쪽 tankDropInset) 밖이면 영역 안으로 당김")]
        private float tankMaxPlayerDistance = 35f;
        [SerializeField, Min(1), Tooltip("탱크 착륙 위치로 무작위 방향을 시도하는 횟수(영역 안에 들어오는 첫 방향 사용)")]
        private int tankDirectionTries = 12;
        private readonly System.Collections.Generic.List<int> nearestPoints = new System.Collections.Generic.List<int>();
        public Transform SpawnPoint => spawnPoint;
        public bool TryGetEnemySpawnPoint(Vector3 playerPosition, bool helicopter, ref int previousIndex,
            out Vector3 position, out Vector3 outward)
        {
            position = outward = Vector3.zero;
            if (!helicopter) return TryGetTankDropPoint(playerPosition, out position, out outward);
            if (enemySpawnPoints == null || enemySpawnPoints.Length == 0) return false;
            nearestPoints.Clear();
            int count = Mathf.Clamp(nearbySpawnPointCount, 1, enemySpawnPoints.Length);
            for (int i = 0; i < enemySpawnPoints.Length; i++)
            {
                float distance = HorizontalDistanceSquared(transform.TransformPoint(enemySpawnPoints[i]), playerPosition);
                int rank = 0;
                while (rank < nearestPoints.Count && HorizontalDistanceSquared(
                    transform.TransformPoint(enemySpawnPoints[nearestPoints[rank]]), playerPosition) <= distance) rank++;
                if (rank >= count) continue;
                nearestPoints.Insert(rank, i);
                if (nearestPoints.Count > count) nearestPoints.RemoveAt(count);
            }
            // 가까운 후보가 여럿이면 직전 위치의 연속 선택을 피합니다.
            if (nearestPoints.Count > 1) nearestPoints.Remove(previousIndex);
            int selected = nearestPoints[Random.Range(0, nearestPoints.Count)];
            previousIndex = selected;
            Vector3 local = enemySpawnPoints[selected] - mapCenter;
            local.y = 0f;
            outward = transform.TransformDirection(local.sqrMagnitude > 0.01f ? local.normalized : Vector3.forward);
            position = transform.TransformPoint(enemySpawnPoints[selected]);
            if (helicopter) position += Vector3.up * helicopterSpawnHeight;
            return true;
        }
        // 탱크: 플레이어 위치에서 min~max 거리의 무작위 방향 지점을 플레이 영역(tankDropInset 안쪽) 안에서 고른다.
        // 영역 밖이면 다른 방향을 다시 뽑고, 모두 밖이면 영역 안으로 당긴 지점 중 거리 오차가 가장 작은 것을 쓴다.
        private bool TryGetTankDropPoint(Vector3 playerPosition, out Vector3 position, out Vector3 outward)
        {
            Vector3 localPlayer = transform.InverseTransformPoint(playerPosition);
            float halfX = Mathf.Max(0f, mapSize.x * 0.5f - tankDropInset), halfZ = Mathf.Max(0f, mapSize.y * 0.5f - tankDropInset);
            float min = tankMinPlayerDistance, max = Mathf.Max(min, tankMaxPlayerDistance);
            Vector3 best = Vector3.zero;
            float bestGap = float.MaxValue;
            for (int i = 0; i < Mathf.Max(1, tankDirectionTries); i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f), distance = Random.Range(min, max);
                Vector3 local = localPlayer + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Vector3 clamped = new Vector3(Mathf.Clamp(local.x, mapCenter.x - halfX, mapCenter.x + halfX), 0f,
                    Mathf.Clamp(local.z, mapCenter.z - halfZ, mapCenter.z + halfZ));
                float gap = (clamped - local).magnitude;
                if (gap < bestGap) { bestGap = gap; best = clamped; }
                if (gap <= 0f) break;
            }
            best.y = mapCenter.y;
            Vector3 away = best - new Vector3(localPlayer.x, mapCenter.y, localPlayer.z);
            away.y = 0f;
            outward = transform.TransformDirection(away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward);
            position = transform.TransformPoint(best);
            return true;
        }
        private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }
        public Vector3 GetTankDropPoint(int index, out Vector3 outward)
        {
            float x = Mathf.Max(0f, mapSize.x * 0.5f - tankDropInset);
            float z = Mathf.Max(0f, mapSize.y * 0.5f - tankDropInset);
            float sideX = (index % 4) < 2 ? -1f : 1f;
            float sideZ = (index % 2) == 0 ? -1f : 1f;
            Vector3 local = new Vector3(sideX*x, 0f, sideZ*z);
            outward = transform.TransformDirection(local.normalized);
            return transform.TransformPoint(mapCenter + local);
        }
        private void OnDrawGizmosSelected()
        {
            var previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(mapCenter, new Vector3(mapSize.x, 0.2f, mapSize.y));
            if (enemySpawnPoints != null)
                foreach (var point in enemySpawnPoints)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(point, 2f);
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(point + Vector3.up * helicopterSpawnHeight, 2f);
                }
            Gizmos.matrix = previous;
        }
    }
}
