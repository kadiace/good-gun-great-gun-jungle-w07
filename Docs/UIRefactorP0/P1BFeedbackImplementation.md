# P1-B 첫 UI 작업 단위: Feedback 확인창·알림 MVP 이관

## 1. 범위와 확인 수준

- 이 문서는 **P1-B 전체의 완료가 아니라 첫 구현 단위**다. 범위는 공통 `FeedbackUI`의 확인창·단일 확인·상단 알림을 `FeedbackPresenter` + 두 View로 분리하고, 실제 `UIManager.prefab`·`StartUI_Manager.prefab`에 authored 참조로 연결한 것이다.
- 기존 `FeedbackUI` 공개 API와 호출부는 소스 형식 호환을 유지한다. `DialogueUI`, 전체 레이어 Canvas, 대화 계층, 게임 규칙·경제·시간·저장·커서 로직은 건드리지 않았다.
- 확인 수준은 **소스·YAML 정적 대조와 수정 자산 개별 임포트**다. 임포트에 따른 Unity 자동 처리와 별도로 컴파일 성공·플레이·빌드 검증을 판정하지 않았으며, 검증 입력·캡처와 에디터에서의 실효 오버라이드 해석도 수행하지 않았다. 테스트·플레이 검증은 사용자 담당이다.
- 최종 수정한 C# 9개와 프리팹 2개만 `AssetDatabase.ImportAsset`으로 각각 한 번 임포트했다. 큐 소비와 같은 11경로의 완료 마커를 확인했다. 전체 Refresh는 호출하지 않았다.

## 2. Old → New 경로

