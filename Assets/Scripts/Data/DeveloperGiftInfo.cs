using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

namespace WeaponWorkshop
{
    public static class DeveloperGiftInfo
    {
        /// <summary>
        /// Decodes the configured weapon code, restores its layout, and applies the table-owned paint and stickers.
        /// weapon receives the layout; saveController resolves paint IDs and error explains any missing data.
        /// </summary>
        public static bool TryBuildWeapon(EquipableWeapon weapon, GameSaveController saveController, out string error)
        {
            error = null;
            if (weapon == null) { error = "보상 총을 만들 수 없습니다."; return false; }
            if (saveController == null) { error = "보상 총 도색 연결을 찾을 수 없습니다."; return false; }
            DeveloperGiftData gift = TableManager.Instance.DeveloperGift;
            if (gift == null || string.IsNullOrEmpty(gift.weaponCode))
            { error = "보상 총 코드가 비어 있습니다."; return false; }
            CubeAssembly assembly = weapon.GetComponentInChildren<CubeAssembly>(true);
            if (assembly == null) { error = "보상 총의 모듈 연결을 찾을 수 없습니다."; return false; }

            WeaponShareCode.Blueprint blueprint;
            try { blueprint = WeaponShareCode.Decode(gift.weaponCode); }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException ||
                exception is System.Security.Cryptography.CryptographicException)
            { error = "보상 총 코드를 읽지 못했습니다: " + exception.Message; return false; }
            if (blueprint.blocks == null || blueprint.blocks.Length != 50)
            { error = "보상 총 코드의 모듈 수가 예상과 다릅니다."; return false; }

            var blocks = new List<CubeAssembly.BlockSnapshot>(blueprint.blocks.Length);
            int shotgunCount = 0;
            long constructionCost = WeaponPurchasePrices.Core;
            foreach (WeaponShareCode.Block block in blueprint.blocks)
            {
                int slot = 0;
                ModuleData module = null;
                for (int candidateSlot = 1; candidateSlot <= assembly.ModuleCount; candidateSlot++)
                {
                    ModuleData candidate = assembly.GetModuleData(candidateSlot);
                    if (candidate == null || !string.Equals(candidate.CodeId, block.module, StringComparison.Ordinal)) continue;
                    slot = candidateSlot;
                    module = candidate;
                    break;
                }
                if (module == null || assembly.GetPrefab(slot) == null)
                { error = "현재 모듈 표에서 보상 총의 모듈을 찾을 수 없습니다: " + block.module; return false; }

                int moduleCreditCost = WeaponPurchasePrices.ModuleCredits(module, shotgunCount);
                if (module.ModuleType == StatType.Shotgun) shotgunCount++;
                constructionCost += module.BuyPrice;
                if (constructionCost > int.MaxValue)
                { error = "보상 총 제작 원가가 허용 범위를 넘었습니다."; return false; }
                blocks.Add(new CubeAssembly.BlockSnapshot
                {
                    slot = slot,
                    prefab = assembly.GetPrefab(slot),
                    cell = block.cell,
                    outputDirection = block.output,
                    orientationNormal = block.normal,
                    paidPurchaseCost = module.BuyPrice,
                    paidModuleCreditCost = moduleCreditCost,
                    hasColor = block.hasColor,
                    color = block.color
                });
            }

            if (!assembly.RestoreLayout(new CubeAssembly.LayoutSnapshot(blocks.ToArray(), (int)constructionCost)))
            { error = "보상 총의 모듈 배치를 복원하지 못했습니다."; return false; }
            if (!ApplyGiftAppearance(weapon, assembly, saveController, gift, out error)) return false;
            weapon.SetWeaponName("개발자의 총");
            return true;
        }

        /// <summary>
        /// Applies the DeveloperGiftData base paint, side-panel overrides, and first sticker placement.
        /// gift is the sole appearance source and the restored assembly supplies the module cells.
        /// </summary>
        private static bool ApplyGiftAppearance(EquipableWeapon weapon, CubeAssembly assembly,
            GameSaveController saveController, DeveloperGiftData gift, out string error)
        {
            error = null;
            Material baseMaterial = saveController.GetDeveloperGiftPaintMaterial(gift.basePaintMaterialId);
            if (baseMaterial == null) { error = "보상 총 기본 도색 색상을 찾을 수 없습니다."; return false; }

            WeaponPaintHistory paintHistory = weapon.GetComponent<WeaponPaintHistory>();
            if (paintHistory == null) paintHistory = weapon.gameObject.AddComponent<WeaponPaintHistory>();
            if (!paintHistory.ApplyToAllExposedFaces(assembly, baseMaterial, StatType.Scope))
            { error = "보상 총 기본 도색을 적용하지 못했습니다."; return false; }

            var blocksByCell = new Dictionary<Vector3Int, PlacedBlock>();
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>(true))
                if (block != null && block.transform.parent == assembly.transform)
                    blocksByCell[block.Cell] = block;

