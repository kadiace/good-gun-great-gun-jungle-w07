# 프로파일러 비교 (수정 전 / 444 / 5555 / 777)
777은 Profiler 창 버퍼(last_report.txt 첫 줄 "SOURCE buffer(Profiler 창 현재 데이터)", 777.data 로드 상태), 1830프레임. 4개 녹화 모두 GPU ms 0.0 — GPU 모듈 미녹화로 GPU 비용 판단 불가. 녹화마다 폭발·습격 규모가 달라 절대값 직접 비교는 주의 필요. gc는 리포트 합산치.

## 프레임 통계
| 항목 | 수정 전(before) | 444(2차) | 5555(3차) | 777(4차+콜라이더 선생성 제거) |
|---|---|---|---|---|
| 프레임 수 | 2000 (0..1999) | 865 (0..864) | 1181 (0..1180) | 1830 (0..1829) |
| 평균 / median | 14.2 / 8.6ms | 18.3 / 7.9ms | 19.0 / 7.9ms | 9.6 / 8.6ms |
| p95 / 최대 | 29.2 / 846.0ms | 42.8 / 1671.9ms | 41.1 / 1455.7ms | 16.7 / 971.5ms |
| >33ms | 65/2000 (3.3%) | 99/865 (11.4%) | 94/1181 (8.0%) | 13/1830 (0.7%) |
| BASELINE cpu/gpu | 8.6/0.0ms (fr 847) | 7.9/0.0ms (fr 603) | 7.9/0.0ms (fr 284) | 8.6/0.0ms (fr 1094) |
| BASELINE Batches | 1644 | 1824 | 1707 | 1708 |

## 폭발 스파이크 (before fr1208 / 444 fr95 523.0ms / 5555 fr104 888.2ms / 777 fr734 89.4ms·fr728 59.8ms)
| 항목 | 수정 전 | 444 fr95 | 5555 fr104 | 777 fr734 | 777 fr728 |
|---|---|---|---|---|---|
| ResolveSupport.Local | 마커 없음(본체 3.52ms/1) | 4.37ms/6 | 158.16ms/210 | 미표시(<0.03ms) | 미표시(<0.01ms) |
| ResolveSupport.FullFallback | 마커 없음 | 123.56ms/250 | 92.08ms/127 | 미표시 | 미표시 |
| Subdivide (ms/회/gc) | top30 미표시 | 30.25/77·75.9MB | 55.78/129·162.7MB | 44.23/58·86.7MB | 6.09/66·6.2MB |
| ExpandingExplosion.Step | 33.18ms/1071 | 66.73ms/171 | 16.03ms/106 | 8.47ms/20·86.8MB | 미표시(0 수준) |
| ProjectileMotor.Update | 38.49ms/54·70.8MB | 52.24ms/438·76.6MB | 104.70ms/361·164.2MB | 미표시(<0.84ms self) | 7.20ms/1265·0.27MB |
| GameObject.AddComponent | 29.54ms/1431 | 18.05ms/2383·3.2MB | 39.81ms/5613·49.3MB | 1.55ms/411·3.7MB | 3.08ms/788·0.26MB |
| GC.Collect | 미표시 | 미표시 | 14.07ms/2 | 8.25ms/3 | 미표시 |
| GC Allocated In Frame | 75.9MB | 96.1MB | 269.6MB | 89.0MB | 6.7MB |
| Instantiate | 1642회·Copy 162.02ms | 240회·Copy 16.83ms | 353회·Copy 25.05ms | Produce 5회·Copy 1회 | 미표시(<0.01ms) |

## 폭발 규모 (추가 확인)
- 777 폭발 프레임은 4개(Create 합계 435회, 코디네이터 측정), 최악 fr 728. 리포트 상세는 상위 5 스파이크 중 fr 728·734뿐(나머지 2개는 5위 밖 미수록).
- fr 728: Create 393/Merge 393/Build 19/CollectPieces 14회, Subdivide 66회. fr 734: Create 미표시(0.03ms 미만)/Build 1/CollectPieces 2/Step 20회, Subdivide 58회. Create 435회 중 393회가 fr 728.
- 5555 fr104: Merge 728/Build 482/CollectPieces 375/Step 106회·Subdivide 129회, fr87: Merge 259/Build 187/CollectPieces 218/Step 245회·Subdivide 102회. 777은 Build·Step이 1/10~1/20 수준 — 규모 차이로 직접 비교 한계.

## 복귀 프레임 (444 fr157 / 5555 fr132 / 777 fr881 — before 녹화엔 대형 복귀 프레임 없음)
| 항목 | 444 fr157 (1671.9ms) | 5555 fr132 (1455.7ms) | 777 fr881 (971.5ms) |
|---|---|---|---|
| Destroy | 251.53ms/8539 | 93.39ms/6597 | 726.17ms/11284 |
| NightRaidReturnOverlay.Fade/LoadWait/GCCollect | 마커 없음(H1 이전) | 미표시(당시 워치 미수록) | 상세 5 프레임 모두 미표시(0.02ms 미만) |
| GC.Collect | 213.62ms/1 | 217.76ms/1 | 26.04ms/3 |
| 프레임 ms | 1671.9 | 1455.7 | 971.5 |
- 복귀 비용 분산: 444·5555·777 모두 최대 1프레임 집중. 다만 666 참고로 오버레이 GCCollect 마커가 별도 fr121(64.8ms)에 있고 그 프레임 GC.Collect 41.88ms/1 — 명시 GC가 활성화 프레임 밖 소프레임으로 분리되는 것은 확인. 777 복귀 프레임 GC는 26.04ms/3으로 축소됐다.

