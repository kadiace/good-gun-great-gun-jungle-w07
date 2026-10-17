# 프로파일러 4차 비교 (수정 전 / 444 / 5555)
5555는 Profiler 창 버퍼(last_report.txt 첫 줄 SOURCE buffer, 5555.data 로드 상태). 세 녹화 모두 GPU ms 0.0 — GPU 모듈 미녹화로 GPU 비용 판단 불가. 녹화마다 폭발 규모가 다르다(폭발 프레임 GC.Alloc 391,895회·74.1MB → 142,976회·93.5MB → 348,524회·263.3MB). gc는 inclusive 합산치.

## 프레임 통계
| 항목 | 수정 전(before) | 444(2차) | 5555(3차) |
|---|---|---|---|
| 프레임 수 | 2000 (0..1999) | 865 (0..864) | 1181 (0..1180) |
| 평균 / median | 14.2 / 8.6ms | 18.3 / 7.9ms | 19.0 / 7.9ms |
| p95 / 최대 | 29.2 / 846.0ms | 42.8 / 1671.9ms | 41.1 / 1455.7ms |
| >33ms | 65/2000 (3.3%) | 99/865 (11.4%) | 94/1181 (8.0%) |
| BASELINE cpu/gpu | 8.6/0.0ms (fr 847) | 7.9/0.0ms (fr 603) | 7.9/0.0ms (fr 284) |
| BASELINE Batches | 1644 | 1824 | 1707 |

## 폭발 스파이크 (수정 전 fr 1208·1207 / 444 fr 95 / 5555 fr 104·87·105)
| 항목 | 수정 전 | 444 (523.0ms) | 5555 fr104 (888.2ms) | 5555 fr87 (586.9ms) | 5555 fr105 (406.8ms) |
|---|---|---|---|---|---|
| ResolveSupport.Local | 마커 없음(본체 3.52ms/1) | 4.37ms/6 | 158.16ms/210 | 89.56ms/143 | 168.46ms/251 |
| ResolveSupport.FullFallback | 마커 없음 | 123.56ms/250 | 92.08ms/127 | 63.27ms/114 | 11.94ms/17 |
| Subdivide (ms/회/gc) | top30 미표시 | 30.25/77·75.9MB | 55.78/129·162.7MB | 47.38/102·154.4MB | 7.88/17·12.6MB |
| Step | 33.18ms/1071 (1208) | 66.73ms/171 | 16.03ms/106 | 63.64ms/245·27.3MB | 69.68ms/364·16.0MB |
| ProjectileMotor.Update | 38.49ms/54·70.8MB (1208) | 52.24ms/438·76.6MB | 104.70ms/361·164.2MB | 35.02ms/259·132.7MB | 19.26ms/284·1.6MB |
| GameObject.AddComponent | 29.54ms/1431 (1208) | 18.05ms/2383·3.2MB | 39.81ms/5613·49.3MB | 35.11ms/5900·6.6MB | 미표시 |
| GC.Collect | 미표시 | 미표시 | 14.07ms/2 | 3.09ms/1 | 미표시 |
| GC Allocated In Frame | 75.9MB(1208)·13.4MB(1207) | 96.1MB | 269.6MB | 179.4MB | 33.1MB |
| Instantiate | 1642회·Copy 162.02ms(1207) | 240회·Copy 16.83ms | 353회·Copy 25.05ms | 163회·Copy 11.94ms | 28회·Copy 1.89ms |

## 복귀 프레임 (수정 전 녹화엔 대형 복귀 프레임 없음 — 최대 846ms는 폭발 fr1208)
| 항목 | 444 fr157 (1671.9ms) | 5555 fr132 (1455.7ms) |
|---|---|---|
| Destroy | 251.53ms/8539 | 93.39ms/6597 |
| NightRaidReturnOverlay.Fade / LoadWait / GCCollect | 마커 없음(H1 이전) | 리포트에 미표시 — 워치 목록에 없어 수집 안 됨 |
| GC.Collect | 213.62ms/1 | 217.76ms/1 |
| 프레임 ms | 1671.9 | 1455.7 |
| 복귀 비용 분산 | fr 157 한 프레임 | fr 132 한 프레임 — 상위 5 스파이크 중 복귀는 1개, 분산 흔적 없음 |

