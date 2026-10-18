using UnityEngine;

namespace WeaponWorkshop
{
    // 투사체 속성의 세부 수치와 연출 프리팹. 기획 확정 전 임시값이며 에셋에서 조정한다.
    [CreateAssetMenu(fileName = "ProjectileTuning", menuName = "Weapon Workshop/Projectile Tuning")]
    public sealed class ProjectileTuning : ScriptableObject
    {
        [Header("Explosion cell damage feedback")]
        [Min(0.01f)] public float explosionHitFlashDuration = 0.12f;
        public Color explosionHitFlashColor = Color.white;
        public Color explosionDamageTint = Color.black;
        [Range(0f, 1f)] public float explosionDamageTintStrength = 0.45f;
        // 공통
        [Header("Common")]
        [Min(0f)] public float maximumTravelDistance = 1000f;
        public LayerMask collisionLayers = Physics.DefaultRaycastLayers;
        [Tooltip("투사체 발사 때 총구에서 재생하는 섬광 효과.")]
        public ParticleSystem muzzleFlashEffect;

        // 산탄
        [Header("Shotgun")]
        [Min(0f), Tooltip("방사 파츠 1개일 때 채워진 원형 탄 배치의 지름 각도(도). 파츠가 늘면 탄 수와 방사 범위도 증가합니다.")]
        public float shotgunSpreadDegrees = 10f;

        // 차지: 누르고 있으면 충전, 떼면 발사. 단계 배율을 총구의 거의 모든 수치에 곱함.
        [Header("Charge")]
        [Min(0.01f), Tooltip("충전 1단계에 걸리는 시간(초).")]
        public float chargeSecondsPerLevel = 0.5f;
        [Min(1), Tooltip("차지 모듈 1개일 때 최대 단계.")]
        public int chargeBaseStages = 3;
        [Min(0), Tooltip("차지 모듈 1개 추가마다 늘어나는 최대 단계.")]
        public int chargeStagesPerExtraModule = 1;
        [Min(1f), Tooltip("1단계 배율.")]
        public float chargeFirstStageMultiplier = 1.1f;
        [Min(0f), Tooltip("단계가 하나 오를 때마다 더해지는 배율. 0.2면 1.1 / 1.3 / 1.5.")]
        public float chargeStageStep = 0.2f;
        [Min(0f), Tooltip("차지 모듈 1개 추가마다 모든 단계 배율에 더해지는 값.")]
        public float chargeMultiplierPerExtraModule = 0.1f;
        [Min(0f), Tooltip("최대 단계에서 추가로 더해지는 배율.")]
        public float chargeMaxStageBonus = 0.3f;
        [Tooltip("충전 중 코어에 붙는 연출(빨려 드는 파티클 + 빛). 진행도에 따라 세기 조절.")]
        public ParticleSystem chargeEffect;
        [Min(0f), Tooltip("최대 충전 시 무기 떨림 크기(m).")]
        public float chargeShakeDistance = 0.012f;

        // 화염방사(FlameThrower): 현재 사용하지 않음. 발열은 오각형 탄(Heat Shot).
        [Header("Flamethrower (unused)")]
        [Min(0f), Tooltip("발열 모듈 1개일 때 사거리(m).")]
        public float flameRange = 6f;
        [Min(0f), Tooltip("발열 모듈이 1개 늘 때마다 추가 사거리(m).")]
        public float flameRangePerExtraPoint = 2f;
        [Range(1f, 120f), Tooltip("원뿔 전체 각도(도).")]
        public float flameConeAngle = 30f;
        [Min(0f), Tooltip("발열 1당 초당 피해. 총구 연사 스탯이 배율로 곱해짐.")]
        public float flameDamagePerSecondPerPoint = 20f;
        [Min(0.02f)] public float flameTickInterval = 0.1f;
        public ParticleSystem flameEffect;

