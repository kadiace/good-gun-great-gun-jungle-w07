# 최신 UI 구현과 개편 설계 차이

## 조사 기준과 범위

- 최초 최신화 조사 기준은 develop `62f7a9af35ddcb511d00d819ab03eb4838b73e44`이다. 주요 조사는 `4891e51` → `d687889`, 추가 변경은 `d687889` → `62f7a9a`를 대조했다. 아래 기존 조사 근거의 줄 번호는 d687889 기준이며, 추가 변경 절의 근거는 62f7a9a 기준이다. 현재 구현 작업은 feature/bs_patch의 HEAD `91122f5`와 작업 트리 기준이며 적용 범위는 [현황](UIRefactorP0/Overview.md)을 따른다.
- [UIRefactorDesign.md](UIRefactorDesign.md)의 합의사항과 총기 에디터·대화·팝업·ESC·관련 튜토리얼 변경을 대조했다. 날짜 진행·습격 결과의 기존 차이도 재확인했다.
- 코드·지정 프리팹·필요한 씬 연결을 읽은 정적 조사다. 실제 플레이·입력 이벤트 순서·화면 외형을 검증한 결과는 아니다. 전체 씬·전체 기능의 P0 전수 목록이 완료된 것도 아니다.
- 최초 작성은 저장소 최신화와 조사·문서화였다. 이후 적용한 UI 소스·씬·프리팹 단위는 각 항목의 구현 링크에 명시한다.
- D01~D09는 모두 사용자 수락으로 확정됐다. 아래 조사 당시 동작과 확정 정책·현재 적용 범위를 구분하며 플레이 확인은 별도다.

## 보존 대상으로 확인한 최신 기능

| 영역 | 확인한 동작 | 근거 |
| --- | --- | --- |
| 2D 층 선택 | 최초 +RIGHT·0층. 이전/다음·코어층·주변 5층 버튼, 점유 셀 수. 설치·호버·삭제가 같은 층 사용 | WorkshopPlanarView.cs:12,34,92,216; WorkshopPlanarLayerUI.cs:36 |
| 층별 표시 | 비선택 층 렌더러 표시만 억제하고 원상 복원. 앞/뒤 참고 셀 윤곽과 다른 층 연결 끊김 삭제 예고 | WorkshopPlanarLayerPresentation.cs:47; WorkshopPlanarGridGraphic.cs:50; CubeAssembly.cs:95 |
| 2D 연속 배치 | 최대 256셀·4연결 보간, 되짚어 경로 축소. 기존 점유 셀 보존·무과금. 이동 없는 클릭은 기존 단일 배치·방향 선택 | CubeBuilder.PlanarStroke.cs:29,54,78; WorkshopPlacementPlan.cs:35 |
| 자동 연장 | 공급 진행과 다음 경로가 수직인 곳에 연장 모듈 대체. 실제 종류별 해금·공급·출력·트리거·가격·누진 부품비 검사 | CubePlacementState.cs:18; WorkshopPlacementPlan.cs:32 |
| 일괄 확정 | 경로·방향·전체 비용 미리보기. 릴리스 때 재검사 후 결제 1회·Undo 1건. 중간 실패 시 레이아웃 복원 | CubeBuilder.PlanarStroke.cs:66,110; WorkshopPlanarGridGraphic.cs:58 |
| Undo | 기존 한도 3건 유지. 연속 배치 중 Ctrl+Z는 이전 확정 조립 대신 예정 경로만 취소 | CubeBuilder.cs:12,817 |
| 방향 표식 | 코어 +Forward 발사 경로의 전체 깊이 총구·겹침 수. 보기 방향별 숨김. 조립 +Z 최외곽 전방 표식과 3D 공통 기준점 | WorkshopPlanarDirectionsGraphic.cs:102,179; CubeAssembly.cs:15; UIManager.cs:205 |
| Tab 목록 ESC | 목록이 열렸으면 목록만 닫음. 다음 ESC는 작업대 퇴장 경로 | GameManager.cs:105; CubeBuilder.cs:164 |
| 대사 넘김 | 좌·우클릭·Space·Z·F와 기존 버튼 포인터·UI Submit. 일반 Yes/No는 범용 클릭 넘김 대상 아님 | InputSystem_Actions.inputactions:1577,1631; DialogueUI.cs:73; FeedbackUI.cs:178 |
| 중복 입력 방지 | 열린 프레임·누르고 있던 입력·제스처 소비 확인. down으로 넘긴 뒤 up/click, F 중복 액션, Space+Submit의 중복 진행 억제 | DialogueUI.cs:89,133,189; FeedbackUI.cs:122,164,230 |
| 사격·조준 누수 방지 | 조작 차단 중 시작한 입력은 버튼을 놓을 때까지 거부하는 처리 | WeaponHolder.cs:69,166; WeaponScope.cs:151,195 |
| 튜토리얼 보상 실습 | 모듈 F 획득 → 티켓 소개·획득 → 부품 소개·획득 → 탈출. 스캔 후 Q 해제 대기와 상자 보호 해제 유지 | Day0_Tutorial.asset:3960,4051,4141; TutorialManager.Flow.cs:183,224,288 |

