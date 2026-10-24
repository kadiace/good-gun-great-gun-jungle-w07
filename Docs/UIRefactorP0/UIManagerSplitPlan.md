# UIManager 분할안

기준: develop `55acc4b`. 정적 분석 7건(UIManager.cs 책임, 프리팹 소유권, 외부 의존, 에디터 도구, 씬 오버라이드, UI 스크립트 인벤토리, 씬 맥락) 기반. 플레이 확인은 없다.

**확정 결정**: 범위 P0~P5 전부. 빌드 밖 테스트 씬 9개도 빌드 씬과 같이 재연결. 과도기 위임(UIManager.Player·ShowNotification·Refresh 등)과 구 에디터 도구 4개는 이번에 제거.

**불변 조건**: UIManager.prefab 루트 GameObject(`5513238841504740859`)·루트 RectTransform(`4338719179332918583`)·EventSystem의 fileID는 바꾸지 않는다. 씬의 `player`/`builder`/`gameManager` 연결, 씬에서 루트에 추가한 오브젝트(Main의 SlotMachineCostPopup·RetirePanel·RetireUI, 습격 7씬의 RaidObjectivePanel), 17씬의 EventSystem 입력 연결이 전부 여기에 걸려 있다(⑤ §4·§5-1).

## 0. 진행 상태 (feature/uimanager-split)

- P0·P1 코드 경계, P2 브리지 이동, P3 중첩 프리팹 추출, P4 씬 재연결, P5 습격 변형까지 적용했다. 플레이 확인은 없다.
- 실제 구조는 아래 §2 안과 다르다. 그리기 순서를 바꾸지 않으려고 묶음을 합치지 않고 레이어 직속 오브젝트를 하나씩 중첩 프리팹(32개)으로 뽑았다. 브리지가 있는 화면만 항상 켜진 화면 묶음(`*Screen`, UIScreenRoot)으로 감쌌다(15개). PresetChoicePanel은 PopupLayer에 그대로 두고 Hud의 PresetPanelScreen이 참조한다.
- 추출 때 Unity가 fileID를 유지해 씬 재연결은 옮긴 컴포넌트 38개(씬 참조 13건)만 필요했다.
- P5는 공통+공방+사격장을 담은 UIManager.prefab(Main·테스트 씬)과, 공방 편집·프리셋·무기 코드·사격장 14개를 뺀 UIManager_Raid 변형(습격 9씬) 두 가지다. 정산·날짜 전환·튜토리얼·대화·지도는 두 쪽 모두에 남는다.
- 남은 것: 기존 저작 명령이 중첩 프리팹을 직접 열도록 바꾸기, StartUI_Manager와 Feedback 화면 공유, DaySettlementUI의 재화 HUD 대체 경로 정리.

## 1. 현재 UIManager가 맡은 역할

UIManager는 클래스(371줄)와 프리팹(921개 오브젝트, 루트 컴포넌트 44개) 두 층에서 모두 과적재다.

| # | 역할 | 실체 | 근거 |
|---|---|---|---|
| R1 | 캔버스 루트·레이어 | Canvas/CanvasScaler(1920×1080 match 1)/GraphicRaycaster/EventSystem, UIRoot 4레이어, 저작 스케일 {0,0,0} 복원 | ② §1, UIManager.cs:181 |
| R2 | 화면 브리지 보관소 | 루트 오브젝트에 브리지 19 + View 18(쌍), 다른 시스템이 `ui.GetComponent<T>()`로 꺼내 씀(64곳, UICancelRouter 한 곳에서 11개) | ② §1, ③ §3, UICancelRouter.cs:50-60 |
| R3 | 비활성 화면 강제 초기화 | Awake에서 NightRaidUI·SaveSlotsUI·SoundSettingsUI·ModulePaletteUI·AimUI·HoveringUI 초기화와 의존 주입(builder·Interaction) | UIManager.cs:174-201 |
| R4 | 플레이어 찾기 창구(UI 아님) | `Player`(FindFirstObjectByType)·`PlayerWeaponHolder`를 약 40곳이 경유 | ① 책임③, ③ §2 |
| R5 | 알림·확인창 파사드 | `ShowNotification`(21곳)·`Feedback`(9)·`IsChoiceOpen`(5). FeedbackUI 위임 + 레거시 CanvasGroup 경로 | ① 책임① |
| R6 | 공방 3D 방향 마크·회전 원 | rotationCircle/frontMark/upMark를 LateUpdate에서 투영, WorkshopSession과 양방향 바인딩 | ① 책임②, UIManager.cs:206-261 |
| R7 | 일시정지 브리지 | PauseView+PausePresenter 생성, `Refresh(GameManager)`(GameManager에서 4곳) | ① 책임⑤ |
| R8 | 맥락 혼재 | 작업대·정산·사격장·습격 실행·튜토리얼 UI가 한 프리팹에 실려 모든 게임 씬(Main+습격 9)에 통째로 로드. 씬마다 새 인스턴스(DontDestroyOnLoad 아님) | ⑦ §3·§5 |
| R9 | 잔재 | 데드코드(moduleLabels·UpdateModuleSelection·Resume/Restart/LeaveWorkshop), 알림 함수 안 중복 공방 바인딩 코드(HideNotification·UpdateNotification), 빈 오브젝트 DimUI/HoverUI, 레이어 밖 ShootingTestHUD | ① 책임⑥, ② §4.2, UIManager.cs:27-75(확인) |

