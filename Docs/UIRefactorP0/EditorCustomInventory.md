# 총기 에디터·커스텀 기능 보존 목록

## 기준과 확인 수준

HEAD `62f7a9a`, 현재 브랜치 `feature/bs_patch`. 워커 초안의 잘못된 API·입력·동작·D번호 설명을 제외하고 관리자가 현재 소스에서 다시 통합했다. 아래는 이관할 기능 가족 목록이며 플레이 확인 완료가 아니다.

- **코드**: 아래 기재한 현재 함수·분기를 정적으로 대조했다. 기능 가족 안의 모든 조합을 실행 확인한 것은 아니다.
- **연결**: [SourceIndex](SourceIndex.md)의 직접 컴포넌트 참조와 기존 [층 버튼 조사](../UIRefactorDifferences.md)를 따른다. 전체 씬의 실효 오버라이드·모든 직렬화 필드 완결성은 P3 대조 전이다.
- **구현/플레이**: 새 MVP 구현·씬 이관은 미적용, 플레이는 모두 미확인이다.
- 공통 근거 경로는 `Assets/Scripts/`다. 표의 `파일:줄`은 현재 소스에서 읽기 시작할 위치다. 근거 없는 새 클래스·메서드를 현재 API로 적지 않는다.

## 파일과 수정 경계

- P2-C: 모듈·슬롯·시점·이름·공유·프리셋·레시피 UI. `CubeBuilder.cs`와 현재 `ModulePaletteUI.cs`의 수정 소유자도 C 한 명이다.
- P2-D: 탄색·도색·스티커·키링 View/Presenter. 기존 큰 파일에서 분리한 뒤 독립 소유한다. 분리 전 같은 파일을 C/D가 동시에 수정하지 않는다.
- 도메인은 기존 `WorkshopSession`, `WorkshopPlanarView`, `CubeBuilder`, `CubeAssembly`, 각 커스텀 History를 재사용한다.
- 공통 흐름·포커스는 P1-A, 확인창·알림은 P1-B, 주문/호버는 P2-B, 습격 보상 표시와 전투 HUD는 P2-E, 튜토리얼 제한은 P2-F와 연결한다.
- `WorkshopStatsUI`는 직접 부착이 확인되지 않은 레거시 후보로 보존한다. 자동 재활성화·삭제하지 않는다.

## 편집·슬롯·시점: P2-C

EDIT-004~005의 모드/축 표시·요청, EDIT-007의 층 버튼·D09, EDIT-025 이름, EDIT-030 레시피는 [P2 병렬 이관](P2ParallelWaveImplementation.md)에 표시/정책 분리를 반영했다. 카메라·조립 도메인은 기존 경로를 사용한다. EDIT-002~003/006/008·015의 D02 점유와 드롭·교환은 기존 구현을 유지하며 모듈 목록/커스텀의 전체 MVP·프리팹 이관과 플레이 대조는 후속이다.

