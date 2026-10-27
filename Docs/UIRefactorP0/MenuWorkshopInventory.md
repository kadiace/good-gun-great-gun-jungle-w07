# 메뉴·공방 영업 기능 보존 목록

## 기준과 확인 수준

HEAD `62f7a9a`, 현재 브랜치 `feature/bs_patch`. 워커 초안·교정본의 허구 경로/API와 현재 소스에 없는 동작 설명을 제외하고 관리자가 통합했다. 아래는 기능 가족 단위의 이관 목록이다.

- 표의 함수·상태는 현재 파일에서 읽은 근거다. 모든 직렬화 필드·씬 오버라이드·실제 입력 순서를 확인했다는 뜻은 아니다.
- 직접 컴포넌트 참조는 [SourceIndex](SourceIndex.md), 정책은 [Contracts](Contracts.md), 변경은 [D01~D09](../UIRefactorDifferences.md)를 따른다.
- 새 MVP 구현·씬 이관은 미적용이며 플레이는 전 항목 미확인이다. 남은 상세 대조는 각 담당 작업에서 기능 ID에 연결한다.
- 근거 파일은 별도 표기가 없으면 `Assets/Scripts/` 아래다. 저장 UI 파일은 `StartMenu_UI/` 아래다.
- 현재 Start_UI에는 새 게임·이어하기·불러오기·종료 경로가 있다. 설정 딤 배경·타자기 대사·새 종료 확인창 등 소스에서 확인되지 않은 기능은 추가하지 않는다.
- MENU-001~005의 시작 메뉴 요청/표시, MENU-009의 나가기 확인 정책, MENU-015~016의 사운드 표시/요청은 [P2 병렬 이관](P2ParallelWaveImplementation.md)에 반영했다. 기존 API·persistent 호출·프리팹 실효 참조를 보존한다. MENU-010~014 저장 카드·복원과 MENU-017 감도 화면의 전체 MVP 이관은 별도이며 플레이 보존 확인은 남아 있다.

## 메뉴·저장·설정과 공용 표시

