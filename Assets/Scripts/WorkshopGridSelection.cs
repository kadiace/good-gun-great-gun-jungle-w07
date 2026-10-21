using UnityEngine;

namespace WeaponWorkshop
{
    public enum WorkshopPlanarAxis { X, Y, Z }

    // A cell on the active plane. Adjacency is checked in all three assembly coordinates.
    public readonly struct WorkshopGridSelection
    {
        public readonly CubeAssembly Assembly;
        public readonly Vector3Int AnchorCell, Cell, InputDirection, OutputDirection;
        public readonly WorkshopPlanarAxis Axis;
        public readonly int Depth, ModuleId;
        public readonly bool IsValid;
        public readonly string FailureReason;

        internal WorkshopGridSelection(CubeAssembly assembly,
            Vector3Int cell, Vector3Int input, Vector3Int output, WorkshopPlanarAxis axis,
            int moduleId, bool valid, string reason)
        {
            Assembly = assembly; AnchorCell = cell - input; Cell = cell;
            InputDirection = input; OutputDirection = output; Axis = axis; ModuleId = moduleId;
            Depth = WorkshopPlanarCoordinates.Depth(cell, axis);
            IsValid = valid; FailureReason = reason;
        }
    }

    public static class WorkshopPlanarCoordinates
    {
        public static Vector3 Normal(WorkshopPlanarAxis axis) => axis == WorkshopPlanarAxis.X
            ? Vector3.right : axis == WorkshopPlanarAxis.Y ? Vector3.up : Vector3.forward;
        public static Vector3 Up(WorkshopPlanarAxis axis) => axis == WorkshopPlanarAxis.Y ? Vector3.forward : Vector3.up;
        public static int Depth(Vector3Int cell, WorkshopPlanarAxis axis) => axis == WorkshopPlanarAxis.X
            ? cell.x : axis == WorkshopPlanarAxis.Y ? cell.y : cell.z;
        public static Vector3 Point(float column, float row, float depth, WorkshopPlanarAxis axis)
            => axis == WorkshopPlanarAxis.X ? new Vector3(depth, row, column)
                : axis == WorkshopPlanarAxis.Y ? new Vector3(column, depth, row) : new Vector3(column, row, depth);
    }

    internal static class WorkshopGridPicker
    {
        private static readonly Vector3Int[] Directions =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            Vector3Int.forward, Vector3Int.back
        };

        internal static bool TryEvaluate(CubeAssembly assembly, Vector3Int cell,
            CubeBuilder builder, WorkshopPlanarAxis axis, out WorkshopGridSelection selection)
        {
            selection = default;
            if (assembly == null || builder == null) return false;

            int slot = builder.SelectedSlot;
            bool adjacent = TryGetAdjacentDirection(assembly, cell, out var input);
            Vector3Int output = input;
            string reason = null;
            var weapon = assembly.GetComponentInParent<EquipableWeapon>();
            if (weapon == null || weapon.ConstructionLocked || builder.IsCustomMode) reason = "커스텀을 제거한 뒤 조립할 수 있습니다.";
            else if (builder.PaletteOpen || builder.ColorPickerOpen) reason = "모듈 선택 창을 먼저 닫아 주세요.";
            else if (builder.DeleteMode) reason = "3D로 돌아가 삭제 모드를 해제해 주세요.";
            else if (builder.IsModuleLocked(slot)) reason = "잠긴 모듈입니다.";
            else if (!TutorialManager.ActionAllowed("attach")) reason = "현재 튜토리얼 단계에서는 설치할 수 없습니다.";
            else if (!adjacent) reason = "코어 또는 배치된 모듈과 면으로 인접한 셀을 선택하세요.";
            else if (!assembly.CanAttach(cell, slot)) reason = "이미 사용 중이거나 설치할 수 없는 셀입니다.";
            else
            {
                var prefab = assembly.GetPrefab(slot);
                var block = prefab != null ? prefab.GetComponent<PlacedBlock>() : null;
                bool followsFeed = slot != 1 && !(block != null && block.IsSight) && !assembly.IsTriggerModuleSlot(slot);
                if (followsFeed && !assembly.TryGetFeedDirection(cell, out input)) reason = "총구에만 설치할 수 있습니다.";
                else
                {
                    output = input;
                    bool oriented = assembly.CanOrient(cell, slot, output, input);
                    if (!oriented && (CubeAssembly.IsSingleOutput(slot) || slot == 3))
                        foreach (var direction in Directions)
                        {
                            if (followsFeed && direction == -input) continue;
                            if (!assembly.CanOrient(cell, slot, direction, input)) continue;
                            output = direction; oriented = true; break;
                        }
                    if (!oriented) reason = "출력 방향이 코어 또는 다른 출력과 충돌합니다.";
                    else
                    {
                        // The same read-only restriction as TryAuthorizePlacement, without hover notifications.
                        var tutorial = TutorialManager.Instance;
                        var step = tutorial != null ? tutorial.CurrentStep : null;
                        if (step != null && (step.restrictPlacement || step.completion == TutorialCompletion.FiveCoreFacesBlocked))
                        {
                            bool expected = tutorial.TutorialWorkbench != null &&
                                tutorial.TutorialWorkbench.CurrentWeapon == weapon;
                            bool allowed = step.completion == TutorialCompletion.FiveCoreFacesBlocked
                                ? slot == 1 && cell != step.openCoreFace && Mathf.Abs(cell.x) + Mathf.Abs(cell.y) + Mathf.Abs(cell.z) == 1
                                : slot == step.requiredModuleId && cell == step.requiredCell && step.MatchesOutputDirection(output);
                            if (!expected || !allowed) reason = "튜토리얼에서 안내한 위치와 방향을 선택해 주세요.";
                        }
                    }
                }
            }
            selection = new WorkshopGridSelection(assembly, cell, input, output, axis, slot,
                reason == null, reason ?? string.Empty);
            return true;
        }

        private static bool TryGetAdjacentDirection(CubeAssembly assembly, Vector3Int cell, out Vector3Int direction)
        {
            direction = Vector3Int.zero;
            var blocks = assembly.GetComponentsInChildren<PlacedBlock>();
            foreach (var candidate in Directions)
            {
                Vector3Int neighbor = cell - candidate;
                if (neighbor == Vector3Int.zero) { direction = candidate; return true; }
                foreach (var block in blocks)
                    if (block.transform.parent == assembly.transform && block.Cell == neighbor)
                    { direction = candidate; return true; }
            }
            return false;
        }
    }
}