## 2. 목표 구조

```
UIManager.prefab  (공통 코어: 씬마다 1개)
├─ 루트: Canvas·Scaler·Raycaster·UIRoot·UIManager(얇은 파사드)
├─ EventSystem
├─ LowHealthVignette            (렌더 순서상 루트 첫 자식 유지)
├─ HudLayer
│   ├─ [Hud/CombatHud]          AimUI·ChargeGauge·ScopeOverlay·HealthHUD·DamageDirection
│   ├─ [Hud/WorkshopView]       WorkshopViewOverlay·PlanarGrid·PlanarDirections·RotationCircle(+마크)·EraseMode
│   ├─ [Hud/WeaponInfo]         WeaponInfo(태그·레시피)·WeaponNamePanel·WeaponCodeButton
│   ├─ [Hud/Currency]           CurrencyGroup(돈·크레딧·티켓·자리세 롤링)
│   ├─ [Hud/PresetPanel]        PresetPanel + PresetChoicePanel(팝업 정렬은 하위 Canvas로)
│   ├─ [Hud/QuestInfo]          QuestInfoPanel(손님 요구·튜토리얼 목표 공용)
│   ├─ [Hud/HoverTooltip]       HoveringUI·ScanLoading·ScanModeText
│   ├─ [Hud/TutorialHud]        TutorialTargetPanel·TutorialButtonCover
│   └─ [Hud/ShootingTest]       ShootingTestHUD(루트 직속에서 이동)
├─ ScreenLayer
│   ├─ [Screens/ModulePalette]  ModulePalette(+커스텀)
│   ├─ [Screens/ColorPicker]
│   ├─ [Screens/WeaponCode]
│   ├─ [Screens/Pause]          Pause·저장 슬롯·사운드·감도
│   ├─ [Screens/Settlement]     DaySettlementUI
│   ├─ [Screens/TargetSelection]
│   ├─ [Screens/RaidDeath]
│   ├─ Dialogue.prefab          (기존 중첩 유지)
│   └─ NightRaidUI.prefab       (기존 중첩 유지)
├─ PopupLayer
│   ├─ [Popup/Feedback]         YesNoUI·NotificationText (StartUI_Manager와 공용)
│   └─ [Popup/TutorialSkip]
└─ TransitionLayer
    └─ [Transition/DayTransition]
```

- `[ ]`는 새 중첩 프리팹(`Assets/Prefabs/UI/<분류>/<이름>.prefab`). 각 프리팹 루트 오브젝트에 그 화면의 브리지+View를 둔다(P3 규칙: View는 브리지와 같은 오브젝트).
- 1차 목표는 **계층과 동작을 그대로 두고 소속만 나누는 것**이다. 모든 중첩 프리팹은 UIManager 루트 아래에 남으므로 `GetComponentInChildren` 탐색, 레이어 정렬, 씬 인스턴스 1개 구조가 유지된다.
- 맥락별로 프리팹 자체를 씬마다 다르게 배치하는 2차 분리(§5 P5)는 별도 결정 사항이다.

## 3. UIManager.cs가 넘겨줄 곳

