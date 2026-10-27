using System;
using System.Collections.Generic;
using UnityEngine;
namespace WeaponWorkshop
{
    public static class GunShapeInfo
    {
        public static GunShapeData.Shape Find(this GunShapeData table, string id)
        {
            foreach (var shape in table?.shapes ?? Array.Empty<GunShapeData.Shape>())
                if (shape != null && shape.valid && shape.id == id) return shape;
            return null;
        }
        public static void Validate(GunShapeData table)
        {
            var ids = new HashSet<string>();
            foreach (var s in table.shapes ?? Array.Empty<GunShapeData.Shape>())
            {
                if (s == null) { Debug.LogError("빈 형태 행입니다."); continue; }
                s.valid = !string.IsNullOrEmpty(s.id) && ids.Add(s.id) && s.plane == "zy";
                s.valid &= s.toleranceCells >= 0 && s.toleranceRatio >= 0f && s.toleranceRatio <= 1f;
                if (s.mode == "bounds") s.valid &= s.width > 0 && s.height > 0 && s.depth > 0;
                else if (s.mode == "silhouette")
                {
                    int width = s.rows != null && s.rows.Length > 0 ? s.rows[0]?.Length ?? 0 : 0;
                    bool filled = false;
                    s.valid &= width > 0 && s.anchorColumn >= 0 && s.anchorColumn < width &&
                        s.anchorRow >= 0 && s.rows != null && s.anchorRow < s.rows.Length;
                    foreach (string row in s.rows ?? Array.Empty<string>())
                    {
                        s.valid &= row != null && row.Length == width;
                        foreach (char c in row ?? "") { filled |= c == '#'; s.valid &= c == '#' || c == '.' || c == '?'; }
                    }
                    s.valid &= filled;
                }
                else s.valid = false;
                if (!s.valid) Debug.LogError("형태 영역 표를 확인하세요: " + s.id);
            }
        }
        public static HashSet<Vector3Int> OccupiedCells(CubeAssembly assembly)
        {
            var cells = new HashSet<Vector3Int> { Vector3Int.zero };
            if (assembly != null)
                foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>())
                    if (block.transform.parent == assembly.transform) cells.Add(block.Cell);
            return cells;
        }
        // 코어 기준 허용 영역: 빈칸은 자유, 영역 밖 셀은 거부.
        public static bool Contains(GunShapeData.Shape s, Vector3Int cell, bool mirrored = false)
        {
            if (s == null || !s.valid) return false;
            if (mirrored) cell.z = -cell.z;
            if (s.mode == "bounds")
                return cell.x >= -(s.width / 2) && cell.x < s.width - s.width / 2 &&
                    cell.y >= -(s.height / 2) && cell.y < s.height - s.height / 2 &&
                    cell.z >= -(s.depth / 2) && cell.z < s.depth - s.depth / 2;
            if (cell.x != 0) return false;
            int col = cell.z + s.anchorColumn, row = s.anchorRow - cell.y;
            return row >= 0 && row < s.rows.Length && col >= 0 && col < s.rows[row].Length && s.rows[row][col] != '.';
        }
        public static bool Matches(this GunShapeData table, string id, CubeAssembly assembly)
        {
            var shape = table.Find(id);
            if (shape == null || assembly == null || assembly.GetComponent<PowerCore>() == null) return false;
            var cells = OccupiedCells(assembly);
            if (shape.mode == "bounds") return MatchesBounds(shape, cells);
            var pattern = PatternCells(shape);
            var actual = new HashSet<Vector2Int>();
            foreach (var cell in cells)
            {
                if (cell.x != 0) return false;
                actual.Add(new Vector2Int(cell.z, cell.y));
            }
            return MatchesSilhouette(shape, pattern, actual, false) ||
                (shape.allowMirrorZ && MatchesSilhouette(shape, pattern, actual, true));
        }
        private static bool MatchesBounds(GunShapeData.Shape shape, HashSet<Vector3Int> cells)
        {
            int minX = 0, maxX = 0, minY = 0, maxY = 0, minZ = 0, maxZ = 0;
            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x); maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y); maxY = Mathf.Max(maxY, cell.y);
                minZ = Mathf.Min(minZ, cell.z); maxZ = Mathf.Max(maxZ, cell.z);
            }
            return maxX - minX + 1 == shape.width && maxY - minY + 1 == shape.height &&
                maxZ - minZ + 1 == shape.depth;
        }
        private static HashSet<Vector2Int> PatternCells(GunShapeData.Shape shape)
        {
            var pattern = new HashSet<Vector2Int>();
            for (int row = 0; row < shape.rows.Length; row++)
                for (int col = 0; col < shape.rows[row].Length; col++)
                    if (shape.rows[row][col] == '#') pattern.Add(new Vector2Int(col, row));
            return pattern;
        }
        private static bool MatchesSilhouette(GunShapeData.Shape shape, HashSet<Vector2Int> pattern,
            HashSet<Vector2Int> actual, bool mirrored)
        {
            var filled = new HashSet<Vector2Int>();
            int mismatch = 0;
            foreach (var cell in actual)
            {
                int z = mirrored ? -cell.x : cell.x;
                int col = z + shape.anchorColumn, row = shape.anchorRow - cell.y;
                bool inside = row >= 0 && row < shape.rows.Length && col >= 0 && col < shape.rows[row].Length;
                if (!inside || shape.rows[row][col] == '.') { mismatch++; continue; }
                filled.Add(new Vector2Int(col, row));
            }
            foreach (var cell in pattern) if (!filled.Contains(cell)) mismatch++;
            int allowed = Mathf.Min(shape.toleranceCells, Mathf.FloorToInt(pattern.Count * shape.toleranceRatio));
            return mismatch <= allowed;
        }
    }
}
