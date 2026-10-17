# 폭발·발사체 경로 GetComponent(null) 조사

작업 B, 읽기 전용 조사. 줄 번호는 조사 시점(git 작업 트리, 다른 워커가 ExpandingExplosion.cs·PoolManager.cs·ProjectileStatsSource.cs·DestructibleTarget*.cs 수정 중) 기준.

## 프로파일러 근거 (Tools/Profiling/last_report.txt)

- SPIKE #2 (frame 1207): `GetComponentNullErrorMessage` 13,792회 / 8.4MB. 같은 프레임 `GameObject.ActivateAwakeRecursively` 13,861회, `Instantiate` 1,642회, `GameObject.AddComponent` 4,481회, `TransformHandle.SetParent` 6,457회, `PooledCombatObject.OnEnable` 2,144회.
- SPIKE #3 (frame 564): `GetComponentNullErrorMessage` 14,695회 / 9.0MB. `ActivateAwakeRecursively` 13,389회, `Instantiate` 2,244회, `AddComponent` 4,950회.
- 해석: null 메시지 수 ≈ 활성화된 GameObject 수(Instantiate 객체당 자식 약 8.4개)와 맞물려, **풀 고갈로 인한 대량 Create + 대량 Rent/Return 체인에서 구조적으로 null이 보장된 GetComponent가 다발**하는 것으로 보인다. 에디터에서는 `GetComponent<T>()`(단수)가 null을 돌려줄 때마다 이 메시지를 만들어 문자열을 할당한다(13.8k × 약 0.6KB ≈ 8MB). 복수형(GetComponents~)과 TryGetComponent는 메시지를 만들지 않는다.
- 빈도 기준: 폭발당 = 폭발 파형 1개 처리, 발사체당 = 발사 1회(Initialize/Configure), 셀당 = 산탄·열탄 셀 1개, 임팩트당 = 충돌 1회.

## 호출 목록

