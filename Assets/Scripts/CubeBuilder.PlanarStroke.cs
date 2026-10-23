using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class CubeBuilder
    {
        private const int MaximumStrokeCells = 256;
        private readonly List<Vector3Int> strokeCells = new();
        private readonly List<Vector3Int> strokeRemovalCells = new();
        private readonly WorkshopPlacementPlan strokePlan = new();
        private CubeAssembly strokeAssembly;
        private EquipableWeapon strokeWeapon;
        private int strokeSlot, strokeDepth, strokeBalance, strokeCredits;
        private WorkshopViewAxis strokeAxis;
        private bool strokeActive, strokeReleased, strokeMoved, strokeDirty, strokeDelete;
        private Vector3Int strokeLastCell;
        public bool HasPlanarStroke => strokeActive;
        public bool IsPlanarDeleteStroke => strokeActive && strokeDelete;
        public IReadOnlyList<Vector3Int> PlanarStrokeCells => strokeCells;
        public IReadOnlyList<Vector3Int> PlanarStrokeRemovalCells => strokeRemovalCells;
        public bool PlanarStrokeValid => strokeDelete
            ? strokeRemovalCells.Count > 0 && TutorialManager.ActionAllowed("delete") : strokePlan.Valid;
        public string PlanarStrokeCaption => strokeDelete
            ? $"삭제 예정 {strokeRemovalCells.Count}개 (연결 끊김 포함)\n놓으면 일괄 삭제 · 우클릭 또는 Ctrl+휠로 취소"
            : !strokeMoved ? "드래그: 연속 설치 / 클릭: 기존 단일 설치" :
            strokePlan.Valid ? $"{strokePlan.Parts.Count}개 (꺾임 연장 {strokePlan.CornerExtensions}개) · ${strokePlan.Dollars:N0} · 재료 {strokePlan.Credits:N0}\n버튼을 놓으면 일괄 설치 · 우클릭 또는 층/축 변경 시 취소" : strokePlan.Error;
        public bool TryGetStrokeDirection(Vector3Int cell, out Vector3Int direction, out int slot)
        {
            if (strokeDelete) { direction = default; slot = 0; return false; }
            foreach (var part in strokePlan.Parts)
                if (part.cell == cell) { direction = part.direction; slot = part.slot; return true; }
            direction = default; slot = 0; return false;
        }
        private void MarkStrokeDirty() => strokeDirty = true;

        private bool TryBeginPlanarStroke()
        {
            if (!UsingPlanarView || paintingMode || preview != null || paletteOpen || colorPickerOpen ||
                favoriteDragActive || GameManager.GameplayBlocked || planarView.IsPanning || !Application.isFocused ||
                (controls.FindAction("Pan", true).IsPressed() || controls.FindAction("Rotate", true).IsPressed()) || IsPointerOverUI(AimPoint()) ||
                !planarView.TryGetCell(AimPoint(), out var cell) || !CanEdit(planarView.Assembly) ||
                (!deleteMode && IsModuleLocked(selectedSlot))) return false;
            if (deleteMode)
            {
                if (!TutorialManager.TryAction("delete")) return false;
            }
            else if (TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizeAttachAction()) return false;
            if (wallet == null) wallet = PlayerWallet.ForScene(this);
            if (wallet == null) return false;
            strokeAssembly = planarView.Assembly;
            strokeSlot = selectedSlot; strokeDepth = planarView.SelectedDepth; strokeAxis = planarView.Axis;
            strokeCells.Clear(); strokeCells.Add(cell);
            strokeDelete = deleteMode; strokeLastCell = cell;
            strokeActive = strokeDirty = true; strokeReleased = strokeMoved = false;
            strokeWeapon = strokeAssembly.GetComponentInParent<EquipableWeapon>();
            if (strokeWeapon != null) strokeWeapon.AssemblyChanged += MarkStrokeDirty;
            RebuildStrokePlan();
            return true;
        }
        private void CancelStroke()
        {
            if (!ReferenceEquals(strokeWeapon, null)) strokeWeapon.AssemblyChanged -= MarkStrokeDirty;
            strokeWeapon = null; strokeAssembly = null;
            strokeActive = strokeReleased = strokeMoved = strokeDirty = false;
            strokeDelete = false;
            strokeCells.Clear(); strokeRemovalCells.Clear();
        }
        private void UpdatePlanarStroke()
        {
            if (!strokeActive) return;
            if (!UsingPlanarView || planarView.Assembly != strokeAssembly || !CanEdit(strokeAssembly) ||
                planarView.Axis != strokeAxis || planarView.SelectedDepth != strokeDepth || selectedSlot != strokeSlot ||
                paintingMode || deleteMode != strokeDelete || paletteOpen || colorPickerOpen || favoriteDragActive ||
                GameManager.GameplayBlocked || !Application.isFocused || planarView.IsPanning ||
                (controls.FindAction("Pan", true).IsPressed() || controls.FindAction("Rotate", true).IsPressed()) || IsPointerOverUI(AimPoint()) ||
                !planarView.TryGetCell(AimPoint(), out var cell))
            { CancelStroke(); return; }
            ExtendStroke(cell);
            if (!strokeActive) return;
            if (strokeDirty || (!strokeDelete && (wallet.Balance != strokeBalance || wallet.ModuleCredits != strokeCredits))) RebuildStrokePlan();
            // 액션 비활성화도 canceled를 발생시킨다. Update에서 유효한 편집 상태를 재확인한 후에만 확정한다.
            if (!strokeReleased) return;
            if (strokeDelete)
            {
                RebuildStrokePlan();
                try { CommitDeleteStroke(); }
                finally { CancelStroke(); }
                return;
            }
            if (!strokeMoved) { CancelStroke(); TryPlace(); return; }
            RebuildStrokePlan();
            if (!strokePlan.Valid)
            { GameManager.Instance?.UI?.ShowNotification(strokePlan.Error); CancelStroke(); return; }
            try { CommitStroke(); }
            finally { CancelStroke(); }
        }
        private void ExtendStroke(Vector3Int target)
        {
            Vector3Int start = strokeLastCell;
            if (start == target) return;
            strokeMoved = true;
            int dx = CubePlacementState.Dot(target - start, planarView.Horizontal);
            int dy = CubePlacementState.Dot(target - start, planarView.Vertical);
            int nx = Mathf.Abs(dx), ny = Mathf.Abs(dy), ix = 0, iy = 0;
            var cell = start;
            // 화면 선분이 통과하는 셀을 4-연결 경로로 보간한다. 코너 동률은 수평 우선으로 고정한다.
            while (ix < nx || iy < ny)
            {
                if ((1L + 2L * ix) * ny <= (1L + 2L * iy) * nx)
                { cell += planarView.Horizontal * (dx > 0 ? 1 : -1); ix++; }
                else { cell += planarView.Vertical * (dy > 0 ? 1 : -1); iy++; }
                int existing = strokeCells.IndexOf(cell);
                if (existing >= 0)
                {
                    // 삭제 브러시는 지나간 셀을 유지한다. 설치 경로만 되짚기로 줄인다.
                    if (!strokeDelete) strokeCells.RemoveRange(existing + 1, strokeCells.Count - existing - 1);
                }
                else
                {
                    if (strokeCells.Count >= MaximumStrokeCells)
                    {
                        GameManager.Instance?.UI?.ShowNotification("한 번에 최대 256셀까지 그릴 수 있습니다");
                        CancelStroke(); return;
                    }
                    strokeCells.Add(cell);
                }
            }
            strokeLastCell = target;
            strokeDirty = true;
        }
        private void RebuildStrokePlan()
        {
            if (strokeDelete)
            {
                strokeAssembly.CollectRemovalCells(strokeCells, strokeRemovalCells);
                strokeDirty = false;
                return;
            }
            strokePlan.Build(strokeAssembly, strokeCells, strokeSlot, planarView.Normal, planarView.Horizontal, wallet, IsModuleLocked);
            strokeBalance = wallet.Balance; strokeCredits = wallet.ModuleCredits; strokeDirty = false;
        }
        private void CommitDeleteStroke()
        {
            if (!PlanarStrokeValid || wallet == null || !CanEdit(strokeAssembly)) return;
            // 튜토리얼의 직접 삭제 대상 제한을 우회하지 않는다. 연결 끊김 제거는 기존 규칙을 따른다.
            foreach (var cell in strokeCells)
            {
                var block = GetBlockAt(strokeAssembly, cell);
                if (block != null && TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizeRemoval(block)) return;
            }
            var assembly = strokeAssembly;
            Vector3 soundPosition = assembly.transform.TransformPoint(strokeRemovalCells[0]);
            bool removed;
            assembly.BeginPlacementBatch();
            try { removed = ExecuteUndoableEdit(assembly, () => assembly.RemoveBlocks(strokeCells, wallet)); }
            finally { assembly.EndPlacementBatch(); }
            if (removed) WorkshopSoundEffects.RaiseBlockRemoved(soundPosition);
        }
        private void CommitStroke()
        {
            if (IsModuleLocked(strokeSlot) || !TutorialManager.ActionAllowed("attach")) return;
            foreach (var part in strokePlan.Parts)
                if (IsModuleLocked(part.slot) || (TutorialManager.Instance != null &&
                    !TutorialManager.Instance.TryAuthorizePlacement(strokeAssembly, part.cell, part.slot, part.direction))) return;
            if (!strokeAssembly.TryCaptureLayout(out var snapshot)) return;
            var assembly = strokeAssembly;
            bool completed = false, layoutChanged = false;
            assembly.BeginPlacementBatch();
            try
            {
                // 지갑은 전체 조립 생성이 성공한 경우에만 한 번 차감한다.
                // 실패 시 레이아웃/구매기록은 확정 전 스냅샷으로 복원한다.
                completed = ExecuteUndoableEdit(assembly, () =>
                    wallet.TryPurchaseModule(strokePlan.Dollars, strokePlan.Credits, () =>
                    {
                        foreach (var part in strokePlan.Parts)
                        {
                            // 가상 경로의 판정만으로 설치하지 않는다. 이전 셀을 실제로 설치한 뒤
                            // 단일 배치와 같은 공급·2D 방향 조건을 다시 검사한다.
                            bool followsFeed = part.slot != 1 && !IsScope(assembly, part.slot) && !assembly.IsTriggerModuleSlot(part.slot);
                            if (followsFeed && (!assembly.TryGetFeedDirection(part.cell, out var actualFeed) || actualFeed != part.feed))
                                return false;
                            if (!CanOrientPlanar(assembly, part.cell, part.slot, part.direction, part.feed)) return false;
                            if (!assembly.TryAttach(part.cell, part.slot, part.direction, part.feed)) return false;
                            layoutChanged = true;
                            PaintPlacedColor(assembly, part.cell);
                            assembly.RecordPurchasedModule(part.cell, part.dollars);
                            assembly.RecordPurchasedModuleCredits(part.cell, part.credits);
                        }
                        return true;
                    }, PartLogName(assembly, strokeSlot)));
            }
            finally
            {
                if (!completed && layoutChanged) assembly.RestoreLayout(snapshot);
                assembly.EndPlacementBatch();
            }
            if (!completed) return;
            // 선택 모듈과 자동 연장의 구매 기록/알림도 실제 설치 종류별로 합산한다.
            var purchases = new Dictionary<int, (int dollars, int credits)>();
            foreach (var part in strokePlan.Parts)
            {
                purchases.TryGetValue(part.slot, out var total);
                purchases[part.slot] = (total.dollars + part.dollars, total.credits + part.credits);
            }
            foreach (var purchase in purchases)
                wallet.NotifyPartPurchased(PartLogName(assembly, purchase.Key), purchase.Value.dollars, purchase.Value.credits);
            WorkshopSoundEffects.RaiseBlockInstalled(assembly.transform.TransformPoint(strokePlan.Parts[strokePlan.Parts.Count - 1].cell));
        }
    }
}
