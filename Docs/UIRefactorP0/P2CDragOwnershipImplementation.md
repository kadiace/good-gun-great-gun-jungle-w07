# P2-C 슬롯 드래그와 시점 조작 점유

사용자가 확정한 D02를 기존 편집 입력에 연결하는 단위다. 에디터 전체 MVP·저작 프리팹 전환이나 전역 입력 소유권 통합의 완료를 뜻하지 않는다.

## 상태와 읽기 순서

ModulePaletteUI의 기존 드래그 시작/정리는 CubeBuilder.SetFavoriteDrag를 통해 상태를 전달한다. 이 상태를 읽기 전용으로 공개하고 WorkshopSession과 WorkshopPlanarView가 같은 원본을 조회한다. UI나 카메라에 별도 드래그 상태를 복제하지 않는다.

읽는 순서는 ModulePaletteUI.BeginDrag/CancelDrag → CubeBuilder.SetFavoriteDrag → WorkshopSession의 회전 시작·종료·Update → WorkshopPlanarView.UpdateInput이다.

## 적용할 경계

| 상황 | 동작 |
| --- | --- |
| 슬롯 드래그 중 3D 회전·롤·줌 | 차단, 기존 회전 드래그는 취소 |
| 슬롯 드래그 중 2D 팬·줌·Ctrl+휠 층 이동 | 차단, 기존 팬은 정리 |
| 슬롯 드래그 중 R 초기화·시점/축/층 버튼 | 같은 점유 조건으로 차단, 버튼 상태도 CanChangeView 사용 |
| UI 밖에서 시작한 일반 3D 회전 | 포인터가 UI 위로 이동해도 유지 |
| UI 위에서 새 3D 회전 시작 | 기존 차단 유지 |
| 슬롯을 UI 밖으로 드래그 | 기존 드래그 유지, 바깥 드롭은 취소 |
| 2D 연속 배치·삭제의 UI 진입/뷰포트 이탈 | 기존 스트로크 취소 유지 |
| ESC | 이전 단위의 D01 라우팅 유지 |

기존 Input System 액션만 사용하며 바인딩을 추가하지 않는다. 슬롯 등록·교환·고스트·결제·Undo·튜토리얼 조건의 정책을 새로 만들지 않는다. 사용자가 드래그로 슬롯을 변경하는 기능은 보존한다.

CubeBuilder에는 IsFavoriteDragging 읽기 전용 프로퍼티만 추가한다. WorkshopSession은 회전 시작/종료 이벤트에서도 상태를 검사하고, Update에서 슬롯 드래그 중 기존 회전·팬을 정리한 뒤 카메라 조작 전에 반환한다. 회전 종료 가드는 프레임 갱신 전 이벤트에서도 잘못된 튜토리얼 완료 신호를 막는다.

WorkshopPlanarView.CanChangeView가 같은 상태를 읽는다. UpdateInput의 팬·줌·층 휠뿐 아니라 시점·축·층 버튼의 실행과 활성 상태가 이 조건을 공유한다. 포인터 드래그 중 선택된 버튼에 키보드 Submit이 들어오는 경로도 버튼 메서드의 조건으로 막는다. 강제 3D 복귀·퇴장·포커스 상실 등의 기존 정리는 유지한다.

## 통합 상태

스크립트3개에 입력 점유를 연결했다. 소스 대조에서 CubeBuilder의 변경은 읽기 전용 프로퍼티뿐이며 ModulePaletteUI와 D01 취소 코드는 이번 단위에서 바뀌지 않았음을 확인했다. 수정한 스크립트3개를 AssetDatabase.ImportAsset으로 각각 임포트했다. 테스트·플레이·빌드는 실행하지 않았다. 기존 스테이징을 보존하며 커밋은 만들지 않는다.

## 정적 검토와 남은 경계

Main의 UIManager와 ModulePaletteUI는 같은 Builder 인스턴스(2087899907347993064)를 직접 참조한다. 해당 인스턴스의 Player 프리팹에서 WorkshopSession.playerCamera와 CubeBuilder는 같은 카메라 오브젝트(4001256099456420255)에 연결된다. 따라서 Main의 상태 원본 연결은 직렬화 소스로 대조했다. 복수 카메라가 남은 테스트 씬의 자동 탐색과 런타임 동작은 별도 확인 대상이다.

오른쪽 버튼 릴리스 콜백이 같은 프레임의 슬롯 BeginDrag보다 먼저 실행되면 드래그 시작 전 회전의 완료 신호가 먼저 전달될 수 있다. 지속 중인 슬롯 드래그는 현재 가드로 차단하며, 이 경계를 위해 UI에서 카메라를 직접 호출하는 연결은 추가하지 않았다.

후속 D06 조사에서 앞선 포커스 정리 지적을 정정했다. OnDisable/OnApplicationFocus(false)는 기존 CancelPreview를 호출하고, CancelPreview는 CancelStroke를 호출한다. 따라서 액션 비활성화 전에 경로·구독이 이미 정리되며 중복 취소 코드를 추가할 필요가 없다. D02 때문에 스트로크 확정·결제·Undo 코드를 변경하지 않았다.
