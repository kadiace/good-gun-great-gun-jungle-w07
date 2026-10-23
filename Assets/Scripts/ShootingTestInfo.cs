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
            foreach (var requirement in row.requiredModules ?? Array.Empty<ShootingTestTableData.ModuleRequirement>())
                if (!TryGetModuleKind(requirement, out _)) return false;
            return true;
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