| ID | 현재 보존 대상 | 취소·실패·확정 정책 | 근거 |
| --- | --- | --- | --- |
| EDIT-001 | 작업대 무기 교환·편집 진입·퇴장, 카메라 전환과 장착 복귀 | 진입 가능·코어·상호작용 조건 재검사. 튜토리얼 퇴장 제한과 종료 정리 유지 | WorkshopSession.cs:113,120,133,159 |
| EDIT-002 | UI 밖에서 시작한 3D 트랙볼 회전 | 기존 시작/종료 조건 유지. D02에 따라 슬롯 드래그 중 새 조작 차단 | WorkshopSession.cs:234,236,248,256 |
| EDIT-003 | 3D 휠 줌과 작업대 퇴장 시 원래 FOV 복원 | 모달·UI·편집 조건과 범위 유지 | WorkshopSession.cs:312,325 |
| EDIT-004 | 3D/2D 전환과 기존 투영·회전·위치 복원 | 2D 최초 축·0층·팬 초기화. 3D 복귀 시 저장한 상태 복원 | WorkshopPlanarView.cs:76,133 |
| EDIT-005 | 6방향 축 선택·축 메뉴·축 그래픽과 클릭 영역 정렬 | 축 변경은 0층·팬 초기화, 줌 유지. 튜토리얼 허용 검사 유지 | WorkshopViewUI.cs:53; WorkshopPlanarView.cs:103 |
| EDIT-006 | 2D 팬·휠 줌 | UI 밖 뷰포트에서 시작. 팬 해제·포커스 상실·모달 조건 유지 | WorkshopPlanarView.cs:168 |
| EDIT-007 | Ctrl+휠 층 이동, 선택 층 수·참고 셀 표시 | 같은 휠을 줌에 쓰지 않음. D09는 기존 층 버튼도 재표시. 비선택 층은 forceRenderingOff로 숨김 | WorkshopPlanarView.cs:39,168; WorkshopPlanarLayerPresentation.cs:47 |
| EDIT-008 | R 프레이밍 복원 | 2D는 층·축 유지, 팬·줌만 복원. 축 변경·3D 복귀와 통합하지 않음 | WorkshopSession.cs:76; WorkshopPlanarView.cs:158 |
| EDIT-009 | 10개 즐겨찾기와 숫자키 선택, 선택 모듈·해금 표시 | 배열 위치와 모듈 ID를 혼동하지 않음. 실제 선택 ID 보존 | CubeBuilder.cs:38,183,234; ModulePaletteUI.cs:409 |
| EDIT-010 | E의 ToggleDelete 액션으로 삭제 모드 전환 | 입력칸·모달·편집 상태 가드 유지. X로 임의 변경하지 않음 | InputSystem_Actions.inputactions의 Build/ToggleDelete; CubeBuilder.cs:301 |
| EDIT-011 | Tab 전체 모듈 목록 열기·닫기와 카테고리·가격·잠금 표시 | D01 라우터는 진행 드래그 취소 → 목록 닫기 → 작업대 퇴장 순으로 한 단계만 소비 | CubeBuilder.cs:164,301; ModulePaletteUI.cs:542; UICancelRouter.cs |
| EDIT-012 | 목록에서 즐겨찾기로 드래그 등록 | 유효한 드롭 때만 SetFavorite. 같은 모듈·잘못된 대상·튜토리얼 제한은 무변경 | ModulePaletteUI.cs:436,465,474,505 |
| EDIT-013 | 즐겨찾기끼리 드래그 교환, 목록 닫힌 상태에서도 교환 | 드롭 때 SwapFavorites. 선택 모듈 ID 유지 | ModulePaletteUI.cs:505; CubeBuilder.cs:205 |
| EDIT-014 | 목록 모듈 호버+숫자키로 즐겨찾기 등록 | 일반 숫자키 선택과 구분하고 해금·튜토리얼 제한 유지 | CubeBuilder.cs:37,38 |
| EDIT-015 | 슬롯 드래그 고스트·대상 강조·바깥 드롭·중단 | UI 밖에서도 유지. 바깥 드롭·원본 변경·목록 닫기·포커스 상실·모달/잠금은 취소. D01 ESC 단독 취소 연결. D02 시점·초기화/축/층 입력 점유는 기존 CubeBuilder 드래그 상태를 공유 | [D02 구현 범위](P2CDragOwnershipImplementation.md); ModulePaletteUI.cs; CubeBuilder.cs |
| EDIT-016 | 3D 단일 설치와 필요한 출력 방향 미리보기 | 공급·점유·해금·튜토리얼·비용 검증, 유효한 확정 때 실제 설치 | CubeBuilder.cs:516,623,673,716 |
| EDIT-017 | 단일 삭제와 연결 끊긴 블록 처리·환불 | 삭제 대상·튜토리얼 제한과 Undo 기록 유지 | CubeBuilder.cs:597,795; CubeAssembly.cs:103 |
| EDIT-018 | 2D 연속 설치/삭제의 시작·릴리스·256셀 상한 | 256셀 초과는 알림 후 전체 스트로크 취소. UI 진입·뷰포트/층/축/모드/대상 변경·포커스 상실 등은 취소 | CubeBuilder.PlanarStroke.cs:35,67,102 |
| EDIT-019 | 4연결 선분 보간과 되짚기 | 설치는 이미 지난 셀로 돌아가면 그 뒤 경로 축소. 삭제는 지나간 대상 유지 | CubeBuilder.PlanarStroke.cs:102 |
| EDIT-020 | 경로 꺾임에서 새 셀을 연장 모듈로 치환 | followsFeed 조건과 실제 공급·다음 방향이 수직일 때 partSlot=2. 기존 점유 셀은 건드리지 않음. 스코프·트리거·기본 블록 제외 및 실제 연장 해금/비용 검사 | WorkshopPlacementPlan.cs:22-93 |
| EDIT-021 | 연속 설치·삭제의 한 번 결제/환불·한 건 Undo | 커밋 전 조건 재검사. 설치 도중 실패는 레이아웃 복구. 삭제는 직접 대상별 튜토리얼 검사와 연결 끊김 포함 | CubeBuilder.PlanarStroke.cs:143,163 |
| EDIT-022 | 연속 배치 비용 부족 피드백 | D06 반영: 실패 종류를 구분하고 경로 취소 후 공통 대출 안내. 부품 부족 우선, 승인 후 직접 재배치·자동 설치 없음 | [D06 구현 범위](P2CStrokeFundsImplementation.md); WorkshopPlacementPlan.cs; CubeBuilder.PlanarStroke.cs; CubeBuilder.cs |
| EDIT-023 | Ctrl+Z와 조립 Undo 최대 3건 | 스트로크 중에는 스트로크만 취소. 일반 Undo는 저장 레이아웃·지갑 상태 복원이며 대출 차이 반영. 커스텀 중에는 도구별 Undo로 분기 | CubeBuilder.cs:795,818 |
| EDIT-024 | 구조 상태·조립 완료/커스텀 진입 가능 여부 표시 | UI가 자체 구조 판정을 복제하지 않음. 기존 도메인의 유효 상태를 사용 | StructuralFailureWarning.cs; CubeBuilder.cs:849 |
| EDIT-025 | 무기 이름 입력·저장·40자 정리·빈 이름 처리 | 저장 때 반영. CancelEditing은 원래 이름 복원·포커스 해제. 대상 변경 시 입력 대상 갱신 | WeaponNameUI.cs:27,36,42,49,70 |
| EDIT-026 | WSC: 공유 코드 열기·복사 | Export 예외 안내, 편집 대상 변경 시 Close, 닫기·ESC 유지. 접두사·기존 codeId 보존 | WeaponCodeUI.cs:28,36,50,58; WeaponShareCode.cs:139,141 |
| EDIT-027 | 공유 코드 검증·가져오기·차액 결제/환불 | 기존 조립 캡처 후 확인창. 확인 때 대상·잠금 재검사, 실제 RestoreLayout과 결제 결과 뒤 갱신. 실패·취소는 기존 상태 유지 | WeaponCodeUI.cs:69; WeaponShareCode.cs:141,213,233 |
| EDIT-028 | 프리셋 저장·불러오기·선택창 | 빈 슬롯은 즉시 저장. 채워진 슬롯은 저장/불러오기 선택. 불러오기 시 잠금·비용·대상 조건 재검사, 교체/편집 종료 시 닫음 | PresetPanelUI.cs:34,52,67,94 |
| EDIT-029 | 무게·판매가·스탯·태그와 습격 보상 표시 | WorkshopTagsUI와 P2-E의 임시 보상 표시 연결을 분리해 누락 방지 | WorkshopTagsUI.cs; NightRaidUI.cs |
| EDIT-030 | 레시피 인라인 열기·접기와 높이 조절 | 현재 ESC로 레시피 닫는 경로 유지. D04를 근거로 이를 제거하지 않음 | WeaponRecipeUI.cs; GameManager.cs:89 |
| EDIT-031 | 외형 주문의 허용 영역 가이드 | 주문·편집 종료 시 표시 정리. 새 UI 오브젝트 생성 금지와 월드 가이드 렌더링은 구분 | OrderShapeGuide.cs |
| EDIT-032 | 목록·슬롯·블록 호버 정보와 가격/태그 표시 | 드래그·해금·대상 변화에 따른 숨김과 정보 갱신 유지. P2-B 호버 화면과 연결 | ModuleSlotUI.cs; ModulePaletteUI.cs:257,428; HoveringUI.cs |

