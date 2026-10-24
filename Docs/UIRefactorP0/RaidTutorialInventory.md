# 습격·튜토리얼·엔딩·공용 표시 기능 보존 목록

## 기준과 확인 수준

HEAD `62f7a9a`, 현재 브랜치 `feature/bs_patch`. 워커 초안은 관리자 대조에서 허구 경로/API와 D번호 오인용이 발견되어 구현 명세로 승인하지 않았다. 아래는 현재 소스에서 다시 통합한 기능 가족 목록이다.

- 코드 근거는 `Assets/Scripts/` 아래 현재 파일이다. 실제 플레이·입력 순서·모든 씬 실효 연결은 미확인이다.
- NightRaidController의 미커밋 변경은 셀 로그/공유 프레임 예산 부분이며 보존한다. UI 개편 코드 적용은 아직 아니다.
- 연결 색인은 [SourceIndex](SourceIndex.md), 공통 계약은 [Contracts](Contracts.md), 명시 변경은 [D01~D09](../UIRefactorDifferences.md)를 따른다.
- D07은 탈출 결과 고정 정책이다. 습격 중 새로운 ESC 귀환/재도전 기능이나 일시정지 변경을 승인한 것이 아니다.

## 습격·결과: P2-E

| ID | 현재 기능·보존 대상 | 취소·실패·합의 변경 | 코드 근거 |
| --- | --- | --- | --- |
| RAID-001 | 밤의 습격 지도 열기와 정산 표시 전환 | busy·파산·낮 조건은 거부. 지도를 연 출발 맥락 보관 | NightRaidController.cs:596 |
| RAID-002 | 지역 핀·호버·선택·해금·입장 가능 표시 | 표·지갑·장착·튜토리얼 허용 상태에 맞춰 비활성화·이유 안내. 배열 위치를 새 영속 ID로 쓰지 않음 | NightRaidUI.cs; RaidRegionHoverUI.cs; RaidAreaMapGraphic.cs |
| RAID-003 | 지도 닫기와 이전 정산/준비 상태 복귀 | CloseMap의 튜토리얼 제한 유지. 목표 ESC는 지도 닫기이며 현재 별도 ESC 경로와 구분 | NightRaidController.cs:604; GameManager.cs:83 |
| RAID-004 | 확인 후 달러 입장 비용 차감·출발 스냅샷·씬 이동 | 무기·씬·표·잔액 재검사. 캡처/훈련 준비 실패는 차감 환불. 실제 진입 확정 뒤에는 같은 입장료를 다시 환불하지 않음 | NightRaidController.cs:612,644 |
| RAID-005 | 체력·목표·임시 달러/부품/티켓/모듈 보상 HUD | 실제 지갑에 미지급인 예정 보상과 보유량을 구분. 무기정보 패널 재사용과 공방 복귀 표시 복원 유지 | RaidHealthUI.cs; RaidObjectiveUI.cs; NightRaidController.cs:284; WorkshopTagsUI.cs |
| RAID-006 | 탈출 구역 진입·5초 진행·취소·완료 표시 | 영역 이탈·영역 비활성·사망은 취소, 조작 차단 중 진행 정지. 훈련 귀환 가능 조건 검사 | NightRaidController.cs:149,162,168 |
| RAID-007 | 생존 결과·예정 보상 표시와 확인 후 귀환 | [D07 반영](P1BResultImplementation.md): 결과 확정 뒤 교체·피해·집계 차단, 이미 사망이면 사망 우선. 지급은 기존 확인 후 복귀 절차에서 한 번. 플레이 미확인 | NightRaidController.cs의 ShowRaidResult/ReturnHome; NightRaidUI.Result.cs |
| RAID-008 | 사망 결과·획득 보상 제외·출발 상태 복귀 | 결과·로그·복귀 데이터의 성공 여부를 일치시킴. 사망 판정은 레거시 패널 활성 여부에 의존하지 않음 | NightRaidController.cs:146,168,735; NightRaidTransfer.cs |
| RAID-009 | 복귀 대상 씬·스냅샷 복원 실패 안내와 단독 씬 경로 | 현재 ReturnHome의 실패 분기를 보존하고 계약의 실패 복구 정책에 연결. 새 재도전 버튼이 있다고 추정하지 않음 | NightRaidController.cs:679; NightRaidReturnOverlay.cs:25 |
| RAID-010 | 훈련 습격의 단계 이어하기·귀환 및 연습 상태 복원 | 일반 보상 지급과 구분. 훈련 사망은 훈련 단계의 재개 경로 유지 | TutorialManager.Flow.cs:253,272,283; NightRaidTransfer.cs |
| RAID-011 | 엔드 상자 조건·연출·계속/Intro 선택 | 기존 카메라·전투·알파·버튼 활성 타이밍 유지. EndChest가 구동하는 SetWhite/SetEnd를 별도 게임 상태로 복제하지 않음 | EndChest.cs; EndChestEndingUI.cs:24,44,66 |
| RAID-012 | 구조 경고 공유 표시 | 조립 구조 경고의 실제 담당은 P2-C/EDIT-024. 습격용 새 경고 기능으로 복제하지 않음 | StructuralFailureWarning.cs |
| RAID-013 | 일반 플레이 일시정지와 지도·결과·전환의 ESC 경계 | 기존 GameManager 상태와 화면별 합의 정책 적용. D07을 ESC 귀환·결과창 ESC 확인의 근거로 쓰지 않음 | GameManager.cs:83; NightRaidController.cs:596 |
| RAID-014 | RaidDeathUI의 컨트롤러 전달·레거시 표시 폴백 | 컨트롤러가 있으면 ShowDeathResult로 전달. health 구독·해제와 숨김 초기화 유지 | RaidDeathUI.cs:14,30,42 |
| RAID-015 | 티켓·달러·부품·모듈 획득과 임시 보상 갱신 | resultOpen 등 신규 수집 거부 조건·훈련 예외 유지. 존재하지 않는 RaidCrate/RaidDropItem 클래스를 새로 만들지 않음 | NightRaidController.cs:284,295,302,310 |
| RAID-016 | 은퇴 선저장·결제·엔딩과 계속/Intro 선택 | P2-F 소유. SaveAuto 실패는 결제·엔딩 진입 거부. 은퇴 전 자동저장·예약 취소·같은 날 후속 저장 억제 유지 | RetireButton.cs:21; RetireUI.cs:10,36; StartMenu_UI/GameSaveController.cs:28 |
| RAID-017 | 지도/지역/보상 표 조회 | RaidMapData JSON→TableManager의 기존 단일 출처 재사용. View에 가격·보상 복사 금지 | TableManager.cs; RaidMapData.cs; RaidStageInfo.cs |
| RAID-018 | 공방 무기·상태·재화·해금 왕복과 Main 복원 | 기존 saveId·장착·복구 상태 유지. 성공 보상은 rewardsCommitted 가드로 한 번 반영. UI 실패로 재지급하지 않음 | NightRaidTransfer.cs; NightRaidController.cs:679,720; StartMenu_UI/GameSaveController.Travel.cs |
| RAID-019 | 결과·임시 보상의 모듈 아이콘/이름 표시 | 기존 moduleId/codeId 연결과 전용 아이콘·색상 유지. 신규 아이콘 재디자인·임의 번호 재배정 금지 | WorkshopTagsUI.cs; NightRaidUI.cs; NightRaidUI.Result.cs |