근거 경로는 별도 표기가 없으면 Assets/Scripts 아래다. 실행 단계 데이터는 Assets/Data/Tutorials/Day0_Tutorial.asset이다.

### 이전부터 있던 슬롯 드래그

ModulePaletteUI·ModuleSlotUI·WorkshopSession·PlayerWallet은 이번 비교 구간에서 변경되지 않았다. 슬롯 드래그를 새 2D 연속 배치와 같은 기능으로 취급하지 않는다.

- 목록에서 즐겨찾기로 등록, 기존 등록 위치와 교환, 목록을 닫은 상태의 즐겨찾기끼리 교환을 보존한다.
- 유효한 드롭 전에는 실제 구성을 변경하지 않는다. 같은 칸·바깥 드롭은 무변경이며 선택된 모듈 ID도 유지한다.
- 숫자키는 보통 모듈 선택이지만 목록 항목 호버 중에는 해당 즐겨찾기 등록으로 동작한다.
- 슬롯 드래그는 UI 밖에서도 유지하며 조립·숫자키 입력을 차단한다.
- 작업대 종료·포커스 상실·원본 비활성·편집 불가·모달/잠금 및 목록을 연 채 시작한 드래그의 목록 닫기는 취소한다.
- 선택 모듈·즐겨찾기는 작업대 퇴장 시 유지한다. 삭제 모드·목록·컬러 선택·임시 미리보기·작업대 Undo는 정리한다.
- 근거: ModulePaletteUI.cs:409,436,465,505,529; ModuleSlotUI.cs:164; CubeBuilder.cs:37,132,190,227,393.

### 층과 축 및 초기화는 서로 다름

| 조작 | 현재 미확정 배치 | 현재 보기 상태 |
| --- | --- | --- |
| 층 선택 | 취소 | 축·팬·줌 유지 |
| 축 변경 | 취소 | 0층·팬 초기화, 줌 유지 |
| R | 취소 | 층·축 유지, 팬·줌 초기화 |
| 작업대 퇴장·3D 복귀 | 정리 | 원래 투영·카메라·ObjRotate 및 숨긴 렌더러 복원 |

근거: WorkshopPlanarView.cs:39,103,132,158; WorkshopPlanarLayerPresentation.cs:89. 단순한 Reset 하나로 통합해 이 차이를 지우지 않는다.

## 동작 차이와 결정 상태

### D01 드래그 중 ESC 확정

