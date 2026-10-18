using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidHelicopter : MonoBehaviour
    {
        [SerializeField] private RaidEnemyCore core;
        [SerializeField] private Transform rotor;
        [SerializeField, Min(1f)] private float flightSpeed = 12f;
        [SerializeField, Min(1f)] private float hoverHeight = 12f;
        [SerializeField, Min(1f)] private float hoverDistance = 14f;
        [SerializeField] private float rotorSpeed = 1200f;

        [Header("Missile (one pod)")]
        [SerializeField, Tooltip("미사일 발사구. 시야가 열리면 플레이어, 부술 수 있는 벽에 가리면 그 벽을 향해 직진탄 발사.")]
        private RaidWeaponMount missileMount;
        [SerializeField] private RaidMissile missilePrefab;
        [SerializeField, Min(0.1f)] private float missileInterval = 3f;
        [SerializeField, Min(1f)] private float missileRange = 45f;

        [Header("Machine gun (other pod)")]
        [SerializeField, Tooltip("기관총 발사구. 플레이어가 보일 때만 발사.")]
        private RaidWeaponMount gunMount;
        [SerializeField] private RaidBullet bulletPrefab;
        [SerializeField, Tooltip("총열 끝. 번갈아 발사하며, 비우면 기관총 발사구 위치에서 발사.")]
        private Transform[] gunMuzzles = new Transform[0];
        [SerializeField, Tooltip("발사마다 총구에서 켜는 화염.")] private ParticleSystem gunFlashPrefab;
        [SerializeField, Min(0.01f)] private float gunInterval = 0.04f;
        [SerializeField, Min(0f), Tooltip("탄 퍼짐 반각(도).")] private float gunSpread = 1.5f;
        [SerializeField, Min(1f)] private float gunRange = 45f;
        [SerializeField, Min(0f), Tooltip("플레이어가 보이기 시작한 뒤 발사까지 대기(초). 대기 중 가리면 처음부터 다시.")]
        private float gunAcquireDelay = 1.6f;
        [SerializeField, Min(0f), Tooltip("조준점이 플레이어를 따라가는 속도(m/s). 달리기보다 느리면 달려서 피할 수 있음.")]
        private float gunAimSpeed = 5f;

        private FirstPersonPlayer player;
        private bool destroyed;
        private float missileTimer, gunTimer, gunAcquired;
        private int gunBarrel;
        private bool hasGunAim;
        private Vector3 gunAim;

        public void Initialize(FirstPersonPlayer target, string enemyId)
        {
            core.AssignIdentity(enemyId, RaidEnemyType.Helicopter);
            player = target;
            missileTimer = missileInterval;
            gunTimer = 0f;
            gunAcquired = 0f;
            hasGunAim = false;
            WorkshopSoundEffects.RaiseHelicopterAmbientStarted(transform);
        }
        private void OnEnable() { if (core != null) core.Destroyed += StopFlying; }
        private void OnDisable()
        {
            if (core != null) core.Destroyed -= StopFlying;
            WorkshopSoundEffects.RaiseHelicopterAmbientStopped(transform);
        }
        private void StopFlying()
        {
            if (destroyed) return;
            destroyed = true;
            WorkshopSoundEffects.RaiseHelicopterAmbientStopped(transform);
        }
        private void Update()
        {
            if (destroyed || player == null || (core != null && core.IsDestroyed)) return;
            if (EndChest.IsDiscoveryPresentationActive) return;
            if (rotor != null) rotor.Rotate(Vector3.up, rotorSpeed * Time.deltaTime, Space.Self);
            if (GameManager.GameplayBlocked) return;
            Vector3 offset = transform.position - player.transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f) offset = Vector3.forward;
            Vector3 destination = player.transform.position + offset.normalized * hoverDistance + Vector3.up * hoverHeight;
            transform.position = Vector3.MoveTowards(transform.position, destination, flightSpeed * Time.deltaTime);
            Vector3 chest = player.transform.position + Vector3.up;
            Vector3 look = chest - transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 3f);
            UpdateMissile(chest);
            UpdateGun(chest);
        }

        private void UpdateMissile(Vector3 chest)
        {
            missileTimer -= Time.deltaTime;
            if (missileTimer > 0f || missilePrefab == null || missileMount == null || !missileMount.IsOperational) return;
            Transform muzzle = missileMount.Muzzle;
            if (Vector3.Distance(muzzle.position, chest) > missileRange) return;
            // Clear view: shoot the player. Breakable wall: shoot where it blocks. Anything else: wait.
            Vector3 aim;
            switch (CachedSight(ref missileSight, muzzle.position, chest, out RaycastHit block))
            {
                case SightResult.Clear: aim = chest; break;
                case SightResult.BreakableWall: aim = block.point; break;
                default: return;
            }
            var missile = Instantiate(missilePrefab, muzzle.position, Quaternion.LookRotation((aim - muzzle.position).normalized));
            missile.Initialize(player, transform);
            WorkshopSoundEffects.RaiseHelicopterAttacked(muzzle.position);
            missileTimer = missileInterval;
        }

        private void UpdateGun(Vector3 chest)
        {
            if (bulletPrefab == null || gunMount == null || !gunMount.IsOperational) { gunAcquired = 0f; return; }
            Transform muzzle = gunMount.Muzzle;
            bool visible = Vector3.Distance(muzzle.position, chest) <= gunRange &&
                           CachedSight(ref gunSight, muzzle.position, chest, out _) == SightResult.Clear;
            // Any blocked moment restarts the acquisition delay.
            if (!visible) { gunAcquired = 0f; return; }
            if (!hasGunAim) { gunAim = chest; hasGunAim = true; }
            gunAim = Vector3.MoveTowards(gunAim, chest, gunAimSpeed * Time.deltaTime);
            gunAcquired += Time.deltaTime;
            if (gunAcquired < gunAcquireDelay) return;
            gunTimer -= Time.deltaTime;
            // Catch up on several rounds in a long frame so the rate does not depend on frame rate.
            while (gunTimer <= 0f)
            {
                gunTimer += gunInterval;
                Transform barrel = gunMuzzles != null && gunMuzzles.Length > 0 ? gunMuzzles[gunBarrel++ % gunMuzzles.Length] : muzzle;
                if (barrel == null) barrel = muzzle;
                Vector3 direction = gunAim - barrel.position;
                if (direction.sqrMagnitude < 0.0001f) continue;
                Quaternion spread = Quaternion.AngleAxis(Random.Range(0f, gunSpread), Random.onUnitSphere);
                // Pooled: a heli fires ~25 rounds/s, so Instantiate/Destroy per round piles up GC spikes.
                var round = PoolManager.RentPrefab(bulletPrefab.gameObject, barrel.position, spread * Quaternion.LookRotation(direction.normalized));
                round.GetComponent<RaidBullet>().Initialize(transform);
                round.SetActive(true);
                WorkshopSoundEffects.RaiseHelicopterAttacked(barrel.position);
                if (gunFlashPrefab != null)
                    PoolManager.RentPrefab(gunFlashPrefab.gameObject, barrel.position, Quaternion.LookRotation(direction.normalized), barrel)
                        .GetComponent<PooledCombatObject>().PlayParticles(true, 0.5f);
            }
        }

        private enum SightResult { Clear, BreakableWall, Blocked }

        // Sight is re-tested at most every SightRefresh seconds per weapon; a stale answer only delays reaction.
        private const float SightRefresh = 0.1f;
        private struct SightCache { public float time; public SightResult result; public RaycastHit block; }
        private SightCache missileSight = new SightCache { time = float.NegativeInfinity };
        private SightCache gunSight = new SightCache { time = float.NegativeInfinity };

        private SightResult CachedSight(ref SightCache cache, Vector3 from, Vector3 to, out RaycastHit block)
        {
            if (Time.time - cache.time >= SightRefresh)
            {
                cache.result = Sight(from, to, out cache.block);
                cache.time = Time.time;
            }
            block = cache.block;
            return cache.result;
        }

        // First thing between the muzzle and the player's chest, ignoring this helicopter and enemy rounds.
        private SightResult Sight(Vector3 from, Vector3 to, out RaycastHit block)
        {
            block = default;
            Vector3 delta = to - from;
            bool found = false;
            int count = RaidPhysicsQueries.Raycast(from, delta.normalized, delta.magnitude, out var hits);
            for (int i = 0; i < count && !found; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<RaidMissile>() != null || hit.collider.GetComponentInParent<RaidBullet>() != null) continue;
                block = hit; found = true;
            }
            if (!found || block.collider.GetComponentInParent<FirstPersonPlayer>() != null) return SightResult.Clear;
            // Other enemies are not walls to blast through.
            var wall = block.collider.GetComponentInParent<DestructibleTarget>();
            return wall != null && RaidEnemyCore.FindForPart(wall.transform) == null ? SightResult.BreakableWall : SightResult.Blocked;
        }
    }
}