        // 중력: 맞은 지점으로 주변 표적·물리 오브젝트를 끌어당김.
        [Header("Gravity")]
        [Min(0f), Tooltip("중력 스탯 1당 반경 증가량(m). 반경 = 이 값 × 중력 스탯. 기본 1→0.5m, 2→1m, 3→1.5m.")]
        public float gravityRadiusPerPoint = 0.5f;
        [Min(0f), Tooltip("중력 모듈 1개당 끌어당기는 속도(m/s, 물리 오브젝트는 가속도).")]
        public float gravityPullPerPoint = 3f;
        [Min(0f), Tooltip("관통력 1당 끌어당기는 힘 증가 비율. 0.5면 관통 2에서 2배.")]
        public float gravityPenetrationBonus = 0.5f;
        [Min(0f)] public float gravityDuration = 1.5f;
        public ParticleSystem gravityEffect;
        public GameObject gravityDistortionPrefab;

        // 유도: 탄은 곧게 가다가 표적 인식 범위에 들어오면 표적이 끌어당김.
        [Header("Homing")]
        [Min(0f), Tooltip("표적 중심 기준 인식 반경(m) × 유도 스탯. 탄이 이 안에 들어오면 끌려감.")]
        public float homingRange = 6f;
        [Min(0f), Tooltip("유도 1당 표적 쪽으로 끌어당기는 가속도(m/s²). 가까울수록 강함.")]
        public float homingAccelerationPerPoint = 40f;
        [Min(1f), Tooltip("끌려오며 빨라질 수 있는 최대 속도 = 발사 속도 × 이 값.")]
        public float homingMaxSpeedMultiplier = 2f;
        [Min(0f), Tooltip("화염 유도: 유도 1당 화염이 표적 쪽으로 꺾이는 초당 각도.")]
        public float homingTurnRatePerPoint = 180f;

        // 회전: 회전 모듈 앞쪽 블록이 통째로 회전, 탄 자체는 회전하지 않음.
        [Header("Spin")]
        [Min(0f), Tooltip("총열 회전: 회전 모듈 1개당 앞쪽 블록이 도는 초당 각도. 장착 중에만 회전.")]
        public float moduleSpinDegreesPerSecond = 180f;

