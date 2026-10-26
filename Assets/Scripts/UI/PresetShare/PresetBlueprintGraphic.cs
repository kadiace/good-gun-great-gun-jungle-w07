using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    /// <summary>
    /// WeaponShareCode 블루프린트 블록을 카메라·생성 오브젝트·텍스처 자산·렌더텍스처 없이
    /// UI 정점만으로 그리는 등각 아이소메트릭 실루엣 그래픽입니다. 블록은 부착 모듈만 담고
    /// 코어는 암묵적이므로 블루프린트가 있으면 원점 코어를 항상 함께 그립니다. 저장된 도색이
    /// 있으면 그 색을, 코어는 코어 색으로, 나머지 모듈은 모듈 색으로 그립니다.
    /// 블루프린트 참조가 바뀔 때만 다시 그리며 블루프린트가 없는 빈 슬롯은 아무것도 그리지 않습니다.
    /// </summary>
    public sealed class PresetBlueprintGraphic : MaskableGraphic
    {
        // +x는 오른쪽 아래, +z는 왼쪽 아래로 내려가는 30도 등각 투영 기저.
        private const float IsoCos = 0.8660254f;
        private const float IsoSin = 0.5f;
        // 윗면이 가장 밝고 +x 옆면, +z 앞면 순으로 어두워지는 고정 음영 배율.
        private const float TopShade = 1f;
        private const float RightShade = 0.78f;
        private const float FrontShade = 0.6f;

        [SerializeField] private float m_padding = 8f;
        [SerializeField] private Color m_coreColor = new Color(0.937f, 0.765f, 0.357f, 1f); // 따뜻한 금색
        [SerializeField] private Color m_moduleColor = new Color(0.616f, 0.659f, 0.71f, 1f); // 차가운 회색

        private WeaponShareCode.Blueprint m_blueprint;

        protected override void Awake()
        {
            // CanvasRenderer가 없으면 그래픽이 캔버스에 제출되지 않아 메시가 만들어지지 않는다(프리팹에 누락된 경우 대비).
            if (GetComponent<CanvasRenderer>() == null) gameObject.AddComponent<CanvasRenderer>();
            base.Awake();
            // 미리보기 그래픽은 클릭·레이캐스트 대상이 아니므로 프리팹 저장값과 무관하게 항상 끈다.
            raycastTarget = false;
        }

        /// <summary>
        /// 그릴 블루프린트를 바꿉니다. null이면 빈 슬롯으로 보고 아무것도 그리지 않고,
        /// 블록 배열이 있는 블루프린트는 블록이 비어 있어도 원점 코어를 그립니다.
        /// 참조가 실제로 바뀔 때만 정점을 다시 만들도록 dirty를 표시합니다.
        /// </summary>
        public void SetBlueprint(WeaponShareCode.Blueprint blueprint)
        {
            if (ReferenceEquals(m_blueprint, blueprint)) return;
            m_blueprint = blueprint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var blueprint = m_blueprint;
            var blocks = blueprint != null ? blueprint.blocks : null;
            // 블루프린트가 없는 빈 슬롯이나 블록 배열이 없는 데이터는 아무것도 그리지 않는다.
            if (blueprint == null || blocks == null) return;

            // 블록은 부착 모듈만 담고 코어는 암묵적이다. 저장된 총은 블록이 없어도 원점 코어를 그린다.
            var ordered = new List<WeaponShareCode.Block>(blocks.Length + 1);
            bool hasCore = false;
            foreach (var block in blocks)
            {
                if (block == null) continue;
                if (block.cell == Vector3Int.zero) hasCore = true;
                ordered.Add(block);
            }
            if (!hasCore) ordered.Add(new WeaponShareCode.Block { cell = Vector3Int.zero });

            Rect rect = GetPixelAdjustedRect();
            float innerWidth = rect.width - m_padding * 2f;
            float innerHeight = rect.height - m_padding * 2f;
            if (innerWidth <= 0f || innerHeight <= 0f) return;

            // 블록마다 최소·최대 모서리 투영으로 전체 경계를 구해 안쪽 영역에 비율을 유지하며 맞춘다.
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var block in ordered) ExpandBounds(block.cell, ref minX, ref maxX, ref minY, ref maxY);
            if (minX > maxX || minY > maxY) return;

            float cellSize = Mathf.Min(innerWidth / (maxX - minX), innerHeight / (maxY - minY));
            Vector2 origin = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f)
                - new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f) * cellSize;

            // 시선 (1,1,1) 등각 투영에서 겹치는 큐브가 뒤에서 앞으로 덮이도록 깊이(x+y+z) 오름차순으로 그린다.
            ordered.Sort(CompareDepth);
            foreach (var block in ordered) AddCube(vertexHelper, block, origin, cellSize);
        }

        private void AddCube(VertexHelper vertexHelper, WeaponShareCode.Block block, Vector2 origin, float cellSize)
        {
            Color baseColor = block.hasColor ? block.color
                : block.cell == Vector3Int.zero ? m_coreColor : m_moduleColor;
            int x = block.cell.x, y = block.cell.y, z = block.cell.z;

            // 윗면(y = y+1): 시계 방향으로 위→오른쪽→아래→왼쪽.
            AddQuad(vertexHelper,
                Project(x, y + 1, z, origin, cellSize),
                Project(x + 1, y + 1, z, origin, cellSize),
                Project(x + 1, y + 1, z + 1, origin, cellSize),
                Project(x, y + 1, z + 1, origin, cellSize),
                Shaded(baseColor, TopShade));

            // +x 옆면(x = x+1): 시계 방향으로 오른쪽 위→오른쪽 아래→왼쪽 아래→왼쪽 위.
            AddQuad(vertexHelper,
                Project(x + 1, y + 1, z, origin, cellSize),
                Project(x + 1, y, z, origin, cellSize),
                Project(x + 1, y, z + 1, origin, cellSize),
                Project(x + 1, y + 1, z + 1, origin, cellSize),
                Shaded(baseColor, RightShade));

            // +z 앞면(z = z+1): 시계 방향으로 왼쪽 위→왼쪽 아래→오른쪽 아래→오른쪽 위.
            AddQuad(vertexHelper,
                Project(x, y + 1, z + 1, origin, cellSize),
                Project(x, y, z + 1, origin, cellSize),
                Project(x + 1, y, z + 1, origin, cellSize),
                Project(x + 1, y + 1, z + 1, origin, cellSize),
                Shaded(baseColor, FrontShade));
        }

        private void AddQuad(VertexHelper vertexHelper, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color quadColor)
        {
            int index = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, v0, quadColor);
            AddVertex(vertexHelper, v1, quadColor);
            AddVertex(vertexHelper, v2, quadColor);
            AddVertex(vertexHelper, v3, quadColor);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index + 2, index + 3, index);
        }

        // RGB에만 음영과 Graphic 색을 곱하고 알파는 원래 값 곱 tint.a를 유지한다.
        private Color Shaded(Color baseColor, float shade)
        {
            Color tint = color;
            return new Color(baseColor.r * shade * tint.r, baseColor.g * shade * tint.g,
                baseColor.b * shade * tint.b, baseColor.a * tint.a);
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color vertexColor)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertex.uv0 = Vector4.zero;
            vertexHelper.AddVert(vertex);
        }

        private Vector2 Project(float x, float y, float z, Vector2 origin, float cellSize)
            => origin + new Vector2((x - z) * IsoCos, y - (x + z) * IsoSin) * cellSize;

        private static void ExpandBounds(Vector3Int cell, ref float minX, ref float maxX, ref float minY, ref float maxY)
        {
            float pxMin = (cell.x - cell.z - 1) * IsoCos;
            float pxMax = (cell.x - cell.z + 1) * IsoCos;
            float pyMin = cell.y - (cell.x + cell.z + 2) * IsoSin;
            float pyMax = cell.y + 1 - (cell.x + cell.z) * IsoSin;
            if (pxMin < minX) minX = pxMin;
            if (pxMax > maxX) maxX = pxMax;
            if (pyMin < minY) minY = pyMin;
            if (pyMax > maxY) maxY = pyMax;
        }

        private static int CompareDepth(WeaponShareCode.Block a, WeaponShareCode.Block b)
        {
            int depth = a.cell.x + a.cell.y + a.cell.z - (b.cell.x + b.cell.y + b.cell.z);
            if (depth != 0) return depth;
            depth = a.cell.x - b.cell.x;
            if (depth != 0) return depth;
            depth = a.cell.y - b.cell.y;
            if (depth != 0) return depth;
            return a.cell.z - b.cell.z;
        }
    }
}