| 파일:줄 | 호출 | 호출 빈도 | null 가능성(근거) | 수정 제안 |
|---|---|---|---|---|
| ExpandingExplosion.cs:185 | collider.GetComponentInParent\<DestructibleTarget\>() | 폭발당 × OverlapSphere 콜라이더 수 | 중간 — 조회가 Tuning.collisionLayers 마스크 안에서 일어나 파괴 대상이 대부분이지만, 같은 레이어의 바닥·소품 콜라이더가 섞이면 폭발마다 반복 null | 콜라이더→DestructibleTarget 사전 조회 캐시(등록/파괴 시 갱신), 파괴 대상 전용 레이어 정리 |
| ExpandingExplosion.cs:195 | collider.GetComponentInParent\<TargetHealth\>() | 폭발당 × 콜라이더 수 | 중간 — 위와 동일 근거(체력 없는 구조물 파츠에서 null) | 같은 콜라이더 캐시에서 함께 조회, 콜라이더당 1회로 묶기 |
| ExpandingExplosion.cs:231 | effect.GetComponent\<PooledCombatObject\>() | 폭발 이펙트 렌트당(프레임 예산 32) | 낮음 — RentPrefab→Create가 없으면 AddComponent로 보장 | 유지 가능. 방어하려면 TryGetComponent |
| ProjectileMotor.cs:55 | effects = GetComponent\<ProjectileEffects\>() | 발사체당(Initialize) | 중간 — ConfigureProjectile이 채우지만 최대 보관 수 초과로 파괴된 인스턴스는 재생성 시 다시 null(AddComponent 4.5k~5k회/스파이크와 부합) | 발사체 프리팹에 ProjectileEffects 탑재(저작 단계), 풀 파괴 억제로 재AddComponent 제거 |
| ProjectileMotor.cs:69 | owner.GetComponent\<PlayerHomingLock\>() | 발사체당 | 낮음 — 소유자(플레이어)에 상시 부착 컴포넌트 | 발사 주체가 캐시한 참조를 Initialize 인자로 전달 |
| ProjectileMotor.cs:76 | GetComponentInChildren\<Renderer\>(true) | 발사체당(drawRenderer 미지정 시) | 낮음 — 발사체 몸통 렌더러가 있어 성공 | 프리팹에 시리얼라이즈 참조 유지, 렌트마다 재조회 제거 |
| ProjectileMotor.cs:206 | nearestHit.collider.GetComponentInParent\<DestructibleTarget\>() | 충돌 후보 히트마다(TryFindHit) | 중간 — 바닥·벽·손님 등 비구조물 히트에서 null | 콜라이더→DestructibleTarget 사전 조회 캐시 |
| ProjectileMotor.cs:219 | other.collider.GetComponentInParent\<DestructibleTarget\>() | 첫 구조물 히트 시 캐스트 결과 전체 재순회 | 중간 — 같은 근거 | 같은 캐시 재사용 |
| ProjectileMotor.cs:231 | collider.GetComponentInParent\<EndChestCore\>() | 모든 충돌 후보마다(PassesEndChestGate) | 높음 — 엔드박스 코어는 해당 오브젝트에만 존재, 일반 스테이지 히트에서는 항상 null | EndChest 레이어(마스크에 이미 존재)일 때만 조회하도록 레이어 선검사 |
| ProjectileMotor.cs:233 | collider.GetComponentInParent\<EndChestShield\>() | 모든 충돌 후보마다 | 높음 — 방어막도 특정 오브젝트에만 존재 → 일반 히트에서 항상 null | 동일(레이어 선검사) |
| ProjectileMotor.cs:239 | hit.collider.GetComponentInParent\<EndChestShield\>() | 임팩트당 | 높음 — 동일 근거 | 동일 |
| ProjectileMotor.cs:248 | hit.collider.GetComponentInParent\<EndChestCore\>() | 임팩트당 | 높음 — 동일 근거 | 동일 |
| ProjectileMotor.cs:251 | hit.collider.GetComponentInParent\<DestructibleTarget\>() | 임팩트당 | 중간 — 구조물 외 히트에서 null | 콜라이더 캐시 |
| ProjectileMotor.cs:253 | hit.collider.GetComponentInParent\<TargetHealth\>() | 임팩트당 | 중간 — 표적 외 히트에서 null | 콜라이더 캐시 |
| ProjectileMotor.cs:291 | hit.collider.GetComponentInParent\<Customer\>() | 임팩트당 | 높음 — 손님 충돌은 드묾, 대부분 히트에서 null | Customer 전용 레이어 선검사 또는 콜라이더→Customer 캐시 |
| ProjectileMotor.cs:335 | target.GetComponentsInChildren\<Collider\>(true) | 관통 시작 시 표적당 | null 아님(배열) — 그러나 호출마다 배열 할당 | TargetHealth가 콜라이더 배열을 캐시해 제공 |
| ProjectileMotor.cs:372 | effect.GetComponentsInChildren\<ParticleSystem\>(true) | 임팩트 이펙트 렌트당 | null 아님 — 배열 할당 | 이펙트 프리팹에 컴포넌트 배열 프리캐시(PooledCombatObject.Initialize 방식) |
| ProjectileMotor.cs:395 | effect.GetComponentsInChildren\<ParticleSystemRenderer\>(true) | 착색 임팩트 이펙트 렌트당 | null 아님 — 배열 할당 | 동일 |
| ProjectileMotor.cs:401 | effect.GetComponent\<PooledCombatObject\>() | 임팩트 이펙트 렌트당 | 낮음 — 풀 Create가 보장 | 유지 가능, TryGetComponent로 통일 권장 |
| ProjectileMotor.cs:414 | GetComponentInChildren\<Renderer\>(true) | 에디터 OnValidate 전용 | 낮음·무해 | 유지 |
| ProjectileStatsSource.cs:92 | GetComponent\<StatCollector\>() | 발사당 다중(BaseSpread·SpreadPerShot이 WeightStability getter를 여러 번 평가) | 높음 — StatCollector 미장착 무기에서는 폴백 경로가 상시 실행 → 매 발사 반복 null | Awake/OnEnable에서 StatCollector 유무를 참조·bool로 캐시 |
| ProjectileStatsSource.cs:95 | GetComponentsInChildren\<ModuleStats\>() | 발사당 다중(위 null 시마다) | null 아님 — 배열 할당(발사당 반복 GC) | 모듈 무게 합을 모듈 구성 변경 시에만 계산해 캐시 |
| ProjectileStatsSource.cs:117 | GetComponent\<StatCollector\>() | 발사당(VerticalRecoilWeightStability) | 높음 — 92와 동일 근거 | 동일 캐시 |
| ProjectileStatsSource.cs:120 | GetComponentsInChildren\<ModuleStats\>() | 발사당 다중 | null 아님 — 배열 할당 | 동일 캐시 |
| ProjectileStatsSource.cs:132 | block.GetComponent\<ModuleStats\>() | 발사당 × 경로 블록 수 | 중간 — 경로 블록은 모듈 위주지만 ModuleStats 없는 블록이 섞이면 null | 경로 구성 시 PlacedBlock에 ModuleStats 캐시 또는 경로를 ModuleStats 배열로 변환해 보관 |
| ProjectileStatsSource.cs:165 | block.GetComponent\<ModuleStats\>() | 발사당 × 경로 블록 수(GetMuzzleStats) | 중간 — 동일 근거 | 동일 |
| ProjectileStatsSource.cs:178 | GetComponent\<CubeAssembly\>() | 발사당(GetMuzzleStats) | 낮음 — 무기 루트에 상시 존재해 성공, 호출 자체가 발사당 낭비 | Awake에서 캐시 |
| ProjectileStatsSource.cs:249 | chargeEffect.GetComponentInChildren\<Light\>(true) | 차지 이펙트 최초 생성 1회 | 낮음 — Light 부재 가능성은 있으나 빈도 미미, null 가드 이미 있음 | 유지 |
| ProjectileStatsSource.cs:363 | block.GetComponent\<ColorModule\>() | 발사당 × 경로 블록 수 | 높음 — 컬러 모듈이 아닌 블록에서 전부 null(색 모듈은 경로의 일부) | 경로 구성 시 ColorModule 참조를 함께 캐시 |
| ProjectileStatsSource.cs:412 | GetComponentInParent\<FirstPersonPlayer\>(true) | 발사체(셀)마다(ConfigureProjectile) | 낮음 — 부모 체인에서 성공, 셀마다 반복은 낭비 | 발사 1회 조사 후 인자로 전달 |
| ProjectileStatsSource.cs:413 | projectile.GetComponent\<EnergyCube\>() | 셀마다 | 낮음 — 템플릿에 존재 | 캐시 필드 |
| ProjectileStatsSource.cs:422 | projectile.GetComponent\<ProjectileEffects\>() | 셀마다 | 중간 — 첫 생성·보관 초과 파괴 후 null → AddComponent 반복 | 프리팹에 탑재 + 풀 파괴 억제 |
| ProjectileStatsSource.cs:426 | projectile.GetComponent\<ProjectileMotor\>() | 셀마다 | 중간 — 동일 근거 | 동일 |
| ProjectileStatsSource.cs:439 | trail.GetComponent\<PooledCombatObject\>() | 열탄 트레일 장착당 | 낮음 — 풀이 보장 | TryGetComponent |
| ProjectileStatsSource.cs:444 | projectile.GetComponentsInChildren\<Renderer\>(true) | 착색 셀마다 | null 아님 — 배열 할당 | 발사체 프리팹 렌더러 배열 프리캐시 |
| ProjectileStatsSource.cs:457 | sourceProjectile.GetComponent\<PooledCombatObject\>() | 산탄 추가 셀마다 | 낮음 — 풀 멤버 보유 | 캐시 |
| PowerCore.cs:34 | GetComponent\<ProjectileStatsSource\>() | Awake 1회 | 낮음 — 무기 프리팹에 상시 존재 | 유지 |
| PowerCore.cs:35 | GetComponent\<SpinRotor\>() | Awake 1회 | 높음(스핀 없는 무기엔 없음 → AddComponent)이지만 빈도가 Awake 1회라 비용 미미 | 유지 가능 |
| PowerCore.cs:47 | GetComponent\<CubeAssembly\>() | Muzzles 캐시 미스 시(자식 변동 시) | 낮음 — 무기 루트에 상시 존재 | 유지 |
| PowerCore.cs:68 | GetComponent\<StatCollector\>() | GetCurrentStats(UI 조회)마다 | 중간 — StatCollector 미장착 무기에서는 조회마다 null | Awake에서 캐시 |
| PowerCore.cs:72 | GetComponent\<CubeAssembly\>() | UI 조회마다 | 낮음 | 캐시 |
| PowerCore.cs:152 | GetComponentInParent\<FirstPersonPlayer\>() | 발사 회차(Fire)마다 | 낮음 — 부모 체인에서 성공 | 캐시 |
| PowerCore.cs:222 | projectile.GetComponent\<EnergyCube\>() | 발사체당 | 낮음 — 템플릿 보유 | 캐시 |
| PowerCore.cs:225 | projectile.GetComponent\<ProjectileMotor\>() | 발사체당 | 낮음 — 풀 인스턴스 보유 | 캐시 |
| PowerCore.cs:245 | flash.GetComponentsInChildren\<ParticleSystem\>(true) | 총구 화염마다 | null 아님 — 배열 할당 | 프리팹 프리캐시 |
| PowerCore.cs:250 | flash.GetComponent\<PooledCombatObject\>()?. | 총구 화염마다 | 낮음 — 풀 보장 | TryGetComponent |
| PowerCore.cs:261 | puff.GetComponent\<PooledCombatObject\>() | 스팀 퍼프마다 | 낮음 — 풀 보장 | TryGetComponent |
| PoolManager.cs:145 | go.GetComponent\<PooledCombatObject\>() | 인스턴스 Create마다(풀 고갈 시 대량) | 높음 — 컴포넌트 풀(new GameObject)과 PooledCombatObject 없는 이펙트 프리팹에서 항상 null → AddComponent. 스파이크의 AddComponent 4.5k~5k회와 부합 | 이펙트 프리팹에 PooledCombatObject 탑재, 컴포넌트 풀은 Create에서 만든 컴포넌트를 필드로 보관해 재조회 제거 |
| PoolManager.cs:148 | go.GetComponent\<VisualDebrisCloud\>() | 인스턴스 Create마다 | 높음 — 파편 구름 외 모든 풀 오브젝트(발사체·폭발 이펙트·파형)에는 없음 → Create마다 항상 null | Pool에 hasDebris 플래그(또는 타입 캐시)를 두고 필요한 풀만 조회 |
| PoolManager.cs:161 | member.GetComponent\<VisualDebrisCloud\>() | Rent마다(모든 렌트에 실행) | 높음 — 대부분의 풀 멤버(발사체·이펙트·파형)에는 없음 → 렌트마다 항상 null. 수천 회 Rent/Return과 부합하는 상위 원인 | PooledCombatObject.Initialize 때 VisualDebrisCloud 참조(없으면 null 포함)를 필드에 캐시 |
| PoolManager.cs:233 | RentComponent\<T\>(...).GetComponent\<T\>() | 컴포넌트 렌트마다(폭발 파형·중력장) | 낮음 — 풀이 T를 AddComponent로 보장 | 유지 |
| PoolManager.cs:238 | go.GetComponent\<PooledCombatObject\>() | Release마다 | 낮음 — 풀 멤버는 보유, 비풀 오브젝트만 null(Destroy 경로) | 유지 가능 |
| PoolManager.cs:275 | member.GetComponent\<VisualDebrisCloud\>() | Return마다 | 높음 — 161과 동일 근거(리턴마다 항상 null) | 동일(캐시 참조) |
| PoolManager.cs:280 | child.GetComponent\<PooledCombatObject\>() | Return마다 × 자식 수 | 높음 — 이펙트 프리팹의 파티클 자식들에는 PooledCombatObject가 없어 자식 수만큼 항상 null. Instantiate당 자식 약 8.4개 × 수천 리턴이 13.8k 규모와 맞물리는 주요 후보 | Initialize 때 PooledCombatObject 보유 자식 목록을 미리 캐시하거나, GetComponentsInChildren 1회로 수집해 순회 |
| PooledCombatObject.cs:24-26 | GetComponentsInChildren×3(파티클·렌더러·트레일) | 인스턴스 Create마다 | null 아님(복수형) — 배열 할당만 | 필요 시 프리팹 단위 공유 캐시 |
| DestructibleTarget.cs:94 | GetComponentInChildren\<MeshFilter\>(true) | Awake·ConfigureCustom 1회 | 낮음 — 미지정 시만 호출, 없으면 에러 후 비활성 | 유지 |
| DestructibleTarget.cs:307 | GetComponentInChildren\<MeshFilter\>(true) | ConfigureCustom 1회 | 낮음 — 동일 | 유지 |
| DestructibleTarget.cs:314 | targetMesh.GetComponent\<Renderer\>() | Awake 1회 | 낮음 — MeshFilter GO에 MeshRenderer 동반 | 유지 |
| DestructibleTarget.cs:323 | targetMesh.GetComponents\<Collider\>() | Awake 1회 | null 아님 | 유지 |
| DestructibleTarget.cs:923 | primitive.GetComponent\<MeshFilter\>() | Subdivide 최초 1회 | 낮음 — Cube 프리미티브 보장 | 유지 |
| DestructibleTarget.StructuralSupport.cs:116 | GetComponent\<FallingRangeTarget\>() | DropDisconnectedStructure(구조 붕괴마다)·StructuralSupportGraph 스캔(타깃 등록·변경마다) | 높음 — 레이드 표적은 FallingRangeTarget 미장착이 일반적 → 조회마다 null(117행 TryGetComponent는 메시지를 만들지 않음) | Awake에서 bool usesPhysicalGravity을 1회 계산해 캐시 |
| HitPayload.cs | GetComponent 호출 없음 | — | — | — (피해 적용은 순수 데이터 전달) |
| TargetHealth.cs:39 | GetComponentsInChildren\<Collider\>() | AimPoint 조회마다(유도 탄이 프레임마다, FlameThrower 조준) | null 아님 — 호출마다 배열 할당, 유도 탄이 있으면 프레임당 반복 GC | 콜라이더 바운즈를 Awake·활성화 시 캐시해 AimPoint에서 재사용 |
| TargetHealth.cs:121 | GetComponentsInChildren\<Renderer\>() | CreateBurnEffect(주석상 미사용 경로) | 낮음·드묾 | 유지 |
| VisualDebrisCloud.cs:171 | hit.collider.GetComponentInParent\<DestructibleTarget\>() | 파편 구름 Initialize마다 RaycastAll 히트 수만큼 | 높음 — 목적 자체가 DestructibleTarget 제외라 바닥·환경 히트에서 항상 null | 레이캐스트 마스크에서 파괴 대상 레이어를 제외(~mask)해 애초에 히트에서 빠지게 하기 |

