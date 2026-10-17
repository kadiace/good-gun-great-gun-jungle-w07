# W2 폭발 병합·프레임 비용 변경

## 원인과 근거
- 기존 병합은 같은 무기 폭발 중심 간 거리가 작은 반경의 0.5배 이내일 때만 합쳤다(변경 전 ExpandingExplosion.cs의 병합 거리 검사; 현재 tuning 배율 적용은 [ExpandingExplosion.cs:152](../../Assets/Scripts/Projectile/ExpandingExplosion.cs:152)); 333.data frame 266에서 요청 200회가 Build 149회로 줄었고, Step 267회/87.80ms·Build 13.69ms/204MB 할당이 남았다([compare_report.md](compare_report.md), [last_report.txt](last_report.txt)).
- 각 파동 Step이 활성 발사체 집합을 매번 복사했고, Build가 파동별 레이어/조각 리스트와 정렬 비교 람다에 의존했다(변경 전 ExpandingExplosion.cs:267, 306).

## 파일별 변경
- `Assets/Scripts/Projectile/ExpandingExplosion.cs`: 병합 배율을 tuning에서 읽는다. 프레임당 활성 발사체 목록을 한 번만 복사해 그 프레임의 모든 파동이 재사용한다. 조각을 파동당 단일 재사용 List에 모아 정적 비교자로 그룹 정렬하고, 레이어는 조각 범위(start/count)만 보유하도록 바꿔 레이어마다 조각 List를 만들고 키 Dictionary를 채우는 경로를 없앴다. 정렬 비교자는 정적 delegate로 유지한다.
- `Assets/Scripts/Projectile/ProjectileTuning.cs`, `Assets/Data/ProjectileTuning.asset`: `explosionMergeRadiusMultiplier`를 추가하고 에셋 기본값을 1.5로 설정했다.

## 동작 변화
- 같은 프레임 같은 무기의 병합 반경은 두 폭발 중 작은 반경 × 1.5(기존 0.5)다. 이 값으로 더 많은 요청을 병합한다.
- 병합된 위력 합, 최강 폭발 위력의 4배 상한, 누적 위력의 세제곱근 반경 산정은 유지했다. 병합되지 않는 단발 폭발의 피해·반경 경로는 바꾸지 않았다.
- Step의 활성 발사체 열거는 프레임당 한 번의 스냅샷을 공유한다. 프레임 중 나중에 등록된 발사체는 다음 프레임 스냅샷에서 확인될 수 있다.

## 인스펙터 값
- Projectile Tuning > Explosion Merge Radius Multiplier: 기본 1.5. 0이면 중심이 정확히 같은 요청만 합친다. 기존 동작에 가까운 값은 0.5다.

## 남은 위험
- 병합 범위가 커져 같은 무기의 근접한 별도 폭발이 단일 중심 파동이 될 수 있다. 누적 피해·위력 상한·반경 보존 규칙은 기존과 같지만 병합 대상과 파동 중심은 바뀐다.
- 단일 재사용 조각 버퍼의 용량 8192를 넘거나 레이어/체력 목록 기본 용량을 넘는 큰 폭발은 List 용량 확장 할당을 할 수 있다. W1 소유의 DestructibleTarget 내부 할당은 수정하지 않았다.
- 수정 후 프로파일러 재측정 및 플레이 검증은 수행하지 않았다.

## 컴파일 결과
- `unity --no-banner recompile --timeout 180` 최종 실행은 출력 없이 성공(exit code 0)했다. 앞선 시도에서는 Unity 서버 연결 실패와 W1 동시 편집 중인 `DestructibleTarget*.cs` 컴파일 오류가 있었으나, 재확인 시 해결되어 전체 컴파일이 통과했다.

## README 제안
- 같은 프레임 같은 무기의 폭발은 두 반경 중 작은 값의 1.5배 거리 안에서 병합하며, ProjectileTuning에서 배율을 조정한다. 위력 합·최강 위력 4배 상한·세제곱근 반경 규칙은 유지한다.

