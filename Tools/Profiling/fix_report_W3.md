# GetComponent null 조회 캐시 (W3)

## 원인과 근거

- `Tools/Profiling/getcomponent_report.md`의 SPIKE #2/#3에서 `GetComponentNullErrorMessage`가 프레임당 13,792/14,695회 발생했다. 인스턴스 생성·활성화 및 대량 풀 대여/반납이 동반되어 풀의 선택적 컴포넌트와 자식별 컴포넌트 조회가 주요 후보였다.
- `PoolManager.cs:152-164, 284-297`의 파편 구름 조회와 `PooledCombatObject.cs:23-54` 초기화 캐시로 반복 조회를 옮겼다. 반환은 자식 효과를 뒤에서 앞으로 반환하는 동작을 유지한다.
- `ProjectileMotor.cs:43-57, 248-275, 313`의 EndChest/Customer/구조물 탐색은 일반 충돌 오브젝트에 없을 수 있는 컴포넌트였다. EndChest 계층은 레이어를 먼저 검사하고, 공통 부모 탐색은 각 노드에서 `TryGetComponent`를 사용한다.
- `ProjectileStatsSource.cs:89-130, 133-170, 361-371`에서 StatCollector 조회, 경로 모듈과 컬러 모듈 탐색을 캐시 또는 `TryGetComponent` 방식으로 바꿨다. StatCollector 부재 시 동적 모듈 상태가 반영되도록 기존 자식 ModuleStats 폴백은 유지했다.
- `VisualDebrisCloud.cs:167-172, 207-212`의 바닥 판정용 파괴 대상 부모 탐색도 null 메시지를 내지 않는 `TryGetComponent` 계층 탐색으로 바꿨다.

## 파일별 변경

- `PoolManager.cs`: 생성/대여/반납의 VisualDebrisCloud 조회를 PooledCombatObject 캐시로 바꾸고, 반납 자식 탐색을 미리 저장한 목록으로 바꿨다. 부모에 붙여 대여하는 풀 오브젝트를 목록에 등록하고, 반납 때 제거한다. 자식 반환 구간에 `PoolManager.ReturnCachedChildren` ProfilerMarker를 추가했다.
- `PooledCombatObject.cs`: Initialize에서 VisualDebrisCloud 참조 및 기존 자식 풀 컴포넌트 목록을 캐시한다.
- `ProjectileMotor.cs`: EndChest 조회 전 계층 레이어를 검사하고, EndChest 판정 및 Customer/DestructibleTarget/TargetHealth의 부모 탐색을 TryGetComponent 기반으로 바꿨다.
- `ProjectileStatsSource.cs`: StatCollector와 CubeAssembly를 Awake에서 보관하고, PlacedBlock의 ModuleStats/ColorModule를 TryGetComponent로 조회한다. Collector 없는 폴백 모듈 합계는 런타임 구성 변경 반영을 위해 기존 조회 동작을 유지한다.
- `VisualDebrisCloud.cs`: DestructibleTarget 부모 검색을 TryGetComponent 계층 탐색으로 바꿨다.

## 동작 변화

- 게임 판정 의도와 풀 반환 순서는 유지했다. EndChest 레이어가 collider 자신이 아니라 상위 Transform에 설정된 경우도 검사하도록 계층 전체의 레이어를 확인한다.
- 인스펙터 값 변경 없음.

## 남은 위험

- 풀 자식 목록은 Initialize 시 기존 자식과 이후 Rent 시 부모 Transform에 직접 연결된 항목을 기록한다. 외부 코드가 대여된 오브젝트의 Transform 부모만 직접 바꾸는 경우는 풀의 소유 목록에 등록되지 않는다.
- 비교 프로파일 측정은 하지 않았다.

## 컴파일 결과

- `unity --no-banner recompile --timeout 180` 실행 완료. 출력이 없어 지침에 따라 성공으로 기록한다.

## README 제안

- 풀 컴포넌트의 선택적 참조와 반환 자식은 초기화/대여 시 캐시하며, ProjectileMotor의 EndChest 부모 탐색은 EndChest 계층 레이어에서만 수행한다.

