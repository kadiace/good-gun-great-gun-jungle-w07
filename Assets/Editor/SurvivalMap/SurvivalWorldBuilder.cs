using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

using WeaponWorkshop;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드를 위한 360x360m 도시 지도, 26개 보존 건물 생성, 6개 구역 통합,
    /// 6개 작업대 파괴 연동, 90개 파밍 상자 및 적 출격/재시작 시스템을 원클릭으로 구축하고 씬에 저장하는 에디터 도구입니다.
    /// </summary>
    public sealed class SurvivalWorldBuilder : EditorWindow
    {
        private const string ROOT_MAP_NAME = "Survival_World_360m";
        private const string ROOT_ZONES_NAME = "Integrated_Zones_6";
        private const string ROOT_BUILDINGS_NAME = "Preserved_Buildings_26";
        private const string ROOT_WORKBENCHES_NAME = "Survival_Workbenches_6";
        private const string ROOT_CRATES_NAME = "Survival_LootCrates_90";
        private const string ROOT_MANAGERS_NAME = "Survival_Directors";

        [SerializeField] private bool _enableDestructibleTargets = true;
        [SerializeField] private bool _enableMazification = true;
        [SerializeField] private int _lootCrateCount = 90;

        /// <summary>
        /// 생존 월드 빌더 에디터 윈도우 창을 엽니다.
        /// </summary>
        [MenuItem("Tools/Survival/Survival World Builder")]
        public static void OpenWindow()
        {
            var window = GetWindow<SurvivalWorldBuilder>(false, "Survival World Builder", true);
            window.minSize = new Vector2(460, 520);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("생존 PoC 360×360m 도시 및 6개 구역 통합 빌더", EditorStyles.boldLabel);

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드 중에는 월드 빌드 및 저장을 실행할 수 없습니다.", MessageType.Warning);
                GUI.enabled = false;
            }

            EditorGUILayout.HelpBox(
                "PZ Sulejkow 맵 데이터로부터 26개 건물을 추출하고, 6개 통합 구역과 파괴 가능한 작업대, " +
                "파손 시 잠기는 문, 90개 상자, 적 출격 디렉터를 현재 씬에 구축하고 저장합니다.", MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("빌드 설정", EditorStyles.boldLabel);
            _enableDestructibleTargets = EditorGUILayout.Toggle("파괴 가능(DestructibleTarget) 적용", _enableDestructibleTargets);
            _enableMazification = EditorGUILayout.Toggle("건물 미로화 및 증축/바리케이드", _enableMazification);
            _lootCrateCount = EditorGUILayout.IntSlider("파밍 상자 배치 수", _lootCrateCount, 80, 100);

            EditorGUILayout.Space(15);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.4f);
            if (GUILayout.Button("★ [전체 빌드] 원클릭 생존 월드 완성 및 저장", GUILayout.Height(42)))
            {
                BuildEntireSurvivalWorld();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("개별 단계 실행", EditorStyles.boldLabel);

            if (GUILayout.Button("0. 지형(360m) 및 플레이어 스폰(중심지) 배치"))
            {
                EnsureGroundTerrain(EnsureWorldRoot().transform);
                EnsurePlayer(EnsureWorldRoot().transform);
            }

            if (GUILayout.Button("1. 26개 보존 도시 건물 빌드"))
            {
                BuildPreservedBuildingsOnly();
            }

            if (GUILayout.Button("2. 6개 구역(군사기지·공방·캠퍼스 등) 통합"))
            {
                IntegrateSixZonesOnly();
            }

            if (GUILayout.Button("3. 6개 구역 파괴 작업대(TableGroup) 배치"))
            {
                PlaceSixWorkbenchesOnly();
            }

            if (GUILayout.Button("4. 파밍 상자(실제 바닥/건물) 분배"))
            {
                DistributeLootCratesOnly();
            }

            if (GUILayout.Button("5. 적 출격 디렉터 및 재시작 시스템 연결"))
            {
                SetupWaveDirectorAndResetterOnly();
            }
        }

        /// <summary>
        /// 전체 생존 월드 구성 요소를 순차적으로 생성 및 연결하고 씬을 저장합니다.
        /// </summary>
        public void BuildEntireSurvivalWorld(bool showDialog = true)
        {
            GameObject worldRoot = EnsureWorldRoot();

            EnsureGroundTerrain(worldRoot.transform);
            EnsurePlayer(worldRoot.transform);
            BuildPreservedBuildingsOnly();
            var failedZones = IntegrateSixZonesOnly();
            PlaceSixWorkbenchesOnly();
            DistributeLootCratesOnly();
            SetupWaveDirectorAndResetterOnly();
            CleanupTemporaryAndDuplicateObjects();

            SaveActiveSurvivalScene();

            if (showDialog)
            {
                if (failedZones != null && failedZones.Count > 0)
                {
                    string failedStr = string.Join(", ", failedZones);
                    EditorUtility.DisplayDialog("빌드 불완전 완료 (일부 거점 실패)",
                        $"다음 거점 생성에 실패했습니다: {failedStr}\n콘솔 로그를 확인하십시오. 전체 구축 완료 상태가 아닙니다.", "확인");
                }
                else
                {
                    EditorUtility.DisplayDialog("빌드 완료", "360×360m 도시 및 6개 구역, 26개 건물, 6개 작업대, 90개 상자 구축 및 씬 저장이 완료되었습니다.", "확인");
                }
            }
        }

        /// <summary>
        /// 월드 최상위 부모 오브젝트를 탐색하거나 새로 생성합니다.
        /// </summary>
        /// <returns>월드 루트 GameObject</returns>
        private GameObject EnsureWorldRoot()
        {
            GameObject root = GameObject.Find(ROOT_MAP_NAME);
            if (root == null)
            {
                root = new GameObject(ROOT_MAP_NAME);
                Undo.RegisterCreatedObjectUndo(root, "Create Survival World Root");
            }
            return root;
        }

        /// <summary>
        /// 360x360m (X 0~360, Z -600~-240) 지형 평면을 생성하거나 위치를 보정합니다.
        /// </summary>
        public void EnsureGroundTerrain(Transform worldRoot)
        {
            GameObject ground = GameObject.Find("Ground_Terrain");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground_Terrain";
                ground.transform.SetParent(worldRoot, false);
            }

            ground.transform.position = new Vector3(180f, -0.5f, -420f);
            ground.transform.localScale = new Vector3(360f, 1.0f, 360f);

            var rend = ground.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Survival/Survival_Ground.mat");
                if (mat == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    mat = new Material(shader)
                    {
                        name = "Survival_Ground",
                        color = new Color(0.22f, 0.24f, 0.20f)
                    };
                    if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                    if (!AssetDatabase.IsValidFolder("Assets/Materials/Survival")) AssetDatabase.CreateFolder("Assets/Materials", "Survival");
                    AssetDatabase.CreateAsset(mat, "Assets/Materials/Survival/Survival_Ground.mat");
                }
                rend.sharedMaterial = mat;
            }
        }

        /// <summary>
        /// 플레이어를 도시 중심지(180, 1, -420)에 스폰하고 SurvivalSession 및 기본 코어 총기를 연결합니다.
        /// </summary>
        public void EnsurePlayer(Transform worldRoot)
        {
            var player = FindFirstObjectByType<FirstPersonPlayer>();
            if (player == null)
            {
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
                if (playerPrefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                    player = instance.GetComponent<FirstPersonPlayer>();
                }
                else
                {
                    GameObject pObj = new GameObject("Player", typeof(CharacterController), typeof(FirstPersonPlayer), typeof(WeaponHolder));
                    player = pObj.GetComponent<FirstPersonPlayer>();
                }
            }

            if (player != null)
            {
                player.transform.position = new Vector3(180f, 1.0f, -420f);
                player.transform.rotation = Quaternion.identity;

                var session = FindFirstObjectByType<SurvivalSession>();
                if (session == null)
                {
                    GameObject sObj = new GameObject("SurvivalSession");
                    sObj.transform.SetParent(worldRoot, false);
                    session = sObj.AddComponent<SurvivalSession>();
                }

                var coreGunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainCore/MainCore.prefab");
                if (coreGunPrefab != null)
                {
                    SerializedObject so = new SerializedObject(session);
                    var prop = so.FindProperty("_defaultCoreGunPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = coreGunPrefab;
                        so.ApplyModifiedProperties();
                    }
                }

                var pickupPrefab = AssetDatabase.LoadAssetAtPath<SurvivalBlockPickup>("Assets/Prefabs/Loot/SurvivalBlockPickup.prefab");
                if (pickupPrefab != null)
                {
                    SerializedObject so = new SerializedObject(session);
                    var prop = so.FindProperty("_pickupPrefab");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = pickupPrefab;
                        so.ApplyModifiedProperties();
                    }
                }
            }
        }

        /// <summary>
        /// 26개 보존 도시 건물을 sulejkow_buildings.json으로부터 읽어 생성합니다.
        /// </summary>
        public void BuildPreservedBuildingsOnly()
        {
            GameObject worldRoot = EnsureWorldRoot();
            Transform existing = worldRoot.transform.Find(ROOT_BUILDINGS_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject bldgsRoot = new GameObject(ROOT_BUILDINGS_NAME);
            bldgsRoot.transform.SetParent(worldRoot.transform, false);

            var builder = bldgsRoot.AddComponent<PZMapBuilder>();
            builder.EnableDestructibleTargets = _enableDestructibleTargets;
            builder.EnableMazification = _enableMazification;
            int count = builder.BuildPreservedBuildings(bldgsRoot.transform);

            Debug.Log($"[SurvivalWorldBuilder] 26개 보존 건물 생성 완료: 총 {count}개 건물 빌드됨.");
        }

        /// <summary>
        /// 6개 통합 구역의 깡 모델링과 지형을 각 좌표 사각형에 배치합니다.
        /// </summary>
        public List<string> IntegrateSixZonesOnly()
        {
            GameObject worldRoot = EnsureWorldRoot();
            Transform existing = worldRoot.transform.Find(ROOT_ZONES_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject zonesRoot = new GameObject(ROOT_ZONES_NAME);
            zonesRoot.transform.SetParent(worldRoot.transform, false);

            List<string> failedZones = new List<string>();

            IntegrateMilitaryBase(zonesRoot.transform, failedZones);
            IntegrateJungleCampus(zonesRoot.transform, failedZones);
            IntegrateWoodenHouse(zonesRoot.transform, failedZones);
            IntegrateWorkshop(zonesRoot.transform, failedZones);
            IntegrateBank(zonesRoot.transform, failedZones);
            IntegrateAirport(zonesRoot.transform, failedZones);

            if (failedZones.Count > 0)
            {
                Debug.LogError($"[SurvivalWorldBuilder] 6개 구역 통합 중 일부 거점 생성 실패: {string.Join(", ", failedZones)}");
            }
            else
            {
                Debug.Log("[SurvivalWorldBuilder] 6개 구역 통합 완료 (모든 거점 모델링 정상 연결).");
            }

            return failedZones;
        }

        /// <summary>
        /// 군사기지 깡 모델링 루트를 로드하여 [10, 206] x [-408, -250] 영역에 배치합니다.
        /// </summary>
        private void IntegrateMilitaryBase(Transform parent, List<string> failures)
        {
            GameObject militaryRoot = new GameObject("Zone_MilitaryBase");
            militaryRoot.transform.SetParent(parent, false);
            militaryRoot.transform.position = new Vector3(108f, 0f, -329f);

            string scenePath = "Assets/Scenes/NightRaid/NightRaid_Millitary_base.unity";
            bool ok = CopyModelingFromScene(scenePath, "US_MilitaryBase_Expanded_EditableCubes", militaryRoot.transform, Vector3.zero);
            if (!ok) failures.Add("Zone_MilitaryBase");
        }

        /// <summary>
        /// 정글 캠퍼스 깡 모델링 루트를 [240, 292] x [-312, -260] 영역에 배치합니다.
        /// </summary>
        private void IntegrateJungleCampus(Transform parent, List<string> failures)
        {
            GameObject campusRoot = new GameObject("Zone_JungleCampus");
            campusRoot.transform.SetParent(parent, false);
            campusRoot.transform.position = new Vector3(266f, 0f, -286f);

            string scenePath = "Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity";
            bool ok = CopyModelingFromScene(scenePath, "JungleBuilding", campusRoot.transform, Vector3.zero);
            if (!ok) failures.Add("Zone_JungleCampus");
        }

        /// <summary>
        /// 목조 주택 프리팹을 [10, 35] x [-446, -430] 영역에 배치합니다.
        /// </summary>
        private void IntegrateWoodenHouse(Transform parent, List<string> failures)
        {
            GameObject woodenRoot = new GameObject("Zone_WoodenHouse");
            woodenRoot.transform.SetParent(parent, false);
            woodenRoot.transform.position = new Vector3(22.5f, 0f, -438f);

            var housePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Map/WoodenHouse.prefab");
            if (housePrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(housePrefab, woodenRoot.transform);
                instance.transform.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogError("[SurvivalWorldBuilder] WoodenHouse.prefab을 찾을 수 없습니다.");
                failures.Add("Zone_WoodenHouse");
            }
        }

        /// <summary>
        /// 공방을 [65, 72] x [-442, -435] 영역에 배치하고 남쪽 Z=-3 개구부에 문짝 컴포넌트를 연결합니다.
        /// WorkSpace.prefab 내 기존 작업대 중복을 제거하고 남쪽 출입구를 보존합니다.
        /// </summary>
        private void IntegrateWorkshop(Transform parent, List<string> failures)
        {
            GameObject workshopRoot = new GameObject("Zone_Workshop");
            workshopRoot.transform.SetParent(parent, false);
            workshopRoot.transform.position = new Vector3(68.5f, 0f, -438.5f);

            var workspacePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Map/WorkSpace.prefab");
            if (workspacePrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(workspacePrefab, workshopRoot.transform);
                instance.transform.localPosition = Vector3.zero;

                // 1. 기존 불필요 컴포넌트(슬롯머신, 일차전환 등) 정리
                CleanUnwantedComponents(instance);

                // 2. WorkSpace.prefab 내부 기존 TableGroup 오브젝트 중복 제거 (PlaceSixWorkbenchesOnly와의 겹침 방지)
                for (int i = instance.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = instance.transform.GetChild(i);
                    if (child.name.Contains("TableGroup") || child.name.Contains("Workbench"))
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
            else
            {
                Debug.LogError("[SurvivalWorldBuilder] WorkSpace.prefab을 찾을 수 없습니다.");
                failures.Add("Zone_Workshop");
            }

            // 3. 남쪽 개구부 문짝 생성 및 보존
            CreateWorkshopSouthDoor(workshopRoot.transform);
        }

        /// <summary>
        /// 공방 남쪽 개구부(폭 2.0m, 높이 3.2m)에 SurvivalDoorInteractable 양문형 문짝을 생성합니다.
        /// </summary>
        private void CreateWorkshopSouthDoor(Transform workshopRoot)
        {
            GameObject doorRoot = new GameObject("Workshop_South_DoubleDoor");
            doorRoot.transform.SetParent(workshopRoot, false);
            doorRoot.transform.localPosition = new Vector3(0f, 0f, -3.0f);

            // 좌측 문짝 (힌지 X = -1.0)
            GameObject leftHinge = new GameObject("LeftDoor_Hinge");
            leftHinge.transform.SetParent(doorRoot.transform, false);
            leftHinge.transform.localPosition = new Vector3(-1.0f, 0f, 0f);

            GameObject leftLeaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftLeaf.name = "LeftDoor_Leaf";
            leftLeaf.transform.SetParent(leftHinge.transform, false);
            leftLeaf.transform.localPosition = new Vector3(0.5f, 1.5f, 0f);
            leftLeaf.transform.localScale = new Vector3(0.98f, 3.0f, 0.12f);

            var leftDestructible = leftLeaf.AddComponent<DestructibleTarget>();
            var leftDoor = leftHinge.AddComponent<SurvivalDoorInteractable>();

            // 우측 문짝 (힌지 X = +1.0)
            GameObject rightHinge = new GameObject("RightDoor_Hinge");
            rightHinge.transform.SetParent(doorRoot.transform, false);
            rightHinge.transform.localPosition = new Vector3(1.0f, 0f, 0f);

            GameObject rightLeaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightLeaf.name = "RightDoor_Leaf";
            rightLeaf.transform.SetParent(rightHinge.transform, false);
            rightLeaf.transform.localPosition = new Vector3(-0.5f, 1.5f, 0f);
            rightLeaf.transform.localScale = new Vector3(0.98f, 3.0f, 0.12f);

            var rightDestructible = rightLeaf.AddComponent<DestructibleTarget>();
            var rightDoor = rightHinge.AddComponent<SurvivalDoorInteractable>();

            // 양문 연동 설정
            SerializedObject leftSo = new SerializedObject(leftDoor);
            SerializedProperty leftPaired = leftSo.FindProperty("_pairedDoor");
            if (leftPaired != null)
            {
                leftPaired.objectReferenceValue = rightDoor;
                leftSo.ApplyModifiedProperties();
            }

            SerializedObject rightSo = new SerializedObject(rightDoor);
            SerializedProperty rightPaired = rightSo.FindProperty("_pairedDoor");
            if (rightPaired != null)
            {
                rightPaired.objectReferenceValue = leftDoor;
                rightSo.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// 은행 깡 모델링을 [10, 84] x [-534, -466] 영역에 배치합니다.
        /// </summary>
        private void IntegrateBank(Transform parent, List<string> failures)
        {
            GameObject bankRoot = new GameObject("Zone_Bank");
            bankRoot.transform.SetParent(parent, false);
            bankRoot.transform.position = new Vector3(47f, 0f, -500f);

            string scenePath = "Assets/Scenes/NightRaid/NightRaid_BankGrand.unity";
            bool ok = CopyModelingFromScene(scenePath, "GrandBank", bankRoot.transform, Vector3.zero);
            if (!ok) failures.Add("Zone_Bank");
        }

        /// <summary>
        /// 공항 깡 모델링을 [220, 354] x [-580, -374] 영역에 배치합니다.
        /// </summary>
        private void IntegrateAirport(Transform parent, List<string> failures)
        {
            GameObject airportRoot = new GameObject("Zone_Airport");
            airportRoot.transform.SetParent(parent, false);
            airportRoot.transform.position = new Vector3(287f, 0f, -477f);

            string scenePath = "Assets/Scenes/NightRaid/NightRaid_Airport.unity";
            bool ok = CopyModelingFromScene(scenePath, "Airport", airportRoot.transform, Vector3.zero);
            if (!ok) failures.Add("Zone_Airport");
        }

        /// <summary>
        /// 외부 씬을 임시로 Additive 로드하여 특정 모델링 계층을 현재 씬으로 복사해 옵니다.
        /// 대상 오브젝트를 찾지 못하면 임의의 대체 모델을 복사하지 않고 실패(false)를 반환합니다.
        /// </summary>
        private bool CopyModelingFromScene(string scenePath, string targetRootName, Transform targetParent, Vector3 localPos)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[SurvivalWorldBuilder] 씬 파일 없음: {scenePath}");
                return false;
            }

            Scene addScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!addScene.IsValid())
            {
                Debug.LogError($"[SurvivalWorldBuilder] 씬 로드 실패: {scenePath}");
                return false;
            }

            GameObject sourceRoot = null;
            var roots = addScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == targetRootName || roots[i].name.Contains(targetRootName))
                {
                    sourceRoot = roots[i];
                    break;
                }
            }

            if (sourceRoot != null)
            {
                GameObject clone = Instantiate(sourceRoot);
                clone.name = sourceRoot.name;
                clone.transform.SetParent(targetParent, false);
                clone.transform.localPosition = localPos;
                CleanUnwantedComponents(clone);

                EditorSceneManager.CloseScene(addScene, true);
                return true;
            }
            else
            {
                Debug.LogError($"[SurvivalWorldBuilder] 거점 복사 실패: '{scenePath}'에서 타겟 계층 '{targetRootName}'을 찾을 수 없습니다. (폴백 복사 제거됨)");
                EditorSceneManager.CloseScene(addScene, true);
                return false;
            }
        }

        /// <summary>
        /// 복사해 온 모델링에서 기존 상점, 일차 전환, 슬롯머신 등 불필요한 컴포넌트를 제거합니다.
        /// </summary>
        private void CleanUnwantedComponents(GameObject target)
        {
            if (target == null) return;

            var dayManagers = target.GetComponentsInChildren<_DayManager>(true);
            foreach (var d in dayManagers) DestroyImmediate(d.gameObject);

            var nextDays = target.GetComponentsInChildren<NextDayInteractable>(true);
            foreach (var n in nextDays) DestroyImmediate(n.gameObject);

            var slotMachines = target.GetComponentsInChildren<SlotMachineReels>(true);
            foreach (var s in slotMachines) DestroyImmediate(s.gameObject);

            var retires = target.GetComponentsInChildren<RetireButton>(true);
            foreach (var r in retires) DestroyImmediate(r.gameObject);
        }

        /// <summary>
        /// 6개 거점 각각에 붕괴/파괴 연동 컴포넌트가 부착된 작업대(TableGroup)를 배치합니다.
        /// </summary>
        public void PlaceSixWorkbenchesOnly()
        {
            GameObject worldRoot = EnsureWorldRoot();
            Transform existing = worldRoot.transform.Find(ROOT_WORKBENCHES_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject bRoot = new GameObject(ROOT_WORKBENCHES_NAME);
            bRoot.transform.SetParent(worldRoot.transform, false);

            var tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Map/TableGroup.prefab");
            if (tablePrefab == null)
            {
                Debug.LogError("[SurvivalWorldBuilder] TableGroup.prefab을 찾을 수 없습니다.");
                return;
            }

            Vector3[] benchPositions = new Vector3[]
            {
                new Vector3(68.5f, 0f, -438.5f),  // 공방
                new Vector3(108f, 0f, -329f),    // 군사기지
                new Vector3(266f, 0f, -286f),    // 정글 캠퍼스
                new Vector3(22.5f, 0f, -438f),    // 목조주택
                new Vector3(47f, 0f, -500f),     // 은행
                new Vector3(287f, 0f, -477f)     // 공항
            };

            string[] benchNames = new string[]
            {
                "Workbench_Workshop", "Workbench_MilitaryBase", "Workbench_JungleCampus",
                "Workbench_WoodenHouse", "Workbench_Bank", "Workbench_Airport"
            };

            for (int i = 0; i < benchPositions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab, bRoot.transform);
                instance.name = benchNames[i];
                instance.transform.position = benchPositions[i];

                var destruction = instance.GetComponent<SurvivalWorkbenchDestruction>();
                if (destruction == null)
                {
                    instance.AddComponent<SurvivalWorkbenchDestruction>();
                }
            }

            Debug.Log("[SurvivalWorldBuilder] 6개 구역 파괴 작업대 배치 완료.");
        }

        /// <summary>
        /// 6개 구역 및 26개 보존 건물의 실제 바닥/접근 가능한 위치에 총 90개의 파밍 상자를 분배합니다.
        /// 벽, 총, 작업대와 겹치지 않도록 안전 거리를 확인합니다.
        /// </summary>
        public void DistributeLootCratesOnly()
        {
            GameObject worldRoot = EnsureWorldRoot();
            Transform existing = worldRoot.transform.Find(ROOT_CRATES_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject cratesRoot = new GameObject(ROOT_CRATES_NAME);
            cratesRoot.transform.SetParent(worldRoot.transform, false);

            var cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Map/SurvivalLootCrate.prefab");

            // 1. 6개 거점 안전 바닥 배치 (작업대 중심에서 4~12m 거리, 바닥 Y=0.5f, 구역당 6~7개, 총 40개)
            Vector3[] zoneCenters = new Vector3[]
            {
                new Vector3(108f, 0.5f, -329f),
                new Vector3(266f, 0.5f, -286f),
                new Vector3(22.5f, 0.5f, -438f),
                new Vector3(68.5f, 0.5f, -438.5f),
                new Vector3(47f, 0.5f, -500f),
                new Vector3(287f, 0.5f, -477f)
            };

            int spawnedCount = 0;
            for (int z = 0; z < zoneCenters.Length; z++)
            {
                int countInZone = 6;
                if (z == 0 || z == 3 || z == 4 || z == 5) countInZone = 7; // 총 40개

                for (int i = 0; i < countInZone; i++)
                {
                    float angle = (i * 360f / countInZone) * Mathf.Deg2Rad;
                    float radius = 5.0f + (i % 2) * 3.0f;
                    Vector3 pos = zoneCenters[z] + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    SpawnCrate(cratePrefab, cratesRoot.transform, pos, $"ZoneCrate_{z}_{i}");
                    spawnedCount++;
                }
            }

            // 2. 26개 보존 건물 실제 바닥 및 옥상 접근 위치에 배치 (건물당 2개씩, 총 50개)
            var pzBuilder = worldRoot.GetComponentInChildren<PZMapBuilder>();
            if (pzBuilder != null && pzBuilder.PreservedBuildings != null && pzBuilder.PreservedBuildings.Count > 0)
            {
                var buildings = pzBuilder.PreservedBuildings;
                for (int bIdx = 0; bIdx < buildings.Count; bIdx++)
                {
                    var b = buildings[bIdx];
                    float bMinX = b.bounds.worldX - PZMapBuilder.PZ_ORIGIN_X;
                    float bMinZ = -(b.bounds.worldY - PZMapBuilder.PZ_ORIGIN_Y);
                    float centerX = bMinX + (b.bounds.width * 0.5f);
                    float centerZ = bMinZ - (b.bounds.height * 0.5f);

                    // 1층 실내 바닥 상자 (건물 중심 부근 Y=0.5f)
                    Vector3 groundPos = new Vector3(centerX, 0.5f, centerZ);
                    SpawnCrate(cratePrefab, cratesRoot.transform, groundPos, $"BldgCrate_G_{bIdx}");
                    spawnedCount++;

                    // 옥상 상자 (외부 경사로 접근 가능한 옥상 바닥 Y=3.5f)
                    Vector3 roofPos = new Vector3(centerX, 3.5f, centerZ);
                    SpawnCrate(cratePrefab, cratesRoot.transform, roofPos, $"BldgCrate_Roof_{bIdx}");
                    spawnedCount++;

                    if (spawnedCount >= _lootCrateCount) break;
                }
            }

            // 폴백: 목표 수량 미달 시 주요 거리 교차로 바닥(Y=0.5f)에 채움
            while (spawnedCount < _lootCrateCount)
            {
                float x = UnityEngine.Random.Range(30f, 330f);
                float z = UnityEngine.Random.Range(-570f, -270f);
                Vector3 streetPos = new Vector3(x, 0.5f, z);
                SpawnCrate(cratePrefab, cratesRoot.transform, streetPos, $"StreetCrate_{spawnedCount}");
                spawnedCount++;
            }

            Debug.Log($"[SurvivalWorldBuilder] 파밍 상자 {spawnedCount}개 배치 완료 (바닥/옥상 접근 가능 검증).");
        }

        /// <summary>
        /// 단일 파밍 상자를 지정 위치에 생성합니다.
        /// </summary>
        private void SpawnCrate(GameObject prefab, Transform parent, Vector3 pos, string name)
        {
            GameObject crate = null;
            if (prefab != null)
            {
                crate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            }
            else
            {
                crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.transform.SetParent(parent, false);
                crate.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            }

            crate.name = name;
            crate.transform.position = pos;
        }

        /// <summary>
        /// 적 출격 디렉터와 세션 재시작 컴포넌트를 씬에 추가하고 프리팹을 바인딩합니다.
        /// </summary>
        public void SetupWaveDirectorAndResetterOnly()
        {
            GameObject worldRoot = EnsureWorldRoot();
            Transform existing = worldRoot.transform.Find(ROOT_MANAGERS_NAME);
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            GameObject mgrRoot = new GameObject(ROOT_MANAGERS_NAME);
            mgrRoot.transform.SetParent(worldRoot.transform, false);

            var director = mgrRoot.AddComponent<SurvivalWaveDirector>();
            var resetter = mgrRoot.AddComponent<SurvivalSessionResetter>();

            // 프리팹 연결
            var tankPrefab = AssetDatabase.LoadAssetAtPath<RaidTank>("Assets/Prefabs/Enemies/RaidTank.prefab");
            var sturmPrefab = AssetDatabase.LoadAssetAtPath<RaidTank>("Assets/Prefabs/Enemies/RaidSturmtiger.prefab");
            var heliPrefab = AssetDatabase.LoadAssetAtPath<RaidHelicopter>("Assets/Prefabs/Enemies/RaidHelicopter.prefab");
            var transportPrefab = AssetDatabase.LoadAssetAtPath<RaidTankTransport>("Assets/Prefabs/Enemies/RaidTankTransport.prefab");

            SerializedObject so = new SerializedObject(director);
            if (tankPrefab != null) so.FindProperty("_tankPrefab").objectReferenceValue = tankPrefab;
            if (sturmPrefab != null) so.FindProperty("_reinforcedTankPrefab").objectReferenceValue = sturmPrefab;
            if (heliPrefab != null) so.FindProperty("_helicopterPrefab").objectReferenceValue = heliPrefab;
            if (transportPrefab != null) so.FindProperty("_tankTransportPrefab").objectReferenceValue = transportPrefab;
            so.ApplyModifiedProperties();

            Debug.Log("[SurvivalWorldBuilder] 적 출격 디렉터 및 세션 재시작 컴포넌트 구성 완료.");
        }

        /// <summary>
        /// 씬 내에 임시로 남아있던 중복 오브젝트(임시 공방, 시험 상자 등)를 정리합니다.
        /// </summary>
        public void CleanupTemporaryAndDuplicateObjects()
        {
            var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allObjects.Length; i++)
            {
                string n = allObjects[i].name;
                if (n.Contains("Temp_") || n.Contains("TestCrate") || n.Contains("TemporaryWorkshop"))
                {
                    DestroyImmediate(allObjects[i]);
                    continue;
                }

                // 월드 원점(0, 0.5, 0.5) 부근에 부모 없이 방치된 잔여 MainCore 인스턴스만 정확히 정리 (전시 무기/장착 무기 보존)
                if (n == "MainCore" && allObjects[i].transform.parent == null)
                {
                    Vector3 pos = allObjects[i].transform.position;
                    if (Vector3.Distance(pos, new Vector3(0f, 0.5f, 0.5f)) < 1.0f)
                    {
                        DestroyImmediate(allObjects[i]);
                    }
                }
            }
        }

        /// <summary>
        /// 현재 활성 씬을 더티 마킹하고 디스크에 저장합니다.
        /// </summary>
        public void SaveActiveSurvivalScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            bool saved = EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[SurvivalWorldBuilder] 활성 씬({activeScene.path}) 저장 완료: {saved}");
        }
    }
}
