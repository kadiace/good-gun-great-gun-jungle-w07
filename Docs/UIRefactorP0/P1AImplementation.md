# P1-A 첫 UI 작업 단위: 공용 4레이어와 일시정지 MVP

## 1. 범위와 확인 수준

- 이 문서는 **P1-A 전체 또는 전체 UI 개편의 완료 기록이 아니라 첫 작업 단위**다. 이번 완료 범위는 프리팹에 작성한 4레이어(`HudLayer`/`ScreenLayer`/`PopupLayer`/`TransitionLayer`)·`UIRoot`, `PauseView`/`PausePresenter`, `UIManager`의 일시정지 연결, 일시정지 기본 선택 복원과 아래의 Dialogue 호환성 예외까지다. 공통 입력·전체 P1/P2/P3는 완료가 아니다.
- 이번 커밋 단위는 authored 4레이어 + `PauseView`/`PausePresenter` + 기본 선택 복원 + Dialogue 호환성 예외다.
- Dialogue는 이번 첫 커밋에서만 **root 직계 legacy 예외**로 유지한다. P1-B에서 정렬을 명시 참조로 이관하기 전까지 `ScreenLayer`로 옮기지 않는다.
- 확인 수준은 **소스·YAML 정적 대조와 수정 자산 개별 임포트**다. 임포트를 위한 Unity 자동 처리와 별도로 컴파일 성공·플레이·빌드 검증을 판정하지 않았으며, 검증 입력/캡처와 에디터에서의 씬 실효 참조·오버라이드 해석도 수행하지 않았다. 플레이 확인과 테스트는 사용자 담당이다.
- `GameManager.cs`, `SoundManager.Music.cs`, 파괴 처리, `NightRaidController.cs` 등 사용자 변경 파일은 읽기만 했고 수정하지 않았다.
- 관리자가 아래 Assets 5개만 `AssetDatabase.ImportAsset`으로 각각 한 번 임포트했다. 기존 에디터 큐의 소비와 동일한 5경로의 완료 마커를 확인했다. 전체 Refresh와 폴더 단위 임포트는 실행하지 않았다.
  1. `Assets/Scripts/UI/Common/UIRoot.cs`
  2. `Assets/Scripts/UI/Pause/PauseView.cs`
  3. `Assets/Scripts/UI/Pause/PausePresenter.cs`
  4. `Assets/Scripts/UIManager.cs`
  5. `Assets/Prefabs/Managers/UIManager.prefab`
- `UIManager.prefab`의 Dialogue 루트 직계 복원·새 블록 끝 공백 정리 이후에도 해당 프리팹만 개별 임포트했다. 별도 임포트 실행의 완료 마커에서 이 경로 하나만 확인했다.

## 2. PauseView 배치와 초기 상태

- `PauseView` 컴포넌트 `&8910000000000000012`를 비활성 `PausePanel` GO `&6287180524973468462`에서 항상 활성인 `UIManager` GO `&5513238841504740859`로 옮겼다. 비활성 부모에 붙어 첫 `Show()`의 `SetActive(true)` 시점에 `Awake`가 실행되며 `HideImmediate()`로 화면을 닫던 문제를 없앤다.
  - `UIManager` GO의 `m_Component` 목록에 추가하고 `PausePanel` 목록에서 제거했다.
  - 컴포넌트 블록의 `m_GameObject`를 `&5513238841504740859`로 맞췄다.
  - `m_pausePanel`은 실제 `PausePanel` GO `&6287180524973468462` 참조를 유지한다. `RectTransform` 속성도 해당 패널의 RT를 반환한다.
- `Awake`와 `Initialize` 중 먼저 실행된 쪽이 `ApplyInitialHiddenState()`로 초기 숨김을 한 번만 적용한다. 이후 `Initialize()` 재호출은 초기화된 상태 그대로 `true`를 반환하며 열린 패널을 다시 숨기지 않는다.

## 3. Presenter 의존성과 Refresh 정책

