using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public static class ShootingTestInfo
    {
        public static ShootingTestTableData.Entry Find(this ShootingTestTableData table, string id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (var row in table?.tests ?? Array.Empty<ShootingTestTableData.Entry>())
                    if (row != null && row.id == id) return row;
            return null;
        }
        public static bool IsAvailable(this ShootingTestTableData table, string id)
        {
            var row = table.Find(id);
            return row != null && !string.IsNullOrWhiteSpace(row.id) && row.enabled && !string.IsNullOrWhiteSpace(row.name) &&
                row.timeLimit > 0f && !float.IsInfinity(row.timeLimit) && row.requiredDestruction > 0f && row.requiredDestruction <= 1f &&
                HasValidModuleRequirements(row);
        }
        public static bool TryGetModuleKind(ShootingTestTableData.ModuleRequirement requirement, out ModuleKind kind)
        {
            kind = default;
            return requirement != null && requirement.count > 0 &&
                Enum.TryParse(requirement.kind, false, out kind) && Enum.IsDefined(typeof(ModuleKind), kind);
        }
        private static bool HasValidModuleRequirements(ShootingTestTableData.Entry row)
        {
            foreach (string name in row.requiredUnlockModules ?? Array.Empty<string>())
                if (!TryUnlockKind(name, out _)) return false;
            foreach (var requirement in row.requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                if (!TryGetModuleKind(requirement, out _)) return false;
            return true;
        }
        private static bool TryUnlockKind(string name, out ModuleKind kind)
            => Enum.TryParse(name, false, out kind) && Enum.IsDefined(typeof(ModuleKind), kind);

        // 주문 등장과 시험 시작이 같은 해금 조건을 사용한다. 무기의 실제 장착 모듈은 검사하지 않는다.
        public static bool CanStart(this ShootingTestTableData table, string id, PlayerModuleUnlocks unlocks)
        {
            if (!table.IsAvailable(id)) return false;
            var row = table.Find(id);
            foreach (string name in row.requiredUnlockModules ?? Array.Empty<string>())
                if (!TryUnlockKind(name, out var kind) || unlocks == null || !unlocks.IsUnlocked(kind)) return false;
            foreach (var requirement in row.requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                if (!TryGetModuleKind(requirement, out var kind) || unlocks == null || !unlocks.IsUnlocked(kind)) return false;
            return true;
        }

        public static string UnlockDescription(this ShootingTestTableData table, string id, PlayerModuleUnlocks unlocks)
        {
            var names = new List<string>();
            var row = table.Find(id);
            foreach (string name in row?.requiredUnlockModules ?? Array.Empty<string>()) AddName(name);
            foreach (var requirement in row?.requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                AddName(requirement?.kind);
            return names.Count == 0 ? "" : string.Join("·", names) + " 모듈 해금 필요";

            void AddName(string name)
            {
                if (!TryUnlockKind(name, out var kind) || (unlocks != null && unlocks.IsUnlocked(kind))) return;
                var module = unlocks != null ? unlocks.FindModule(kind) : null;
                string label = module != null ? module.DisplayName : name;
                if (!names.Contains(label)) names.Add(label);
            }
        }
        public static void Validate(ShootingTestTableData table)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in table.tests ?? Array.Empty<ShootingTestTableData.Entry>())
                if (row == null || string.IsNullOrWhiteSpace(row.id) || !ids.Add(row.id) ||
                    (row.enabled && !table.IsAvailable(row.id))) Debug.LogError("사격 테스트 표의 ID·시간·합격 기준·필요 모듈을 확인하세요.");
        }
    }
}
