# H1 습격 복귀 프레임 가리기

## 원인과 근거
- `Tools/Profiling/return_frame_options.md`에는 Main 활성화 프레임에 습격 씬 오브젝트 Destroy 8,539회(251.5ms), Main Integrate 132.8MB, 엔진 유발 `GC.Collect` 213.6ms가 겹치며 전환 화면이 없다고 기록돼 있다.
- `Assets/Scripts/NightRaidController.cs:457-487`의 기존 복귀는 데이터 처리 후 `PoolManager.ClearActive()`와 `LoadSceneAsync(..., Single)`을 연속 실행했다. `Assets/Scripts/StartMenu_UI/GameSaveController.cs:167-185`는 Main에서 저장/귀환 상태를 소비한 뒤 프레임을 넘겨 `AdvanceAfterRaid(true)`를 호출하고, `DayTransitionUI.BeginReturn`은 그 이후 시작한다.

## 파일별 변경
- `Assets/Scripts/NightRaidController.cs:484-487`: 기존 `PrepareReturn` / standalone 스냅샷 확정, 사망 플래그, `ReturnDayFadePending` 순서는 유지했다. 풀 정리·씬 로드는 런타임 오버레이에 맡긴다.
- `Assets/Scripts/NightRaidReturnOverlay.cs`: 시작 시 비활성화된 런타임 검은 Canvas를 생성하고 `DontDestroyOnLoad`로 유지한다. 0.3초 페이드 후 풀을 비우고 `LoadSceneAsync`를 시작, 진행률 0.9에서 명시 `GC.Collect` 후 씬 활성화를 허용한다. Main의 `DayTransitionUI`가 시작하면 완료까지 기다렸다 걷고, 전환이 없는 복귀는 0.3초 대기 후 걷는다. 전환 대기는 10초 제한을 둔다.
- 새 경로에 `NightRaidReturnOverlay.Fade`, `.LoadWait`, `.GCCollect` ProfilerMarker를 추가했다.

## 동작 변화
- 체크포인트 복귀는 준비·복원 데이터 확정 뒤 검은 화면으로 덮고 비동기 로드한다. `NightRaidTransfer` 소비 시점과 `AdvanceAfterRaidNextFrame` → `StartNextDay`는 변경하지 않았다.
- 체크포인트 없는 직접 실행도 `ReturnHome`에서 동일한 오버레이·로드를 사용한다. 사망 복귀도 같은 메서드를 통하므로 같은 검은 페이드와 비동기 활성화를 사용하고, 날짜 전환은 기다리지 않고 자동으로 걷는다.
- 날짜 전환이 시작되면 오버레이가 `DayTransitionUI.IsTransitioning`이 끝날 때까지 UI 레이캐스트를 막는다. 별도 입력 액션은 추가하지 않았다.

## 인스펙터 값
- 추가 연결 항목 없음. 런타임 설정: 페이드 0.3초, Main 전환 감지 유예 0.3초, 전환 대기 상한 10초, Canvas 정렬 순서 32767.

## 남은 위험
- B+D는 언로드·Integrate·GC 총 CPU 비용을 줄이는 대신 검은 화면 뒤로 감춘다. 명시 GC 자체도 멈춤을 만들 수 있으나, 관측된 엔진 GC가 213.6ms이며 적재 뒤 활성화 전에 수행해 이를 씬 표시 전으로 옮기도록 했다. 실제 빌드 측정은 하지 않았다.
- `DayTransitionUI`를 찾지 못하거나 전환이 10초 이상 이어지면 오버레이는 자동 해제된다. 수정 범위 제한으로 해당 UI의 완료 이벤트에는 연결하지 않았다.

## 컴파일 결과
- 저장소 루트에서 `unity --no-banner recompile --timeout 180` 실행 성공(종료 코드 0, 출력 없음). 수정한 Assets 두 파일은 `AssetDatabase.ImportAsset`으로 각각 한 번 임포트했다. 플레이·테스트는 실행하지 않았다.

## README 제안
- 습격 복귀는 준비 완료 후 런타임 검은 오버레이 뒤에서 비동기 씬 활성화와 명시 GC를 수행한다.
- 체크포인트 복귀 날짜 페이드 완료 시 오버레이가 해제되며, 사망/직접 실행 복귀는 자동 해제 대기 경로를 쓴다.
