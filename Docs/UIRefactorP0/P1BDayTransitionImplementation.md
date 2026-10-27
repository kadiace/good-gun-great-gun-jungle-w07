# P1-B 날짜 전환 MVP

확인창·알림 다음 작업 단위다. 날짜 전환만 분리하며 정산 화면 전체, 습격 결과 화면, 전역 입력·시간·커서 소유권 정리는 후속이다.

## 읽는 순서와 책임

1. `Assets/Scripts/DayTransitionUI.cs`: 기존 호출부의 진입점과 이벤트를 유지하는 조립부.
2. `Assets/Scripts/UI/Transition/DayTransitionPresenter.cs`: 전환 종류, 날짜 진행 요청, 입력 차단과 실행 상태.
3. `Assets/Scripts/UI/Transition/DayTransitionView.cs`: 작성된 패널·CanvasGroup·TMP 참조와 실제 시간 기준 페이드·제목 연출.

날짜와 지갑 데이터의 원본은 기존 `_DayManager`·PlayerWallet이다. 별도의 복제 Model이나 보상 테이블은 만들지 않는다. 전환 시간은 기존 프리팹 저작 값으로 유지한다.

## 유지할 계약

| 진입점 | 용도 | 스킵 부품 지급 |
| --- | --- | --- |
| `Begin` | 기존 일반 날짜 전환 API 호환 | 없음 |
| `BeginSkippedNightRaid` | 정산창·셔터에서 사용자가 야간 습격을 건너뜀 | 기존 `StartNextDay(true)`에서 100 |
| `BeginFirstDay` | 튜토리얼 종료 후 첫 영업일 | 없음 |
| `BeginReturn` | 습격 귀환, 이미 가려진 상태에서 시작 | 없음 |
| `BeginTutorial` | 가림 시 전달받은 콜백 실행 후 다시 표시 | 없음 |

일반 날짜 경로는 가림 → `Covered` → 날짜 진행 → Day 제목 표시와 `DayTextShown` → 제목 페이드·대기 → 화면 페이드 해제 → `Completed` 순서를 유지한다. 튜토리얼 경로는 전달된 콜백만 실행하고 날짜 경로의 세 이벤트를 발생시키지 않는다.

페이드 0.45초, 제목 유지 1초와 기존 SmoothStep 보간·실제 시간 사용을 유지한다. 중복·비활성·참조 누락 요청은 거부한다. 비활성화·파괴 시 취소는 완료 이벤트나 날짜 진행을 대신 실행하지 않는다. 이미 가려진 뒤 실제로 완료된 날짜/지갑 변경을 되돌리는 기능은 아니다.

## D05 호출부

- `DaySettlementUI.ConfirmSkipNightRaid`와 `NextDayInteractable`의 확인 콜백만 전용 스킵 진입점으로 연결한다.
- 정산창은 확인 콜백 시 소유자 활성 상태·정산 상태·다음 날 가능 상태를 다시 검사한다. 전환을 찾지 못하거나 요청이 거부되면 직접 날짜를 진행하는 우회 처리를 하지 않는다.
- 부품 지급은 UI에서 하지 않는다. 두 경로 모두 가림 시 기존 `_DayManager.StartNextDay(skippedNightRaid: true)`를 요청한다. 기존 도메인의 날짜/진행 가능 조건이 재요청을 제한한다.
- 습격 귀환의 `GameSaveController.BeginReturn`과 튜토리얼 경로에는 스킵 플래그를 전달하지 않는다.

## 프리팹과 후속 범위

공용 `UIManager.prefab`의 기존 날짜 전환 컴포넌트 ID `9195474757741827381`, 패널과 하위 참조 ID를 유지하고 루트에 View `7300000000000000001`을 작성한다. 기존 `TransitionLayer` 배치는 유지한다. Main·Tutorial_WoodenHouse의 해당 스크립트는 프리팹 원본에 대한 stripped 참조이며 씬을 직접 수정하지 않는다.

기존 6개 저작 필드 이름은 직렬화 호환을 위해 조립부에 유지한 예외다. 새 `m_view` 참조를 명시적으로 연결하고 `Configure`로 View에 전달한다. 기존 블록 3184개는 모두 유지했으며 View 1개만 추가해 3185개가 됐다. 기존 루트의 컴포넌트 목록과 조립부의 `m_view` 이외에 기존 블록은 바꾸지 않았다.

GameManager는 요청 시 주입된 공급자로 조회하고, 실제 입력을 차단한 인스턴스를 보관해 해제한다. 실행 번호를 이벤트 이후 다시 비교하고 이전 실행이 새 실행의 코루틴 핸들을 덮어쓰지 않도록 한다. 이 검토는 소스 대조이며 실동작 확인을 뜻하지 않는다.

전체 P1-B 완료가 아니다. 정산창의 동적 재화 UI 생성, 요청자별 Feedback 강제 종료 이관, 전역 시간·커서 정책, 씬 실효 연결·플레이 대조는 남는다. 결과 화면은 별도 [결과·D07 단위](P1BResultImplementation.md)에 반영했다. 전환의 입력 차단은 기존 GameManager 대화 상태 계약을 사용한다.

정적 소스·직렬화 대조와 수정 자산 6개(스크립트 5개·공용 프리팹 1개)의 개별 임포트를 완료했다. 테스트·플레이·빌드는 실행하지 않았다. 기존 스테이징 22파일의 전체 cached diff는 시작 시점과 동일하며 이번 작업은 별도 미스테이징 상태다. 실제 커밋은 생성하지 않았다.

이번 단위는 위 6개 자산과 새 Transition 폴더·스크립트 메타, 이 문서 및 Overview·Contracts·SourceIndex·Differences·README의 관련 설명으로 구성한다. 앞선 Feedback 작업 단위와 함께 남은 변경을 임의로 일부만 스테이징하지 않는다.
