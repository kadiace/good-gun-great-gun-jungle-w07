using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidTankTransport : MonoBehaviour
    {
        [SerializeField] private RaidTank tankPrefab;
        [SerializeField] private Transform cargoAnchor;
        [SerializeField] private GameObject suspensionDraw;
        [SerializeField, Min(1f)] private float flightSpeed = 15f;
        [SerializeField, Min(1f)] private float dropHeight = 50f;
        private RaidTank cargo;
        private Vector3 destination, exit;
        private bool released, initialized;
        public bool Initialize(FirstPersonPlayer player, Vector3 landingPoint, Vector3 outward, string enemyId,
            RaidTank cargoPrefabOverride = null)
        {
            RaidTank selectedPrefab = cargoPrefabOverride != null ? cargoPrefabOverride : tankPrefab;
            if (!isActiveAndEnabled || player == null || selectedPrefab == null || !selectedPrefab.gameObject.activeSelf || !selectedPrefab.enabled ||
                cargoAnchor == null || !cargoAnchor.IsChildOf(transform) || !cargoAnchor.gameObject.activeInHierarchy ||
                !Finite(landingPoint) || !Finite(outward) || !Finite(dropHeight) || dropHeight < 1f ||
                !Finite(flightSpeed) || flightSpeed <= 0f)
                return FailInitialization("플레이어·활성 탱크 프리팹·수송선 하위 Cargo Anchor·투하 좌표/높이/속도 설정을 확인하세요.");
            try
            {
                outward.y = 0f; if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
                outward.Normalize();
                transform.rotation = Quaternion.LookRotation(-outward);
                // Include every parent transform/scale, not just the anchor's local Y.
                // At arrival the cargo, rather than the transport pivot, is dropHeight above the chosen point.
                Vector3 anchorOffset = cargoAnchor.position - transform.position;
                destination = landingPoint + Vector3.up * dropHeight - anchorOffset;
                transform.position = destination + outward * 50f;
                exit = destination - outward * 80f;
                cargo = Instantiate(selectedPrefab, cargoAnchor.position, transform.rotation, cargoAnchor);
                cargo.SetTransport(this);
                cargo.Initialize(player, enemyId);
                initialized = true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                return FailInitialization($"탱크 '{selectedPrefab.name}' 초기화 실패. 위 예외의 Core 참조와 생성 콜백을 확인하세요.");
            }
            WorkshopSoundEffects.RaiseTankAppeared(transform.position);
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private bool FailInitialization(string reason)
        {
            Debug.LogError($"RaidTankTransport '{name}': {reason}", this);
            initialized = false;
            if (cargo != null) { cargo.gameObject.SetActive(false); Destroy(cargo.gameObject); cargo = null; }
            gameObject.SetActive(false);
            Destroy(gameObject);
            return false;
        }
        private void ReleaseCargo()
        {
            if (released) return;
            released = true;
            try { if (cargo != null) cargo.Release(); }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                FailInitialization("탱크 투하 실패. 분리된 화물과 수송선을 정리합니다.");
                return;
            }
            if (suspensionDraw != null) suspensionDraw.SetActive(false);
        }
        private void Update()
        {
            if (!initialized || GameManager.GameplayBlocked) return;
            Vector3 target = released ? exit : destination;
            transform.position = Vector3.MoveTowards(transform.position, target, flightSpeed * Time.deltaTime);
            if ((transform.position-target).sqrMagnitude > 0.01f) return;
            if (!released) ReleaseCargo(); else Destroy(gameObject);
        }
    }
}