| ID | 현재 기능·정상 결과 | 취소·실패·이관 주의 | 근거 / 담당 |
| --- | --- | --- | --- |
| MENU-001 | Intro 초기 표시·저장 가능 여부 갱신, 배경 코어 연출 | 이어하기 버튼은 저장 가능 여부를 따른다. 연출은 기존 외형 유지 | StartMenu_UI/Start_UI.cs:11; StartMenu_UI/StartMenuCoreDisplay.cs / P2-A |
| MENU-002 | 새 게임의 PendingLoad·복구 상태 초기화 후 게임 씬 진입 | loading 가드와 빌드 씬 존재 검사. 저장 형식을 새로 만들지 않음 | StartMenu_UI/Start_UI.cs:64,73 / P2-A |
| MENU-003 | 최신 유효 슬롯으로 이어하기 | 슬롯이 없으면 PendingLoad 정리·버튼 갱신·알림. 자동저장 포함 최신 슬롯 판정 유지 | StartMenu_UI/Start_UI.cs:29; StartMenu_UI/GameSaveFiles.cs:291 / P2-A |
| MENU-004 | 불러오기 슬롯 패널 열기·선택한 저장의 씬 진입 | 잘못된 저장은 읽기 실패. 로딩 중 중복 진입 차단 | StartMenu_UI/Start_UI.cs:43,45; StartMenu_UI/SaveSlotsUI.cs:43 / P2-A |
| MENU-005 | 게임 종료 요청 | 현재 QuitGame의 직접 종료 경로를 유지한다. 별도 종료 확인창을 기존 사실로 적지 않음 | StartMenu_UI/Start_UI.cs:97 / P2-A |
| MENU-006 | ESC의 화면별 우선 처리와 일반 일시정지 | UICancelRouter로 판단 분리. 상위 차단·선택창·Pause 하위 화면을 먼저 소비하고 D01을 목록·퇴장보다 먼저 처리. 기존 편집 화면 닫기 유지 | [ESC 구현 범위](P1ACancelImplementation.md) / P1-A |
| MENU-007 | 일시정지 표시·재개·하위 화면 정리 | D03 대화 중 일시정지·이탈 확인 중첩과 포커스 복원. 현재 런타임 Canvas 추가는 프리팹으로 이관 | GameManager.cs:83,146; UIManager.cs:261 / P1-A, P2-A |
| MENU-008 | 일시정지에서 저장 패널 열기·닫기 | 메인 패널 숨김/Close 복원 유지. ESC는 하위 화면만 닫고 일시정지 유지·진입 버튼 포커스 복원 반영. 전체 저장 MVP는 후속 | StartMenu_UI/SaveSlotsUI.cs / P1-A, P2-A |
| MENU-009 | 확인 후 Intro로 나가기 | 중복 이탈 가드, 빌드 씬 확인, 습격·튜토리얼·PendingLoad 정리. 취소는 현재 화면 유지 | StartMenu_UI/PauseMenuNavigation.cs:11,20 / P2-A |
| MENU-010 | 수동 3슬롯·자동저장 카드의 요약·가능 상태·레이아웃 | 자동저장 직접 덮어쓰기 금지. 고정 카드·삭제 버튼의 런타임 생성은 작성된 프리팹으로 이관 | StartMenu_UI/SaveSlotsUI.cs:24,77,107,151; StartMenu_UI/GameSaveFiles.cs:199 / P2-A |
| MENU-011 | 수동/자동 저장 삭제·삭제 후 이어하기 갱신 | confirmDelete에 따른 기존 확인 정책과 I/O 실패 안내 보존. StartUI_Manager는 0, UIManager는 1로 직렬화되어 있으므로 씬별 실효값 대조 필요 | StartMenu_UI/SaveSlotsUI.cs:23 및 DeleteSlot/DeleteAutoSave; 관련 프리팹 confirmDelete / P2-A |
| MENU-012 | 슬롯 선택 시 저장 또는 불러오기, 필요 시 덮어쓰기 확인 | Save 실패는 알림·현재 화면 유지. 저장/불러오기 맥락을 혼동하지 않음 | StartMenu_UI/SaveSlotsUI.cs:359; StartMenu_UI/GameSaveController.cs:119 / P2-A |
| MENU-013 | 날짜 진행 자동저장·대기 세이브와 공방 상태 복원 | 튜토리얼·습격·은퇴의 저장 제한과 예약 취소·재시도 유지. UI가 별도 파일 저장을 만들지 않음 | StartMenu_UI/GameSaveController.cs:28,66,77,86,135 및 partial / P2-A |
| MENU-014 | 기존 저장 형식·해금·프리셋·즐겨찾기·월드 상태 호환 | 현재 읽기 버전 검사는 2~8. UI 개편으로 버전 범위나 ID를 바꾸지 않음. 세부 복원 필드는 P2-A와 영역 담당이 공동 대조 | StartMenu_UI/GameSaveFiles.cs:218,240; StartMenu_UI/GameSaveController.World.cs / P2-A |
| MENU-015 | 일시정지의 사운드 설정 열기·닫기 | mainPanel/panel 상태 복원과 표시 중 음악 이벤트 구독 해제. Intro에 같은 설정 화면이 있다고 추정하지 않음 | SoundSettingsUI.cs:33,57,71 / P2-A |
| MENU-016 | 볼륨·음소거 즉시 적용, 해금 곡 선택·현재 곡 표시 | SoundManager.TryChooseMainTrack 결과와 해금·Main 선택 제한 유지. 닫기로 설정 롤백하지 않음 | SoundSettingsUI.cs:46,86,96; SoundManager.cs / P2-A |
| MENU-017 | 감도 슬라이더·직접 수치 입력·즉시 저장 | MouseSensitivityUI.Multiplier 사용, 0.1~3 범위·유한값·문화권 파싱·잘못된 입력 복원 유지 | MouseSensitivityUI.cs:12,31,39 / P2-A |
| MENU-018 | 버튼 호버 외곽선·스크롤 초기화·공통 폰트 보조 | 호버와 선택 포커스는 구분. 복귀 스크롤 보존 정책과 현재 OnEnable 초기화가 충돌하는 화면은 담당자가 명시 이관 | ButtonHoverOutline.cs; ResetScrollOnEnable.cs; UIFonts.cs / P1-B |
| MENU-019 | ShowNotification의 실제 시간 알림·중복 교체·숨김 | 기존 지속·색·효과 유지. UIManager의 알림 fallback과 편집 UI 상태 결합은 P1에서 소유권 분리 시 대조 | FeedbackUI.cs:139,302; UIManager.cs:17,27,45 / P1-B |
| MENU-020 | ShowChoice·ShowAcknowledgement·확정/취소·Suspend | 중복 요청 거부·제스처 중복 방지 유지. 강제 종료는 사용자 취소와 분리. D04의 단일 안내만 전체 클릭 확인 예외 | FeedbackUI.cs:178,187,293,429 / P1-B |
| MENU-021 | 대화 표시·진행·숨김과 입력 중복 방지 | D03 일시정지 허용, 하위 대화 입력 차단, 기존 튜토리얼 제한 보존. 타자기·새 다지선다를 임의 추가하지 않음 | DialogueUI.cs:73,104,133,147,180 / P2-F |
| MENU-022 | 클릭 차단기·대화/pause/확인의 표시 순서 | 현재 런타임 Canvas·차단기·릴레이를 프리팹으로 이관. 최상위 입력과 뒤 화면의 포커스/차단 유지 | DialogueUI.cs; FeedbackUI.cs; UIManager.cs:266 / P1-A, P1-B |

