# 씬 리셋·재시작 프레임 1963ms 원인 조사 (SPIKE #1, frame 347)

## 1. 프레임 상황 추정
- 결론: 습격(야간 레이드)에서 공방(Main) 복귀 프레임. 이전 습격 씬 언로드 + 공방 씬 로드 + 저장 복원 + 다음 날 전환이 한 프레임에 모두 실행됨.
- 경로: `NightRaidController.ReturnHome` Assets/Scripts/NightRaidController.cs:457 → `PoolManager.ClearActive`(:484) → `SceneManager.LoadSceneAsync(homeScene, Single)`(:487) → 씬 활성화 프레임에 `GameSaveController.Start` Assets/Scripts/StartMenu_UI/GameSaveController.cs:156 → `InitializeSaveState`(:162) → `TryConsumeReturn`(:169) → `RestoreWorldSaveCore` Assets/Scripts/StartMenu_UI/GameSaveController.World.cs:174 → `AdvanceAfterRaid(true)` GameSaveController.cs:211 → `BeginReturn` Assets/Scripts/DayTransitionUI.cs:49 → `StartCoroutine(Advance)`(:55) → 코루틴 첫 MoveNext가 동기 실행 → `Covered?.Invoke()`(:61) → `day.StartNextDay()`(:62, _DayManager.cs:343).
- 근거: `DayTransitionUI.Advance`는 `BeginReturn`(alreadyCovered=true)로만 페이드 없이 즉시 StartNextDay에 진입하고, 동일 프레임에 `GameSaveController.Start`와 PlacedBlock 190개 복원(OnEnable)이 있음 → 새로 활성화된 Main 씬의 Start가 습격 체크포인트를 복원한 것. 다음 날 전환만으로는 씬 로드가 없고(StartNextDay는 씬을 안 바꿈), 신규 시작은 복원이 없어 PlacedBlock OnEnable이 발생하지 않으므로 배제.
- SPIKE #3(frame 981, 453.9ms)는 에디터 플레이 모드 종료 프레임(BurstLoader/InputExitPlayModeAnalytic의 playModeStateChanged)으로 게임 코드와 무관.

## 2. 원인 표
| 항목 | ms | 원인 경로(파일:줄) | 개선 방향 |
|---|---|---|---|
| Destroy 8026회 | 208.9 | NightRaidController.cs:487 Single 로드 → 이전 습격 씬 전체 파괴(스폰분 포함 ≈8,026: NightRaid_JungleCampus.unity 7,402 GO) | 습격 씬 오브젝트 수 축소(정적 배치 통합·프리팹화), 전환을 페이드 뒤 비동기 유지 |
| GC.Collect 2회 | 223.0 | 프레임 GC 47.3MB(Integrate 33.5MB + Start 7.9MB)에 에디터 GC 일시정지 | 아래 할당원 제거가 근본책, 복원 전체를 로딩 화면 뒤로 |
| GameSaveController.Start | 165.3 | GameSaveController.cs:156→169→World.cs:174. `Resolve` 선형탐색(World.cs:88), paint마다 블록 전체 재스캔(World.cs:229), 무기별 `TryCaptureLayout` LINQ OrderBy/SequenceEqual(CubeAssembly.cs:189), `InScene` FindObjectsByType(World.cs:85) | Resolve를 Dictionary 캐시, cell→블록 사전 캐시, LINQ 비교를 직접 루프로, 복원을 무기당 프레임 분산 |
| PlacedBlock.OnEnable 190회 | 107.1 | PlacedBlock.cs:93→95 `IgnorePlayerCollisions` EquipableWeapon.cs:173: 블록마다 무기 콜라이더 전수 수집(:175)+`FindObjectsByType<FirstPersonPlayer>`(:176)+플레이어 콜라이더 약 43개×`GetComponentInParent` 실패(:181)×Physics.IgnoreCollision(:183) | 복원 중 호출 억제 후 무기 완성 시 1회만 실행, 플레이어 콜라이더 목록 캐시, 레이어 단위 IgnoreLayerCollision 전환 검토 |
| LoadLevelAsync Integrate | 24.8 | Main.unity 역직렬화·통합(엔진, 할당 33.5MB) | 씬 경량화, 페이드 뒤 배치 유지 |
| DayTransitionUI.Advance | 21.7 | DayTransitionUI.cs:55→58→62 → StartNextDay(_DayManager.cs:343, PrepareDay:369/ApplyMorning:391 동기) | StartNextDay 단계 분산 또는 결과 UI 갱신 지연 |
| GetComponentNullErrorMessage 8136회 | 10.6 | EquipableWeapon.cs:181 플레이어 콜라이더에서 EquipableWeapon 미발견≈190×43 | 위 OnEnable 개선으로 소멸 |
| FindObjectsOfType 207회 | 12.2 | 그중 190회=EquipableWeapon.cs:176(블록 OnEnable마다 전 씬 탐색) | 플레이어 참조 캐시 |
| Instantiate 287회 | 24.4 | 무기 Instantiate(World.cs:217)+블록 CreateBlock(CubeAssembly.cs:200,280)+타깃 벨 복원(TargetBell.cs:88) | 불가피, 프레임 분산으로 흡수 |
| EditorLoop / RenderLoop | 419.1 / 437.2 | 에디터 자체 오버헤드·렌더 스레드 첫 프레임 구성(Gfx.CreateTexture 90회). 메인과 병행이라 실중첩 작음 | 빌드 프로파일링으로 재측정 |

## 3. 요약
- 1963ms의 절반가량(EditorLoop 419ms+RenderLoop 437ms)은 에디터/렌더 병행 오버헤드이고, 실제 게임 원인은 ①습격 씬 언로드 파괴 208.9ms, ②복원 중 GC 223ms, ③저장 복원 165.3ms, ④블록 190개 OnEnable마다 전체 콜라이더 재설정 107.1ms 순.
- 최우선 개선은 `PlacedBlock.OnEnable`의 `IgnorePlayerCollisions` 호출 억제(복원 완료 후 무기당 1회)와 `RestoreWorldSaveCore` 탐색 캐싱이며, 씬 오브젝트 수 축소가 그 다음.

## 4. 기타
- 파일별 변경: 없음(읽기 전용 조사). 동작 변화: 없음. 인스펙터 값: 없음.
- 남은 위험: Destroy 8026의 정체는 씬 GO 수와의 일치로 추정한 것(정확 파악엔 프로파일러 계층 데이터 필요). 에디터 프레임이라 빌드 수치는 더 작을 것. GC.Collect가 명시 호출인지 내부 유발인지는 원본 데이터로 재확인 권장.
- 컴파일 결과: 코드 수정 없음(확인 불요).
- README 제안: `Tools/Profiling/reset_frame_report.md`에 습격 복귀 프레임 1963ms 원인 조사 결과 기록(PlacedBlock.OnEnable의 IgnorePlayerCollisions 반복 호출이 주원인 중 하나).
