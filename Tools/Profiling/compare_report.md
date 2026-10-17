# 프로파일러 수정 전후 비교
수정 전: report_before.txt(GoodGunGreatGun_2026-10-17_14-53-54.data) / 수정 후: Profiler 창에 불러온 333.data 버퍼(last_report.txt)

## 프레임 통계
| 항목 | 수정 전 | 수정 후 |
|---|---|---|
| 프레임 수 | 2000 (0..1999) | 982 (0..981) |
| 평균 | 14.2ms | 19.7ms |
| p95 | 29.2ms | 39.9ms |
| 최대 | 846.0ms | 1963.5ms |
| >33ms 프레임 수 | 65 / 2000 | 77 / 982 |
| BASELINE cpu / gpu | 8.6ms / 0.0ms (frame 847) | 8.8ms / 0.0ms (frame 796) |
| GPU ms | 전 프레임 0.0ms — 미녹화 | 전 프레임 0.0ms — GPU Usage를 켰으나 데이터상 0(미녹화). GPU 비용 판단 불가 |

## 최악 스파이크 상위 항목 (self ms / 호출 수)
| 항목 | 수정 전 (frame 1208, 846.0ms) | 수정 후 (frame 347, 1963.5ms) |
|---|---|---|
| ExpandingExplosion.Create | 253.88ms / 446 | top30 미표시 |
| ExpandingExplosion.CollectPieces | 167.06ms / 446 | top30 미표시 |
| ExpandingExplosion.Step | 33.18ms / 1071 | top30 미표시 |
| ExpandingExplosion.Merge / Build | 미표시 | 최악 프레임 미표시(폭발 프레임 266: Merge 1.10ms/200, Build 13.69ms/149) |
| GC.Collect | 미표시 | 223.02ms / 2 |
| Destroy | 3.39ms / 868 | 208.93ms / 8026 |
| Instantiate 횟수 | 529회(Copy 82.26ms) | 287회(Copy 13.99ms) |
| ParticleSystem 비용 | UpdateJob 50.62+GeometryJob 24.95+Update 14.92 ≈ 90.5ms | 전 항목 합계 ≤0.2ms |
| GC.Alloc | 13.23ms / 391,895회 / 74.1MB | 3.19ms / 89,677회 / 13.1MB |
| GC Allocated In Frame | 75,925,996B | 47,321,332B |
| Batches / SetPass | 1695 / 97 | 363 / 44 |
| RenderLoop(렌더 스레드) | 14.90ms | 437.24ms |
| EditorLoop | 14.78ms | 419.05ms |

## 폭발 스파이크 비교 (수정 전 frame 1207 645.2ms / 수정 후 frame 266 670.5ms)
| 항목 | 수정 전 | 수정 후 |
|---|---|---|
| Create / CollectPieces | 46.99ms/1071 / 0.54ms/1071 | 미표시 / 10.44ms/149 |
| Step·Merge·Build 호출 수 | Step 미표시 / Merge·Build 미표시 | Step 267 / Merge 200 / Build 149 |
| Instantiate | 1642회 | 134회 |
| Subdivide | 미표시 | 114.84ms/174, gc 271.5MB |
| ResolveSupport | 4.03ms/1 | 125.24ms/266 |
| GC Allocated In Frame | 13,375,026B | 304,153,201B |
| Batches / SetPass | 3850 / 1440 | 6120 / 410 |

## H1~H6 판정 (수정 후 데이터, 스파이크 프레임 기준)
- H1 폭발 생성: 부분 해소 — 최악 프레임에서 Create·CollectPieces·Step 모두 소멸. 폭발 프레임(266)에 Create 미표시, CollectPieces 10.44ms(149)·Step 87.80ms(267)·Build 13.69ms(149) 잔존.
- H2 첫 분할: 문제임 — 폭발 프레임 Subdivide 114.84ms(174회, gc 271.5MB). 수정 전 스파이크에서는 top30 미표시였던 신규 병목.
- H3 파괴 후처리: 문제임 — ResolveSupport 125.24ms(266), UpdateCollisionRuns 20.57ms(2794), RebuildChunkMeshes 15.47ms(397), Physics.SyncTransforms 0.49ms(379). 수정 전 ResolveSupport 3.52ms(1) 대비 급증.
- H4 파티클: 아님 — 최악 프레임 ParticleSystem 전 항목 ≤0.05ms, 폭발 프레임 UpdateJob 7.29ms(728). 단 GPU ms 0.0ms(미녹화)라 GPU 비용은 판단 불가.
- H5 발사체 렌더링: 부분 문제 — 폭발 프레임 Batches 6120·SetPass 410(BASELINE 1810/69의 3.4배/5.9배), Draw Calls 6282. RenderLoop 13.56ms(6)로 CPU 렌더 부담은 작고 GPU 0이라 실부하는 미확인. Dynamic Batched Draw Calls는 수정 전 3036~3141 → 177~226.
- H6 GC: 문제임 — 폭발 프레임 GC Allocated In Frame 304MB(Subdivide 271.5MB·Build 204MB 할당), GC.Collect는 최악 프레임 223.02ms(2)·frame 841 188.29ms(1).

## 최악 스파이크(1963.5ms, frame 347) 원인
폭발 코드가 아니라 씬 리셋/재시작 프레임이다. EditorLoop 419.05ms(4)와 렌더 스레드 RenderLoop 437.24ms(1593)가 프레임 대부분이고, 게임 측 상위는 Destroy 208.93ms(8026회), GC.Collect 223.02ms(2), GameSaveController.Start 165.34ms(1), PlacedBlock.OnEnable 107.10ms(190), LoadLevelAsync Integrate 24.82ms(gc 33.5MB), DayTransitionUI.Advance 21.66ms다. ExpandingExplosion.*는 전부 top30 밖. 최대치 증가(846→1963.5ms)는 이 리셋 프레임 때문이며 두 녹화는 길이(2000 vs 982)와 구간이 달라 직접 비교에는 한계. 평균·p95 상승(14.2→19.7ms, 29.2→39.9ms)은 리셋/로드 프레임 비중 증가와 폭발 프레임의 신규 GC 부하(304MB)가 겹친 결과다.

## 에디터 잡음 (게임 비용 아님, 분리)
EditorLoop(최악 419.05ms), playModeStateChanged 핸들러(BurstLoader 1.93ms 등, frame 981), MaterialEditor.ApplyMaterialPropertyDrawersFromNative 35.18ms(522, frame 266 — 인스펙터가 파괴 조각 머티리얼을 다시 그림), Selection.Internal_NonSelectedObjectWasDestroyed 1.97ms(30,730, frame 841).

## 결론 — 가장 큰 남은 원인
1. 폭발 프레임 파괴 처리와 GC: Subdivide 114.84ms·271.5MB, ResolveSupport 125.24ms, Step 87.80ms → 프레임 GC 304MB, GC.Collect 스파이크 188~223ms.
2. 씬 리셋/재시작 프레임: Destroy 8026회 208.93ms, GameSaveController.Start 165.34ms, PlacedBlock.OnEnable 107.10ms.
3. 드로우 콜 수: 폭발 프레임 Batches 6120·Draw Calls 6282 — GPU 미녹화로 실제 GPU 부담은 미확인.