## 튜토리얼·대화·엔딩: P2-F

| ID | 현재 기능·보존 대상 | 취소·실패·합의 변경 | 코드 근거 |
| --- | --- | --- | --- |
| TUT-001 | 대화 Show/Hide·내용/버튼 표시·다음 대사 요청 | 현재 즉시 표시 경로를 보존. 타자기·문장 스킵 기능이 있다고 추정하지 않음 | DialogueUI.cs:104,147,180 |
| TUT-002 | 포인터 down/up/click·Submit·Space·Z·F 등의 제스처 중복 방지 | 열린 프레임·누르고 있던 입력·소비한 제스처를 검사. 같은 입력이 다음 대화/선택까지 진행하지 않게 유지 | DialogueUI.cs:73,89,133,189; FeedbackUI.cs:122,164,230 |
| TUT-003 | 튜토리얼 공용 알림 | 표시 소유자는 P1-B. ShowNotification의 문구·실제 시간 연출·숨김 유지 | FeedbackUI.cs:302 |
| TUT-004 | 단일 안내 확인·취소 | D04 전체 화면 클릭 확인과 ESC 취소 유지. 일반 Yes/No에 범용 클릭 넘김 적용하지 않음 | FeedbackUI.cs:178,187,221,388,429 |
| TUT-005 | Yes/No 선택·소유자별 취소·Suspend | 중복/다른 요청 거부 결과 처리. OnDisable의 현재 Cancel→onNo 부작용은 강제 종료 구분으로 이관 | FeedbackUI.cs:105,178,293,429 |
| TUT-006 | 대화 위 일시정지·이탈 확인, 대출 안내의 선택 Suspend | D03 복귀 시 기존 대화/포커스 복원. Suspend 중 ESC 소비와 재개 프레임의 중복 입력 방지 유지 | GameManager.cs:83; TutorialManager.cs:322; DialogueUI.cs:159; FeedbackUI.cs:293 |
| TUT-007 | 작성된 튜토리얼 시퀀스·단계 표시·완료 신호·목표/안내 UI | 실제 단계 수·필드·표시 참조를 담당자가 원문에서 대조. 이 문서에서 임의 단계 수/새 상태 머신 확정 금지 | TutorialManager.cs; TutorialManager.Flow.cs; TutorialSequence.cs; Assets/Data/Tutorials/Day0_Tutorial.asset |
| TUT-008 | 허용 월드 상호작용 대상과 실제 타깃 변화 | 컴포넌트 동일성과 Transform 제한, 상자 파괴 후 모듈 픽업 타깃 전환을 유지 | TutorialManager.cs; TutorialManager.Flow.cs; Interaction.cs; ModuleUnlockPickup.cs |
| TUT-009 | 액션·퀵슬롯·배치/삭제·방향·셀·단계 완료 제한 | ActionAllowed/TryAction과 도메인의 TryAuthorize 계열 검사 유지. 연속 삭제에서 직접 대상마다 검사 | TutorialManager.Flow.cs:242; CubeBuilder.PlanarStroke.cs:143; ModulePaletteUI.cs:465,474 |
| TUT-010 | 스캔 후 Q 해제 대기·모듈 F 획득·티켓/부품 단계 | D08은 이번 습격 누적 획득>0 인정. 안내 후 새 획득을 강제하지 않음. 스캔 완료만으로 보호 해제/진행하지 않고 기존 해제 조건 유지 | TutorialManager.Flow.cs:183,224,288 |
| TUT-011 | 단계별 준비 상태·훈련 진입/복귀와 공방 진행 재개 | 단계 소유자·대상·무기·잔액·저장 예외를 각 영역과 대응. 표시만 옮기고 도메인 제약을 빠뜨리지 않음 | TutorialManager.cs; TutorialManager.Flow.cs:253,272,283 |
| TUT-012 | 목표·타깃·실제 안내 버튼의 강조 표시 | 비상호작용 커버와 클릭 대상 분리. 표시 프리팹과 가변 커버 재사용, 다른 선택창의 버튼을 임의 강조하지 않음 | TutorialQuestUI.cs; TutorialTargetUI.cs; TutorialButtonHighlightUI.cs:20 |
| TUT-013 | 매뉴얼 안내·실제 열람/닫기 동작 연결 | 줌·팬·닫기와 튜토리얼 완료 조건 유지. 페이지 넘김/끝까지 읽기 신호를 추정 추가하지 않음 | WorkshopManual.cs; PlayerManualReader.cs; TutorialManager.Flow.cs |
| TUT-014 | 처음 튜토리얼 선택·스킵과 날짜 페이드 | 선택 중 중복 입력 차단. 전환 시작 실패는 버튼 복구. Covered/콜백/입력 점유 해제와 각 선택 경로 유지 | TutorialSkipUI.cs:24,36; DayTransitionUI.cs:86 |
| TUT-015 | 비활성·미참조 튜토리얼 데이터와 레거시 경로 분류 | 전체 참조를 확인하기 전 활성/미사용을 단정하지 않음. 개편을 이유로 자동 재활성화·삭제하지 않음 | TutorialSequence.cs; Assets/Data/Tutorials 및 참조 소스 |