- 조사 당시: 목록 열림이면 목록만 닫는다. 목록이 닫힌 슬롯 드래그와 2D 배치 드래그 중에는 다른 상위 화면이 없다면 작업대를 퇴장하고 종료 정리로 드래그를 취소한다.
- 확정: 슬롯 드래그와 2D 연속 배치 모두 ESC는 드래그만 취소한다. 드래그가 없으면 열린 Tab 목록만 닫고, 둘 다 없으면 작업대를 나간다. 한 입력에 한 단계만 처리하며 각 드래그의 취소 방식은 유지한다.
- 반영: [ESC·D01](UIRefactorP0/P1ACancelImplementation.md)의 UICancelRouter에서 슬롯 드래그와 연속 배치·삭제 취소를 목록 닫기·퇴장보다 먼저 소비한다. 기존 드롭·교환·결제·Undo 구현은 유지한다. 플레이 확인은 별도다.
- 조사 당시 근거: GameManager.cs:83-117; CubeBuilder.cs:164,393; ModulePaletteUI.cs:294,470; CubeBuilder.PlanarStroke.cs:54.

### D02 슬롯 드래그와 카메라 조작 점유 확정

- 조사 당시: 슬롯 드래그는 조립·숫자키를 막지만 WorkshopSession의 3D 우클릭·줌, 2D 팬·줌에 같은 점유 플래그가 전달되지 않았다. UI 밖에서 추가 카메라 조작을 받을 수 있는 코드 경로가 있었다. 실제 동시 조작은 미확인이다.
- 현재의 별도 규칙: 슬롯 드래그는 UI 밖에서도 유지하지만, 2D 배치 드래그는 UI 진입·뷰포트 이탈 시 취소한다. 월드 회전은 UI 밖에서 시작한 뒤 UI 위로 이동해도 계속될 수 있다.
- 확정: 슬롯 드래그 중 무기 회전·팬·줌도 차단한다. 슬롯 드래그는 UI 밖에서도 유지하고 바깥 드롭은 취소한다. 2D 연속 배치의 UI 진입·뷰포트 이탈 취소는 유지한다. UI 밖에서 시작한 3D 회전은 UI 위로 이동해도 유지하며 UI 위에서는 새 회전을 시작하지 않는다.
- 반영 범위: [P2-C 입력 점유](UIRefactorP0/P2CDragOwnershipImplementation.md). CubeBuilder의 기존 드래그 상태를 읽어 시점 조작·초기화와 시점/축/층 변경을 차단한다. 슬롯 드롭·교환과 D01 취소는 기존 구현을 사용한다. 플레이 확인은 별도다.
- 근거: ModulePaletteUI.cs:436-539; CubeBuilder.cs:227,350,1334; WorkshopSession.cs:235-321; WorkshopPlanarView.cs:168-193.

### D03 대화와 일시정지 중첩 확정

- 현재: 대화 중 ESC로 일시정지를 열 수 있고 대화는 유지된다. 일시정지 메뉴는 대화 위에 표시되며 뒤 대화 입력을 막는다.
- 이전 합의의 대화 중 ESC 기본 무시는 이번 확정으로 대체한다. 기본 기능 화면 하나 원칙에는 아래 중첩을 명시적 예외로 둔다.
- 확정: 대화 중 ESC로 일시정지를 허용한다. 대화 위에 일시정지를 열고, 닫으면 기존 대화와 포커스로 복귀한다. 일시정지에서 연 이탈 확인창은 다시 그 위에서 입력을 받는다.
- 구별: 단일 안내와 일반 Yes/No는 현재 모두 ESC로 Cancel 처리하며 pause를 열지 않는다. 대출 안내처럼 choice 입력을 Suspend한 경우 ESC도 소비하고 취소·pause를 하지 않는다.
- 근거: GameManager.cs:83,93,119; UIManager.cs:266; DialogueUI.cs:104,159,192; FeedbackUI.cs:293,429; TutorialManager.cs:322.
- 반영: [대화 MVP](UIRefactorP0/P1BDialogueImplementation.md)에서 Pause·전환·일반 선택창 뒤의 대화 입력/Raycast와 확인 버튼을 차단하고 해제 시 포커스를 복구한다. Suspend 대출 안내는 대화151/선택창150의 명시적 겹침으로 보존한다. [ESC 라우터](UIRefactorP0/P1ACancelImplementation.md)에서 Pause 하위 저장·설정 닫기와 직접 Resume을 연결했다. Intro·엔딩 등 나머지 화면과 전역 상태 소유권은 후속이다. 위 줄 번호는 조사 시점 기준이다.