            foreach (DeveloperGiftPaintOverrideData paint in gift.paintOverrides ?? Array.Empty<DeveloperGiftPaintOverrideData>())
            {
                Material material = saveController.GetDeveloperGiftPaintMaterial(paint.materialId);
                if (material == null || paint.cells == null || paint.faces == null)
                { error = "보상 총 측면 도색 설정을 읽을 수 없습니다."; return false; }
                foreach (Vector3Int cell in paint.cells)
                {
                    if (!blocksByCell.TryGetValue(cell, out PlacedBlock block))
                    { error = "보상 총 측면 도색 대상 모듈을 찾을 수 없습니다."; return false; }
                    ModuleStats stats = block.GetComponent<ModuleStats>();
                    if (stats != null && stats.Data != null && stats.Data.ModuleType == StatType.Scope) continue;
                    foreach (Vector3Int face in paint.faces)
                    {
                        if (!IsAxis(face)) { error = "보상 총 도색 면 설정이 올바르지 않습니다."; return false; }
                        Vector3 worldFace = assembly.transform.TransformDirection((Vector3)face);
                        Vector3Int localFace = Vector3Int.RoundToInt(block.transform.InverseTransformDirection(worldFace));
                        if (!IsAxis(localFace) || Vector3.Dot(block.transform.TransformDirection(localFace), worldFace) < 0.999f ||
                            !paintHistory.Apply(block, localFace, material))
                        { error = "보상 총 측면 도색을 적용하지 못했습니다."; return false; }
                    }
                }
            }

            DeveloperGiftStickerData[] stickers = gift.stickers ?? Array.Empty<DeveloperGiftStickerData>();
            if (stickers.Length == 0) return true;
            if (string.IsNullOrEmpty(gift.stickerId))
            { error = "보상 총 스티커 ID가 비어 있습니다."; return false; }
            Texture2D texture = Resources.Load<Texture2D>("Stickers/" + gift.stickerId);
            if (texture == null) { error = "보상 총 스티커를 찾을 수 없습니다: " + gift.stickerId; return false; }

            WeaponStickerHistory stickerHistory = weapon.GetComponent<WeaponStickerHistory>();
            if (stickerHistory == null) stickerHistory = weapon.gameObject.AddComponent<WeaponStickerHistory>();
            foreach (DeveloperGiftStickerData sticker in stickers)
            {
                if (!TryGetStickerSurface(assembly, blocksByCell, sticker, out PlacedBlock anchor,
                        out Vector3 surfacePoint, out Vector3 surfaceNormal, out error)) return false;
                if (!stickerHistory.Place(anchor, texture, surfacePoint, surfaceNormal,
                        assembly.transform.up, assembly.transform.forward, sticker.sizeMultiplier, sticker.flipHorizontally))
                { error = "보상 총 스티커를 지정 면에 붙이지 못했습니다."; return false; }
            }
            return true;
        }

        /// <summary>
        /// Finds the outer plane and center for a sticker spanning the configured cells.
        /// sticker supplies its cell list and face; the result feeds WeaponStickerHistory.Place.
        /// </summary>
        private static bool TryGetStickerSurface(CubeAssembly assembly,
            Dictionary<Vector3Int, PlacedBlock> blocksByCell, DeveloperGiftStickerData sticker,
            out PlacedBlock anchor, out Vector3 surfacePoint, out Vector3 surfaceNormal, out string error)
        {
            anchor = null;
            surfacePoint = default;
            surfaceNormal = default;
            error = null;
            if (sticker == null || sticker.cells == null || sticker.cells.Length == 0 || !IsAxis(sticker.face))
            { error = "보상 총 스티커 면 설정이 올바르지 않습니다."; return false; }

            surfaceNormal = assembly.transform.TransformDirection((Vector3)sticker.face).normalized;
            Vector3 totalFaceCenter = Vector3.zero;
            foreach (Vector3Int cell in sticker.cells)
            {
                if (!blocksByCell.TryGetValue(cell, out PlacedBlock block))
                { error = "보상 총 스티커 대상 모듈을 찾을 수 없습니다."; return false; }
                BoxCollider collider = block.GetComponent<BoxCollider>();
                if (collider == null) { error = "보상 총 스티커 대상 면에 콜라이더가 없습니다."; return false; }

                Vector3 blockLocalNormal = block.transform.InverseTransformDirection(surfaceNormal);
                Vector3Int blockLocalFace = Vector3Int.RoundToInt(blockLocalNormal);
                if (!IsAxis(blockLocalFace) || Vector3.Dot(block.transform.TransformDirection(blockLocalFace), surfaceNormal) < 0.999f)
                { error = "보상 총 스티커 방향이 모듈 면과 일치하지 않습니다."; return false; }

                Vector3 halfSize = collider.size * 0.5f;
                Vector3 faceCenter = collider.center + Vector3.Scale((Vector3)blockLocalFace, halfSize);
                totalFaceCenter += block.transform.TransformPoint(faceCenter);
                if (anchor == null) anchor = block;
            }
            surfacePoint = totalFaceCenter / sticker.cells.Length;
            return true;
        }

        /// <summary>
        /// Reports whether a direction is one of the six cardinal module faces.
        /// face must have one unit axis and two zero axes.
        /// </summary>
        private static bool IsAxis(Vector3Int face)
            => Mathf.Abs(face.x) + Mathf.Abs(face.y) + Mathf.Abs(face.z) == 1;
    }
}
