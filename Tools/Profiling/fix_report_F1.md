# F1 폭발 분할 예산 계약 연결

## 원인과 근거

- `fix_report_W1.md`의 동작 계약(`DestructibleTarget.cs:632-648`)에 따르면 `TryCollectExplosionPieces`는 예산 초과 시 조각 목록을 바꾸지 않고 `false`를 반환하며, 같은 대상을 다음 프레임에 재시도해야 한다. 준비 여부는 `CanCollectExplosionPiecesNow`로 조회한다.
- `ExpandingExplosion.cs`의 기존 `CreateWave`는 `CollectExplosionPieces`를 동기 호출한 뒤 즉시 조각을 그룹화했다. 따라서 분할을 분산하거나 대기 상태를 보존할 수 없었다.
- `compare_report.md:38-40`의 폭발 프레임 266에서 `Subdivide` 174회에 114.84ms·271.5MB 할당이 기록됐다. W1 추정 분할 평균은 약 0.66ms/대상이다.

## 파일별 변경

- `Assets/Scripts/Projectile/ExpandingExplosion.cs:268-397`: 폭발 대상의 최근접 거리로 후보를 정렬해 중심에 가까운 대상부터 수집한다. 웨이브별 후보 목록·커서·단일 조각 버퍼를 보존하고 `CanCollectExplosionPiecesNow`와 `TryCollectExplosionPieces`를 호출한다. Try가 `false`면 커서와 버퍼를 유지해 같은 대상을 다음 프레임에 다시 시도한다. 모든 대상 수집이 끝난 뒤에만 조각을 정렬·그룹화하며, `OnDisable`의 `ResetWave`가 대기 상태와 버퍼를 비운다. 기존 Create/Merge/Build/Collect/Step 마커를 유지하고 `CollectionWait` 마커를 추가했다.
- `Assets/Scripts/Projectile/ProjectileTuning.cs:87-88`, `Assets/Data/ProjectileTuning.asset`: 인스펙터에 `Explosion Subdivisions per Frame`을 추가하고 기본값을 16으로 설정했다.

## 동작 변화

- 이 작업은 웨이브 전체를 수집 완료까지 대기시킨다. 반경 확장, 시각 효과, 직접 체력 피해도 그동안 시작하지 않는다. 수집이 끝나기 전에 레이어를 구성하면 뒤늦게 준비된 대상이 이전 반경을 놓칠 수 있어, 모든 대상과 피해 레이어가 함께 준비된 뒤 웨이브를 시작하도록 했다.
- 이미 분할된 대상만 맞는 단발 폭발은 같은 프레임에 수집·그룹화된다. 해당 대상은 분할 상한을 소비하지 않는다. 폭발 웨이브가 풀에 반환되거나 비활성화되면 `ResetWave`가 후보, 커서, 조각 버퍼를 정리한다.

## 인스펙터 값 및 예산

- 기본값은 프레임당 새 분할 대상 16개다. 활성 수집 웨이브는 양수 설정 중 가장 낮은 값을 공유하고, 0은 제한 후보에서 제외한다.
- W1 측정 평균으로 계산하면 174개는 약 11프레임(약 0.18초)에 걸친다. 분할만 단순 환산해 프레임당 약 10.6ms이므로 4ms 예산보다 큰 상한을 택해 대기 시간을 줄였다. 대상별 편차가 있어 이는 추정치이며 실제 프레임 비용은 프로파일링이 필요하다.

## 남은 위험과 검증

- 개수 상한은 분할 대상 크기에 따른 시간 편차를 제한하지 않는다. 이미 분할된 대상의 조각 수집도 상한에 포함되지 않아 한 프레임의 나머지 수집 비용은 커질 수 있다.
- 웨이브 전체 대기 방식은 대상 하나의 예산 대기가 해당 폭발의 확장과 피해를 늦춘다. 실제 체감과 프레임 분산은 프로파일 측정이 필요하다.
- Unity 재컴파일: `unity --no-banner recompile --timeout 180` 종료 코드 0, 출력 없음. 수정한 Assets 파일 3개를 `AssetDatabase.ImportAsset`으로 한 번씩 임포트했다. 플레이와 프로파일 테스트는 실행하지 않았다.

## README 제안

- 폭발은 중심에 가까운 파괴 대상을 먼저 분할 수집하며, 모두 준비된 뒤 반경 확장과 피해를 시작한다. 인스펙터 기본 분할 상한은 프레임당 16개다.