### D04 단일 안내의 전체 화면 확인 확정

- 현재: 대사와 단일 안내는 화면 어디든 좌·우클릭·Space·Z·F로 진행할 수 있다. 일반 Yes/No 선택은 이 범용 클릭 진행 대상이 아니다.
- 합의: 배경 클릭으로 창을 닫지 않음. 확인 종료·취소 종료·강제 종료 구분.
- 확정: 대사·단일 안내의 전체 화면 입력은 확인 입력의 명시적 예외로 보존한다. 일반 Yes/No 선택창에는 적용하지 않는다. 단일 안내 ESC는 현재 취소 동작을 유지한다.
- 근거: DialogueUI.cs:73,147,180; FeedbackUI.cs:178,187,221,388,429; InputSystem_Actions.inputactions:1631.
- 대화 구현은 [대화 MVP](UIRefactorP0/P1BDialogueImplementation.md)의 Presenter·View·저작된 버튼 중계로 옮겼으며 기존 액션과 제스처/홀드 가드를 유지한다.

### D05 다음 날 스킵의 지급과 연출 확정

| 진입 경로 | 조사 기준 호출 | 부품 | 연출 |
| --- | --- | --- | --- |
| 정산창 다음 날 | StartNextDay(skippedNightRaid: true) | 100 지급 | 날짜를 직접 진행 |
| 셔터 다음 날 | DayTransitionUI.Begin → StartNextDay() | 지급 없음 | 검정 페이드·Day 제목 |

- 확정 지급: 정산창·셔터의 다음 날 스킵 모두 부품 100을 지급한다.
- 확정 연출: 두 경로 모두 기존 날짜 페이드·Day 제목을 사용한다. 생존 습격 귀환·튜토리얼 날짜 진행은 스킵과 구별해 보상 중복을 막는다.
- 근거: DaySettlementUI.cs:128-140; NextDayInteractable.cs:139-149; DayTransitionUI.cs:30,62-79; _DayManager.cs:354,401-407.
- 반영: [P1-B 날짜 전환](UIRefactorP0/P1BDayTransitionImplementation.md)에서 두 스킵 호출을 `BeginSkippedNightRaid`로 통일했다. 가림 시 Presenter가 기존 `StartNextDay(true)`를 호출하며 귀환·튜토리얼은 별도 경로다. 위 표와 줄 번호는 개편 전 조사 근거다.

### D06 연속 배치의 비용 부족과 대출 안내 확정

- 조사 당시: 단일 모듈 설치는 달러 부족·부품 충족·대출 가능 조건에서 대출 확인을 제안한다. 연속 배치는 전체 비용 부족 안내 후 경로를 취소하고 같은 대출 제안을 호출하지 않는다.
- 확정: 드래그 경로는 취소하되, 달러 부족·부품 충족·대출 가능 조건이라면 단일 설치와 같은 대출 안내를 제공한다. 대출 승인만으로 취소한 경로를 자동 설치하지 않고 사용자가 다시 배치한다. 부품 부족과 대출 불가에는 해당 이유만 안내한다.
- 근거: CubeBuilder.cs:638-657; WorkshopPlacementPlan.cs:87-90; CubeBuilder.PlanarStroke.cs:69-74,124-142.
- 반영: [연속 배치 비용·D06](UIRefactorP0/P2CStrokeFundsImplementation.md). 구조·지갑·부품·달러 실패를 구분하고 경로 취소 뒤 단일 설치와 공유하는 대출 함수를 호출한다. 승인 콜백은 대출과 기존 튜토리얼 신호만 실행하며 자동 설치하지 않는다. 위 줄 번호는 조사 당시 기준이다.

### D07 탈출 성공 결과를 고정하는 시점 확정