## null 가능성 높은 상위 5

1. **PoolManager.cs:161 / :275 — `GetComponent<VisualDebrisCloud>()`**: 모든 Rent·Return에서 실행되고 대부분의 풀 멤버에는 VisualDebrisCloud가 없어 상시 null. 스파이크 프레임의 수천 회 Rent/Return과 직결되는 최대 상시 null원. → PooledCombatObject에 참조 캐시.
2. **PoolManager.cs:280 — 자식 `GetComponent<PooledCombatObject>()`**: Return 시 자식 수만큼 실행되며 이펙트 파티클 자식에는 대부분 없어 상시 null. Instantiate당 자식 약 8.4개 × 수천 건이 13.8k~14.7k null 카운트 규모를 설명하는 주요 후보. → 보유 자식 목록 사전 캐시.
3. **ProjectileMotor.cs:231·233·239·248 — `GetComponentInParent<EndChestCore/EndChestShield>()`**: 모든 충돌 후보·임팩트에서 실행되고 일반 스테이지에는 엔드박스가 없어 사실상 상시 null. 충돌이 몰리는 스파이크 프레임에서 누적량이 큼. → EndChest/EndChestShield 레이어 선검사.
4. **ProjectileStatsSource.cs:92·117 — `GetComponent<StatCollector>()`**: 발사당 여러 번(BaseSpread·SpreadPerShot·수직 반동) 평가되고 StatCollector 미장착 무기에서는 상시 null + 폴백 GetComponentsInChildren 배열 할당까지 반복. → 캐시 + 무게 합 재계산 캐시.
5. **DestructibleTarget.StructuralSupport.cs:116 — `GetComponent<FallingRangeTarget>()`**: 폭발로 구조가 붕괴할 때마다·그래프 스캔마다 실행되고 레이드 표적에는 보통 없어 상시 null. 폭발 스파이크에서 붕괴 건수에 비례해 누적. → Awake에서 bool 캐시.

차점(폭발 경로에서 누적이 큰 나머지): ProjectileStatsSource.cs:363 ColorModule(경로 블록 대부분에서 null), VisualDebrisCloud.cs:171 DestructibleTarget(바닥 히트 상시 null), ProjectileMotor.cs:291 Customer(대부분 히트에서 null).

## 참고

- 이 보고서는 조사 전용이며 파일을 수정하지 않았다. git 사용은 status 확인뿐이었다.
- 표의 줄 번호는 다른 워커의 동시 수정으로 이동할 수 있다. 식별 기준은 "파일 + 호출 심볼 + 주변 코드"로 볼 것.