| 역할 | 새 위치 | 호환 |
|---|---|---|
| R4 플레이어 찾기 | `GameManager.Player`(씬의 GameManager가 직렬화 참조 보유, 없으면 1회 탐색) | `UIManager.Player`/`PlayerWeaponHolder`는 `[Obsolete]` 위임으로 남기고 호출부(약 40곳)를 단계적으로 옮긴다 |
| R2 화면 보관소 | 각 브리지는 자기 화면 오브젝트로. 조회는 `UIManager.Screen<T>()`(루트 아래 `GetComponentInChildren<T>(true)` 결과 캐시) 하나로 통일 | `ui.GetComponent<T>()` 64곳을 `ui.Screen<T>()`로 교체. `GetComponent`를 `new`로 가리는 방식(③ 제안)은 쓰지 않는다 |
| R3 강제 초기화 | `IUIScreen.Initialize(UIContext)` 인터페이스. UIManager.Awake가 루트 아래 구현체를 모아 한 번 호출. UIContext = GameManager·Player·CubeBuilder·Interaction | 기존 개별 `Initialize()`는 내부에서 이 경로로 수렴 |
| R5 알림·확인창 | FeedbackUI가 본체. `UIManager.ShowNotification`·`Feedback`·`IsChoiceOpen`은 한 줄 위임만 남김 | 레거시 notificationText/Group 경로와 필드 제거 |
| R6 공방 방향 마크 | `Hud/WorkshopView`의 `WorkshopViewUI` 쪽 새 View/Presenter(`WorkshopDirectionMarksView`). WorkshopSession은 이 컴포넌트에 바인딩 | `SetWorkshopUI`·`GetWorkshopRotationRadius`는 호출부가 WorkshopSession 2곳뿐이라 바로 이전 |
| R7 일시정지 | `Screens/Pause` 루트의 `PauseUI` 브리지가 PauseView·PausePresenter 소유. GameManager는 `ui.Screen<PauseUI>().Refresh(this)` | `UIManager.Refresh`·`PauseView`는 위임으로 과도기 유지 |
| R9 잔재 | 삭제 | 호출 0건 확인됨 |

최종 UIManager.cs에 남는 것: 루트 스케일 복원, 화면 초기화 순회, `Screen<T>()`, 알림 위임, 과도기 `[Obsolete]` 위임.

## 4. 교차 결합: 분리 전에 끊어야 할 것

| 결합 | 처리 |
|---|---|
| HoveringUI → ModulePaletteUI 직접 참조(팔레트 열림 판정) | `CubeBuilder.PaletteOpen` 또는 UIInputPolicy 상태로 판정 |
| DaySettlementUI/SettlementView → HudLayer/CurrencyGroup 내부 10곳 | `Hud/Currency`에 CurrencyView 공개 API(롤링 시작·값 표시)를 두고 정산 Presenter가 그 API를 호출 |
| PresetPanelUI가 HudLayer와 PopupLayer에 걸침 | PresetChoicePanel을 `Hud/PresetPanel` 아래로 옮기고 정렬은 하위 Canvas(overrideSorting)로 유지 |
| QuestInfoPanel을 손님 요구·튜토리얼 목표가 공유 | `Hud/QuestInfo` 하나로 두고 두 브리지를 같은 프리팹에 배치(분리하지 않음) |
| RaidHealthUI → 루트 LowHealthVignette | 비네트는 렌더 순서 때문에 루트 첫 자식 유지, 참조는 프리팹 간이 아니라 UIContext 주입으로 |
| WeaponCodeUI → HudLayer/WeaponCodeButton | 버튼을 `Hud/WeaponInfo`에 두고 WeaponCode 화면은 열기 요청 이벤트만 받음 |
| Main 씬의 RetireUI(UIManager 인스턴스에 씬에서 추가한 컴포넌트) | `Screens/Retire.prefab`에 브리지를 넣고 씬 추가 컴포넌트 제거 |

## 5. 단계

각 단계는 하나의 커밋 묶음. `UIManager.prefab`·`Main.unity`는 한 YAML 파일이라 **이 두 파일을 건드리는 단계는 병렬로 돌리지 않는다**(워크트리 병합 충돌이 사실상 해결 불가). 코드만 바꾸는 작업만 워크트리 병렬로 돌린다.

