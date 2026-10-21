using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 좀보이드 도시 건물 데이터를 읽어 360x360m 생존 맵 내 26개 보존 건물을 생성하고,
    /// 외형 교체, 파괴 가능(DestructibleTarget) 구조화, 내부 미로화, 개폐/파괴 문짝을 구축하는 빌더 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PZMapBuilder : MonoBehaviour
    {
        public const int MAP_MIN_X = 0;
        public const int MAP_MAX_X = 360;
        public const int MAP_MIN_Z = -600;
        public const int MAP_MAX_Z = -240;
        public const int PZ_ORIGIN_X = 12900;
        public const int PZ_ORIGIN_Y = 11100;

        [Header("빌드 높이 및 규격 설정")]
        [SerializeField, Min(1f)] private float _wallHeight = 2.8f;
        [SerializeField, Min(1f)] private float _floorHeight = 3.0f;
        [SerializeField] private bool _enableMazification = true;
        [SerializeField] private bool _enableDestructibleTargets = true;
        [SerializeField] private TextAsset _buildingsJsonAsset;

        private readonly Dictionary<Color, Material> _materialCache = new Dictionary<Color, Material>();
        private readonly List<BuildingData> _preservedBuildings = new List<BuildingData>();

        public IReadOnlyList<BuildingData> PreservedBuildings => _preservedBuildings;
        public bool EnableDestructibleTargets { get => _enableDestructibleTargets; set => _enableDestructibleTargets = value; }
        public bool EnableMazification { get => _enableMazification; set => _enableMazification = value; }

        [Serializable]
        private class BuildingListWrapper
        {
            public List<BuildingData> buildings;
        }

        private struct ZoneBounds
        {
            public string name;
            public float minX;
            public float maxX;
            public float minZ;
            public float maxZ;

            /// <summary>
            /// 구역 바운딩 박스를 초기화합니다.
            /// </summary>
            public ZoneBounds(string name, float minX, float maxX, float minZ, float maxZ)
            {
                this.name = name;
                this.minX = minX;
                this.maxX = maxX;
                this.minZ = minZ;
                this.maxZ = maxZ;
            }

            /// <summary>
            /// 대상 AABB 사각형과의 교차 여부를 판별합니다.
            /// </summary>
            /// <param name="bMinX">대상 최소 X</param>
            /// <param name="bMaxX">대상 최대 X</param>
            /// <param name="bMinZ">대상 최소 Z</param>
            /// <param name="bMaxZ">대상 최대 Z</param>
            /// <returns>교차 시 true 반환</returns>
            public bool Overlaps(float bMinX, float bMaxX, float bMinZ, float bMaxZ)
            {
                return bMinX <= maxX && bMaxX >= minX && bMinZ <= maxZ && bMaxZ >= minZ;
            }
        }

        private static readonly ZoneBounds[] INTEGRATED_ZONES = new ZoneBounds[]
        {
            new ZoneBounds("MilitaryBase", 10f, 206f, -408f, -250f),
            new ZoneBounds("JungleCampus", 240f, 292f, -312f, -260f),
            new ZoneBounds("WoodenHouse", 10f, 35f, -446f, -430f),
            new ZoneBounds("Workshop", 65f, 72f, -442f, -435f),
            new ZoneBounds("Bank", 10f, 84f, -534f, -466f),
            new ZoneBounds("Airport", 220f, 354f, -580f, -374f)
        };

        /// <summary>
        /// 건물 JSON 텍스트를 로드하고 26개 보존 건물을 필터링하여 씬에 생성합니다.
        /// </summary>
        /// <param name="parentRoot">건물들을 배치할 상위 트랜스폼</param>
        /// <returns>생성된 보존 건물 수</returns>
        public int BuildPreservedBuildings(Transform parentRoot)
        {
            string jsonText = LoadJsonText();
            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[PZMapBuilder] sulejkow_buildings.json 로드 실패!");
                return 0;
            }

            List<BuildingData> allBuildings = ParseBuildingsJson(jsonText);
            if (allBuildings == null || allBuildings.Count == 0)
            {
                Debug.LogError("[PZMapBuilder] 건물 데이터 파싱 실패 또는 빈 목록!");
                return 0;
            }

            FilterPreservedBuildings(allBuildings);

            Transform buildingsContainer = parentRoot != null ? parentRoot : transform;
            foreach (var b in _preservedBuildings)
            {
                BuildSingleBuilding(b, buildingsContainer);
            }

            return _preservedBuildings.Count;
        }

        /// <summary>
        /// TextAsset 또는 파일 경로로부터 건물 데이터 JSON 문자열을 로드합니다.
        /// </summary>
        /// <returns>JSON 문자열 또는 null</returns>
        public string LoadJsonText()
        {
            if (_buildingsJsonAsset != null)
            {
                return _buildingsJsonAsset.text;
            }

            TextAsset loadedAsset = Resources.Load<TextAsset>("SurvivalMap/sulejkow_buildings");
            if (loadedAsset != null)
            {
                return loadedAsset.text;
            }

            string projectPath = Path.Combine(Application.dataPath, "Resources/SurvivalMap/sulejkow_buildings.json");
            if (File.Exists(projectPath))
            {
                return File.ReadAllText(projectPath);
            }

            return null;
        }

        /// <summary>
        /// JSON 문자열을 파싱하여 건물 데이터 목록을 반환합니다.
        /// </summary>
        /// <param name="json">원본 JSON 문자열</param>
        /// <returns>파싱된 건물 데이터 목록</returns>
        public List<BuildingData> ParseBuildingsJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string wrapped = json.TrimStart();
            if (wrapped.StartsWith("["))
            {
                wrapped = "{\"buildings\":" + json + "}";
            }

            BuildingListWrapper wrapper = JsonUtility.FromJson<BuildingListWrapper>(wrapped);
            return wrapper?.buildings;
        }

        /// <summary>
        /// 360x360m 지도 범위와 6개 통합 구역과의 충돌을 검사하여 보존 대상 26개 건물을 추출합니다.
        /// </summary>
        /// <param name="allBuildings">전체 건물 데이터</param>
        public void FilterPreservedBuildings(List<BuildingData> allBuildings)
        {
            _preservedBuildings.Clear();
            if (allBuildings == null) return;

            foreach (var b in allBuildings)
            {
                float bMinX = b.bounds.worldX - PZ_ORIGIN_X;
                float bMaxX = bMinX + b.bounds.width;
                float bMaxZ = -(b.bounds.worldY - PZ_ORIGIN_Y);
                float bMinZ = -(b.bounds.worldY + b.bounds.height - PZ_ORIGIN_Y);

                if (bMaxX < MAP_MIN_X || bMinX > MAP_MAX_X || bMaxZ < MAP_MIN_Z || bMinZ > MAP_MAX_Z)
                {
                    continue;
                }

                bool overlapsZone = false;
                for (int i = 0; i < INTEGRATED_ZONES.Length; i++)
                {
                    if (INTEGRATED_ZONES[i].Overlaps(bMinX, bMaxX, bMinZ, bMaxZ))
                    {
                        overlapsZone = true;
                        break;
                    }
                }

                if (overlapsZone) continue;

                _preservedBuildings.Add(b);
            }
        }

        /// <summary>
        /// 단일 건물의 구조 타일, 문짝, 창문, 미로 격벽 및 외형 장식을 생성합니다.
        /// </summary>
        /// <param name="b">건물 데이터</param>
        /// <param name="parent">배치할 상위 트랜스폼</param>
        private void BuildSingleBuilding(BuildingData b, Transform parent)
        {
            string buildingUsage = (b.names != null && b.names.Count > 0) ? string.Join("_", b.names) : "residence";
            GameObject bldgObj = new GameObject($"Bldg_{b.buildingId:D3}_{buildingUsage}");
            bldgObj.transform.SetParent(parent, false);

            Dictionary<int, Transform> floorRoots = new Dictionary<int, Transform>();
            int maxFloor = 0;

            if (b.tiles != null)
            {
                for (int i = 0; i < b.tiles.Count; i++)
                {
                    var t = b.tiles[i];
                    if (t.z > maxFloor) maxFloor = t.z;

                    if (!floorRoots.TryGetValue(t.z, out Transform floorRoot))
                    {
                        GameObject fObj = new GameObject($"Floor_{t.z}");
                        fObj.transform.SetParent(bldgObj.transform, false);
                        floorRoot = fObj.transform;
                        floorRoots[t.z] = floorRoot;
                    }

                    int worldTileX = b.bounds.worldX + t.lx - PZ_ORIGIN_X;
                    int worldTileY = b.bounds.worldY + t.ly - PZ_ORIGIN_Y;
                    float posX = worldTileX;
                    float posY = t.z * _floorHeight;
                    float posZ = -worldTileY;

                    var (cat, sub) = PZTileClassifier.Classify(t.tile);
                    PrimitiveGeometry geom = PZTileClassifier.GetPrimitiveGeometry(cat, sub, t.tile, buildingUsage, _wallHeight);

                    Vector3 finalPos = new Vector3(posX, posY, posZ) + geom.localPosition;

                    if (geom.isDoor)
                    {
                        SpawnDoor(floorRoot, finalPos, geom, sub);
                    }
                    else if (geom.isWindow)
                    {
                        SpawnWindow(floorRoot, finalPos, geom);
                    }
                    else if (geom.hasCollider)
                    {
                        SpawnSolidPart(floorRoot, finalPos, geom, $"{cat}_{t.tile}");
                    }
                    else
                    {
                        SpawnVisualPart(floorRoot, finalPos, geom, $"{cat}_{t.tile}");
                    }
                }
            }

            if (_enableMazification)
            {
                ApplyMazification(b, bldgObj.transform, buildingUsage, maxFloor);
            }
        }

        /// <summary>
        /// F키 개폐와 파손 시 잠금 기능이 포함된 회전식 문짝을 생성합니다.
        /// </summary>
        private void SpawnDoor(Transform parent, Vector3 centerPos, PrimitiveGeometry geom, TileSubType sub)
        {
            bool isWest = (sub == TileSubType.WestDoor);

            Vector3 hingePos = isWest
                ? new Vector3(centerPos.x, centerPos.y - (geom.size.y * 0.5f), centerPos.z - (geom.size.z * 0.5f))
                : new Vector3(centerPos.x - (geom.size.x * 0.5f), centerPos.y - (geom.size.y * 0.5f), centerPos.z);

            GameObject hingeObj = new GameObject(isWest ? "WestDoor_Hinge" : "NorthDoor_Hinge");
            hingeObj.transform.SetParent(parent, false);
            hingeObj.transform.position = hingePos;

            GameObject leafObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafObj.name = "Door_Leaf";
            leafObj.transform.SetParent(hingeObj.transform, false);

            Vector3 leafOffset = isWest
                ? new Vector3(0f, geom.size.y * 0.5f, geom.size.z * 0.5f)
                : new Vector3(geom.size.x * 0.5f, geom.size.y * 0.5f, 0f);

            leafObj.transform.localPosition = leafOffset;
            leafObj.transform.localRotation = Quaternion.identity;
            leafObj.transform.localScale = geom.size;

            MeshRenderer renderer = leafObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMaterial(geom.color);
            }

            DestructibleTarget dt = null;
            if (_enableDestructibleTargets)
            {
                dt = leafObj.AddComponent<DestructibleTarget>();
            }

            SurvivalDoorInteractable doorInteractable = hingeObj.AddComponent<SurvivalDoorInteractable>();
            InteractionLabel label = hingeObj.GetComponent<InteractionLabel>();
            if (label != null)
            {
                label.SetTitle("문 열기");
            }
        }

        /// <summary>
        /// 파괴 가능한 반투명 유리창 큐브를 생성합니다.
        /// </summary>
        private void SpawnWindow(Transform parent, Vector3 centerPos, PrimitiveGeometry geom)
        {
            GameObject winObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            winObj.name = "Destructible_Window";
            winObj.transform.SetParent(parent, false);
            winObj.transform.position = centerPos;
            winObj.transform.localScale = geom.size;
            winObj.transform.rotation = geom.rotation;

            MeshRenderer renderer = winObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMaterial(geom.color);
            }

            if (_enableDestructibleTargets)
            {
                winObj.AddComponent<DestructibleTarget>();
            }
        }

        /// <summary>
        /// 파괴 가능한 일반 고체 벽/바닥/지붕 큐브를 생성합니다.
        /// </summary>
        private void SpawnSolidPart(Transform parent, Vector3 centerPos, PrimitiveGeometry geom, string name)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.position = centerPos;
            cube.transform.localScale = geom.size;
            cube.transform.rotation = geom.rotation;

            MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMaterial(geom.color);
            }

            if (_enableDestructibleTargets)
            {
                cube.AddComponent<DestructibleTarget>();
            }
        }

        /// <summary>
        /// 충돌체 없는 얇은 시각 장식 평면을 생성합니다.
        /// </summary>
        private void SpawnVisualPart(Transform parent, Vector3 centerPos, PrimitiveGeometry geom, string name)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.position = centerPos;
            visual.transform.localScale = geom.size;
            visual.transform.rotation = geom.rotation;

            Collider col = visual.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }

            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateMaterial(geom.color);
            }
        }

        /// <summary>
        /// 건물 내부 격벽, 바리케이드, 단층 건물의 옥상 증축 및 계단/난간을 절차적으로 배치하여 미로화합니다.
        /// </summary>
        private void ApplyMazification(BuildingData b, Transform bldgRoot, string usage, int maxFloor)
        {
            GameObject mazeRoot = new GameObject("Maze_Extensions");
            mazeRoot.transform.SetParent(bldgRoot, false);

            Color barricadeColor = new Color(0.42f, 0.35f, 0.28f);

            if (b.rooms != null)
            {
                for (int r = 0; r < b.rooms.Count; r++)
                {
                    var room = b.rooms[r];
                    if (room.rects == null) continue;

                    for (int rc = 0; rc < room.rects.Count; rc++)
                    {
                        var rect = room.rects[rc];
                        if (rect.w >= 4 && rect.h >= 4)
                        {
                            float bMinX = b.bounds.worldX - PZ_ORIGIN_X;
                            float bMinZ = -(b.bounds.worldY - PZ_ORIGIN_Y);

                            float midX = bMinX + rect.x + (rect.w * 0.5f);
                            float midZ = bMinZ - (rect.y + (rect.h * 0.5f));
                            float floorY = room.floor * _floorHeight;

                            GameObject barricade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            barricade.name = $"Barricade_{room.name}_{r}";
                            barricade.transform.SetParent(mazeRoot.transform, false);
                            barricade.transform.position = new Vector3(midX, floorY + 1.0f, midZ);
                            barricade.transform.localScale = new Vector3(Mathf.Max(1.0f, rect.w - 1.6f), 2.0f, 0.25f);

                            var rend = barricade.GetComponent<MeshRenderer>();
                            if (rend != null) rend.sharedMaterial = GetOrCreateMaterial(barricadeColor);

                            if (_enableDestructibleTargets) barricade.AddComponent<DestructibleTarget>();
                        }
                    }
                }
            }

            if (maxFloor == 0)
            {
                float bMinX = b.bounds.worldX - PZ_ORIGIN_X;
                float bMinZ = -(b.bounds.worldY - PZ_ORIGIN_Y);
                float centerX = bMinX + (b.bounds.width * 0.5f);
                float centerZ = bMinZ - (b.bounds.height * 0.5f);
                float roofY = _floorHeight;
                float parapetThick = 0.3f;
                float parapetHeight = 1.0f;
                var parapetMat = GetOrCreateMaterial(new Color(0.35f, 0.35f, 0.35f));

                GameObject parapetGroup = new GameObject("Rooftop_Parapet_Enclosure");
                parapetGroup.transform.SetParent(mazeRoot.transform, false);

                // South parapet
                CreateParapetSegment("Parapet_South", parapetGroup.transform,
                    new Vector3(centerX, roofY + parapetHeight * 0.5f, bMinZ - parapetThick * 0.5f),
                    new Vector3(b.bounds.width, parapetHeight, parapetThick), parapetMat);

                // North parapet
                CreateParapetSegment("Parapet_North", parapetGroup.transform,
                    new Vector3(centerX, roofY + parapetHeight * 0.5f, bMinZ - b.bounds.height + parapetThick * 0.5f),
                    new Vector3(b.bounds.width, parapetHeight, parapetThick), parapetMat);

                // West parapet
                CreateParapetSegment("Parapet_West", parapetGroup.transform,
                    new Vector3(bMinX + parapetThick * 0.5f, roofY + parapetHeight * 0.5f, centerZ),
                    new Vector3(parapetThick, parapetHeight, b.bounds.height), parapetMat);

                // East parapet
                CreateParapetSegment("Parapet_East", parapetGroup.transform,
                    new Vector3(bMinX + b.bounds.width - parapetThick * 0.5f, roofY + parapetHeight * 0.5f, centerZ),
                    new Vector3(parapetThick, parapetHeight, b.bounds.height), parapetMat);

                GameObject stairRamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stairRamp.name = "Exterior_Access_Ramp";
                stairRamp.transform.SetParent(mazeRoot.transform, false);
                stairRamp.transform.position = new Vector3(bMinX - 1.0f, roofY * 0.5f, centerZ);
                stairRamp.transform.localScale = new Vector3(1.4f, 0.3f, roofY * 1.5f);
                stairRamp.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

                var rampRend = stairRamp.GetComponent<MeshRenderer>();
                if (rampRend != null) rampRend.sharedMaterial = GetOrCreateMaterial(new Color(0.5f, 0.45f, 0.4f));

                if (_enableDestructibleTargets)
                {
                    stairRamp.AddComponent<DestructibleTarget>();
                }
            }
        }

        /// <summary>
        /// ?μ긽 ?뚮몢由??쒓컙 媛쒕퀎 ?멸렇癒쇳듃瑜??앹꽦?섍퀬 ?뚭눼 媛??而댄룷?뚰듃瑜??ㅼ젙?⑸땲??
        /// </summary>
        private void CreateParapetSegment(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = name;
            segment.transform.SetParent(parent, false);
            segment.transform.position = pos;
            segment.transform.localScale = size;
            var rend = segment.GetComponent<MeshRenderer>();
            if (rend != null) rend.sharedMaterial = mat;
            if (_enableDestructibleTargets)
            {
                segment.AddComponent<DestructibleTarget>();
            }
        }

        /// <summary>
        /// 吏?뺥븳 ?됱긽????묓븯??URP Lit ?먮뒗 Standard 癒명떚由ъ뼹??罹먯떆?먯꽌 媛?몄삤嫄곕굹 ?앹꽦?⑸땲??
        /// ?먮뵒?곗뿉?쒕뒗 ??????꾩뿉???좏슚?섎룄濡?Assets/Materials/Survival ?붾젆?곕━???먯뀑?쇰줈 ?곴뎄 蹂댁〈?⑸땲??
        /// </summary>
        /// <param name="color">癒명떚由ъ뼹 ?됱긽</param>
        /// <returns>?앹꽦?섍굅??罹먯떆??Material</returns>
        public Material GetOrCreateMaterial(Color color)
        {
            if (_materialCache.TryGetValue(color, out Material mat) && mat != null)
            {
                return mat;
            }

#if UNITY_EDITOR
            string dir = "Assets/Materials/Survival";
            string assetPath = $"{dir}/Survival_Mat_{ColorUtility.ToHtmlStringRGBA(color)}.mat";
            Material existingMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existingMat != null)
            {
                _materialCache[color] = existingMat;
                return existingMat;
            }
#endif

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material newMat = new Material(shader)
            {
                name = $"Survival_Mat_{ColorUtility.ToHtmlStringRGBA(color)}",
                color = color
            };

            if (color.a < 0.95f)
            {
                newMat.SetFloat("_Mode", 3);
                newMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                newMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                newMat.SetInt("_ZWrite", 0);
                newMat.renderQueue = 3000;
            }

#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
            }
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "Survival");
            }
            UnityEditor.AssetDatabase.CreateAsset(newMat, assetPath);
#endif

            _materialCache[color] = newMat;
            return newMat;
        }
    }
}