- 조사 기준: 결과 표시 뒤 성공 화면을 사망으로 교체하는 경로가 있고 ReturnHome도 사망 여부를 재검사했다.
- 합의: 결과가 확정되면 전투·탈출·보상 집계를 종료한다.
- 확정: 탈출 5초 완료 시점에 생존 성공과 예정 보상을 고정하고 이후 피해로 뒤집지 않는다. 같은 시점에 이미 사망했으면 사망을 우선한다. 지갑·저장 반영은 기존 복귀 확인 흐름에서 한 번만 수행한다.
- 결과창 뒤 사망이 실제 발생하는지 재현한 것은 아니다. 결과 고정·피해 처리 경계는 UI만 바꿔 해결할 수 있다고 단정하지 않는다.
- 근거: NightRaidController.cs:105-144,168-181,270-317,674-724; NightRaidUI.Result.cs:33-55; RaidPlayerHealth.cs:28-43.
- 반영: [결과 화면·D07](UIRefactorP0/P1BResultImplementation.md)에서 기존 종료 상태를 한 번 확정하고 후속 사망 덮어쓰기·해당 플레이어 피해·집계를 차단했다. 기존 야간 정지 정책과 실제 시간 결과 연출을 연결했으며 지급·저장 시점은 유지했다. 위 줄 번호는 조사 당시 기준이며 플레이 재현은 수행하지 않았다.

### D08 튜토리얼 보상 단계의 선획득 인정 확정

- 현재: 티켓·부품 단계는 이번 습격의 누적 획득량 > 0으로 완료한다. 단계 진입 후 새로 획득했는지, 안내한 탱크·상자를 이용했는지는 검사하지 않는다. 모듈은 실제 F 획득 성공 신호를 사용한다.
- 확정: 이번 습격의 누적 획득을 인정하는 최신 동작을 보존한다. 안내 이후 새로 획득하는 행동은 강제하지 않는다.
- 근거: TutorialManager.Flow.cs:224-230,288,302,316; Day0_Tutorial.asset:3960,4051,4141; ModuleUnlockPickup.cs:59,72.

## 추가 커밋의 짧은 분석

62f7a9a에서 다음 동작이 추가됐다. 기존 슬롯 드래그와 별개인 2D 조립 기능으로 보존 목록에 포함한다.

- Ctrl+휠은 선택 층을 한 번에 ±1 이동하고 같은 휠의 줌 처리를 막는다. 뷰포트 안·UI 밖·팬 해제 조건이며 새 Build/LayerModifier와 기존 Zoom 액션을 사용한다(WorkshopPlanarView.cs:168-213; WorkshopSession.cs:75,226,270; InputSystem_Actions.inputactions:1131,1319).
- E 삭제 모드에서 좌클릭 드래그로 선택 층의 제거 대상을 누적한다. 설치 경로와 달리 되짚어도 대상이 줄지 않는다. 경로 상한은 256셀이고 빈 셀·코어는 실제 제거 대상에서 제외한다(CubeBuilder.PlanarStroke.cs:36-160; CubeAssembly.cs:103-158).
- 직접 대상 전체를 제외한 뒤 코어 연결성을 한 번 계산해 다른 층의 연결 끊김까지 미리보기·실제 삭제에 같이 반영한다. 릴리스 시 일괄 환불·Undo 한 건이며 직접 대상별 튜토리얼 삭제 허가를 재검사한다(CubeBuilder.PlanarStroke.cs:143-158; WorkshopPlanarGridGraphic.cs:60-68).
- Session(25)에서 층·팬 입력을 먼저 처리하고 CubeBuilder(50)에서 배치를 처리한다. 같은 프레임의 Ctrl+휠과 릴리스가 이전 층에 잘못 확정되지 않도록 한 순서다(CubeBuilder.cs:9-10; WorkshopSession.cs:9).
- Main의 LayerControls·LayerHint 배치와 글자 스타일을 조정하고, 이전/다음·코어·주변 5층 버튼을 씬 오버라이드로 비활성화했다. Bind는 패널 활성만 바꾸고 자식 버튼을 다시 켜지 않는다. 따라서 Main의 현재 층 선택은 Ctrl+휠 중심이며 버튼 코드는 남아 있다(Main.unity의 UIManager 인스턴스; WorkshopPlanarLayerUI.cs:30-54).
- D01의 ESC 취소 우선순위에는 새 연속 삭제도 포함하며 ESC 라우터에 반영했다. D02의 회전·팬·줌·층 이동 점유는 [P2-C 입력 점유](UIRefactorP0/P2CDragOwnershipImplementation.md)에 반영했다. 전역 입력 소유권과 플레이 대조는 별도다.

