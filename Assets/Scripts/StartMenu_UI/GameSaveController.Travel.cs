using System;
using System.Linq;
using UnityEngine;
namespace WeaponWorkshop
{
    public sealed partial class GameSaveController
    {
        public static bool TryCaptureCarried(EquipableWeapon weapon,out WeaponWorldSave state,out string error, bool allowMoving = false)
        {
            state=null;error=null;
            if(weapon.IsMoving && !allowMoving){error="무기 이동이 끝난 후 귀환하세요.";return false;}
            var assembly=weapon.GetComponentInChildren<CubeAssembly>(true);
            if(assembly==null||!assembly.TryCaptureLayout(out var layout)){error="장착한 무기를 저장할 수 없습니다.";return false;}
            var passedTests = weapon.CapturePassedTests(out var testKey);
            state=new WeaponWorldSave { weaponName=weapon.WeaponName, passedTestIds=passedTests,testAssemblyKey=testKey,held=true,active=true,position=Vector3.zero,rotation=Quaternion.identity,
                scale=weapon.UnequippedWorldScale,paidCost=layout.paidConstructionCost,blocks=CaptureBlocks(layout),
                constructionLocked=weapon.ConstructionLocked,paint=CapturePaint(assembly),
                hasSelectedScopeCell=weapon.HasSelectedScopeCell,selectedScopeCell=weapon.SelectedScopeCell,
                returnBenchId=weapon.GetComponent<RaidWeaponOrigin>()?.ReturnBenchId };
            return true;
        }
        public static bool TryRestoreCarried(WeaponWorldSave state, EquipableWeapon prefab,
            FirstPersonPlayer player, PaintMaterialLibrary paints, out string error)
            => TryRestoreRaidWeapon(state, prefab, player, paints, true, out _, out error);

        public static bool TryRestoreRaidWeapon(WeaponWorldSave state, EquipableWeapon prefab,
            FirstPersonPlayer player, PaintMaterialLibrary paints, bool equip, out EquipableWeapon restored, out string error)
        {
            restored = null;
            error = null;
            if (state == null) return true;
            if (prefab == null || player == null || paints == null)
            { error = "습격 플레이어 또는 무기 복원용 프리팹 연결이 없습니다."; return false; }
            var template = prefab.GetComponentInChildren<CubeAssembly>(true);
            if (template == null || state.blocks == null || state.blocks.Any(b => template.GetPrefab(b.slot) == null))
            { error = "이동할 무기의 모듈 프리팹을 찾을 수 없습니다."; return false; }
            var materials = new Material[state.paint?.Length ?? 0];
            for (int i = 0; i < materials.Length; i++)
            {
                var key = state.paint[i].materialKey;
                materials[i] = paints.Options.FirstOrDefault(p => p.material != null && p.material.name == key)?.material;
                const string prefix = "Paint - Custom RGB #";
                if (materials[i] == null && key != null && key.StartsWith(prefix, StringComparison.Ordinal) &&
                    ColorUtility.TryParseHtmlString("#" + key.Substring(prefix.Length), out var color))
                    materials[i] = paints.GetCustomColorMaterial(color);
                if (materials[i] == null) { error = "이동할 무기의 도색을 복원할 수 없습니다."; return false; }
            }
            var weapon = Instantiate(prefab, player.transform.position, Quaternion.identity);
            weapon.SetWeaponName(state.weaponName);
            var assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
            var blocks = state.blocks.Select(b => new CubeAssembly.BlockSnapshot { slot = b.slot,
                prefab = assembly.GetPrefab(b.slot), cell = b.cell, outputDirection = b.output,
                orientationNormal = b.normal, paidPurchaseCost = b.paidCost, paidModuleCreditCost = b.paidModuleCreditCost, hasColor = b.hasColor, color = b.color }).ToArray();
            if (!assembly.RestoreLayout(new CubeAssembly.LayoutSnapshot(blocks, state.paidCost)))
            { Destroy(weapon.gameObject); error = "이동할 무기 조립을 복원할 수 없습니다."; return false; }
            weapon.RestorePassedTests(state.passedTestIds, state.testAssemblyKey);
            for (int i = 0; i < materials.Length; i++)
            {
                var face = state.paint[i];
                var block = assembly.GetComponentsInChildren<PlacedBlock>(true)
                    .FirstOrDefault(b => b.Cell == face.cell && b.transform.parent == assembly.transform);
                if (block == null) continue;
                var paint = block.GetComponent<ModuleFacePaint>() ?? block.gameObject.AddComponent<ModuleFacePaint>();
                paint.SetMaterial(face.face, materials[i]);
            }
            weapon.transform.localScale = state.scale;
            if (state.constructionLocked) weapon.LockConstruction();
            weapon.RestoreSelectedScopeCell(state.selectedScopeCell, state.hasSelectedScopeCell);
            weapon.gameObject.AddComponent<RaidWeaponOrigin>().Initialize(state.returnBenchId);
            if (equip && !player.GetComponent<WeaponHolder>().Equip(weapon))
            { Destroy(weapon.gameObject); error = "이동한 무기를 장착할 수 없습니다."; return false; }
            restored = weapon;
            return true;
        }
    }
}