        // 폭발
        [Header("Explosion")]
        [Min(0f), Tooltip("같은 프레임 같은 무기의 폭발 병합 거리 배율. 두 폭발 중 작은 반경에 곱합니다. 기존 0.5배보다 크게 설정하면 파동 수가 줄어듭니다.")]
        public float explosionMergeRadiusMultiplier = 1.5f;
        [Min(0f), Tooltip("표적 분할의 프레임당 시간 예산(ms). 최소 한 표적은 예산을 넘겨도 분할합니다.")]
        public float subdivisionBudgetMilliseconds = 4f;
        [Min(0f), InspectorName("Explosion Radius Per Point"), Tooltip("폭발력의 세제곱근당 최대 반경 증가량(m). 최대 반경 = 이 값 × ∛폭발력. 기본 폭발력 1→0.5m, 8→1m, 27→1.5m.")] public float explosionRadiusPerPoint = 0.5f;
        [Min(0.01f), InspectorName("Explosion Visual Scale Multiplier"), Tooltip("폭발 이펙트 크기 배율. 폭발 최대 반경에 곱해지며 피해/검색 범위에는 영향이 없습니다. 기본 2배.")]
        public float explosionVisualScaleMultiplier = 2f;
        [Min(0.01f)] public float explosionExpansionSpeed = 10f;
        [Min(0.01f)] public float explosionCheckDistance = 0.2f;
        [Min(0f), Tooltip("폭발 파편의 바깥쪽 초기 속도 배율. 지속적인 힘/파편 간 상호작용은 계산하지 않습니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("explosionDebrisSpeed")] public float explosionDebrisPushPerPower = 2.5f;
        public ParticleSystem explosionEffect;

        // 발열 탄: 발열 총구는 오각형 5발, 멀어질수록 벌어짐. 연사 +4는 발열 모듈 SO.
        [Header("Heat Shot")]
        [Min(0f), Tooltip("오각형 각 탄이 중심 방향에서 벌어지는 각도(도). 거리가 멀수록 간격이 넓어짐.")]
        public float heatPentagonDegrees = 4f;
        public Color heatColor = new Color(1f, 0.45f, 0.1f, 0.8f);
        [Tooltip("발열 탄을 감싸는 불꽃 꼬리 연출(피해 없음).")]
        public ParticleSystem heatTrailEffect;

        // 화상 스택: 현재 발열은 화상을 주지 않음, 값만 보관.
        [Header("Burn (unused)")]
        [Min(0f), Tooltip("화상 1스택·발열 1당 초당 피해.")]
        public float heatDamagePerSecondPerPoint = 5f;
        [Min(0f), Tooltip("마지막으로 불이 붙은 뒤 화상이 유지되는 시간.")]
        public float heatDuration = 3f;
        [Min(1), Tooltip("화상 최대 스택. 불에 맞을 때마다 1스택.")]
        public int burnMaxStacks = 5;
        public ParticleSystem burnEffect;

        // 피격 효과 공통
        [Header("Hit Effects")]
        [Min(0.05f), Tooltip("화염처럼 계속 맞는 공격에서 폭발·중력장·화상 스택이 발동하는 간격(초).")]
        public float areaPayloadInterval = 0.5f;

        // 물 / 수증기: 같은 총구에 물 + 발열이면 수증기. 탄 없이 연사에 맞춰 수증기 파티클만 뿜고 아무 능력 없음.
        [Header("Water / Steam")]
        [Tooltip("미정 규칙의 초기값: 물 탄은 표적에 맞으면 관통하지 않습니다.")]
        public bool waterStopsPenetration = true;
        public Color waterColor = new Color(0.15f, 0.55f, 1f, 0.7f);
        [Tooltip("수증기 총구가 발사 때마다 뿜는 연출(피해·충돌 없음).")]
        public ParticleSystem steamEffect;
        [Tooltip("화염방사(미사용) 수증기 색.")]
        public Color steamColor = new Color(0.9f, 0.95f, 1f, 0.6f);

        // 화염 + 다른 모듈(화염방사 전용, 현재 사용하지 않음)
        [Header("Flamethrower Combos (unused)")]
        [Min(1), Tooltip("관통이 없을 때 화염이 한 번에 태우는 표적 수. 관통 1당 +1, 관통이 있으면 벽도 통과.")]
        public int flameBaseTargets = 3;
        [Min(0f), Tooltip("산탄 1당 원뿔 각도 증가(도).")]
        public float flameConePerShotgun = 15f;
        [Min(0f), Tooltip("회전 1당 원뿔 각도 증가(도). 소용돌이 화염.")]
        public float flameConePerSpin = 10f;
        [Min(0f), Tooltip("유도 1당 표적을 찾는 각도(도, 최대 90).")]
        public float flameHomingAnglePerPoint = 30f;
        [Min(0), Tooltip("화염 반사 최대 횟수(반사 스탯과 비교해 작은 값).")]
        public int flameMaxRicochets = 3;
        [Min(0f), Tooltip("차지 1단계당 화염 사거리·원뿔 증가 비율.")]
        public float flameChargeGrowthPerLevel = 0.25f;
        [Min(0f), Tooltip("차지 화염 폭발: 1단계·발열 1당 피해.")]
        public float flameBurstDamagePerLevel = 30f;
        [Min(0f), Tooltip("발열 1개 추가당 화염 파티클 양 증가 비율.")]
        public float flameEmissionPerExtraPoint = 0.5f;
        [Min(0f), Tooltip("발열 1개 추가당 화염 파티클 크기 증가 비율.")]
        public float flameSizePerExtraPoint = 0.2f;

        // 연사: 총구 연사 = 코어 기본 × 아래 배율^(경로 연사 모듈의 연사 합) + 다른 모듈 연사(발열 +4 등).
        // 상한은 따로 없고, 발사 시도가 프레임당 1번이라 초당 최대 프레임 수만큼 나감.
        [Header("Fire Rate")]
        [Min(1f), Tooltip("연사 모듈 1개당 곱해지는 배율. 1.5면 1개=1.5배, 2개=2.25배, 3개=3.375배.")]
        public float fireRateMultiplierPerModule = 2f;

        // 탄퍼짐: 현재 퍼짐 = 기본(코어 Spread) + 누적. 총구가 한 번 발사할 때마다 누적 += (spreadPerShot + 경로 모듈 Spread),
        // 마지막 발사 후 spreadRecoveryDelay가 지나면 초당 spreadRecoveryPerSecond씩 줄어 기본으로 돌아감.
        // 안정도 = 1 + 무게 × spreadReductionPerWeight: 기본·증가량은 ÷안정도, 회복 속도는 ×안정도(무거울수록 안정).
        [Header("Spread")]
        [Min(0f), Tooltip("발사 1회당 기본 퍼짐 증가(도). 경로 모듈의 Spread가 여기에 더해짐.")]
        public float spreadPerShot = 1f;
        [Min(0f), Tooltip("발사로 쌓이는 퍼짐의 최대치(도).")]
        public float spreadMaxBloom = 8f;
        [Min(0f), Tooltip("마지막 발사 후 회복이 시작되기까지 대기(초).")]
        public float spreadRecoveryDelay = 0.1f;
        [Min(0f), Tooltip("회복 속도(도/초).")]
        public float spreadRecoveryPerSecond = 15f;
        [Min(0f), Tooltip("총 전체 무게 1당 안정도. 0.1이면 무게 5 → 증가량 ÷1.5·회복 ×1.5.")]
        public float spreadReductionPerWeight = 0.1f;
        [Min(0f), Tooltip("최종 퍼짐 최대 각도(도).")]
        public float spreadMaxDegrees = 30f;

        // Vertical camera recoil is separate from projectile spread. One emitted muzzle round adds one impulse,
        // regardless of how many shotgun pellets or heat-pattern projectiles that round creates.
        [Header("Vertical Recoil")]
        [Min(0f)] public float verticalRecoilDegreesPerShot = 4.5f;
        [Min(0f)] public float verticalRecoilDegreesPerRouteModule = 0.06f;
        [Min(0f)] public float verticalRecoilDegreesPerPenetration = 0.08f;
        [Min(0f)] public float verticalRecoilDegreesPerExplosion = 0.12f;
        [Min(0f)] public float verticalRecoilDegreesPerShotgunPoint = 0.25f;
        [Min(0f), Tooltip("방사 총구의 발사 1회당 전체 수직 반동 배율.")]
        public float verticalRecoilShotgunMultiplier = 10f;
        [Min(0f), Tooltip("Charged shots multiply recoil by this value in addition to their charge-stage multiplier.")]
        public float verticalRecoilChargeMultiplier = 2f;
        [Min(0f)] public float verticalRecoilReductionPerWeight = 0.06f;
        [Min(0f), Tooltip("Maximum upward pitch impulse per firing event; view pitch remains within -89 to 89 degrees.")]
        public float verticalRecoilMaximumDegrees = 178f;
        [Min(0.001f), Tooltip("Time for the view pitch to smoothly catch up to the accumulated recoil target.")]
        public float verticalRecoilSmoothTime = 0.08f;

        // 가속: 그 총구 경로의 가속 모듈 1개마다 탄 기본 속도의 일정 비율을 더함.
        [Header("Speed")]
        [Min(0f), Tooltip("가속 모듈 1개당 기본 탄속 대비 증가 비율. 1면 1개=2배, 2개=4배, 3개=8배.")]
        public float speedBonusPerModule = 1f;

        // 조준경: 스코프 모듈, 우클릭 줌.
        [Header("Scope")]
        [Min(1f), Tooltip("스코프 1개의 기본 배율. 같은 방향으로 연결한 추가 스코프마다 (기본 배율-1)을 더함. 기본값: 2배, 3배, 4배...")]
        public float scopeMagnificationPerModule = 2f;
        [Min(0.01f), Tooltip("배율과 무관하게 줌 인/아웃에 걸리는 시간(초).")]
        public float scopeZoomDuration = 0.15f;
        [Tooltip("줌 배율만큼 마우스 감도를 낮춤.")]
        public bool scopeScalesSensitivity = true;
    }
}