## 공용 표시의 소유권 연결

아래 항목은 다른 목록과 겹치는 연결점이다. 같은 기능을 두 작업자가 따로 구현하지 않는다.

| ID | 항목 | 담당·보존/주의 | 근거 |
| --- | --- | --- | --- |
| COMMON-001 | UI 초기화·표시·게임 조작 상태 연결 | P1-A. 현재 UIManager에 별도 UIState/SetState API가 있다고 가정하지 않음 | UIManager.cs; GameManager.cs |
| COMMON-002 | ESC 우선 경로·포커스·커서·시간 | P1-A, MENU-006/007. 입력 에셋의 한 제스처가 두 경로로 소비되지 않게 이관 | GameManager.cs:83 |
| COMMON-003 | 조준·탄퍼짐·유도·스코프 | P2-E, BIZ-023. 기존 무기 상태에서 표시, 새로운 도메인 상태 생성 금지 | AimUI.cs; SpreadCrosshair.cs; HomingLockGraphic.cs; HomingRetentionRing.cs; ScopeOverlayGraphic.cs |
| COMMON-004 | 차지 게이지 | P2-E, BIZ-023. 무기 교체·비활성·충전 종료에 따른 숨김 유지 | ChargeGaugeUI.cs |
| COMMON-005 | GoodShot 표시 후보 | 기존 부착·호출 경로의 실제 활성 조건을 대조. 자동 연결/재활성화하지 않음 | GoodShotUI.cs |
| COMMON-006 | 호버 정보·상호작용 안내 | P2-B, BIZ-021. Interaction은 공통 시선/입력 전달만 담당 | HoveringUI.cs; Interaction.cs |
| COMMON-007 | Q 홀드 스캔과 로딩 그래픽 | P2-B/F, BIZ-022. Tab 스캔이나 새 토글로 바꾸지 않음 | PlayerScanMode.cs; ScanLoadingGraphic.cs |
| COMMON-008 | 재화 숫자 롤링 | P2-B, BIZ-001~003. 실제 금액 변경과 연출을 분리 | MoneyRollDisplay.cs; DaySettlementUI.cs |
| COMMON-009 | 판매 확인 UI 후보 | P2-B. 연결되지 않은 기능을 새로 활성화하지 않음 | SaleConfirmationUI.cs; Customer.cs |
| COMMON-010 | 폰트·스크롤·태그 표시 보조 | P1-B. 기존 외형과 재표시 상태 정책을 함께 대조 | UIFonts.cs; ResetScrollOnEnable.cs; TagFlowLayout.cs; TagPillGraphic.cs |
| COMMON-011 | EventSystem·UI 액션·반복값 | P1-A/P3. 공용 프리팹은 실제 입력 에셋 GUID 참조가 있음. 반복0.5/0.1 유지, 씬 오버라이드 별도 대조 | Assets/Prefabs/Managers/UIManager.prefab:76063; Assets/InputSystem_Actions.inputactions.meta |
| COMMON-012 | 버튼 호버 외곽선 | P1-B. 호버가 선택 포커스를 빼앗지 않게 하고 기존 외형 유지 | ButtonHoverOutline.cs |

## 이관 전 남은 대조

- 습격 성공/사망 확정·피해 차단·예정 보상 고정은 [결과 화면·D07 단위](P1BResultImplementation.md)에 반영했다. P2-E는 나머지 습격 화면·기능 연결을 이관하며 D07 이외 전투/경제 개선을 섞지 않는다.
- 각 튜토리얼 단계의 데이터 필드→새 표시/도메인 신호와 훈련 왕복 복원은 P2-F가 기능 ID에 연결한다. 시퀀스 표시만으로 전수 보존 완료가 아니다.
- Result/Retire/EndChest의 기존 연출 시간·카메라·적 행동을 실제 소스에 맞춰 별도 정책으로 이관한다. UI가 열렸다는 이유로 일괄 timeScale=0을 적용하지 않는다.
- NightRaidReturnOverlay의 현재 런타임 Canvas 생성은 작성된 전환 프리팹으로 옮긴다. 지연·복구 실패·중복 진입·씬 종료 정리를 함께 연결한다.
- 실제 씬의 입력 참조·Navigation·레이어·오버라이드는 P3에서 확인한다. 이 문서는 플레이·시각 확인 완료가 아니다.
