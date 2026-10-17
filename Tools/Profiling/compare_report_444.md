# 프로파일러 3차 비교 (수정 전 / 333 / 444)
444는 Profiler 창 버퍼(444.data, last_report.txt SOURCE buffer). 세 녹화 모두 GPU ms 0.0 — GPU 모듈 미녹화, GPU 비용 판단 불가. 녹화 길이·플레이 내용이 달라 절대치 직접 비교에는 한계가 있다.

## 프레임 통계
| 항목 | 수정 전(before) | 333(1차) | 444(2차) |
|---|---|---|---|
| 프레임 수 | 2000 (0..1999) | 982 (0..981) | 865 (0..864) |
| 평균 / median | 14.2 / 8.6ms | 19.7 / 8.8ms | 18.3 / 7.9ms |
| p95 / 최대 | 29.2 / 846.0ms | 39.9 / 1963.5ms | 42.8 / 1671.9ms |
| >33ms | 65/2000 (3.3%) | 77/982 (7.8%) | 99/865 (11.4%) |
| BASELINE cpu/gpu | 8.6/0.0ms (fr 847) | 8.8/0.0ms (fr 796) | 7.9/0.0ms (fr 603) |
| BASELINE Batches | 1644 | 1810 | 1824 |

## 폭발 스파이크 (수정 전 fr 1207·1208 / 333 fr 266 / 444 fr 95)
| 항목 | 수정 전 | 333 (670.5ms) | 444 (523.0ms) |
|---|---|---|---|
| Subdivide | top30 미표시 | 114.84ms/174, gc 271.5MB | 30.25ms/77, gc 75.9MB |
| ResolveSupport | 3.52ms/1 (1208) | 125.24ms/266 | Local 4.37ms/6 · FullFallback 123.56ms/250 · 본체 2.17ms/288 |
| Step | 33.18ms/1071 (1208) | 87.80ms/267 | 66.73ms/171 |
| Build | top30 미표시 | 13.69ms/149, gc 204.2MB | 18.20ms/336, gc 68.8KB |
| CollectPieces | 167.06ms/446 (1208) | 10.44ms/149, gc 204.0MB | 10.42ms/233, gc 625.6KB |
| CollectionWait / SubdivisionBudget | 마커 없음 / 미표시 | 미표시 / 미표시 | 0.57ms/233 / 미표시 |
| GC.Collect | top30 미표시 | 13.12ms/2 | top30 미표시 |
| GC Allocated In Frame | 13.4MB(1207) / 75.9MB(1208) | 304.2MB | 96.1MB |
| Instantiate | 1642회·Copy 162.02ms(1207) | 134회·Copy 9.32ms | 240회·Copy 16.83ms |
| ParticleSystem(UpdateJob+GeometryJob+Update) | ≈89.5ms(1207) | ≈10.1ms | ≈12.2ms(GeometryJob 미표시) |
| Batches / SetPass | 3850/1440 (1207) | 6120/410 | 944/47 |

## 복귀 프레임 (333 fr 347 / 444 fr 157 — 수정 전 녹화엔 대형 복귀 프레임 없음, 최대 846ms는 폭발 프레임)
| 항목 | 333 (1963.5ms) | 444 (1671.9ms) |
|---|---|---|
| Destroy | 208.93ms/8026 | 251.53ms/8539 |
| GameSaveController.Start | 165.34ms/1 | 8.15ms/1 |
| GameSaveController.ResolveWorldSaveId | 미표시 | 1.05ms/2291 |
| PlacedBlock.OnEnable | 107.10ms/190 | top12 미표시 |
| EquipableWeapon.IgnorePlayerCollisions / IgnoreBlockPlayerCollisions | 미표시 | 2.03ms/4 · 0.46ms/190 |
| GetComponentNullErrorMessage | 10.62ms/8136 | 전 스파이크 미표시(0) |
| GC.Collect | 223.02ms/2 | 213.62ms/1 |
| LoadLevelAsync Integrate | 24.82ms, gc 33.5MB | 12.39ms, gc 132.8MB |
| GC Allocated In Frame | 47.3MB | 141.7MB |

## 수정별 판정
- W1 분할 할당·붕괴 국소화: 부분 효과 — Subdivide 대상당 할당 1,560KB(271.5MB/174)→986KB(75.9MB/77), UpdateCollisionRuns 20.57→13.10ms. 붕괴 국소화는 호출이 Local 6 vs FullFallback 250으로 대체 경로가 지배 — 국소화 이득은 이 녹화로 판단 불가.
- W2 병합 확대: 부분 효과 — Build 할당 204.2MB→68.8KB, CollectPieces 할당 204.0MB→625.6KB, Step 87.80→66.73ms. 병합율(파동/요청) 149/200=74.5%→336/439=76.5%로 변화 미미, 병합 확대 자체는 판단 불가(녹화 내용 상이).
- W3 GetComponent 캐시: 효과 있음 — GetComponentNullErrorMessage 13,792회(수정 전 fr 1207)·8,136회(333 fr 347)·2,534회(333 fr 267) → 444 전 스파이크 0회(미표시).
- F1 분할 예산: 효과 있음 — 최악 폭발 프레임 Subdivide 114.84ms/174회→30.25ms/77회, CollectionWait 233회 동작 확인, 폭발 프레임 Batches 6120→944로 조각 렌더 부하 분산.
- F2 복귀 프레임: 효과 있음 — GameSaveController.Start 165.34→8.15ms, PlacedBlock.OnEnable 107.10ms→미표시, GetComponentNullErrorMessage 8,136→0, 프레임 1963.5→1671.9ms. Destroy 8,026→8,539회로 씬 언로드 비용은 잔존.

## 남은 최악 스파이크 3개 (444)
1. fr 157 (1671.9ms) 복귀·리셋: Destroy 251.53ms/8539 + GC.Collect 213.62ms/1 + Instantiate 280회(Copy 13.05+Produce 8.67+Awake 2.47) + LoadLevelAsync Integrate 12.39ms(gc 132.8MB), 프레임 GC 141.7MB.
2. fr 95 (523.0ms) 폭발: FullFallback 123.56ms/250, Step 66.73ms/171, Subdivide 30.25ms(75.9MB), ProjectileMotor.Update 52.24ms/438(gc 76.6MB), GC.Alloc 142,976회·93.5MB, AddComponent 18.05ms/2383.
3. fr 864 (478.9ms): 에디터 프레임(플레이 모드 종료) — EditorLoop 455.35ms·RenderLoop 456.35ms, 게임 비용 1ms 미만. 다음 게임 스파이크 fr 148(324.2ms)은 Loading.LockPersistentManager 271.01ms/2(에셋 로드 히치).
- 에디터 잡음(분리): fr 157/864 EditorLoop·RenderLoop 455~604ms, fr 95 MaterialEditor 15.81ms/231, fr 621 Selection.NonSelectedObjectWasDestroyed 1.94ms/30,478, playModeStateChanged 핸들러.

## 결론 — 다음에 손댈 것
1. 복귀 프레임 Destroy 8,539회 251.53ms — 습격 씬 언로드 오브젝트 축소 또는 언로드를 로딩 화면 뒤로(F2 리포트의 잔여 과제).
2. 폭발 프레임 FullFallback 123.56ms/250 — 바닥 지지 제거 시 전체 판정으로 빠지는 경로를 제거 경계 성분 재판정·지지 상태 재사용으로 줄이기.
3. GC.Collect 스파이크 190.70~213.62ms — 잔존 대량 할당(Subdivide 75.9MB, ProjectileMotor.Update 76.6MB)을 줄여 GC.Collect 규모·빈도 축소.
