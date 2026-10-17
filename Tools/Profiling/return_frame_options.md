# 습격 복귀 프레임(1,671.9ms, frame 157) 씬 언로드 비용 절감 방안(읽기 전용 조사)

## 1. 복귀 흐름(현행)
- 결과 UI 확인 → `ReturnHome`(NightRaidController.cs:471): `NightRaidTransfer.PrepareReturn`으로 지갑·모듈·무기 스냅샷 확정(동기·저렴) → `SetNight(true)`, `ShowReturn(false)`, `PoolManager.ClearActive()`(:485, 살아있는 풀 오브젝트만 반납) → `LoadSceneAsync(homeScene, Single)`(:487).
- Single 활성화 프레임(frame 157)에 동시 발생: 습격 씬 언로드 Destroy 8,539회 251.5ms(그중 DestructibleTarget.OnDestroy·OnDisable 각 3,602회 ≈16.5ms), Main Integrate 12.4ms(할당 132.8MB), Main `GameSaveController.Start`의 체크포인트 복원 8.2ms, Instantiate 280회, 엔진 유발 GC.Collect 213.6ms(코드에 명시 호출 없음). EditorLoop 582ms·RenderLoop 604ms는 에디터 병행 오버헤드.
- 이 경로엔 로딩 화면이 없다. `NightRaidUI.SetLoading`은 지도 버튼만 잠그고 DayTransitionUI 페이드는 Main 활성화 다음 프레임(`AdvanceAfterRaidNextFrame`)에 시작. UIManager·NightRaidController는 씬마다 사본이고 DontDestroyOnLoad는 SoundManager뿐이라 전환 동안 살아남는 오버레이가 없음.

## 2. 방안 비교
| 방안 | 효과 | 위험 | 고칠 범위 |
|---|---|---|---|
| A. 로딩 화면 먼저 → 습격 씬을 Additive 유지 상태에서 `UnloadSceneAsync` → Main 로드 | Destroy 비용을 오버레이 뒤 여러 프레임으로 분산. 체감 개발 최대 | 가장 큼. 전환 중 살아남는 오버레이 신설 필요, Additive 해제 시 Awake/Start 순서와 씬별 UIManager·컨트롤러 사본 정리, NightRaidTransfer 소비 시점 재검증 | 신규 리턴 로더 클래스 + NightRaidController.ReturnHome |
| B. `allowSceneActivation=false` + 페이드로 활성화 지연 | 준비를 백그라운드에서 끝내고 언로드+Integrate 프레임을 검은 화면 뒤로 숨김. 구현 최소 | 스파이크 총량은 그대로(숨김만). RaidUI 캔버스를 오버레이로 재사용 시 UI 정리 순서 확인 | NightRaidController.ReturnHome, NightRaidUI(.Result) |
| C. 습격 씬 정적 오브젝트 축소 | 복귀 GO 7,402중 97%가 건물 벽·장식. 파괴 불가 장식 통합·콜라이더/렌더러 정리 시 Destroy 횟수와 언로드 ms 직접 감소 | 메시 합치기는 AGENTS.md상 사용자 승인 필요. DestructibleTarget 벽(3,587)은 파괴 로직 때문에 통합 불가, 장식 한정 | 습격 씬 7개(.unity 씬 편집 위주, 코드 거의 없음) |
| D. 로딩 중 명시 `GC.Collect` | frame 157의 GC.Collect 213.6ms는 141.7MB 프레임 할당 후 엔진 유발. 오버레이 뒤 호출로 체감 프레임에서 제거 | GC 비용 총량은 그대로 1회. 단독으론 무의미, B/A 오버레이와 함께 | B/A 경로에 호출 1줄 |
| E. 풀 오브젝트를 언로드 전 반납·비활성화 | 이미 `PoolManager.ClearActive`가 ReturnHome에서 실행 중. 풀 물량은 Destroy 8,539에서 비중 작음 | 없음 | 추가 없음(이미 구현) |

## 3. 습격 씬 계층 상위(오브젝트 수, NightRaid_JungleCampus 기준)
1. JungleBuilding 7,258(내부 F3 3,254 / F2 2,804 / F1 1,199; DestructibleTarget 3,587·MeshRenderer 3,639·BoxCollider 3,610 포함)
2. CrateSpawnManager 71
3. EndChest 27
4. StandardExitPerimeter 13
5. PerimeterFence_100x100 9
6. NightRaidArea 6
7. ModuleCrateSpawnManager 3
8. RaidEnemySpawnManager 2
9. StructuralSupportSettings 1
10. PlayerSpawn 1
- 루트 17개 합계 약 7,398. 참고: Millitary_base 12,470 > JungleCampus 7,402 > BankGrand 3,284 > Bank 2,293 > WoodenHouse·Tutorial 1,059 > Airport 310 > Tower 114.

## 4. 권장 순서
1. **B+D**(페이드 오버레이 + 오버레이 뒤 GC.Collect): 변경 최소. 복귀 저장·복원 흐름 무영향 — PrepareReturn이 LoadSceneAsync 전에 이미 끝나기 때문.
2. **C**(파괴 불가 장식·콜라이더/렌더러 정리): 씬 데이터 작업. 메시 합치기는 사용자가 배치를 승인한 뒤 별도 작업으로.
3. **A**(Additive 언로드): 구조 변경이 가장 커서 마지막. 게임 흐름 영향: `NightRaidTransfer` 스냅샷 소비(TryConsumeReturn 1회성)와 다음 날 시작(`AdvanceAfterRaidNextFrame`→`StartNextDay`)이 Main 활성화 시점에 묶여 있어 A는 이 두 지점 검증 필수. B·D·C는 흐름 무영향.
- 검증 참고: frame 157의 절반가량이 EditorLoop·RenderLoop이므로 빌드에서 재측정 필요.