## 탄색·도색·스티커·키링: P2-D

| ID | 현재 보존 대상 | 취소·실패·유지 범위 | 근거 |
| --- | --- | --- | --- |
| CUSTOM-001 | 탄색 모듈용 ColorPickerUI 열기·현재 HSV 표시 | 실제 builder.ColorPickerOpen 기준. 단순 도색 브러시와 별개 | ColorPickerUI.cs:44; CubeBuilder.cs:234,268 |
| CUSTOM-002 | SV·Hue 조절·미리보기·확인/취소 | ConfirmModuleColor와 CancelModuleColor 분리. 텍스처 계산은 코드 허용, 조작 요소는 프리팹으로 이관 | ColorPickerUI.cs:58,61,68,77,90 |
| CUSTOM-003 | 커스텀 진입 가능 상태와 버튼 표시 | builder.CanStartCustom 등 실제 상태 사용. UI에서 잠금/구조 조건 재구현하지 않음 | ModulePaletteUI.cs:257,822,1656 |
| CUSTOM-004 | 확인 후 조립 완료 잠금·커스텀 진입 | ShowChoice 표시 실패·취소는 진입하지 않음. BeginCustomMode 성공 뒤 도구·페이지 임시 상태 초기화 | ModulePaletteUI.cs:1656,1670; CubeBuilder.cs:849 |
| CUSTOM-005 | 확인 후 커스텀 제거·조립 편집 복귀 | RemoveCustomAndResumeEditing 성공에 따라 표시 변경. 도색·스티커·키링 정리와 잠금 해제는 도메인 소유 | ModulePaletteUI.cs:1663,1697; CubeBuilder.cs:996 |
| CUSTOM-006 | 도색·스티커·키링 패널 선택과 커스텀 탐색 | 현재 선택 패널·도구 상태·표시를 함께 이관. 등록된 커스텀 패널 연결 유지 | ModulePaletteUI.cs:1405,1440,1475,1487 |
| CUSTOM-007 | 재질 목록·페이지·선택 브러시 | 재질 선택만으로 무기 전체 도색하지 않음. 기존 옵션을 표로 옮기되 내용 유지 | ModulePaletteUI.cs:808,1713,1719,1727; PaintMaterialLibrary.cs:30 |
| CUSTOM-008 | 커스텀 RGB 입력·색상 영역·Hue바·색 선택·재질 목록 복귀 | SelectCustomColor 뒤 브러시에 반영. Hex 입력/최근 색상 목록은 현재 기능으로 확인되지 않았으므로 추가하지 않음 | ModulePaletteUI.cs:1748-1775,1812-1866 |
| CUSTOM-009 | 블록의 선택 면 도색 | UI 위·유효 표면 없음·코어·탄색 모듈 등 기존 거부 조건 유지 | CubeBuilder.cs:1033,1130,1204; WeaponPaintHistory.cs:28 |
| CUSTOM-010 | 채우기 모드의 연결된 동일 표면 도색 | 명시적인 SetPaintFillMode와 FillPaintRegionAtSelection 경로. 더블클릭 기능으로 임의 설명하지 않음 | CubeBuilder.cs:946,1072; WeaponPaintHistory.cs:76 |
| CUSTOM-011 | 전체 노출 면 도색 | 내부 면·코어·탄색 모듈 제외 등 기존 조건 유지. 한 작업의 History 기록 유지 | CubeBuilder.cs:1217; WeaponPaintHistory.cs:42 |
| CUSTOM-012 | 도색 Undo와 커스텀 제거 시 도색 정리 | 실제 적용 결과는 화면 닫기로 되돌리지 않음. UI 작업으로 별도 전체 도색 삭제 기능을 추가하지 않음 | CubeBuilder.cs:961,996; WeaponPaintHistory.cs:136,156 |
| CUSTOM-013 | 스티커 목록·선택·표면 부착, 최대 10개 | 유효 표면·리소스·개수 검사. 배치 거부 시 실제 스티커 추가 없음 | ModulePaletteUI.cs:780,1740; CubeBuilder.cs:1085; WeaponStickerHistory.cs:9,39 |
| CUSTOM-014 | 마지막 활성 스티커 좌우 반전·크기 조절 | 대상 없음/한계에서 변경 거부. 기존 0.25~2 배율·0.25단계 유지 | CubeBuilder.cs:920,927; StickerOverlay.cs:10; WeaponStickerHistory.cs:68 |
| CUSTOM-015 | 스티커 지우개 모드·개별 제거·Undo | 소유하지 않은/이미 제거된 대상 거부, Undo가 제거/부착을 구분 | CubeBuilder.cs:934,969,1144; WeaponStickerHistory.cs:80,104 |
| CUSTOM-016 | 키링 모델 목록·선택·표면 부착, 최대 1개 | 잠금된 현재 무기·부착 프레임·개수 검사 유지. 리소스가 없으면 빈 상태 표시 | ModulePaletteUI.cs:789,801,1577; CubeBuilder.cs:902,1109; WeaponKeyringHistory.cs:9,25 |
| CUSTOM-017 | 키링 제거·Undo·배치 선택 취소 | 실제 제거와 임시 선택 취소를 구분. 보유 키링·배치 상태 표시 갱신 | CubeBuilder.cs:914,977,985; WeaponKeyringHistory.cs:49,63 |
| CUSTOM-018 | 편집 결과 저장·복원과 공유 코드의 데이터 범위 | GameSaveController의 실제 저장·습격 왕복 경로와 대조 필요. 공유 코드/프리셋에 커스텀 전체가 포함된다고 추정하지 않음 | StartMenu_UI/GameSaveController.World.cs; WeaponShareCode.cs |
| CUSTOM-019 | 커스텀 중 작업대 종료·비활성·포커스 상실 정리 | 도구·미리보기·입력 점유는 정리하고 적용 결과는 보존. ESC의 새 단계별 복귀를 임의 추가하지 않음 | CubeBuilder.cs:394,433; ModulePaletteUI.cs:226,529 |

## 이관 전에 남겨 둘 확인

- P2-C/D는 실제 초기화·구독·해제와 각 버튼의 프리팹/씬 연결을 기능 ID별로 대조한다. 현재 파일 분류와 동작 목록을 모든 직렬화 필드 검증 완료로 표시하지 않는다.
- 슬롯·모듈 ID와 codeId/GUID를 유지하고 새 표에서 기존 배열 위치를 새 번호로 재배정하지 않는다.
- 현재 ModulePaletteUI는 모듈 핫바/목록을 런타임 구성하고 커스텀 UI에는 작성된 참조를 연결하는 경로가 섞여 있다. 모든 커스텀 UI가 런타임에 새로 생성된다고 단정하지 않는다. Build/BindCustomUI/BakeCustomUI를 구분한다.
- 새 전체 화면·고정 슬롯·색상 조작 요소는 프리팹으로 옮긴다. 가변 재질·스티커·키링 행은 작성된 항목 프리팹만 반복 사용한다.
- 별도 범용 인터페이스 5종이나 우선순위 프레임워크를 이 문서에서 확정하지 않는다. [Contracts](Contracts.md)의 화면 단위 MVP와 공통 정책을 따른다.
