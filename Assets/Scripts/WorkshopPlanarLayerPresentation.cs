using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 렌더링 억제의 단일 소유자. 점유/충돌/에너지에 쓰는 활성 상태와 재질은 변경하지 않는다.
    [DefaultExecutionOrder(105)]
    public sealed class WorkshopPlanarLayerPresentation : MonoBehaviour
    {
        private readonly Dictionary<Renderer, bool> originals = new();
        private readonly List<(Renderer renderer, Vector3Int cell)> renderers = new();
        private readonly List<Vector3Int> cells = new();
        private readonly Dictionary<int, int> layerCounts = new();
        private readonly Dictionary<Vector2Int, Vector3Int> references = new();
        private readonly HashSet<Vector2Int> selected = new();
        private WorkshopPlanarView view;
        private CubeAssembly assembly;
        private EquipableWeapon weapon;
        private bool visible, dirty;
        private int depth;
        private WorkshopViewAxis axis;
        public IReadOnlyDictionary<Vector2Int, Vector3Int> References => references;
        public int CountAt(int value) => layerCounts.TryGetValue(value, out int count) ? count : 0;

        private void Awake() => visible = false;
        public void Bind(WorkshopPlanarView value, bool show)
        {
            view = value; visible = show;
            if (!show) Release();
        }
        private void MarkDirty() => dirty = true;
        private void LateUpdate()
        {
            if (!visible || view == null || !view.Is2D || view.Assembly == null) { Release(); return; }
            if (assembly != view.Assembly)
            {
                Release();
                assembly = view.Assembly;
                weapon = assembly.GetComponentInParent<EquipableWeapon>();
                if (weapon != null) weapon.AssemblyChanged += MarkDirty;
                dirty = true;
            }
            if (dirty) Rebuild();
            if (dirty || depth != view.SelectedDepth || axis != view.Axis) ApplyLayer();
            dirty = false;
        }
        private void RestoreRenderers()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            originals.Clear(); renderers.Clear();
        }
        private void Rebuild()
        {
            RestoreRenderers(); cells.Clear(); cells.Add(Vector3Int.zero);
            foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>())
                if (block.transform.parent == assembly.transform) cells.Add(block.Cell);
            foreach (var renderer in assembly.GetComponentsInChildren<Renderer>(true))
            {
                var block = renderer.GetComponentInParent<PlacedBlock>();
                if (block != null && block.transform.parent != assembly.transform) continue;
                originals[renderer] = renderer.forceRenderingOff;
                renderers.Add((renderer, block != null ? block.Cell : Vector3Int.zero));
            }
        }
        private Vector2Int ProjectCell(Vector3Int cell) => new(
            CubePlacementState.Dot(cell, view.Horizontal), CubePlacementState.Dot(cell, view.Vertical));
        private void ApplyLayer()
        {
            depth = view.SelectedDepth; axis = view.Axis;
            references.Clear(); selected.Clear(); layerCounts.Clear();
            foreach (var cell in cells)
            {
                int layer = view.DepthOf(cell);
                layerCounts[layer] = CountAt(layer) + 1;
                if (layer == depth) selected.Add(ProjectCell(cell));
            }
            foreach (var cell in cells)
            {
                var point = ProjectCell(cell);
                if (selected.Contains(point)) continue;
                if (!references.TryGetValue(point, out var previous) ||
                    Mathf.Abs(view.DepthOf(cell) - depth) < Mathf.Abs(view.DepthOf(previous) - depth))
                    references[point] = cell;
            }
            foreach (var item in renderers)
                if (item.renderer != null)
                    item.renderer.forceRenderingOff = originals[item.renderer] || view.DepthOf(item.cell) != depth;
        }
        private void Release()
        {
            if (!ReferenceEquals(weapon, null)) weapon.AssemblyChanged -= MarkDirty;
            weapon = null; assembly = null;
            RestoreRenderers(); cells.Clear(); references.Clear(); layerCounts.Clear(); selected.Clear();
        }
        private void OnDisable() { visible = false; Release(); }
        private void OnDestroy() => Release();
    }
}
