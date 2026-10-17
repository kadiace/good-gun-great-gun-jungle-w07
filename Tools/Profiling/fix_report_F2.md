# 습격 복귀 프레임 개선 (F2)

## 원인과 근거
- `reset_frame_report.md` 원인 표의 frame 347 항목에서 `PlacedBlock.OnEnable` 190회가 무기 콜라이더 수집, 플레이어 탐색, 플레이어 콜라이더별 부모 검색을 반복한 것으로 확인했다(107ms, `GetComponentNullErrorMessage` 8,136회). 관련 코드는 `PlacedBlock.cs:93-96` → `EquipableWeapon.cs:179-187`이었다.
- `GameSaveController.World.cs:101`의 `Resolve`는 매 ID마다 `mapObjects.FirstOrDefault`를 수행했고, 복원 도색 적용(`World.cs:289-294`)은 매 도색마다 블록 전체를 검색했다. 유효성 검사·블록 변환(`World.cs:215-234, 272-281`)에도 중첩 LINQ 순회가 있었다.
- 복귀 경로는 `NightRaidController.cs:487`의 Single 씬 로드 뒤 `GameSaveController.cs:156-185`에서 체크포인트 복원과 `AdvanceAfterRaid` 호출이 같은 시작 흐름에 이어졌다. 언로드의 `Destroy` 8,026회는 씬 언로드 비용이므로 이번 코드 변경에 포함하지 않았다.

## 파일별 변경
- `EquipableWeapon.cs`: 플레이어 콜라이더 목록을 무기별로 캐시하고, 전체 재설정과 단일 블록 재설정 경로를 분리했다. 복원 범위용 disposable 배치를 추가해 종료 시 무기 단위 전체 처리를 한 번 수행한다. 두 경로에 ProfilerMarker를 추가했다.
- `PlacedBlock.cs`: 블록이 활성화될 때 자신과 자식의 콜라이더만 처리하도록 변경했다.
- `GameSaveController.World.cs`: ID 별칭 조회 딕셔너리, 도색 재료 캐시, 셀→블록 사전 조회를 추가하고 복원 검증·블록 변환의 중첩 LINQ를 루프로 바꿨다. ID 조회 ProfilerMarker를 추가했다.
- `GameSaveController.cs`, `GameSaveController.Travel.cs`: 레거시·습격 무기 복원에도 충돌 설정 배치를 적용했다. 습격 복귀 후 다음 날 처리는 다음 프레임으로 예약한다.
- `NightRaidController.cs`: 수정하지 않았다. `LoadSceneAsync` 경로를 유지했다.

## 동작 변화
- 처음 활성화·장착 시 무기와 플레이어 콜라이더 간 충돌 무시 결과를 유지한다. 신규 블록은 해당 블록 콜라이더만 즉시 처리하고, 세이브/습격 복원에서 여러 블록이 추가될 때는 배치 종료 후 한 번 처리한다.
- ID 충돌이 있는 경우 기존 `FirstOrDefault`와 같이 `mapObjects`에서 먼저 등장한 바인딩을 유지한다. 셀 중복이 있을 때 페인트 적용도 기존 첫 블록 선택과 같이 첫 항목을 사용한다.
- 체크포인트 복원 의미는 유지하며, 복원과 다음 날 시작 사이에 한 프레임을 둔다. 페이드 UI는 다음 프레임에 시작한다.

## 인스펙터 값
- 변경 없음.

## 남은 위험
- 동적으로 플레이어를 추가하거나 플레이어 콜라이더를 바꾸는 런타임 경로는 캐시 무효화가 필요하다. 현재는 무기 활성화 시 캐시를 갱신하며, 기존 게임 플레이 경로는 플레이어를 씬 시작 시 구성한다.
- 씬 언로드의 `Destroy` 8,026회는 여전히 복귀 비용에 남는다. 개선 제안: 습격 씬 정적 오브젝트 수를 줄이고 전환 전 페이드/로딩 화면 뒤에 씬 언로드를 유지해 체감 부하를 가린다.
- 프로파일러 재측정은 실행하지 않았다.

## 컴파일 결과
- 첫 `unity --no-banner recompile --timeout 180`에서는 F2 파일 오류가 없었고, 다른 워커 소유 `Assets/Scripts/Projectile/ExpandingExplosion.cs`의 `collectTargets`/`collectPieces` 미정의 오류 8건이 보고되었다. 약 1분 뒤 재확인과 최종 Assets 임포트 후 컴파일은 출력 없이 완료되었다.

## README 제안
- 습격 복귀 무기 복원은 충돌 무시 설정을 무기 단위로 묶고, 다음 날 시작을 복원 다음 프레임으로 넘긴다.