| 이전 | 이후 |
| --- | --- |
| `Assets/Scripts/FeedbackUI.cs` 단일 MonoBehaviour (상태·입력·표시·연출·Canvas 생성) | 같은 경로의 **레거시 브리지** + 아래 신규 파일 |
| 확인창/단일 안내 표시·버튼·Canvas 정렬·블로커 | `Assets/Scripts/UI/Feedback/FeedbackChoiceView.cs` |
| 알림 표시·숨김·unscaled 연출 | `Assets/Scripts/UI/Feedback/FeedbackNotificationView.cs` |
| 열림 상태·요청자 콜백·제스처 중복 방지·Suspend·포커스 보관·종료 결과 | `Assets/Scripts/UI/Feedback/FeedbackPresenter.cs` (plain C#) |
| 종료 결과 enum | `Assets/Scripts/UI/Feedback/FeedbackCloseReason.cs` (`Confirmed`, `Cancelled`, `Forced`) |
| Yes 버튼 포인터/Submit 전달 | `Assets/Scripts/UI/Feedback/FeedbackConfirmButtonEventRelay.cs` |
| — | `Assets/Scripts/UI/Feedback.meta` 및 각 스크립트 `.meta` |

- `DialogueUI.cs`의 `internal ConfirmButtonEventRelay`는 수정하지 않았다. 이름 충돌과 런타임 `AddComponent` 폴백을 피하려고 Feedback 전용 relay를 별도 파일·타입으로 두고 두 프리팹 Yes 버튼에 authored 컴포넌트로 붙였다.
- 도메인 콜백(`onYes`/`onNo`)은 계속 요청자 소유이며 Presenter가 보관·1회 호출한다. 범용 Model, 무제한 스택, 전역 이벤트 버스는 추가하지 않았다.

## 3. 공개 API와 종료 결과

- `FeedbackUI.For`, `ShowNotification`, `ShowChoice`, `ShowAcknowledgement`, `SuspendChoiceInput`, `ResumeChoiceInput`, `CancelChoice`, `Cancel`, `IsChoiceOpen`, `IsChoiceInputSuspended`, `TutorialConfirmButton`의 반환 형식·인자 순서·기본값을 유지한다.
- public 파라미터 이름은 프로젝트 표기 규칙(`_` 접두)으로 바꿨다. 호출부 전체를 검색해 `caller:`, `requester:`, `message:`, `onYes:`, `onNo:`, `yesLabel:`, `noLabel:`, `canConfirm:`, `buttonLabel:` named argument 사용이 없음을 확인했다.
- `ShowChoice`/`ShowAcknowledgement`는 이미 열려 있으면 새 요청으로 소유자·콜백을 덮어쓰지 않고 `false`를 반환한다. 동일 요청이 다시 와도 기존 화면을 유지한다.
- `FeedbackPresenter.Closed`는 `Action<object, FeedbackCloseReason>`이며 `FeedbackUI.Closed`로 전달된다. 결과는 상태·화면·포커스·게임 입력 갱신을 모두 마친 뒤, 요청자 콜백 직전에 알린다.
- `ForceClose()`를 새로 추가했다. 열린 확인창을 Yes/No 콜백 없이 닫고 `Forced`를 알린다. `FeedbackUI.OnDisable`/`OnDestroy`, `UIManager.OnDisable`은 `ForceClose()`를 사용한다.
- `CancelChoice(requester)`는 이번 단계에서 기존과 동일하게 `Cancel()`과 같은 의미다. 요청자 소유 확인 후 취소 콜백을 1회 실행하므로 기존 호출부의 상태 정리 부수효과가 유지된다. 호출부별 forced-close 이관은 9절의 P2 목록이다.

## 4. 입력·제스처·Suspend 보존

- Workshop 맵을 복제하고 그 안의 기존 `Interact`, `AdvanceDialogue` 이벤트를 사용한다. 코드 전용 바인딩, 구 Input API 직접 폴링, 새 ESC 구독은 없다.
- `openedFrame`, `openedGestureId`, `consumedGestureId`, `inputHeldOnOpen`, 프레임 단위 제스처 병합, LateUpdate 예약 확인을 그대로 옮겼다.
- 포인터 down은 제스처를 발급만 하고, click은 현재 제스처를 소비하며, Submit은 새 제스처를 발급한다. 같은 제스처의 중복 확인은 차단된다.
- 단일 안내(`AdvanceDialogue`)만 범용 넘김을 받는다. 일반 Yes/No 선택창은 `AdvanceDialogue`를 무시한다.
- Game/Pause ESC 구독은 기존 `GameManager` 한 곳만 유지한다. `TogglePause`의 `IsChoiceOpen` → `Cancel()` 라우팅과 Suspend 중 ESC 소비를 바꾸지 않았다.
- Suspend 중에는 확인 요청을 비우고 Yes/No를 비활성화하며, 일시정지 중 뒤에 깔린 단일 안내는 포인터·Submit·예약 확인 모두 차단된다.
- `ResumeChoiceInput`은 오픈 시 저장한 원래 `canConfirm`으로 Yes를 복원하고 No를 다시 활성화한다. 기존처럼 무조건 Yes를 활성화하지 않는다.
- Suspend 시 Presenter가 팝업 내부의 유효 선택을 별도 슬롯에 보관하고, Resume에서 그 선택이 유효하면 복원하며 아니면 일반 No·단일 안내 Yes 기본 선택을 다시 적용한다. `UIRoot`의 Pause 전용 슬롯은 사용하지 않는다.
- Resume의 `inputHeldOnOpen`은 Interact에 더해 단일 안내일 때 AdvanceDialogue 유지도 검사해 Open과 같은 홀드 가드를 쓴다.

## 5. 포커스·내비게이션·정렬

- 팝업을 열기 직전의 유효한 `Selectable` 선택을 Presenter가 팝업 단위로 보관하고, 닫을 때 유효하면 복원하고 아니면 선택을 비운다. `UIRoot`의 Pause 전용 단일 포커스 슬롯은 사용하지 않는다.
- 복원은 항상 `Selectable`이 존재하고 활성·상호작용 가능할 때만 수행하며, `Selectable`이 없는 객체를 포커스로 되돌리지 않는다.
- 기본 포커스는 일반 Yes/No 선택창이 **No**, 단일 확인이 **Yes**다. 호버만으로 포커스를 빼앗는 코드는 없다.
- 실제 Yes/No 버튼은 `activeInHierarchy`·`isActiveAndEnabled`·`IsInteractable()`을 모두 만족하는 상대가 있을 때만 `Explicit`, `wrapAround: false`로 연결한다. 단일 안내(No 숨김), `canConfirm=false`(Yes 비활성), Suspend(둘 다 비활성)에서는 `Navigation.Mode.None`과 null 참조만 두어 비활성 버튼으로 이동하지 않는다.
- 패널을 활성화한 뒤 내비게이션을 구성하고, `SetButtonInteractivity`(Suspend/Resume) 후에도 다시 갱신한다.
- authored Canvas는 `overrideSorting: 1`, 기본 `sortingOrder: 150`이다. 표시 시 일반 선택창이 일시정지 중이면 300, 그 외 일반 선택창과 단일 확인은 150을 적용한다. 기존 Pause Canvas 200/Dialogue 150 예외는 유지한다.

## 6. 알림 보존

- `m_enterDuration .18`, `m_holdDuration 2`, `m_exitDuration .4`를 실제 시간(`Time.unscaledDeltaTime`)으로 재생한다.
- 표시 시 텍스트 설정, `elapsed 0`, alpha 0, scale `.92` → 1, 활성화, 마지막 sibling 이동을 유지한다. 다시 표시하면 처음부터 재생한다.
- 빈 메시지는 즉시 숨기고, 숨김 시 alpha 0, `interactable`/`blocksRaycasts` false, scale 1, 비활성화한다.
- 알림의 저작된 `raycastTarget` 값은 바꾸지 않았고 비차단 표시를 유지한다.

## 7. 프리팹 이관

### UIManager.prefab

- 기존 `FeedbackUI` 컴포넌트 `&6433941928138802473`(루트 GO `&5513238841504740859`)의 직렬화 필드를 `m_choiceView`/`m_notificationView`로 교체했다. 스크립트 GUID는 그대로다.
- 루트 GO에 `FeedbackChoiceView &7100000000000000001`, `FeedbackNotificationView &7100000000000000002`를 추가했다.
- Yes 버튼 GO `&2717105284374754061`에 relay `&7100000000000000003`를 추가했다.
- YesNoUI GO `&2038595064660975237`에 authored `Canvas &7100000000000000004`, `GraphicRaycaster &7100000000000000005`를 추가했다.
- `AcknowledgementClickBlocker` GO `&7100000000000000006`(RT `&7100000000000000007`, CanvasRenderer `&7100000000000000008`, 투명 Image `&7100000000000000009`)를 YesNoUI RT `&6220790012621788212`의 첫 자식으로 두고 비활성으로 저장했다.
- 기존 값 이동: notificationText `&6956398518808537298`, notificationGroup `&2047208334233308410`, durations .18/2/.4, choicePanel `&2038595064660975237`, choiceMessage `&1518906667039896017`, yesButton `&5834757374081279307`, noButton `&38680561561049791`, Yes/No 라벨 TMP `&6488343405630288727`/`&3244895079214406213`, inputActions `052faaac...`.

### StartUI_Manager.prefab

- 기존 `FeedbackUI` `&2938864146216797178`(루트 GO `&1205328163195860000`)의 필드를 View 참조로 교체하고, 같은 루트에 View `&7200000000000000001`/`&7200000000000000002`를 추가했다.
- Yes 버튼 GO `&7568042022480714053`에 relay `&7200000000000000003`, YesNoUI GO `&8540534897088664551`에 Canvas `&7200000000000000004`/Raycaster `&7200000000000000005`를 추가했다.
- blocker GO `&7200000000000000006`~Image `&7200000000000000009`를 YesNoUI RT `&6008179177265079076`의 첫 자식으로 두었다. YesNoUI는 기존처럼 비활성으로 시작한다.
- 기존 값 이동: notificationText `&1279112241925455809`, notificationGroup `&8527249949590244252`, choicePanel `&8540534897088664551`, choiceMessage `&3779254009195012460`, yesButton `&1766085301755360795`, noButton `&2712244622550458760`, 라벨 TMP `&6945316609726875314`/`&3234143893152528144`, inputActions 동일 asset.

### 공통

- 기존 YAML block ID를 모두 보존했다. UIManager는 3175 → 3184(신규 9), StartUI는 161 → 170(신규 9)이며 중복 ID는 없다. 새 블록의 로컬 참조·`m_Component`·`m_Children`·`m_Father`가 모두 실제 블록과 일치한다.
- 신규 스크립트 GUID와 `.meta` GUID, 프리팹 `m_Script` GUID가 일치한다.
- 이전 FeedbackUI 필드에 대한 씬/프리팹 인스턴스 `propertyPath` 오버라이드는 검색되지 않아 `FormerlySerializedAs` 호환 필드는 두지 않았다. 씬 실효 해석은 에디터에서 확인하지 않았다.
- **persistentOnClick:** Yes/No 버튼의 기존 persistent call은 비어 있었고 이번 작업에서도 추가·삭제하지 않았다(두 프리팹의 `m_OnClick` 내용 불변). No 버튼은 View의 Initialize/Dispose에서 이름 있는 리스너를 한 번 등록·해제하고, Yes relay는 `Bind`로 콜백을 교체한다. 다른 버튼의 persistent call은 건드리지 않았다.

## 8. 실패 처리·초기 상태

- 필수 참조 누락 시 화면명·필드명·null 여부를 포함한 오류를 1회 기록하고 View 초기화를 실패시킨다. 확인창은 `false`를 반환하고, 알림은 참조가 유효한 범위에서만 동작한다.
- `FeedbackChoiceView.Initialize`는 필수 참조, `Workshop` 맵, 필수 `Interact`/`AdvanceDialogue` 액션을 모두 검증한 뒤에만 No 버튼·relay와 액션을 구독한다. 실패 시 구독을 남기지 않고, 누락 액션은 로그 후 초기화 실패로 처리한다.
- `Awake`와 `Initialize` 중 먼저 실행된 쪽만 확인창·알림의 초기 숨김을 한 번 적용한다. 이후 늦게 실행된 `Awake`가 이미 표시한 UI를 다시 끄지 않는다.
- 두 프리팹 모두 시작 시 확인창과 알림을 코드로 명시해 숨긴다. blocker는 비활성, Canvas는 authored 상태로 시작한다. 런타임 `new GameObject`/`AddComponent`/Canvas 생성은 Feedback 경로에 없다.

## 9. 레거시 CancelChoice 호출부와 P2 강제 종료 이관 목록

이번 단계에서는 `CancelChoice`를 기존 `Cancel` 의미로 유지한다. 아래 호출부는 P2에서 소유자별 `ForceClose`/종료 결과 구독으로 옮길 대상이다.

| 영역 | 호출부 |
| --- | --- |
| P2-A Pause/저장 | `Assets/Scripts/StartMenu_UI/PauseMenuNavigation.cs:18`, `Assets/Scripts/StartMenu_UI/SaveSlotsUI.cs:67` |
| P2-B 정산/판매/다음 날 | `Assets/Scripts/DaySettlementUI.cs:191`, `Assets/Scripts/SaleConfirmationUI.cs:34`, `:36`, `Assets/Scripts/NextDayInteractable.cs:57` |
| P2-C 모듈 팔레트 | `Assets/Scripts/ModulePaletteUI.cs:237`, `:280` |
| P2-E 야간 습격 | `Assets/Scripts/NightRaidUI.cs:99`, `:148`, `:156` |

`GameManager.TogglePause`의 `Cancel()`는 사용자 취소 라우팅이므로 강제 종료 이관 대상이 아니다.

기존 호출부는 위치 인자·동기 `bool` 계약을 사용한다. `SaveSlotsUI.DeleteSlot`/`DeleteAutoSave`에는 확인창 표시 실패가 무확인 삭제로 이어지는 분기가 있어 이번 연결 범위에서 차단했다. `confirmDelete=true`면 창을 열지 못했을 때 삭제하지 않고, 실제 삭제는 확인 콜백에서만 수행한다. `confirmDelete=false` 설정의 직접 삭제 동작은 유지한다. `SelectSlot`의 덮어쓰기 분기는 원래부터 확인창 요청 후 항상 반환하므로 표시 실패 시 저장하지 않으며 이번에 변경하지 않았다.

## 10. 구체적 한계

- 정적 확인과 자산 개별 임포트까지 수행했다. 버튼·포커스·연출 실동작과 씬의 실효 연결은 플레이로 확인하지 않았다.
- `Assets/Editor/SharedFeedbackMigration.cs`의 기존 이관 메뉴에는 원본·필수 필드·새 View 스키마 검사를 추가했다. 현재 스키마에서는 프리팹 편집·생성·저장 전에 안내를 남기고 중단한다. 레거시 이관 기능을 새 MVP 작성 도구로 포팅한 것은 아니며 메뉴 자체는 실행하지 않았다.
- `UIManager.cs`에는 Feedback과 무관한 자체 notification 필드/메서드 조각이 남아 있다. 이번 소유 범위를 지키기 위해 손대지 않았고, `ShowNotification`은 기존처럼 `feedback`이 있으면 Feedback 경로를 우선한다.
- `FeedbackUI.Closed`는 새 public 이벤트다. 기존 호출부는 구독하지 않으므로 동작 변화는 없지만, 향후 소유자별 후처리에서 사용할 수 있다.
- `OnDisable`/`OnDestroy`/`UIManager.OnDisable`은 이제 사용자 취소 콜백 대신 `ForceClose`를 사용한다. 이는 이번 작업 요구에 따른 의도적 변경이며 `Forced`에서는 Yes/No 콜백이 실행되지 않으므로, 기존 취소 콜백에서 도메인 상태를 정리하던 호출부는 그 정리를 받지 못한다. `Closed`에서 `Forced`를 구독해 소유자가 직접 정리하도록 9절 P2 목록에서 이관해야 한다.

## 11. 수정 파일·임포트·커밋 경계

수정:

1. `Assets/Scripts/FeedbackUI.cs`
2. `Assets/Scripts/UI/Feedback.meta`
3. `Assets/Scripts/UI/Feedback/FeedbackCloseReason.cs` (+ `.meta`)
4. `Assets/Scripts/UI/Feedback/FeedbackConfirmButtonEventRelay.cs` (+ `.meta`)
5. `Assets/Scripts/UI/Feedback/FeedbackChoiceView.cs` (+ `.meta`)
6. `Assets/Scripts/UI/Feedback/FeedbackNotificationView.cs` (+ `.meta`)
7. `Assets/Scripts/UI/Feedback/FeedbackPresenter.cs` (+ `.meta`)
8. `Assets/Scripts/UIManager.cs` (OnDisable의 Feedback 호출 한 곳만)
9. `Assets/Prefabs/Managers/UIManager.prefab`
10. `Assets/Prefabs/UI/StartUI_Manager.prefab`
11. `Docs/UIRefactorP0/P1BFeedbackImplementation.md`
12. `.orca/ui-p1b-feedback-report.md`

인접 연결 보완: `Assets/Editor/SharedFeedbackMigration.cs`(구 스키마 도구 보호), `Assets/Scripts/StartMenu_UI/SaveSlotsUI.cs`(삭제 확인창 실패 시 중단).

폴더 메타와 스크립트 메타를 제외한 실제 자산 11개는 개별 임포트를 마쳤다. 앞선 P1-A 첫 단위 22파일의 스테이징 내용은 유지했으며, 이번 단위 변경은 별도 미스테이징 상태로 남겼다. 실제 커밋은 생성하지 않았다.
