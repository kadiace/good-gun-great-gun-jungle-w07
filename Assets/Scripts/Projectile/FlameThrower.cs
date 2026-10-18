using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 발열 모듈이 있는 총구의 화염방사. 누르고 있는 동안 PowerCore가 총구마다 Emit을 호출한다.
    // 화염은 탄 대신 "형태"만 바뀔 뿐, 같은 총구의 다른 모듈은 모두 HitPayload와 아래 규칙으로 함께 적용된다.
    //   발열 누적: 사거리·파티클 양/크기 증가     연사: 틱 피해 배율
    //   산탄·회전: 원뿔 확대                       관통: 태우는 표적 수 증가 + 벽 통과(없으면 벽에 막힘)
    //   유도: 가까운 표적 쪽으로 화염이 휨          반사: 벽에 막힌 화염이 반사되어 이어짐
    //   차지: 누르는 동안 화염 확대, 떼면 화염 폭발   폭발·중력: 일정 간격으로 화염이 닿은 지점에 발동
    //   물: 증기(피해 0, 소화, 밀어냄)
    [DisallowMultipleComponent]
    public sealed class FlameThrower : MonoBehaviour
    {
        private const float ReleaseGrace = 0.08f;

        private sealed class Stream
        {
            public readonly List<ParticleSystem> segmentEffects = new List<ParticleSystem>();
            public float lastEmitTime = -1f;
            public float tickTimer;
            public float payloadTimer;
            public Vector3 aimDirection;
        }

        private struct Segment
        {
            public Vector3 origin;
            public Vector3 direction;
            public float range;
        }

        private readonly Dictionary<string, Stream> streams = new Dictionary<string, Stream>();
        private readonly List<string> expired = new List<string>();
        private readonly List<Segment> segments = new List<Segment>();
        private ProjectileStatsSource statsSource;
        private Transform owner;
        private Transform weapon;

        private ProjectileTuning Tuning => statsSource.Tuning;

        private void Awake()
        {
            statsSource = GetComponent<ProjectileStatsSource>();
            ResolveIgnored();
        }

        private void ResolveIgnored()
        {
            FirstPersonPlayer player = GetComponentInParent<FirstPersonPlayer>();
            owner = player != null ? player.transform : null;
            EquipableWeapon equipable = GetComponentInParent<EquipableWeapon>();
            weapon = equipable != null ? equipable.transform : transform;
        }

        private int ChargeLevel(WeaponStats stats) => stats.charge > 0f ? statsSource.GetChargeLevel(stats.charge) : 0;

        private float Range(WeaponStats stats, int chargeLevel)
            => (Tuning.flameRange + Tuning.flameRangePerExtraPoint * Mathf.Max(0f, Mathf.Floor(stats.heat) - 1f))
               * (1f + Tuning.flameChargeGrowthPerLevel * chargeLevel);

        private float ConeAngle(WeaponStats stats, int chargeLevel)
            => Mathf.Min(170f, (Tuning.flameConeAngle + Tuning.flameConePerShotgun * Mathf.Max(0f, stats.shotgun)
                                + Tuning.flameConePerSpin * Mathf.Max(0f, stats.spin))
                               * (1f + Tuning.flameChargeGrowthPerLevel * chargeLevel));

        /// <summary>Called every frame while the trigger is held.</summary>
        public void Emit(string key, Vector3 localOrigin, Vector3 localDirection, WeaponStats stats)
        {
            if (statsSource == null) return;
            if (owner == null) ResolveIgnored();
            if (!streams.TryGetValue(key, out Stream stream))
            {
                stream = new Stream();
                streams[key] = stream;
            }
            bool firstThisFrame = stream.lastEmitTime < Time.time;
            bool restarted = Time.time - stream.lastEmitTime > ReleaseGrace;
            stream.lastEmitTime = Time.time;

            Vector3 origin = transform.TransformPoint(localOrigin);
            Vector3 muzzleDirection = transform.TransformDirection(localDirection).normalized;
            if (restarted || stream.aimDirection.sqrMagnitude < 0.5f) stream.aimDirection = muzzleDirection;
            int chargeLevel = ChargeLevel(stats);
            float range = Range(stats, chargeLevel);
            float cone = ConeAngle(stats, chargeLevel);
            if (firstThisFrame) stream.aimDirection = Steer(stream.aimDirection, muzzleDirection, origin, range, stats);

            BuildSegments(origin, stream.aimDirection, range, stats);
            UpdateEffects(stream, stats, cone);
            if (!firstThisFrame) return;

            float interval = Tuning.flameTickInterval;
            stream.tickTimer -= Time.deltaTime;
            stream.payloadTimer -= Time.deltaTime;
            if (stream.tickTimer > 0f) return;
            stream.tickTimer = Mathf.Max(stream.tickTimer + interval, 0f);
            bool payload = stream.payloadTimer <= 0f;
            if (payload) stream.payloadTimer = Tuning.areaPayloadInterval;
            float damage = Tuning.flameDamagePerSecondPerPoint * stats.heat * Mathf.Max(1f, stats.fireRate) * interval;
            BurnSegments(stats, cone, damage, payload);
        }

        /// <summary>Charge release on a flame muzzle: one big flame blast scaled by the charge level.</summary>
        public void Burst(string key, Vector3 localOrigin, Vector3 localDirection, WeaponStats stats)
        {
            if (statsSource == null) return;
            if (owner == null) ResolveIgnored();
            int level = ChargeLevel(stats);
            Vector3 origin = transform.TransformPoint(localOrigin);
            Vector3 direction = streams.TryGetValue(key, out Stream stream) && stream.aimDirection.sqrMagnitude > 0.5f
                ? stream.aimDirection : transform.TransformDirection(localDirection).normalized;
            float range = Range(stats, level);
            float cone = ConeAngle(stats, level);
            BuildSegments(origin, direction, range, stats);
            float damage = Tuning.flameBurstDamagePerLevel * Mathf.Max(1, level) * stats.heat;
            BurnSegments(stats, cone, damage, true);
            if (stream != null && stream.segmentEffects.Count > 0 && stream.segmentEffects[0] != null)
                stream.segmentEffects[0].Emit(40 + 20 * level);
        }

        // Homing bends the flame toward the nearest target within the homing angle.
        private Vector3 Steer(Vector3 current, Vector3 muzzleDirection, Vector3 origin, float range, WeaponStats stats)
        {
            if (stats.homing <= 0f) return muzzleDirection;
            float searchAngle = Mathf.Min(90f, Tuning.flameHomingAnglePerPoint * stats.homing);
            TargetHealth best = null;
            float bestDistance = range;
            foreach (TargetHealth target in FindObjectsByType<TargetHealth>(FindObjectsSortMode.None))
            {
                if (target == null || target.IsDead) continue;
                Vector3 offset = target.AimPoint - origin;
                float distance = offset.magnitude;
                if (distance > bestDistance || Vector3.Angle(muzzleDirection, offset) > searchAngle) continue;
                best = target;
                bestDistance = distance;
            }
            Vector3 desired = best != null ? (best.AimPoint - origin).normalized : muzzleDirection;
            float maxRadians = Tuning.homingTurnRatePerPoint * stats.homing * Mathf.Deg2Rad * Time.deltaTime;
            return Vector3.RotateTowards(current, desired, maxRadians, 0f).normalized;
        }

        // Walls stop the flame unless it penetrates; with ricochet it reflects and continues.
        private void BuildSegments(Vector3 origin, Vector3 direction, float range, WeaponStats stats)
        {
            segments.Clear();
            bool penetrates = stats.penetration >= 1f;
            int bounces = Mathf.Min(Tuning.flameMaxRicochets, Mathf.Max(0, Mathf.FloorToInt(stats.ricochet)));
            float remaining = range;
            for (int i = 0; i <= bounces && remaining > 0.05f; i++)
            {
                if (penetrates || !TryHitWall(origin, direction, remaining, out RaycastHit wall))
                {
                    segments.Add(new Segment { origin = origin, direction = direction, range = remaining });
                    return;
                }
                segments.Add(new Segment { origin = origin, direction = direction, range = wall.distance });
                if (i == bounces) return;
                remaining -= wall.distance;
                direction = Vector3.Reflect(direction, wall.normal).normalized;
                origin = wall.point + wall.normal * 0.02f;
            }
        }

        private bool TryHitWall(Vector3 origin, Vector3 direction, float distance, out RaycastHit wall)
        {
            wall = default;
            float nearest = float.PositiveInfinity;
            int layers = Tuning.collisionLayers & ~LayerMask.GetMask("Energy");
            PoolManager.FlushExplosionFinish();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, layers, QueryTriggerInteraction.Ignore))
            {
                if (IsIgnored(hit.collider) || hit.collider.GetComponentInParent<TargetHealth>() != null) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                wall = hit;
            }
            return nearest < float.PositiveInfinity;
        }

        private bool IsIgnored(Collider collider)
        {
            Transform hit = collider.transform;
            return (owner != null && hit.IsChildOf(owner)) || (weapon != null && hit.IsChildOf(weapon));
        }

        private void BurnSegments(WeaponStats stats, float cone, float damage, bool payload)
        {
            bool penetrates = stats.penetration >= 1f;
            int maxTargets = Tuning.flameBaseTargets + Mathf.Max(0, Mathf.FloorToInt(stats.penetration));
            var burned = new HashSet<TargetHealth>();
            foreach (Segment segment in segments)
            {
                var candidates = new List<(TargetHealth target, Vector3 point, float distance)>();
                PoolManager.FlushExplosionFinish();
                foreach (Collider collider in Physics.OverlapSphere(segment.origin, segment.range, Tuning.collisionLayers,
                             QueryTriggerInteraction.Ignore))
                {
                    TargetHealth target = collider.GetComponentInParent<TargetHealth>();
                    if (target == null || target.IsDead || burned.Contains(target)) continue;
                    Vector3 point = collider.bounds.ClosestPoint(segment.origin + segment.direction *
                        Mathf.Min(segment.range, Vector3.Distance(segment.origin, collider.bounds.center)));
                    Vector3 offset = point - segment.origin;
                    float distance = offset.magnitude;
                    if (distance > segment.range) continue;
                    if (distance > 0.01f && Vector3.Angle(segment.direction, offset) > cone * 0.5f) continue;
                    if (!penetrates && TryHitWall(segment.origin, offset.normalized, distance, out _)) continue;
                    int existing = candidates.FindIndex(c => c.target == target);
                    if (existing >= 0) { if (candidates[existing].distance > distance) candidates[existing] = (target, point, distance); }
                    else candidates.Add((target, point, distance));
                }
                candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
                foreach (var candidate in candidates)
                {
                    if (burned.Count >= maxTargets) break;
                    burned.Add(candidate.target);
                    HitPayload.Apply(new HitPayload.Context
                    {
                        stats = stats,
                        source = statsSource,
                        point = candidate.point,
                        direction = segment.direction,
                        target = candidate.target,
                        damage = damage,
                        applyStatus = payload,
                        // Area effects fire once per payload, at the nearest burned point.
                        applyArea = payload && burned.Count == 1,
                        owner = owner,
                        weapon = weapon
                    });
                }
                if (burned.Count >= maxTargets) break;
            }
            // Nothing burned: area effects still go off where the flame ends against a wall.
            if (payload && burned.Count == 0 && segments.Count > 0)
            {
                Segment last = segments[segments.Count - 1];
                if (!penetrates && TryHitWall(last.origin, last.direction, last.range + 0.05f, out RaycastHit wall))
                    HitPayload.Apply(new HitPayload.Context
                    {
                        stats = stats, source = statsSource, point = wall.point, direction = last.direction,
                        damage = 0f, applyStatus = false, applyArea = true, owner = owner, weapon = weapon
                    });
            }
        }

        private void UpdateEffects(Stream stream, WeaponStats stats, float cone)
        {
            ParticleSystem prefab = Tuning.flameEffect;
            if (prefab == null) return;
            float extra = Mathf.Max(0f, Mathf.Floor(stats.heat) - 1f);
            bool steam = stats.water > 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                if (stream.segmentEffects.Count <= i) stream.segmentEffects.Add(null);
                ParticleSystem effect = stream.segmentEffects[i];
                bool created = effect == null;
                if (created)
                {
                    effect = Instantiate(prefab, segments[i].origin, Quaternion.identity, transform);
                    effect.gameObject.SetActive(true);
                    stream.segmentEffects[i] = effect;
                }
                Vector3 direction = segments[i].direction;
                effect.transform.SetPositionAndRotation(segments[i].origin, Quaternion.LookRotation(direction,
                    Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up));
                var main = effect.main;
                ParticleSystem source = prefab;
                var prefabMain = source.main;
                float lifetime = prefabMain.startLifetime.constantMax > 0f ? prefabMain.startLifetime.constantMax : 0.5f;
                main.startSpeed = segments[i].range / lifetime;
                float sizeScale = 1f + Tuning.flameSizePerExtraPoint * extra;
                main.startSize = new ParticleSystem.MinMaxCurve(prefabMain.startSize.constantMin * sizeScale,
                    prefabMain.startSize.constantMax * sizeScale);
                main.startColor = steam ? new ParticleSystem.MinMaxGradient(Tuning.steamColor) : prefabMain.startColor;
                var emission = effect.emission;
                emission.rateOverTime = source.emission.rateOverTime.constantMax * (1f + Tuning.flameEmissionPerExtraPoint * extra);
                var shape = effect.shape;
                shape.angle = cone * 0.5f;
                var colorOverLifetime = effect.colorOverLifetime;
                colorOverLifetime.enabled = !steam;
                if (!effect.isEmitting) effect.Play(true);
            }
            // Segments no longer reached (e.g. the wall moved) stop emitting.
            for (int i = segments.Count; i < stream.segmentEffects.Count; i++)
                if (stream.segmentEffects[i] != null && stream.segmentEffects[i].isEmitting)
                    stream.segmentEffects[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            expired.Clear();
            foreach (var entry in streams)
            {
                Stream stream = entry.Value;
                if (Time.time - stream.lastEmitTime <= ReleaseGrace) continue;
                foreach (ParticleSystem effect in stream.segmentEffects)
                    if (effect != null && effect.isEmitting) effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                stream.tickTimer = 0f;
                // Muzzles that vanished (module removed) are cleaned up once their particles fade.
                if (Time.time - stream.lastEmitTime > 2f) expired.Add(entry.Key);
            }
            foreach (string key in expired)
            {
                foreach (ParticleSystem effect in streams[key].segmentEffects)
                    if (effect != null) Destroy(effect.gameObject);
                streams.Remove(key);
            }
        }

        private void OnDisable()
        {
            foreach (Stream stream in streams.Values)
                foreach (ParticleSystem effect in stream.segmentEffects)
                    if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
