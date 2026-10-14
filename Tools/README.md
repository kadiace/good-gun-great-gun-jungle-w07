# Unity 머지 설정

Windows에서 새 클론마다 한 번 실행:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ConfigureUnityMerge.ps1
```

씬과 프리팹은 프로젝트의 ProjectVersion.txt와 같은 Unity 버전의 UnityYAMLMerge를 사용한다. 해당 버전의 Unity Hub 에디터가 필요하다.

해결되지 않은 충돌은 그대로 남긴다. 한쪽 내용을 자동으로 선택하거나 README를 union 머지하지 않는다. rerere는 이전 해결 내용을 재사용하지만 자동 스테이징은 하지 않으므로 결과를 확인한 뒤 git add한다.

.gitattributes와 Tools 파일은 저장소에서 공유되고, Git 드라이버/rerere 설정은 각 클론에만 적용된다. 기존 에셋 전체 줄바꿈 변경은 하지 않는다.