# W1 파괴 처리 병목

## 원인과 근거

- `compare_report.md:38-40`의 폭발 프레임 266에서 `Subdivide`는 174회·114.84ms·271.5MB, `ResolveSupport`는 266회·125.24ms였고 프레임 할당량은 304MB였다.
- 기존 `DestructibleTarget`은 칸마다 `Cell` 객체를 만들고 `grid`, `coordinates`, `cellPositions`, `cellSizes` 사전을 각각 채웠다. 이 중복 저장과 용량 증가가 분할 호출에 누적됐다. 현행 평탄 배열 인덱스는 `DestructibleTarget.cs:251-300`, 생성은 `:1015-1061`이다.
- 기존 `DropUnsupported`는 모든 격자 칸을 지지·낙하 집합에 넣고 여러 차례 전체 Flood를 수행했다. 이제 안정 상태가 확인됐고 바닥 지지 칸이 제거되지 않은 경우 제거 경계에 닿은 연결 성분만 방문한다(`DestructibleTarget.Rendering.cs:106-158`, `DestructibleTarget.cs:903-989`).
- `FallingRangeTarget`의 반복 `GetComponent`는 기존 `DestructibleTarget.StructuralSupport.cs:116` 조회가 근거 자료 `getcomponent_report.md:75-76,88`의 5위 항목이었다.

## 파일별 변경

- `DestructibleTarget.cs`: `Cell`을 구조체 배열로 바꾸고 점유 격자와 셀 목록을 평탄 인덱스로 연결했다(`index = x * dy * dz + y * dz + z`). 좌표·초기 위치·크기를 셀에 넣어 사전 3개를 제거했고, 리스트 용량을 분할 때 확보한다. 충돌 컬럼의 실행 목록은 용량을 예약하고 대상 파괴 후 정적 풀에 돌려준다. 기존 `CollectExplosionPieces`는 유지하고 `TryCollectExplosionPieces` 및 준비 조회를 추가했다.
- `DestructibleTarget.Rendering.cs`: 기본값 0(무제한)인 정적 프레임 예산, 새 경로용 ProfilerMarker, 지지 국소 탐색·전체 대체 경로 마커를 추가했다.
- `DestructibleTarget.StructuralSupport.cs`: 외부 지지 셀을 인덱스로 기록하고 `FallingRangeTarget` 조회 결과를 Awake에서 캐시한다. Start에서 한 번 갱신해 TargetBell/튜토리얼 스폰처럼 Awake 뒤 Start 전에 추가되는 컴포넌트를 반영한다.
- `StructuralSupportGraph.cs`는 수정하지 않았다.

## 동작 변화와 결과 동등성

- 격자의 차원·칸 좌표·칸 순서는 기존과 같다. 활성 칸 목록은 이전과 동일하게 swap-remove 순서를 유지하고, 폭발 후보는 기존 슬롯 순으로 정렬한다.
- 지지 판정은 이전에 유효했던 안정 상태에서 제거 경계의 각 연결 성분을 한 번씩 탐색한다. 각 성분 안에 바닥 접촉 셀 또는 외부 지지 셀이 하나라도 있으면 기존 두 단계 Flood와 같이 그 성분 전체를 유지한다. 둘 다 없으면 그 성분 전체를 기존과 같이 떨어뜨린다. 따라서 제거 경계에 닿지 않은 성분은 기존 안정 상태를 그대로 유지하고, 바닥 지지가 제거됐거나 유효한 과거 지지 상태가 없으면 기존 전체 판정 경로를 쓴다.
- `TryCollectExplosionPieces`는 예산 초과 시 아직 분할되지 않은 대상에 대해 `false`를 반환하고 조각 목록을 변경하지 않는다. 호출자는 같은 폭발의 대상 목록·현재 커서·조각 버퍼를 보존하고 같은 대상을 다음 프레임에 재시도해야 한다. `true`이면 대상 처리가 끝난 것이므로 커서를 진행한다. 모든 대상 처리가 끝나기 전에는 결과를 그룹화하거나 버퍼를 비우면 안 된다. `CanCollectExplosionPiecesNow`는 비예약 준비 조회이며, 최종 판정은 항상 Try 메서드 결과를 따른다.
- 현재 `ExpandingExplosion` 호출부는 이 계약을 아직 사용하지 않으므로 기존 동기 동작은 변하지 않는다. 후속 호출부 작업에서 계약을 연결해야 분할 분산이 실제 적용된다.

## 인스펙터 값 및 예산

- 인스펙터 항목은 없다. `DestructibleTarget.MaxSubdivisionsPerFrame` 정적 설정의 기본값은 0으로 무제한이다. 호출부가 재시작 가능한 수집 계약을 채택하기 전에 기본 폭발 결과가 바뀌지 않도록 한 값이다.
- 호출부 연결 후 권장 시작값은 4다. 근거 데이터의 분할 평균은 약 0.66ms/대상이므로 프레임당 약 2.7ms, 174대상 묶음은 약 44프레임에 분산된다. 이는 프로파일 측정 전 추정치다.
- 씬 로딩 중 분할을 추가하지 않았다.

## 남은 위험과 검증

- 후속 호출부가 Try/준비 조회 계약을 연결하기 전에는 기존 호출부가 동기 분할을 계속한다. 다른 직접 파괴 API도 이 폭발 수집용 예산 계약을 통하지 않으므로 프레임 예산 대상이 아니다.
- 수정 후 플레이·프로파일 측정은 실행하지 않았다. `compare_report.md`와 `last_report.txt`는 수정 전 자료로 남아 있어 할당량·프레임 시간 감소 수치는 아직 없다.
- Unity 재컴파일: `unity --no-banner recompile --timeout 180`, 종료 코드 0, 출력 없음. 수정한 Assets 파일은 `AssetDatabase.ImportAsset`으로 임포트했다.

## README 제안

- DestructibleTarget은 셀과 점유 상태를 평탄 배열로 저장하며, 유효한 지지 상태의 국소 파괴는 제거 경계 연결 성분만 판정한다.
- 폭발 분할 예산은 기본 무제한이며, 호출부가 `TryCollectExplosionPieces` 재시작 계약을 연결한 뒤 프레임당 제한을 설정할 수 있다.