- `PausePresenter(PauseView, GameManager, UIRoot)` 생성자 모델을 유지한다. `Initialize()`는 View·GameManager·UIRoot가 모두 필수이며 `UIRoot.ValidateLayers()`가 `false`면 실패한다.
- `UIRoot` 누락 시 `transform.SetAsLastSibling()`이나 `EventSystem`을 직접 조작하던 우회 경로를 제거했다. 표시·포커스는 `UIRoot`를 통해서만 적용한다.
- `SaveCurrentFocus`에는 `m_view.transform`이 아니라 실제 `PausePanel`의 `RectTransform`(`m_view.RectTransform`)을 전달한다. 패널 내부 선택이 이전 포커스로 덮어써지지 않게 하기 위함이다.
- `Refresh()`는 무인자이며 생성자에 받은 `GameManager`만 읽는다. `UIManager`는 같은 명시적 `GameManager`로 Presenter를 생성·Refresh하고, 다른 인스턴스가 실제 전달되면 `IsBoundTo` 확인 후 기존 Presenter를 `Dispose()`하고 새 모델로 재생성한다.
- 새로 열리는 전환(`shouldShow && pauseChanged`)에서만 `SaveCurrentFocus` → `BringToLayerFront` → `Show` → 기본 선택 적용을 수행한다. 이미 일시정지 중인 상태의 Refresh에서는 `Show()`를 반복 호출하지 않으므로 `SoundSettingsUI.Open()`이 숨긴 `MainPanel`을 다시 켜지 않는다.
- 닫힘 전환에서만 `Hide()`와 `RestorePreviousFocus()`를 적용한다. `UIManager.Refresh`는 일시정지가 닫히는 전환에서 기존처럼 `SoundSettingsUI.Close()`를 한 번 호출한다.
- 시작 시 명시적 초기 숨김은 `PauseView.Awake`/`Initialize`에서 유지한다.

## 4. 실제 일시정지 메뉴 버튼 4개

실제 `UIManager.prefab`에는 아래 4개 버튼만 있다. `Restart`/`LeaveWorkshop` 버튼은 없으므로 View 필드·이벤트·Presenter 처리와 `SetLeaveWorkshopAvailable`을 제거했다.

| 버튼 | 컴포넌트 fileID | 연결 |
| --- | --- | --- |
| `ResumeButton` | `&3421273533541874154` | Inspector persistent call 비움 유지. `PauseView.ResumeRequested` → Presenter → `GameManager.Resume()`. 기본 선택 |
| `OpenSaveSlotsButton` | `&1780604437048568443` | 기존 `SaveSlotsUI.Open` persistent call 유지 |
| `OpenSoundSettings` | `&1165571622861086053` | 기존 `SoundSettingsUI.Initialize`의 `openButton` 구독 유지 (Inspector persistent call은 원래 비어 있음) |
| `ExitToMainMenuButton` | `&8172066909719320594` | 기존 `PauseMenuNavigation.ExitToMainMenu` persistent call 유지 |

- `m_navigationButtons`는 필수다. null/빈 배열/null 항목을 Initialize에서 오류로 기록하며 Resume 단독으로 조용히 대체하지 않는다. 오류 메시지에 배열 상태와 null 인덱스를 포함한다.
- 실제 4버튼은 상하 `Explicit`, `wrapAround: false`, 양 끝 비순환으로 구성하고 기본 선택은 `ResumeButton`이다.
- `UIManager.Resume()/Restart()/LeaveWorkshop()` public API는 외부 호환을 위해 유지한다. 이 메뉴에 대응 버튼을 새로 만들지 않는다.

## 5. 실제 호출 경로 (소스 대조)

`GameManager.ApplyPauseState()`라는 함수는 소스에 없다. 실제 경로는 다음과 같다.

| 트리거 | 실제 경로 |
| --- | --- |
| 씬 시작 | `GameManager.Start()` → `UIManager.Refresh(this)` |
| Pause 입력 | `GameManager.Awake()`에서 `Game` 맵 `Pause` performed 구독 → `TogglePause()` → `UICancelRouter.RouteCancel()` → 화면별 소비 또는 Pause 상태 갱신 → `UIManager.Refresh(this)` |
| Resume 버튼 | `PauseView.OnResumeClicked` → `ResumeRequested` → `PausePresenter.OnResumeRequested` → `GameManager.Resume()` → `IsPaused`이면 취소 라우팅 없이 `SetPaused(false)` |
| Restart (legacy API) | `GameManager.Restart()` → `IsPaused = false`, `Time.timeScale = 1` → `UIManager.Refresh(this)` |
| SetNight | `GameManager.SetNight(bool)` → `IsPaused = false`, `Time.timeScale = night ? 0 : 1` → `UIManager.Refresh(this)` |
| LeaveWorkshop (legacy API) | `GameManager.LeaveWorkshop()` → `Resume()` 후 `workshopSession.Leave()` |
| 표시 갱신 | `UIManager.Refresh(gameManager)` → `EnsurePausePresenter(gameManager)` → `PausePresenter.Refresh()` → `UIRoot`/`PauseView` |

- `TogglePause`의 기존 우선 처리(밤 습격 진입 차단, 하위 UI·대화·선택·팔레트·공방 퇴장 등)는 이번 slice에서 바꾸지 않았다.

## 6. 프리팹 보존과 레이어 구조