| 단계 | 내용 | 파일 성격 | 병렬 |
|---|---|---|---|
| P0 정리 | 이미 끊긴 씬 오버라이드 636건(대상 소실 618 + target 0인 무효 18) 정리를 위해 `SceneOverrides/Clean`에 "프리팹에 대상이 없는 수정" 제거 모드 추가, R9 데드코드·중복 바인딩 제거, 빈 오브젝트 DimUI/HoverUI 삭제, ShootingTestHUD를 HudLayer로, 고아 코드(StickerOverlay·WorkshopStatsUI)·미사용 프리팹 3개 삭제, 레거시 에디터 도구 4개(RoundSetup·SetupWeaponCodeUI·SharedFeedbackMigration·WorkshopPlanarViewAuthoring) 제거 또는 레거시 표시 | 코드 + 프리팹 소량 | 코드 부분만 병렬 |
| P1 코드 경계 | 묶음 오브젝트에 붙은 씬 참조를 코드 주입으로 전환: `DaySettlementUI.nightRaid`(습격·Main 12씬), `dayManager`(Main), SaveSlotsUI `saveController`(Main). `GameManager.Player`, `UIManager.Screen<T>()`, `IUIScreen`/`UIContext`, 공방 방향 마크 이전, 교차 결합 API(§4) 추가. 호출부 교체 | 코드만 | 워크트리 3~4개 병렬 가능(플레이어 창구 / 화면 조회 64곳 / 공방 마크 / 정산·재화·호버 결합) |
| P2 브리지 이동 | 루트의 브리지+View 세트를 각 화면 오브젝트로 이동(이동 가능 12세트 먼저, Preset은 §4 처리 후). 저작 명령의 대상 경로 갱신 | UIManager.prefab | 순차 1개 |
| P3 중첩 프리팹 추출 | §2의 `[ ]` 묶음을 프리팹으로 추출하는 멱등 저작 명령(`Tools/UI/Authoring/Split/*`) + 경로 상수 `UIAuthoringPaths` + 기존 저작 명령이 각 하위 프리팹을 열도록 변경 | 프리팹 + 에디터 | 순차(묶음 1개씩) |
| P4 씬 오버라이드 이전 | 추출로 끊기는 씬 오버라이드를 옛→새 fileID 대응표로 재연결하거나 프리팹 값으로 흡수. 묶음별 손실 예상(⑤ §5-2): 작업대 HUD 231건(Main 45), 팔레트·커스텀 94건(Main 24), 일시정지 32건, 정산 22건, 튜토리얼 16건, 나머지 묶음 각 10건 이하. 빌드 씬 기준으로는 Main 98건·습격 씬 11~31건이 대상 | 씬 | 순차 |
| P5 맥락 분리(선택) | 공통 코어 + 공방 + 습격 실행 + 사격장을 씬별로 다르게 배치 | 프리팹 + 씬 + GameManager 참조 | 결정 후 |

## 6. 위험과 대응

- **씬 오버라이드 유실**: 하위 트리를 중첩 프리팹으로 바꾸면 그 안 오브젝트의 fileID가 바뀌어 씬 오버라이드가 자동 이관 없이 끊긴다. 이 저장소에 이미 그렇게 끊긴 오버라이드가 636건 남아 있다(NightRaidUI 중첩화 등 과거 재구성 흔적). P3 직전에 대상 목록을 뽑고, 추출 명령이 옛→새 fileID 대응표를 남겨 P4에서 일괄 재연결한다. 값 오버라이드는 가능하면 프리팹 저작값으로 흡수하고, 상태는 코드 초기화가 잡게 해서 씬 오버라이드 자체를 줄인다(NightRaidUI 선례).
- **추출 순서**(위험 낮은 순, ⑤ §5-3): 전투 HUD → 팝업·알림 → 전환 → 사격장 → 튜토리얼 → 일시정지 → 팔레트·커스텀 → 작업대 HUD. 정산은 P1의 `nightRaid` 코드 주입 이후에만.
- **초기화 순서**: GameManager(order -200)가 UIManager.Awake 전에 UICancelRouter를 만든다. `Screen<T>()`는 지연 조회+캐시로 순서와 무관하게 만든다.
- **저작 명령 멱등성**: 경로 문자열 17곳 이상, `Transform.Find` 경로 다수. P3에서 `UIAuthoringPaths` 하나로 모으고, 각 명령을 두 번 실행해 변경 0건을 확인한다.
- **해상도 값**: 추출은 값 이동만 해야 한다. `Resolution` 변환 도구는 재실행 금지.
- **테스트 씬**: Bank·JungleCampus·Test/* 9개는 빌드 밖이고 오버라이드가 낡았다(폰트 50건 등). P4에서 같이 고칠지, 끊어진 채로 둘지 결정이 필요하다.

## 7. 결정이 필요한 것

1. P5(씬 맥락별 프리팹 배치)까지 갈지, P4에서 멈출지.
2. 빌드 밖 테스트 씬(9개)의 UIManager 오버라이드를 같이 고칠지.
3. `UIManager.Player` 등 과도기 위임을 언제 제거할지(호출부 이관 완료 시 바로 / 다음 정리 주기).
4. P0의 레거시 에디터 도구 4개를 삭제할지, `레거시` 표시만 할지.