## 공방 HUD·영업·정산·사격장

| ID | 현재 기능·정상 결과 | 취소·실패·이관 주의 | 근거 / 담당 |
| --- | --- | --- | --- |
| BIZ-001 | 돈·부품·티켓·자리세/대출 관련 HUD와 숫자 연출 | Wallet/Day 변경 이벤트·표시 초기화·숨김 유지. 반복 텍스트·자리수 UI 생성 경로를 프리팹으로 이관 | DaySettlementUI.cs:52,88,175; MoneyRollDisplay.cs / P2-B |
| BIZ-002 | 돈/부품 부족 알림과 해당 표시 펄스 | 새 부족 요청·화면 종료 때 색·스케일·연출 상태 정리. 실제 지갑 값을 UI에서 변경하지 않음 | DaySettlementUI.cs:158 및 BalanceDisplay / P2-B |
| BIZ-003 | 부품 손실 표시·숫자 롤링 | 실제 지갑의 복원 결과를 표시한다. 연출이 지급·회수의 두 번째 처리 경로가 되지 않게 함 | DaySettlementUI.cs:165 / P2-B |
| BIZ-004 | 셔터 개점 확인·간판/셔터 연출·영업 시작 | 개점 대기·애니메이션 중복·튜토리얼 제한 유지 | NextDayInteractable.cs; _DayManager.cs:324 / P2-B |
| BIZ-005 | 손님 선택·생성·호출·퇴장에 따른 다음 손님 진행 | 영업 상태·현재 손님·오늘 주문 한도와 도메인 이벤트 유지 | Bell.cs; _DayManager.cs / P2-B |
| BIZ-006 | 손님 상호작용 주문 대사와 접수 상태 | 기존 F 전달·대화 진행·마감/손님 종료 시 정리 유지. 도착 시 자동 대화·좌클릭 제출을 새 사실로 쓰지 않음 | Customer.cs:76,256; Interaction.cs / P2-B, P2-F |
| BIZ-007 | 활성 주문 조건·현재 무기 충족 표시와 본문 크기/스크롤 | 작업대/장착 대상 전환·빈 주문 상태 유지. 복수 행의 기존 폰트·최대 높이 차이는 씬 오버라이드와 함께 이관 | CustomerRequirementsUI.cs; QuestInfoTextSizing.cs / P2-B |
| BIZ-008 | 주문 외형 허용 영역 가이드 | 주문·편집 종료 시 가이드 정리. UI 프리팹화와 월드 표시 생성은 구분 | OrderShapeGuide.cs / P2-B, P2-C |
| BIZ-009 | 무기 제출·요구조건 판정·판매 대금·손님 완료 | 실제 지급과 완료 이벤트 중복 방지 유지. 튜토리얼 제출 제한 보존. 미연결 판매 확인 UI는 자동 활성화하지 않음 | Customer.cs:76,100; Bell.cs; _DayManager.cs:161,173 / P2-B |
| BIZ-010 | 손님 피격·사망·대화 종료·완료 집계 | 이미 완료된 손님의 보상/완료를 재처리하지 않음. 전투 판정은 UI Model에 복제하지 않음 | Customer.cs:156; Bell.cs; _DayManager.cs:173 / P2-B |
| BIZ-011 | 마감 버튼의 기존 영업 종료 요청 | 개점 전·밤·조작 차단 상태의 상호작용 조건 유지 | CloseShopButton.cs; _DayManager.cs:219 / P2-B |
| BIZ-012 | 정산 상태·자리세/대출 비용 계산·한 번 정산 | isSettled 가드와 도메인 소유 계산 유지. View가 이자·지급을 다시 계산하지 않음 | _DayManager.cs:232; DaySettlementUI.cs / P2-B |
| BIZ-013 | 정산 확인·닫기·다시 열기·다음 날 요청 | 열린 프레임·대화/모달 입력 가드 유지. 목표 ESC 무시·F/버튼 확인. 확인과 다음 날 스킵을 분리 | DaySettlementUI.cs:11,128,144,155; _DayManager.cs:282,346 / P2-B |
| BIZ-014 | 파산 정산과 게임오버 결과·복귀 | 현재 확인 후 결과 UI로 이어짐. 중복 결과·중복 씬 이동 가드 유지. 정산부터 바로 강제 Intro로 간다고 설명하지 않음 | _DayManager.cs:232,282,301 / P2-B, P2-E |
| BIZ-015 | 정산창/셔터의 밤 습격 스킵 | D05: 둘 다 부품100·날짜 페이드/Day 제목. 현재 경로별 차이와 습격 귀환/튜토리얼의 중복 지급 방지 | DaySettlementUI.cs:128; NextDayInteractable.cs:139; _DayManager.cs:354 / P2-B |
| BIZ-016 | 날짜·첫 영업일·습격 귀환·튜토리얼 전환 페이드 | Covered/DayTextShown/Completed 이벤트와 중복 전환·중단 정리 유지. 실제 시간 연출과 게임 시간 분리 | DayTransitionUI.cs:21,30,40,50,62,86 / P1-B, P2-B |
| BIZ-017 | 코어·프리셋 배출기 구매/장착과 모듈 해금 알림 | 가격·해금·지갑 조건은 기존 도메인/표를 재사용. 이름·프리뷰·부족 안내 유지 | CoreDispenser.cs; ModuleUnlockNotice.cs / P2-B |
| BIZ-018 | 사격장 선택 UI의 테스트/타겟/정리 요청 | 모달 상태·기본 포커스·튜토리얼 대상 제한과 ESC 닫기 유지 | TargetBell.cs; TargetSelectionUI.cs / P2-B |
| BIZ-019 | 일반 타겟·커스텀 큐브 입력 및 소환 실패 안내 | 실제 입력 검증·크기/저항 제한·생성 가능 판정은 기존 코드를 그대로 이관하며 세부 경계값은 P2-B에서 원문 대조 | TargetSelectionUI.cs; TargetBell.cs / P2-B |
| BIZ-020 | 사격 시험 시간·진행·경고·실패·F 재시도/ESC 종료 | 무기 조작 등 다른 F 상호작용과 충돌하지 않게 현재 조준 대상·Tutorial 가드 유지. HUD 생성은 프리팹으로 이관 | ShootingTestController.cs; ShootingTestHUD.cs; PlayerShootingTest.cs:13 / P2-B |
| BIZ-021 | 월드·슬롯·블록의 호버 설명·태그·상호작용 안내 | 현재 대상/빈 대상/드래그/모드 변화에 따른 표시 갱신. Interaction은 공통 전달만 유지 | HoveringUI.cs; Interaction.cs; InteractionLabel.cs / P2-B |
| BIZ-022 | Q를 누르는 동안 스캔·대상별 진행·완료 표시 | Q 해제·대상 전환·모달·포커스 상태에 따라 리셋. 튜토리얼 Q 해제 대기와 본체 스캔 판정 유지. 토글로 바꾸지 않음 | PlayerScanMode.cs:27,49,62; HoveringUI.cs; ScanLoadingGraphic.cs / P2-B, P2-F |
| BIZ-023 | 조준점·탄퍼짐·유도·차지·스코프 표시 | 무기/조준/충전 상태에서 읽어 표시. 도메인 입력 누수 방지 가드 보존. Main과 습격의 공통 전투 HUD 소유자는 P2-E | AimUI.cs; ChargeGaugeUI.cs; SpreadCrosshair.cs; HomingLockGraphic.cs; HomingRetentionRing.cs; ScopeOverlayGraphic.cs / P2-E |
| BIZ-024 | 공방·사격장 매뉴얼 열람·줌·팬·닫기 | F·ESC 정리와 원래 카메라/조작 복원 유지. 상세 UI 판정은 모델·화면 책임으로 분리 | WorkshopManual.cs; PlayerManualReader.cs / P2-B |

## 남은 대조와 비활성 보존

- SaleConfirmationUI, GoodShotUI 등의 미연결/비활성 후보는 신규 화면으로 재연결하지 않는다. 실제 참조와 호출 조건은 P3에서 함께 기록한다.
- StartUI_Manager.prefab의 confirmDelete=0과 UIManager.prefab의 confirmDelete=1은 실제 직렬화 차이다. 공통 확인창의 기본 선택 정책이 모든 기존 삭제를 새 확인창으로 바꾸라는 뜻은 아니다.
- 저장·복구·월드 스냅샷 필드의 전체 대응, 사격장 상세 경계값, 각 씬 Navigation·OnClick 참조는 이 목록만으로 완료가 아니다. P2/P3가 기능 ID별 소스/연결 상태를 갱신한다.
- 알림·정산·일시정지에서 시간·커서·UI 활성화를 직접 건드리는 기존 경로는 공통 흐름으로 이관하되 기존 Model 요청을 중복 실행하지 않는다.