- HEAD 프리팹의 기존 YAML block 3163개 fileID를 모두 보존했다. 새 block은 `&8910000000000000001`~`&8910000000000000012` 12개뿐이며 합계 3175개다(4레이어 GO/RT, `UIRoot`, authored `Canvas`, `GraphicRaycaster`, `PauseView`).
- 루트 `UIManager` GO `&5513238841504740859` 아래 `HudLayer`/`ScreenLayer`/`PopupLayer`/`TransitionLayer` 전체 화면 스트레치 RT와 `UIRoot` `&8910000000000000009`를 유지한다. 루트 직계 비UI `Transform` 3개(EventSystem, 에디터 구분선 2개)도 유지한다. 각 레이어 RT의 `m_Father`와 루트 `m_Children`은 서로 일치한다.
- 새 4레이어의 `HudLayer` → `ScreenLayer` → `PopupLayer` → `TransitionLayer` 순서는 루트 `m_Children`에서 유지한다. 원래 root 직계 36개의 상대 순서 전체를 되돌리지 않고 Dialogue만 예외로 복원했다.
- Dialogue nested instance `&6669556778551344344`는 `m_TransformParent`를 루트 `&4338719179332918583`로 되돌리고, stripped RT `&1242774878275622100`을 루트 `m_Children`에 둔다. `ScreenLayer` `m_Children`에는 넣지 않는다. `DialogueUI.EnsureCanvasSorting`이 최상위 rootCanvas의 직계 branch에만 Canvas 150/Raycaster를 붙이는 기존 경로를 보존하기 위한 **첫 커밋 한정 legacy 예외**이며 P1-B 전용 이관 대상이다. RT local/anchor/layout·활성 상태는 바꾸지 않았다.
- 추가한 블록 `&8910000000000000001`~`&8910000000000000012`의 빈 `m_Name`/`m_EditorClassIdentifier` 6행 끝 공백만 제거했다. prefab 전체 포맷은 실행하지 않았다.
- `PausePanel`은 기존 `ScreenLayer` 위치, authored `Canvas`(`sortingOrder: 200`, `overrideSorting: 1`), `GraphicRaycaster`를 유지한다. 이는 레거시 정렬 예외이며 P1-B/P3에서 제거 대상이다.
- 게임 규칙·버튼 외형·기존 persistent call은 바꾸지 않았다. Resume persistent call 비움은 유지했다.
- `WeaponNamePanel`, `PresetPanel` 등 현재 `HudLayer` 배치는 **임시 legacy 분류**다. 전체 화면의 최종 레이어 확정이 아니며 P2-C 등에서 다시 분류한다.

## 7. 씬 영향과 후속 제약

이 절의 첫 단위 이후 대화는 [P1-B 대화 MVP](P1BDialogueImplementation.md)에서 저작된 Canvas/차단기/중계로 이관하고 ScreenLayer로 옮겼다. 아래 루트 직계·런타임 생성 설명은 당시 남긴 예외의 근거이며 현재 미이관 상태로 읽지 않는다. 전체 정렬·입력 통합은 여전히 후속이다.

- 정적 grep에서 `UIManager.prefab`(`guid: cd14c968f8efebe4c8bd772e4099a0a2`)을 참조하는 씬은 17개다. 모두 base prefab을 가리키는 stripped 참조이며, 새 `PauseView`/`UIRoot` 컴포넌트 fileID를 직접 오버라이드하는 씬은 없다. 씬 실효 참조·오버라이드 해석은 에디터에서 확인하지 않았다.
- 씬에서 추가한 UI와 레거시 sorting override는 이번 slice에서 보존을 완료로 주장하지 않는다. 씬 통합·정렬 정리는 후속 **P3-A** 대상이다.
- `UIRoot`의 포커스 보관은 현재 Pause 전용 단일 슬롯이다. 중첩 팝업이 같은 슬롯을 공유하면 복원 대상이 섞이므로, 중첩 화면을 붙이기 전에 화면 소유자별 보관 구조가 필요하다.
- Dialogue가 루트 직계에 남은 것은 이번 첫 커밋의 호환성 예외다. P1-B 이관 전까지 이 상태를 회귀로 취급하지 않는다.
- 아래 후속 항목은 구현 완료가 아니다.
  1. **P1-B**: `FeedbackUI`/`DayTransitionUI`의 런타임 `Canvas`·`overrideSorting` 제거와 공통 흐름 통합. `DialogueUI.EnsureCanvasSorting`의 root 직계 branch 의존을 명시 참조 기반 정렬로 이관하고 이번 커밋의 Dialogue root 직계 예외를 해소한다.
  2. **P2-A**: Save/Settings/Exit 버튼과 `SaveSlotsUI`/`SoundSettingsUI`/`PauseMenuNavigation` 연결을 View/Presenter 경로로 이관. D03 중첩·ESC 단계 복귀.
  3. **P3-A**: 씬 prefab instance, scene-added UI, legacy sorting override 통합 대조.