### D09 층 이동 버튼 병행 확정

- 이전 기준에는 층 선택 버튼이 있었지만 최신 Main은 이를 숨겼다. 코드·공용 프리팹의 버튼은 남아 있다.
- 확정: Ctrl+휠과 기존 층 이동 버튼을 병행한다. 버튼을 다시 표시해 마우스·방향키로도 선택할 수 있게 한다.
- 기존 비활성 기능 보존 원칙에 대한 명시적 예외다. [P2 병렬 이관](UIRefactorP0/P2ParallelWaveImplementation.md)에서 Main의 버튼8개 비활성 오버라이드와 패널 세로 공간을 복원하고 두 줄·모드 버튼 연결의 Explicit Navigation을 저작했다. Ctrl+휠·D02 차단은 유지한다. 플레이 확인은 별도다.

## 추가 사용자 결정 없이 합의대로 이관할 항목

| 현재 구현 | 합의된 이관 방향 |
| --- | --- |
| Canvas·GraphicRaycaster·투명 클릭 차단기·릴레이의 런타임 생성 | 프리팹에 명시적으로 구성하고 필요한 클릭 차단 유지 |
| Canvas 150/200/300 및 SetAsLastSibling 직접 변경 | 흐름 담당자의 Layer·중첩 순서 적용. 대화·pause 정책은 D03 결정 반영 |
| OnDisable → Cancel → onNo 호출 | 강제 종료를 취소와 구분해 후속 게임 동작 실행 방지 |
| 기본 선택 없음·Automatic Navigation | 화면별 기본 선택·명시 이동 경로·포커스 복원 |
| UIManager CanvasScaler 기준 2560×1440 | 합의한 1920×1080 기준·16:9/21:9 대응 |
| 작업대 RotationViewport가 전체 카메라 pixelRect | 합의한 실제 작업 영역과 일치하도록 표시·포인터 계산 조율 |
| UI 자동 탐색·직접 상태 변경·매 프레임 표시 혼재 | 명시 초기 연결·화면별 MVP·Model 이벤트 갱신 |

근거: DialogueUI.cs:63,206,236,301; FeedbackUI.cs:105,249,279,410,429,448; UIManager.cs:256,266,279; UIManager.prefab:45646,76060; WorkshopSession.cs:26; ModulePaletteUI.cs:199,542.

## 보존 점검 시 주의할 제약

- 비선택 층 숨김은 모든 카메라에 영향을 준다. 동시 별도 3D 미리보기는 현재 제약이며 새 UI가 이를 요구하면 추가 설계가 필요하다.
- 차단 중 시작한 공격·조준 거부는 보존하되, 차단 전 충전 시작 → 차단 중 유지 → 해제 후 릴리스까지 모두 안전하다고 확인한 것은 아니다. WeaponHolder.ReleaseCharge의 권한 확인 범위를 P0 입력 보존 목록에 남긴다(WeaponHolder.cs:192).
- README의 공용 UI 입력 액션 미연결 설명과 현재 프리팹 참조 존재가 일치하지 않는다. 실제 액션 참조 해석과 전 씬 연결은 후속 통합 조사 대상으로 남긴다.
- 새로운 방향키·프리팹 구조를 적용하면서 드래그·호버 숫자키 등록·튜토리얼 대상 제한·대출 중첩·입력 제스처 중복 방지를 삭제하지 않는다.

## 결정 반영 방식

- D01~D09 모두 확정이며 설계 문서에도 반영했다. 새로 발견하는 게임 동작 충돌은 별도로 보고한다.
- 합의된 정책의 코드 적용은 후속 구현 단계에서 수행한다. 이번 조사를 코드 수정 완료나 기능 보존 전수 확인으로 표시하지 않는다.
