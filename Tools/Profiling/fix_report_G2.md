# G2 발사체 경로 할당 감소

## 원인과 근거

- `Tools/Profiling/last_report.txt:68`의 폭발 프레임에서 `ProjectileMotor.Update` 438회가 inclusive GC 76,575.7KB를 기록한다(호출당 약 175KB). 이 보고서는 하위 메서드별 할당 스택을 제공하지 않으므로 이 총량을 특정 한 줄의 비용으로 단정할 수 없다.
- 기존 충돌 경로는 임팩트마다 `ProjectileMotor.ShowImpact`에서 `GetComponentsInChildren<ParticleSystem>` 및 `GetComponentsInChildren<ParticleSystemRenderer>` 배열을 각각 만들고, 물 속성탄은 `new MaterialPropertyBlock()`을 수행했다. 대상 관통 시작 때 `GetComponentsInChildren<Collider>(true)` 배열도 만든다. 유도탄은 `TargetHealth.AimPoint`를 읽을 때마다 Collider 배열을 만든다.
- `TryFindHit`의 `LayerMask.GetMask`는 `params string[]` 호출이므로 매 충돌 질의마다 문자열 배열 두 개를 만들 수 있었다. `UpdateIgnoredColliders`의 캡처 람다도 매 Update 재생성 경로였다.
- `CombatPhysicsQueries.BoxCast`는 이미 `BoxCastNonAlloc`과 정적 버퍼를 사용한다. 결과 수가 버퍼 크기에 닿을 때만 버퍼를 늘리고 재질의하므로 일반 호출의 배열 반환 할당은 없다. 다만 용량 확장 프레임에는 배열 할당이 남는다.

## 파일별 변경

- `Assets/Scripts/Projectile/ProjectileMotor.cs:16,214`: 충돌 레이어 마스크를 정적으로 계산해 쿼리마다 생기던 params 배열을 제거했다. `AddTargetCollidersToIgnore`는 인스턴스 `List<Collider>` 버퍼를 재사용하며, 무시 콜라이더 정리도 재사용 목록으로 처리한다. 수중 파편 `MaterialPropertyBlock`은 발사체 인스턴스에서 재사용한다. `CollisionQuery`, `ImpactEffect` ProfilerMarker를 추가했다.
- `Assets/Scripts/Projectile/ProjectileMotor.cs:368,440`: 관통 콜라이더 목록과 수중 tint 객체의 재사용 지점.
- `Assets/Scripts/Projectile/PooledCombatObject.cs:34`: 풀 객체 생성/초기화 시 파티클 렌더러 배열을 한 번 캐시한다. 임팩트 재생 때 기존 파티클·렌더러 자식 배열 재조회 없이 캐시된 배열을 사용한다.
- `Assets/Scripts/Projectile/TargetHealth.cs:41`: AimPoint 조회 때 `GetComponentsInChildren`의 List 오버로드와 인스턴스 목록을 사용한다. 호출마다 목록은 갱신되므로 자식 Collider 추가/제거를 반영하면서 배열을 만들지 않는다.
- `HitPayload.cs`, `ProjectileEffects.cs`, `CombatPhysicsQueries.cs`, `ExpandingExplosion.cs`, `PoolManager.cs`는 수정하지 않았다. 피해 문맥은 값 형식이며, 물리 질의는 이미 NonAlloc이어서 이 변경에서 손댈 반복 배열 원인이 없었다.

## 동작, 인스펙터, 위험

- 충돌 필터·순서·관통·피해량은 변경하지 않았다. 같은 `GetComponentsInChildren` 결과 순서로 순회하고, 물리 쿼리의 기존 버퍼 확장/재시도 방식도 유지했다.
- 인스펙터 값 변경 없음.
- 원시 프로파일에는 하위 할당 스택이 없고 수정 후 프로파일 캡처도 수행하지 않아 76.6MB 중 각 원인의 기여량과 실제 감소량은 아직 수치화하지 않았다. `PooledCombatObject`의 파티클 캐시는 객체 초기화 시점의 자식 구성을 사용하므로 생성 후 파티클 계층을 동적으로 변경하는 프리팹은 다시 검토해야 한다. 공유 `CombatPhysicsQueries` 버퍼가 포화되면 용량 증가 할당이 발생할 수 있다.
- G1 소유 `DestructibleTarget*` 파일 등 발사체 밖의 하위 호출은 수정하지 않았다. 이들의 inclusive 할당이 남을 수 있다.

## 컴파일

- 수정한 Assets 세 파일에 `AssetDatabase.ImportAsset`을 각각 한 번 호출했다.
- `unity --no-banner recompile --timeout 180`: 성공(출력 없음, 종료 코드 0).
- 플레이 및 테스트는 수행하지 않았다.

## README 제안

- 발사체 임팩트/관통/유도 조준 경로는 재사용 버퍼와 풀 초기화 시 파티클 컴포넌트 캐시를 사용한다.
- `ProjectileMotor.Update`의 기존 프로파일은 하위 호출별 GC 스택이 없어 수정 후 할당량 비교 측정이 남아 있다.
