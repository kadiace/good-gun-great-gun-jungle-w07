# UI 소스와 직접 직렬화 참조 색인

현재 파일과 m_Script GUID에서 추출한 색인이다. 동작·씬 실효 오버라이드·런타임 생성 결과를 확인했다는 뜻이 아니다. 경로를 찾는 용도이며 각 기능의 정상·취소 경로는 영역 목록을 따른다.

| 소스 | 담당 | 직접 컴포넌트 참조 | 이관 메모 |
| --- | --- | --- | --- |
| [BreakWithWall.cs](../../Assets/Scripts/BreakWithWall.cs) | P2-E | `Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [ButtonHoverOutline.cs](../../Assets/Scripts/ButtonHoverOutline.cs) | P1-B | 직접 참조 없음/추가 분류 대상 | 공유 표시 보조, 여러 영역이 임의 수정하지 않음 |
| [ChargeGaugeUI.cs](../../Assets/Scripts/ChargeGaugeUI.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [ColorPickArea.cs](../../Assets/Scripts/ColorPickArea.cs) | P2-D | 직접 참조 없음/추가 분류 대상 | 영역 UI·연결부 |
| [ColorPickerUI.cs](../../Assets/Scripts/ColorPickerUI.cs) | P2-D | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [CoreDispenser.cs](../../Assets/Scripts/CoreDispenser.cs) | P2-B | `Assets/Prefabs/Map/CoreDispenser.prefab`<br>`Assets/Prefabs/Presets/PresetDispenser.prefab`<br>`Assets/Scenes/Main.unity` | 영역 UI·연결부 |
| [CubeBuilder.cs](../../Assets/Scripts/CubeBuilder.cs) | P2-C | `Assets/Prefabs/Player/Player.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/Test/KYGTest.unity`<br>`Assets/Scenes/Test/Customer.unity`<br>`Assets/Scenes/Test/Time_Scene.unity`<br>`Assets/Scenes/Test/ProjectileTest.unity`<br>`Assets/Scenes/Test/PreSet.unity`<br>`Assets/Scenes/Test/KYGTest3.unity`<br>`Assets/Scenes/Test/KYGTest2.unity` | 실제 편집 도메인, D가 필요한 변경은 C 담당자와 조율 |
| [CustomerRequirementsUI.cs](../../Assets/Scripts/CustomerRequirementsUI.cs) | P2-B | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [DamageDirectionIndicator.cs](../../Assets/Scripts/DamageDirectionIndicator.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [DaySettlementUI.cs](../../Assets/Scripts/DaySettlementUI.cs) | P2-B | `Assets/Prefabs/UI/DaySettlementUI 1.prefab`<br>`Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [DayTransitionUI.cs](../../Assets/Scripts/DayTransitionUI.cs) | P2-B | `Assets/Prefabs/Managers/UIManager.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/NightRaid/Tutorial_WoodenHouse.unity` | 영역 UI·연결부 |
| [DialogueUI.cs](../../Assets/Scripts/DialogueUI.cs) | P2-F | `Assets/Prefabs/UI/Dialogue.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/NightRaid/Tutorial_WoodenHouse.unity` | 영역 UI·연결부 |
| [EndChestEndingUI.cs](../../Assets/Scripts/EndChestEndingUI.cs) | P2-F | `Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity` | 영역 UI·연결부 |
| [FeedbackUI.cs](../../Assets/Scripts/FeedbackUI.cs) | P1-B | `Assets/Prefabs/UI/StartUI_Manager.prefab`<br>`Assets/Prefabs/Managers/UIManager.prefab` | 확인창·알림, D03/D04 중첩 연결 |
| [GameManager.cs](../../Assets/Scripts/GameManager.cs) | P1-A | `Assets/Prefabs/Managers/GameManager.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/Test/KYGTest.unity`<br>`Assets/Scenes/Test/Customer.unity`<br>`Assets/Scenes/Test/Time_Scene.unity`<br>`Assets/Scenes/Test/ProjectileTest.unity`<br>`Assets/Scenes/Test/PreSet.unity`<br>`Assets/Scenes/Test/KYGTest3.unity`<br>`Assets/Scenes/Test/KYGTest2.unity`<br>`Assets/Scenes/NightRaid/NightRaid_BankGrand.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Bank.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Airport.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Millitary_base.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Tower.unity`<br>`Assets/Scenes/NightRaid/Tutorial_WoodenHouse.unity`<br>`Assets/Scenes/NightRaid/NightRaid_WoodenHouse.unity` | 공통 흐름·초기화 소유 |
| [GoodShotUI.cs](../../Assets/Scripts/GoodShotUI.cs) | P2-B | `Assets/Prefabs/UI/CustomerOutcomeUI 1.prefab` | 현재 직접 부착·호출 여부를 구분하고 비활성 자동 활성화 금지 |
| [HomingLockGraphic.cs](../../Assets/Scripts/HomingLockGraphic.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [HomingRetentionRing.cs](../../Assets/Scripts/HomingRetentionRing.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [HoveringUI.cs](../../Assets/Scripts/HoveringUI.cs) | P2-B | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [InteractionLabel.cs](../../Assets/Scripts/InteractionLabel.cs) | P2-B | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [LockPictogramGraphic.cs](../../Assets/Scripts/LockPictogramGraphic.cs) | P2-C | 직접 참조 없음/추가 분류 대상 | 영역 UI·연결부 |
| [ModulePaletteUI.cs](../../Assets/Scripts/ModulePaletteUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 현 파일 수정 담당은 C, 커스텀 View 분리 후 D로 이관 |
| [ModuleSlotUI.cs](../../Assets/Scripts/ModuleSlotUI.cs) | P2-C | 직접 참조 없음/추가 분류 대상 | 영역 UI·연결부 |
| [ModuleUnlockNotice.cs](../../Assets/Scripts/ModuleUnlockNotice.cs) | P2-B | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [MoneyRollDisplay.cs](../../Assets/Scripts/MoneyRollDisplay.cs) | P2-B | 직접 참조 없음/추가 분류 대상 | 영역 UI·연결부 |
| [MouseSensitivityUI.cs](../../Assets/Scripts/MouseSensitivityUI.cs) | P2-A | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [NextDayInteractable.cs](../../Assets/Scripts/NextDayInteractable.cs) | P2-B | `Assets/Prefabs/Map/NextDayInteractable.prefab`<br>`Assets/Scenes/Main.unity` | 영역 UI·연결부 |
| [NightRaidReturnOverlay.cs](../../Assets/Scripts/NightRaidReturnOverlay.cs) | P2-E | 직접 참조 없음/추가 분류 대상 | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [NightRaidUI.cs](../../Assets/Scripts/NightRaidUI.cs) | P2-E | `Assets/Prefabs/UI/NightRaidUI.prefab`<br>`Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [NightRaidUI.Result.cs](../../Assets/Scripts/NightRaidUI.Result.cs) | P2-E | 직접 참조 없음/추가 분류 대상 | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [OrderShapeGuide.cs](../../Assets/Scripts/OrderShapeGuide.cs) | P2-B | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [PlayerManualReader.cs](../../Assets/Scripts/PlayerManualReader.cs) | P2-B | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [PresetPanelUI.cs](../../Assets/Scripts/PresetPanelUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [QuestInfoTextSizing.cs](../../Assets/Scripts/QuestInfoTextSizing.cs) | P2-B | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [RaidAreaMapGraphic.cs](../../Assets/Scripts/RaidAreaMapGraphic.cs) | P2-E | `Assets/Prefabs/UI/NightRaidUI.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [RaidDeathUI.cs](../../Assets/Scripts/RaidDeathUI.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [RaidHealthUI.cs](../../Assets/Scripts/RaidHealthUI.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [RaidObjectiveUI.cs](../../Assets/Scripts/RaidObjectiveUI.cs) | P2-E | `Assets/Scenes/NightRaid/NightRaid_BankGrand.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Millitary_base.unity`<br>`Assets/Scenes/NightRaid/Tutorial_WoodenHouse.unity`<br>`Assets/Scenes/NightRaid/NightRaid_WoodenHouse.unity` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [RaidRegionHoverUI.cs](../../Assets/Scripts/RaidRegionHoverUI.cs) | P2-E | 직접 참조 없음/추가 분류 대상 | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [ResetScrollOnEnable.cs](../../Assets/Scripts/ResetScrollOnEnable.cs) | P1-B | `Assets/Prefabs/Managers/UIManager.prefab` | 공유 표시 보조, 여러 영역이 임의 수정하지 않음 |
| [RetireUI.cs](../../Assets/Scripts/RetireUI.cs) | P2-F | `Assets/Scenes/Main.unity` | 영역 UI·연결부 |
| [RotationCircleGraphic.cs](../../Assets/Scripts/RotationCircleGraphic.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/Test/Time_Scene.unity`<br>`Assets/Scenes/Test/KYGTest3.unity`<br>`Assets/Scenes/Test/KYGTest2.unity` | 영역 UI·연결부 |
| [ScanLoadingGraphic.cs](../../Assets/Scripts/ScanLoadingGraphic.cs) | P2-C | 직접 참조 없음/추가 분류 대상 | 영역 UI·연결부 |
| [ScopeOverlayGraphic.cs](../../Assets/Scripts/ScopeOverlayGraphic.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [ShootingTestHUD.cs](../../Assets/Scripts/ShootingTestHUD.cs) | P2-B | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [SoundManager.cs](../../Assets/Scripts/SoundManager.cs) | 기존 도메인 | `Assets/Prefabs/Managers/Resources/SoundManager.prefab` | 설정은 기존 API 재사용. UI 작업으로 전체 재작성하지 않음 |
| [SoundSettingsUI.cs](../../Assets/Scripts/SoundSettingsUI.cs) | P2-A | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [SpreadCrosshair.cs](../../Assets/Scripts/SpreadCrosshair.cs) | P2-E | `Assets/Prefabs/Managers/UIManager.prefab` | Main 공통 전투 HUD도 해당 표시 소유권으로 조율 |
| [SaveSlotsUI.cs](../../Assets/Scripts/StartMenu_UI/SaveSlotsUI.cs) | P2-A | `Assets/Prefabs/UI/StartUI_Manager.prefab`<br>`Assets/Prefabs/Managers/UIManager.prefab`<br>`Assets/Scenes/Intro.unity` | 영역 UI·연결부 |
| [Start_UI.cs](../../Assets/Scripts/StartMenu_UI/Start_UI.cs) | P2-A | `Assets/Prefabs/UI/StartUI_Manager.prefab` | 영역 UI·연결부 |
| [StartMenuCoreDisplay.cs](../../Assets/Scripts/StartMenu_UI/StartMenuCoreDisplay.cs) | P2-A | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [StructuralFailureWarning.cs](../../Assets/Scripts/StructuralFailureWarning.cs) | P2-C | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [TagFlowLayout.cs](../../Assets/Scripts/TagFlowLayout.cs) | P1-B | `Assets/Prefabs/Managers/UIManager.prefab` | 공유 표시 보조, 여러 영역이 임의 수정하지 않음 |
| [TagPillGraphic.cs](../../Assets/Scripts/TagPillGraphic.cs) | P1-B | `Assets/Prefabs/Managers/UIManager.prefab` | 공유 표시 보조, 여러 영역이 임의 수정하지 않음 |
| [TargetSelectionUI.cs](../../Assets/Scripts/TargetSelectionUI.cs) | P2-B | `Assets/Prefabs/Managers/UIManager.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/Test/KYGTest3.unity` | 영역 UI·연결부 |
| [TutorialButtonHighlightUI.cs](../../Assets/Scripts/TutorialButtonHighlightUI.cs) | P2-F | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [TutorialQuestUI.cs](../../Assets/Scripts/TutorialQuestUI.cs) | P2-F | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [TutorialSkipUI.cs](../../Assets/Scripts/TutorialSkipUI.cs) | P2-F | `Assets/Prefabs/Managers/UIManager.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/NightRaid/Tutorial_WoodenHouse.unity` | 영역 UI·연결부 |
| [TutorialTargetUI.cs](../../Assets/Scripts/TutorialTargetUI.cs) | P2-F | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [UIFonts.cs](../../Assets/Scripts/UIFonts.cs) | P1-B | 직접 참조 없음/추가 분류 대상 | 공유 표시 보조, 여러 영역이 임의 수정하지 않음 |
| [UIManager.cs](../../Assets/Scripts/UIManager.cs) | P1-A | `Assets/Prefabs/Managers/UIManager.prefab`<br>`Assets/Scenes/Main.unity`<br>`Assets/Scenes/Test/KYGTest.unity`<br>`Assets/Scenes/Test/Customer.unity`<br>`Assets/Scenes/Test/Time_Scene.unity`<br>`Assets/Scenes/Test/ProjectileTest.unity`<br>`Assets/Scenes/Test/PreSet.unity`<br>`Assets/Scenes/Test/KYGTest3.unity`<br>`Assets/Scenes/Test/KYGTest2.unity`<br>`Assets/Scenes/NightRaid/NightRaid_BankGrand.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Bank.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Airport.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity`<br>`Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Millitary_base.unity`<br>`Assets/Scenes/NightRaid/NightRaid_Tower.unity`<br>`Assets/Scenes/NightRaid/Tutorial_WoodenHouse.unity`<br>`Assets/Scenes/NightRaid/NightRaid_WoodenHouse.unity` | 공통 흐름·초기화 소유 |
| [WeaponCodeUI.cs](../../Assets/Scripts/WeaponCodeUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WeaponNameUI.cs](../../Assets/Scripts/WeaponNameUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WeaponRecipeUI.cs](../../Assets/Scripts/WeaponRecipeUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WorkshopAxesGraphic.cs](../../Assets/Scripts/WorkshopAxesGraphic.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WorkshopManual.cs](../../Assets/Scripts/WorkshopManual.cs) | P2-B | 이번 GUID 추출 범위 외, 연결 대조 예정 | 영역 UI·연결부 |
| [WorkshopPlanarDirectionsGraphic.cs](../../Assets/Scripts/WorkshopPlanarDirectionsGraphic.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WorkshopPlanarGridGraphic.cs](../../Assets/Scripts/WorkshopPlanarGridGraphic.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WorkshopPlanarLayerUI.cs](../../Assets/Scripts/WorkshopPlanarLayerUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WorkshopStatsUI.cs](../../Assets/Scripts/WorkshopStatsUI.cs) | P2-C | 직접 참조 없음/추가 분류 대상 | 현재 직접 부착·호출 여부를 구분하고 비활성 자동 활성화 금지 |
| [WorkshopTagsUI.cs](../../Assets/Scripts/WorkshopTagsUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |
| [WorkshopViewUI.cs](../../Assets/Scripts/WorkshopViewUI.cs) | P2-C | `Assets/Prefabs/Managers/UIManager.prefab` | 영역 UI·연결부 |

색인 추출 기반: UI 네임스페이스·포인터 인터페이스를 참조한 65개 스크립트와 별도로 확인한 UI 관련 보조 파일 7개. 보조 파일의 직접 참조는 아직 이 색인에 추가 대조하지 않았으므로 0건으로 단정하지 않는다. partial·정적 유틸·런타임 생성에는 직접 컴포넌트 참조가 없을 수 있다.

## P1-A 첫 구현에서 추가한 파일

| 소스 | 담당 | 연결 | 역할 |
| --- | --- | --- | --- |
| [UIRoot.cs](../../Assets/Scripts/UI/Common/UIRoot.cs) | P1-A | `UIManager.prefab` | 작성된 4레이어와 일시정지 포커스 보관·적용·복원 |
| [PauseView.cs](../../Assets/Scripts/UI/Pause/PauseView.cs) | P1-A → P2-A | `UIManager.prefab` | 패널·버튼 참조, 표시·숨김, Resume Action 전달 |
| [PausePresenter.cs](../../Assets/Scripts/UI/Pause/PausePresenter.cs) | P1-A → P2-A | `UIManager`가 생성하는 일반 C# 객체 | 기존 GameManager 상태를 읽고 View에 표시·Resume 요청 전달 |

이 파일들의 추가만으로 공통 입력·중첩 팝업·다른 화면의 MVP 이관이 끝난 것은 아니다. 연결 및 남은 범위는 [첫 구현 보고](P1AImplementation.md)에 구분한다.

## P1-B 확인창·알림에서 추가한 파일

기존 `FeedbackUI.cs`는 요청자의 공개 진입점을 유지하는 브리지다. 요청 흐름은 `FeedbackUI → FeedbackPresenter → FeedbackChoiceView / FeedbackNotificationView` 순서로 읽는다.

| 소스 | 연결과 역할 |
| --- | --- |
| [FeedbackPresenter.cs](../../Assets/Scripts/UI/Feedback/FeedbackPresenter.cs) | 요청자·콜백·제스처·입력 중단·포커스·종료 결과를 소유하는 일반 C# 객체 |
| [FeedbackChoiceView.cs](../../Assets/Scripts/UI/Feedback/FeedbackChoiceView.cs) | 공용/Intro 프리팹의 확인창 참조·버튼 입력·표시·Navigation |
| [FeedbackNotificationView.cs](../../Assets/Scripts/UI/Feedback/FeedbackNotificationView.cs) | 공용/Intro 프리팹의 알림 참조·표시 연출 |
| [FeedbackConfirmButtonEventRelay.cs](../../Assets/Scripts/UI/Feedback/FeedbackConfirmButtonEventRelay.cs) | 두 프리팹의 Yes 버튼에 작성된 포인터·Submit 이벤트 전달 |
| [FeedbackCloseReason.cs](../../Assets/Scripts/UI/Feedback/FeedbackCloseReason.cs) | 확정·취소·강제 종료 결과 |

기존 `CancelChoice` 호출자의 강제 종료 이관과 정렬 예외는 [P1-B 구현 범위](P1BFeedbackImplementation.md)에 남긴다.

## P1-B 날짜 전환

읽는 순서는 `DayTransitionUI` → 일반 C# `DayTransitionPresenter` → `DayTransitionView`다. [날짜 전환 구현 범위](P1BDayTransitionImplementation.md)를 따른다.

| 소스 | 연결 | 역할 |
| --- | --- | --- |
| [DayTransitionPresenter.cs](../../Assets/Scripts/UI/Transition/DayTransitionPresenter.cs) | DayTransitionUI가 조립 | 날짜 진행 요청·실행 종류·입력 차단 |
| [DayTransitionView.cs](../../Assets/Scripts/UI/Transition/DayTransitionView.cs) | UIManager.prefab | 기존 저작 참조를 사용하는 페이드·Day 제목 연출 |

## P1-B 결과 화면

읽는 순서는 기존 [NightRaidUI.Result.cs](../../Assets/Scripts/NightRaidUI.Result.cs) 조립부 → Presenter → View다. [구현·D07 범위](P1BResultImplementation.md)를 따른다.

| 소스 | 연결 | 역할 |
| --- | --- | --- |
| [ResultPresenter.cs](../../Assets/Scripts/UI/Result/ResultPresenter.cs) | NightRaidUI가 조립 | 표시 요청·확인 콜백·입력 소유·강제 정리 |
| [ResultView.cs](../../Assets/Scripts/UI/Result/ResultView.cs) | NightRaidUI.prefab 원본 루트 | 기존 결과 참조·실제 시간 페이드·버튼·포커스 |

D07은 기존 NightRaidController의 결과 종료 상태와 RaidPlayerHealth의 해당 플레이어 피해 차단으로 연결한다. 결과 UI에 보상/저장 Model을 복제하지 않는다.

## P1-B 대화

읽는 순서는 기존 [DialogueUI.cs](../../Assets/Scripts/DialogueUI.cs) → Presenter → View → 버튼 중계다. [구현·중첩 범위](P1BDialogueImplementation.md)를 따른다.

| 소스 | 연결 | 역할 |
| --- | --- | --- |
| [DialoguePresenter.cs](../../Assets/Scripts/UI/Dialogue/DialoguePresenter.cs) | DialogueUI가 조립 | 응답 콜백·제스처·차단/안내 중첩 상태·대화 상태 요청 |
| [DialogueView.cs](../../Assets/Scripts/UI/Dialogue/DialogueView.cs) | Dialogue.prefab 루트 | 기존 저작 참조·Workshop 액션·Raycast/버튼 차단·포커스 |
| [DialogueConfirmButtonEventRelay.cs](../../Assets/Scripts/UI/Dialogue/DialogueConfirmButtonEventRelay.cs) | Dialogue.prefab의 Answer | 포인터 누름·클릭·Submit을 Action으로 전달 |

## P1-A ESC·D01

읽는 순서는 GameManager.OnPausePerformed/TogglePause → [UICancelRouter.RouteCancel](../../Assets/Scripts/UI/Common/UICancelRouter.cs) → 해당 화면의 취소 진입점이다. [적용 범위](P1ACancelImplementation.md)를 따른다.

- UICancelRouter: GameManager.Awake에서 조립하는 일반 C# 객체. 한 ESC의 소비 순서와 Pause/재개 요청을 결정한다.
- ModulePaletteUI.TryCancelSlotDrag / CubeBuilder.TryCancelPlanarStroke: 기존 드래그 취소 함수로 위임한다.
- SaveSlotsUI.TryCloseForCancel / SoundSettingsUI.TryCloseForCancel: Pause 하위 화면만 닫고 유효한 진입 버튼/기본 선택으로 복귀한다.

## P2-C 슬롯 드래그 입력 점유

[구현 범위](P2CDragOwnershipImplementation.md)는 기존 ModulePaletteUI → CubeBuilder 드래그 상태 전달과 WorkshopSession/WorkshopPlanarView 입력 경로를 연결한다. 슬롯 드롭·교환을 별도로 구현하거나 드래그 상태를 UI/카메라 양쪽에 복제하지 않는다. 전체 에디터 MVP와 프리팹 이관은 별도다.

## P2-C 연속 배치 비용·D06

[구현 범위](P2CStrokeFundsImplementation.md)는 WorkshopPlacementPlan의 검사 결과 → CubeBuilder.PlanarStroke의 릴리스 재검사/취소 → CubeBuilder의 기존 대출 안내 흐름을 연결한다. 실제 대출·구매·지갑은 PlayerWallet이 소유한다. 승인 콜백에서 배치 경로를 재생하지 않는다.

## P2 메뉴·설정·편집 표시

기존 브리지 → 일반 C# Presenter → 구체 View 순서로 읽는다. [적용 범위](P2ParallelWaveImplementation.md)와 함께 본다. 브리지에 남은 직렬화 참조는 씬 오버라이드 호환을 위한 것이며 초기화 전에 View로 전달한다.

| 기존 진입점 | 정책·표시 가공 | 저작된 표시 |
| --- | --- | --- |
| StartMenu_UI/Start_UI.cs | [StartMenuPresenter](../../Assets/Scripts/UI/Menu/StartMenuPresenter.cs), [StartMenuDomainAdapter](../../Assets/Scripts/UI/Menu/StartMenuDomainAdapter.cs) | [StartMenuView](../../Assets/Scripts/UI/Menu/StartMenuView.cs), StartUI_Manager.prefab |
| SoundSettingsUI.cs | [SoundSettingsPresenter](../../Assets/Scripts/UI/Settings/SoundSettingsPresenter.cs) | [SoundSettingsView](../../Assets/Scripts/UI/Settings/SoundSettingsView.cs), UIManager.prefab |
| WeaponNameUI.cs | [WeaponNamePresenter](../../Assets/Scripts/UI/WeaponEditor/Name/WeaponNamePresenter.cs) | [WeaponNameView](../../Assets/Scripts/UI/WeaponEditor/Name/WeaponNameView.cs), UIManager.prefab |
| WeaponRecipeUI.cs | [RecipePresenter](../../Assets/Scripts/UI/WeaponEditor/Recipe/RecipePresenter.cs) | [RecipeView](../../Assets/Scripts/UI/WeaponEditor/Recipe/RecipeView.cs), UIManager.prefab |
| WorkshopPlanarLayerUI.cs | [WorkshopPlanarLayerPresenter](../../Assets/Scripts/UI/WeaponEditor/Layers/WorkshopPlanarLayerPresenter.cs), WorkshopPlanarLayerState | [WorkshopPlanarLayerView](../../Assets/Scripts/UI/WeaponEditor/Layers/WorkshopPlanarLayerView.cs), UIManager.prefab/Main 층 버튼 오버라이드 |
| WorkshopViewUI.cs | [WorkshopOrientationPresenter](../../Assets/Scripts/UI/WeaponEditor/Orientation/WorkshopOrientationPresenter.cs) | [WorkshopOrientationView](../../Assets/Scripts/UI/WeaponEditor/Orientation/WorkshopOrientationView.cs), UIManager.prefab |
| StartMenu_UI/PauseMenuNavigation.cs | [PauseExitPresenter](../../Assets/Scripts/UI/Pause/Exit/PauseExitPresenter.cs) | 기존 FeedbackUI 확인창 재사용, 새 컴포넌트 없음 |

저장/파일 I/O는 기존 GameSaveFiles·GameSaveController, 실제 이름은 EquipableWeapon, 레시피는 TableManager, 카메라·층 변경은 WorkshopPlanarView가 소유한다. 이관된 화면에 별도 원본을 만들지 않는다. 레시피와 층 표시의 프레임 진입은 기존 브리지에 유지해 브리지 비활성 시 갱신하지 않는다.