## 씬 로드(복귀 활성화) 프레임 — 777 fr881 vs 666 fr128(폭발 없는 녹화)
- ExpandingExplosion..ctor: 777 256회·gc 100,304KB = 666 256회·100,304KB(동일). Instantiate.Awake gc 125,150.6KB도 동일 — 풀 워밍업 규모 같음.
- PoolManager.Awake: 666 7.74ms/1·gc 125,072.6KB. 777 리포트에는 미표시(Main top12 컷 7.28ms 미만).

## G1·G2·H1 판정
- G1 ResolveSupport 축소: 효과 있음 — Local+FullFallback 합계 127.93ms(444 fr95)·250.24ms(5555 fr104) → 777 두 폭발 프레임 모두 미표시(<0.03ms). 다만 폭발 규모가 5555의 절반 이하라 정량 비교 한계.
- G1 분할 할당(셀 구조체 축소): 판단 불가 — Subdivide 호출당 gc 986KB(444) → 1,495KB(fr734)·94KB(fr728). 호출당 셀 수가 녹화마다 달라 정규화 불가.
- G1 충돌 박스 풀(선생성 제거 후): 효과 있음 — 폭발 중 AddComponent 2,383회·3.2MB(444)/5,613회·49.3MB(5555) → 788회·0.26MB(fr728)/411회·3.7MB(fr734). UpdateCollisionRuns gc도 52.3KB(fr728)/27.5KB(fr734). fr734의 3.7MB는 풀 보충 잔여로 보임.
- G2 ProjectileMotor: 효과 있음 — 호출당 inclusive gc 175KB(444 fr95, 438회) → 0.22KB(777 fr728, 1265회).
- H1 복귀 오버레이·명시 GC: 효과 있음 — GC.Collect 213.62(444)→217.76(5555)→26.04ms/3(777), 복귀 최대 프레임 1671.9→1455.7→971.5ms. Fade/LoadWait/GCCollect 마커는 상세 프레임에 미표시라 오버레이 동작 위치만 미확인.

## 남은 최악 스파이크 3개
1. fr 881 (971.5ms) 복귀·씬 활성화: Destroy 726.17ms/11284(74.7%) + AddComponent 15.06ms/3717·102,603KB(풀 워밍업) + GC.Collect 26.04ms/3 + Instantiate 1193회 + Loading.AwakeFromLoad 7.87ms/14820·127,711.5KB + Integrate 7.28ms·127,724.7KB + ModulePaletteUI.Awake 28.51ms, GC.Alloc 63,632회·131,493KB. 에디터 잡음: 미표시(5555 fr132의 EditorLoop 555.07·RenderLoop 566.90ms 없음, Main 최소 표시 항목 7.28ms). GPU 0.0ms.
2. fr 734 (89.4ms) 폭발: Subdivide 44.23ms/58·86,713.9KB(프레임의 49%) + Step 8.47ms/20 + GC.Collect 8.25ms/3 + VisualDebrisCloud..ctor 2.64ms/58 + AddComponent 411회, GC.Alloc 24,814회·89.0MB. 에디터 잡음: 미표시. GPU 0.0ms.
3. fr 728 (59.8ms) 폭발(Create 최다): ActivateAwakeRecursively 13.24ms/707 + ProjectileMotor.Update 7.20ms/1265 + Subdivide 6.09ms/66 + AddComponent 788회 + UpdateCollisionRuns 3.06ms/4110 + Build 1.69ms/19, GC.Alloc 4,378회·6.7MB. 에디터 잡음: 미표시. GPU 0.0ms.
- fr 1829(84.4ms)는 플레이 종료(Internal_ApplicationWantsToQuit·shutdownBeforeCleanupAllObjects), fr 1606(74.7ms)는 별도 씬 로드(SaveSlotsUI.Awake 26.72ms+Start_UI.Awake 13.90ms+MapObjectSave 11,052회+Integrate 1.82ms·31.7MB)라 게임 스파이크에서 제외. 전 프레임 GPU ms 0.0.

## 결론 — 다음에 손댈 것
1. 복귀 활성화 프레임의 구 씬 teardown Destroy(726.17ms/11284회, fr 881의 74.7%) — H1 오버레이로도 못 숨기는 활성화 프레임 비용. 오브젝트 수 축소 또는 비동기 분산.
2. Subdivide — fr 734에서 44.23ms·86.7MB로 여전히 프레임의 49%. 호출당 셀 수 카운터 병행 정규화 측정 뒤 처리 분산.
3. 측정 보강(다음 비교용): ProfileReport.cs에 폭발 프레임 4개 전 나열과 NightRaidReturnOverlay 마커가 실린 소프레임 출력 추가 — 이번 리포트 구조로는 소프레임 분산 확인이 불가했다.
