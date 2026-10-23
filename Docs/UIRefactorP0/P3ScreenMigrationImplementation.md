# P3 화면 이관·해상도 통일

목표는 남은 화면을 읽기 쉬운 MVP로 나누고, 런타임에 만들던 UI를 프리팹에 저작해 연결하는 것이다. 각 기존 MonoBehaviour는 public API·직렬화 필드·씬 오버라이드를 보존하는 브리지로 남고, View는 저작된 참조 표시·입력 이벤트만, Presenter는 기존 도메인 조회·요청만 담당한다. 실제 플레이 확인은 하지 않았다.

## 이관한 화면

| 영역 | 기능 ID | 새 코드 | 저작 명령 |
| --- | --- | --- | --- |
| 모듈 목록·즐겨찾기 10칸 | EDIT-009~015, 032 | `UI/WeaponEditor/ModulePalette/*`, `ModuleSlotUI`, `ModuleSelectionBorder` | `ModulePalette/Author` |
| 커스텀(도색·스티커·키링·탄색) | CUSTOM-001~019 | `UI/Customization/*`, `ModulePaletteUI.Customization.cs`(partial), `ColorPickerUI` | `Customization/Author` |
| 저장 슬롯(수동3+자동) | MENU-004, 008, 010~012, 014 | `UI/Save/*` | `SaveSlots/Author`(UIManager·StartUI 두 프리팹) |
| 영업 정산·숫자 롤링 | BIZ-001, 003, 012~015, COMMON-008 | `UI/Settlement/*`, `MoneyRollDisplay` | `Settlement/Author` |
| 습격 지도·HUD·진입/귀환 가림막·저체력 비네트 | RAID-001~006, 009, 013, 015, 017, 019 | `UI/Raid/*` | `Raid/Map`·`Hud`·`Vignette`·`Transition` |
| 호버·손님 요구·스캔 로딩 | BIZ-007, 021, COMMON-006/007 | `UI/Hover/*`, `ScanLoadingGraphic` | `HoverOrder/Author` |
| 사격장 선택·시험 HUD | BIZ-018~020 | `UI/ShootingRange/*` | `ShootingRange/Author` |
| 조준·차지·피격 방향 | BIZ-023, COMMON-003/004 | `UI/Combat/*` | `CombatHud/Author` |
| 튜토리얼 퀘스트·목표·강조·스킵 | TUT-007, 012, 014 | `UI/TutorialHud/*` | `TutorialHud/Author` |
| 프리셋·공유 코드·공방 태그 | EDIT-026~029 | `UI/PresetShare/*` | `PresetShare/Author` |
| 매뉴얼·감도·구조 경고 | BIZ-024, TUT-013, MENU-017, EDIT-024 | `UI/ManualSettings/*` | `ManualSettings/Author` |
| 엔딩·은퇴·사망 결과 | RAID-011, 014, 016 | `UI/Ending/*` | `SceneScreens/Extract` → `Ending/Author` |

저작 명령은 모두 `Tools/UI/Authoring/` 아래에 있고 멱등이다. 전부 다시 실행해도 프리팹·씬이 바뀌지 않는 상태를 유지한다. `Assets/Editor/AgentAuthoringQueue.cs`가 `Library/AgentPendingAuthoring.txt`에 적힌 메뉴만 컴파일·임포트 뒤 순서대로 실행한다.

## 보존·주의

- 슬롯 드래그 등록·교환·바깥 드롭·D01/D02, 커스텀 진입/제거 확인, 저장 덮어쓰기·삭제 확인 실효값(UIManager 확인, StartUI 즉시), D05 스킵, D07 결과 확정과 귀환 지급 경로는 기존 도메인을 그대로 쓴다.
- 직렬화되는 컴포넌트 클래스는 클래스명과 같은 파일에 둔다. 다른 파일에 두면 프리팹에서 missing script가 되어 이후 프리팹 저장 전체가 실패한다.
- View는 브리지와 같은 GameObject에 붙인다. 켜고 끄는 패널에 붙이면 브리지가 찾지 못하거나 수명이 달라진다.
- 저체력 비네트는 기존처럼 UIManager 루트 Canvas의 첫 자식이며 텍스처는 직렬화 값으로 런타임 계산한다.
- 습격 지도 지역 버튼은 표의 씬 이름으로 컨트롤러 인덱스를 찾는다. 컨트롤러 API는 바꾸지 않았다.
- 씬에 직접 붙어 있던 은퇴·엔드 상자 화면은 `Assets/Prefabs/UI/Screens/Retire.prefab`, `EndChestEnding.prefab`으로 추출했다. `RetireUI` 브리지는 Main 씬에 남고 프리팹 인스턴스의 View를 참조한다. 엔드 상자의 `chest` 씬 참조는 인스턴스 오버라이드다. TutorialSkipUI·TargetSelectionUI는 UIManager 인스턴스 일부라 추출 대상이 아니었다.
- 레거시 로더(스티커·키링 `Resources.LoadAll`+이름 정렬, `PaintMaterialLibrary.CreateDefaults`)는 `레거시 테이블:` 주석만 달고 남겼다. 도색 저장 키가 재질 이름이므로 테이블 승격 시 구 이름→ID 매핑을 먼저 만든다.

## 해상도 통일

- 루트 CanvasScaler는 1920×1080, ScaleWithScreenSize, match 1(높이 기준)이다. 21:9에서는 UI 크기를 16:9와 같게 두고 좌우 여백만 넓힌다.
- 1920×1080 16:9에서 기존과 같은 물리 크기를 유지하도록 저작값을 환산했다(UIManager 0.75배, StartUI 1.2배). 대상은 RectTransform 크기·위치, 글자 크기·여백, LayoutGroup·LayoutElement, Outline, 화면 컴포넌트의 픽셀 필드, 코드 픽셀 상수, 저작 명령 상수다. 루트 `referencePixelsPerUnit`은 계수를 곱해(UIManager 75, StartUI 120) 9슬라이스 테두리 두께를 유지했다.
- 빌드 씬 9개의 UIManager/StartUI 인스턴스 오버라이드와 씬에서 직접 추가한 오브젝트도 같은 계수로 환산했다. `NightRaid_JungleCampus_improve`의 개별 HUD 크기도 환산해 보존했다.
- 중첩 Canvas의 CanvasScaler(정산 등)는 루트가 아니어서 실제 배율에 쓰이지 않는다. 정산도 루트 계수로 환산했다.
- `DamageDirectionIndicator`·`RotationCircleGraphic`·핫바 간격처럼 "픽셀 ÷ scale"로 계산하는 값은 물리 픽셀이라 환산하지 않았다.
- UIManager 루트 localScale {0,0,0}은 코드가 실행 시 복구하므로 그대로다. 변환 도구 `Tools/UI/Authoring/Resolution/*`는 이미 변환된 상태를 감지해 건너뛴다. 다시 실행하지 않는다.
- 추출된 `Screens/EndChestEnding.prefab`의 자체 스케일러는 아직 1920×1080 match 0이다.

## 남은 범위

- 중첩 Canvas·레이어 배치·중앙 최대 너비, 전 화면 기본 선택·Navigation·포커스 복원, 커서·시간·액션맵 단일화, 월드 상호작용 브리지(G8), 씬 오버라이드 정리.
- 플레이에서 확인할 것: 1920×1080·21:9·4K에서 각 화면 크기·배치, 슬롯 드래그·커스텀·저장/삭제·정산·습격 입장/귀환·엔딩.
- 빌드 미등록 테스트 씬(KYGTest 등)에는 삭제된 필드 오버라이드가 남아 있을 수 있다. 자동 정리하지 않았다.
