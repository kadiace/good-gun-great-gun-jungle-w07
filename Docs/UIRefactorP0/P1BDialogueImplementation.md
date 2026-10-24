# P1-B 대화 UI MVP

대화 화면의 실행 중 UI 생성과 루트 직계 계층 의존을 제거하는 단위다. 대화 내용·손님 진행·튜토리얼 데이터, 전체 공통 입력·시간·커서 흐름을 재작성하는 작업은 아니다.

## 읽는 순서

1. `DialogueUI`: 기존 Instance·Show·Hide·IsOpen·TutorialButton을 유지하는 조립부.
2. `UI/Dialogue/DialoguePresenter`: 현재 응답 콜백과 입력 제스처·표시 프레임·열릴 때 누른 입력의 소비 상태.
3. `UI/Dialogue/DialogueView`: 저작 참조와 표시·포커스, 기존 Workshop 액션의 이벤트 전달.
4. `DialogueConfirmButtonEventRelay`: 작성된 확인 버튼에서 포인터 누름·클릭·Submit을 Action으로 전달.

손님과 튜토리얼은 기존처럼 대사·선택 문구·응답 콜백을 전달한다. 응답을 받는 도메인이 다음 줄을 표시하거나 Hide를 결정하며, UI가 손님 완료·보상·튜토리얼 단계를 별도로 계산하지 않는다. 대사 데이터 저장소도 이번에 새로 만들지 않는다.

## 보존할 계약

- `Show`는 이미 열린 대화의 다음 줄과 콜백을 교체할 수 있다. 일반 확인창의 중복 요청 거부 규칙을 그대로 적용하지 않는다.
- 답변은 LateUpdate에서 한 번 전달하며, 콜백을 먼저 비우고 호출한다. 콜백에서 Show나 Hide가 다시 호출돼도 이전 처리의 후속 쓰기로 새 상태를 덮지 않는다.
- 같은 프레임의 범용 넘김·포인터·Submit은 하나의 제스처로 합친다. 열린 프레임·열린 제스처 및 열릴 때부터 누르고 있는 입력은 소비하지 않는다.
- 기존 InputSystem_Actions의 Workshop/AdvanceDialogue·Interact를 사용한다. 대사의 전체 화면 좌·우클릭·Space·Z·F 진행은 D04 예외로 유지하며, 버튼 Submit도 유지한다. 직접 키·마우스 폴링이나 새 코드 전용 바인딩을 만들지 않는다.
- Hide·비활성화·파괴는 응답 콜백을 실행하지 않는다. 초기 상태는 코드에서 숨김으로 설정한다.

## D03 중첩과 포커스

대화 위에 Pause와 이탈 확인을 띄우는 합의는 유지한다. 대화 UI가 별도로 ESC를 구독하지 않으며 GameManager의 기존 진입점을 사용한다. 상위 화면이나 날짜 전환 동안 대사의 범용 입력·확인 버튼 입력을 막고, 표시를 닫거나 대사 진행 상태를 초기화하지 않는다.

Presenter가 차단 상태를 계산해 View의 GraphicRaycaster와 확인 버튼 상호작용을 끄며, 해제 시 저작 값을 복구한다. 일반 선택창이 열리면 대화를 차단하지만, 선택창 입력을 Suspend한 뒤 그 위에 띄우는 대출 안내 대화는 예외다. 일반 대화 Canvas150과 별도로 이 안내가 열린 동안만 Canvas151을 사용해 PopupLayer의 중단된 선택창150 위에 표시하고, 닫을 때150으로 복구한다. Pause200은 이 대화보다 위에 남는다.

확인 버튼을 최초 선택과 대화 복귀 대상으로 사용하되, 매 프레임 포커스를 다시 선택하지 않는다. 일시정지 중 들어온 Show도 상위 메뉴의 포커스를 빼앗지 않게 한다. 전체 메뉴·저장·설정의 단계별 ESC 이관은 별도 작업이다.

## 저작 구조

실제 원본은 `Assets/Prefabs/UI/Dialogue.prefab`이며 기존 루트·Box·Line·Answer·라벨 ID와 스크립트 GUID를 보존한다. 기존 조립부의 직렬화 필드는 호환을 위해 유지하고 View로 명시적으로 전달한다. 발화자 라벨은 기존처럼 선택 참조다.

프리팹에 View·Canvas150·GraphicRaycaster·전체 화면 투명 클릭 차단기·확인 중계 컴포넌트를 작성한다. 실행 중 AddComponent나 GameObject 생성, 이름 탐색으로 참조를 보충하지 않는다. 차단기는 대화 루트의 첫 자식이며 대화가 닫히면 함께 숨긴다.

공용 UIManager의 중첩 Dialogue 인스턴스는 루트 직계 임시 예외에서 ScreenLayer로 옮긴다. 대화 자신의 Canvas가 정렬을 담당하므로 ScreenLayer 전체에 Canvas150을 붙이던 루트 가지 탐색이 필요 없다. Pause200과 확인창150/300 등 기존 숫자 예외를 전부 없앤 것은 아니며, TransitionLayer와 씬별 Canvas의 전체 정렬 통합도 후속이다.

원본 블록37개를 보존하고 View·Canvas·Raycaster·차단기4블록·중계 컴포넌트의8개를 추가해45개가 됐다. View는 `7500000000000000001`, Canvas는 끝자리2, Raycaster3, 차단기 GO4/RT5/Renderer6/Image7, 중계8이다. 기존 Answer GO `668728987328721331`과 Button `1610996105437710554`의 참조·빈 persistent call을 유지한다. 공용 UIManager3185개 블록의 ID는 모두 유지하고 루트/ScreenLayer 자식 목록과 중첩 인스턴스 부모만 바꿨다. Main·Tutorial_WoodenHouse 씬 파일은 수정하지 않았다.

공개 Show의 인자 이름은 `_text`·`_answer`·`_onAnswer`·`_speaker`로 정리했다. 저장소 호출부에서 이름 있는 인자는 없었으며 타입·순서·기본 발화자 값은 유지한다. 비활성 조립부 또는 View의 Show는 거부한다.

정적 소스·직렬화 대조와 수정 자산6개(스크립트4개·프리팹2개)의 개별 임포트를 완료했다. 테스트·플레이·빌드는 실행하지 않았다. 기존22파일의 UI 스테이징 변경 본문을 보존했고 이번 단위는 별도 미스테이징 상태다. 작업 중 반영된 별도 성능 커밋도 보존했다. 이 작업으로 실제 커밋은 생성하지 않았다.

이번 단위의 소스는 기존 DialogueUI와 신규 Dialogue 폴더의3개 클래스·각 메타이며 실제 저작 변경은 Dialogue.prefab과 UIManager.prefab이다. 데이터·저장 형식·GameManager·Customer·TutorialManager·씬 파일은 수정하지 않았다.

남은 범위는 전역 SetDialogue/시간 소유권과 전체 Canvas 정렬, Save/Settings를 포함한 중첩 메뉴 흐름, 씬 실효 연결·플레이 대조다. 공용 Dialogue 인스턴스의 기존 추가 TMP·발화자 참조·제거 컴포넌트 오버라이드는 임의로 정리하지 않고 보존했다. 기반 프리팹의 발화자 라벨 null은 선택 참조 계약이며 현재 연결은 공용/씬 오버라이드에서 온다. 중복 렌더 의심과 스테일 오버라이드, 현재2560×1440 스케일러와 목표1920×1080 차이는 별도 씬 통합 대조 대상이다.