## G1·G2·H1 판정
- G1 FullFallback 축소: 부분 효과 — 경로 이동 확인: Local:FullFallback 호출 6:250(444 fr95) → 210:127(fr104)·143:114(fr87)·251:17(fr105). 다만 판정 총비용(Local+Full)은 fr104 250.24ms로 444 fr95 127.93ms보다 크고 폭발 규모도 커져(Subdivide 77→129회) 총 감소는 판단 불가. Local 호출당 0.63~0.75ms는 444 Local 0.73ms(6회)·FullFallback 0.49ms(250회)와 비슷하거나 비싸다.
- G1 분할 할당(셀 구조체 축소): 판단 불가 — Subdivide 호출당 gc 986KB(444) → 1,261KB(fr104)·1,514KB(fr87)·739KB(fr105). 호출당 셀 수가 녹화마다 달라 정규화 불가.
- G1 충돌 박스 풀: 판단 불가 + 우려 — UpdateCollisionRuns 호출 2,979→3,368(+13%)인데 AddComponent 2,383→5,613(+136%)·gc 3.2→49.3MB. 최악 기준 선생성이 제거된 상태라 풀 보충이 폭발 중 AddComponent로 실행되는 양상.
- G2 ProjectileMotor 할당: 부분 효과 — 폭발을 유발하지 않은 fr105 호출당 inclusive gc 175KB(444 fr95)→5.5KB. fr104·87의 455KB·512KB는 하위 폭발 생성(ExpandingExplosion..ctor gc 42.3MB)이 inclusive로 포함돼 판단 불가.
- H1 복귀 오버레이·명시 GC: 부분 효과 — 복귀 프레임 1671.9→1455.7ms, Destroy 251.53ms/8539→93.39ms/6597회(녹화 내용 차이 가능). GC.Collect는 213.62→217.76ms로 규모 유지. Fade/LoadWait/GCCollect 마커가 수집되지 않아 오버레이 동작·명시 GC 위치 확인 불가.

## 남은 최악 스파이크 3개 (에디터 잡음 분리 표기)
1. fr 132 (1455.7ms) 복귀·리셋: GC.Collect 217.76ms/1 + Destroy 93.39ms/6597 + LoadLevelAsync Integrate 8.37ms·gc 130.0MB + Instantiate 279회, 프레임 GC 151.7MB. 에디터 잡음: EditorLoop 555.07ms·RenderLoop 566.90ms·Selection.NonSelected 108,921회.
2. fr 104 (888.2ms) 폭발: Local 158.16ms/210 + FullFallback 92.08ms/127 + Subdivide 55.78ms/129·162.7MB + ProjectileMotor.Update 104.70ms/361·164.2MB(inclusive) + AddComponent 5,613회·49.3MB + GC.Alloc 348,524회·263.3MB. 에디터 잡음: MaterialEditor 26.43ms/387.
3. fr 87 (586.9ms) 폭발: Local 89.56ms/143 + Step 63.64ms/245·27.3MB + Subdivide 47.38ms/102·154.4MB + UpdateCollisionRuns 38.30ms/1871 + AddComponent 5,900회·35.11ms + GC.Alloc 192,103회·175.2MB. 에디터 잡음: MaterialEditor 20.77ms/306·EditorLoop 23.48ms.
- fr 1180 (443.8ms)은 순수 에디터 프레임(EditorLoop 419.33·RenderLoop 420.43ms, 게임 마커 미표시)이라 최악에서 제외. 전 프레임 GPU ms 0.0 — GPU 병목 여부 판단 불가.

## 결론 — 다음에 손댈 것
1. ResolveSupport.Local — 폭발 프레임 최상위(158.16·168.46ms)이고 호출당 0.63~0.75ms. 경계 성분 재판정의 배치 처리·성분 캐시로 호출당 비용 축소.
2. 충돌 박스 풀 보충 — 폭발 중 AddComponent 5,613회·49.3MB(fr104). 풀 상한 산식을 실제 최대 런에 맞춰 폭발 중 보충을 제거.
3. 측정 보강 — ProfileReport.cs 워치에 "NightRaidReturnOverlay." 추가(H1 판정 불가 해소), Subdivide·ResolveSupport에 호출당 셀·성분 수 카운터 추가로 녹화 간 정규화 비교 가능하게.
