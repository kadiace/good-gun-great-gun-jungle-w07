using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(Camera))]
    public sealed class CubeBuilder : MonoBehaviour
    {
        private const int UndoLimit = 3;

        private struct UndoEntry
        {
            public CubeAssembly assembly;
            public CubeAssembly.LayoutSnapshot snapshot;
            public PlayerWallet wallet;
            public int walletBalance;
            public int walletModuleCredits;
        }

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private int selectedSlot = 1;
        [SerializeField] private bool deleteMode;
        [SerializeField] private GameObject eraseModeUI;
        [SerializeField, Tooltip("이름·색 표시와 5번 이후 모듈 목록. 설치 프리팹은 CubeAssembly가 담당합니다.")]
        private ModuleCatalog moduleCatalog;
        [SerializeField, Tooltip("숫자키 1~9,0에 대응하는 즐겨찾기 모듈 ID.")]
        private int[] favorites = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private bool paletteOpen;
        private int hoveredPaletteModule;
        public void SetHoveredPaletteModule(int moduleId) => hoveredPaletteModule = moduleId;
        private void HandleFavoriteKey(int index)
        {
            if (GameManager.GameplayBlocked) return;
            if (PaletteOpen && hoveredPaletteModule > 0) { SetFavorite(index, hoveredPaletteModule); return; }
            SelectModule(GetFavorite(index));
        }
        private InputActionMap controls;
        private Camera view;
        private CubeAssembly pendingAssembly;
        private Vector3Int pendingCell;
        private Vector3Int pendingDirection;
        private Vector3Int pendingAttachmentDirection;
        private int pendingSlot;
        private GameObject preview;
        [SerializeField] private Material hoverPreviewMaterial;
        [SerializeField] private Material erasePreviewMaterial;
        private GameObject hoverPreview;
        private Transform workspace;
        private PlayerWallet wallet;
        private bool paintingMode;
        private bool stickerToolActive;
        private bool stickerEraseMode;
        private bool keyringToolActive;
        private GameObject selectedKeyringModel;
        private float customLimitMessageUntil;
        private string customLimitMessage = string.Empty;
        private bool paintFillMode;
        private bool paintPointerHeld;
        private Material paintBrush;
        private Texture2D stickerBrush;
        private PlacedBlock selectedPaintBlock;
        private Vector3Int selectedPaintFace;
        private bool colorPickerOpen;
        private int slotBeforeColorPick = 1;
        private Color moduleColor = Color.white;
        private const int ModulePrice = WeaponPurchasePrices.Module;
        private readonly List<UndoEntry> undoHistory = new List<UndoEntry>(UndoLimit);
        private readonly RaycastHit[] surfaceHits = new RaycastHit[256];

        public bool IsPaintingMode => paintingMode;
        public bool IsCustomMode => paintingMode;
        public bool IsStickerToolActive => stickerToolActive;
        public bool IsStickerEraseMode => stickerEraseMode;
        public bool IsKeyringToolActive => keyringToolActive;
        public GameObject SelectedKeyringModel => selectedKeyringModel;
        private StickerOverlay LastPlacedSticker => CurrentStickerHistory?.LastActiveSticker;
        private WeaponStickerHistory CurrentStickerHistory
        {
            get
            {
                EquipableWeapon weapon = FindWorkspaceWeapon();
                return weapon != null ? weapon.GetComponent<WeaponStickerHistory>() : null;
            }
        }
        private WeaponKeyringHistory CurrentKeyringHistory
        {
            get
            {
                EquipableWeapon weapon = FindWorkspaceWeapon();
                return weapon != null ? weapon.GetComponent<WeaponKeyringHistory>() : null;
            }
        }
        public int KeyringCount => CurrentKeyringHistory != null ? CurrentKeyringHistory.Count : 0;
        public bool HasLastPlacedSticker => LastPlacedSticker != null;
        public bool IsStickerFlippedHorizontally => LastPlacedSticker != null && LastPlacedSticker.IsFlippedHorizontally;
        public float StickerSizeMultiplier => LastPlacedSticker != null
            ? LastPlacedSticker.SizeMultiplier
            : StickerOverlay.DefaultSizeMultiplier;
        public bool CanDecreaseStickerSize => LastPlacedSticker != null && LastPlacedSticker.CanDecreaseSize;
        public bool CanIncreaseStickerSize => LastPlacedSticker != null && LastPlacedSticker.CanIncreaseSize;
        public bool IsCustomLimitMessageVisible => Time.unscaledTime < customLimitMessageUntil;
        public string CustomLimitMessage => customLimitMessage;
        public bool IsPaintFillMode => paintFillMode;
        public bool HasPaintBrush => paintBrush != null;
        public bool CanStartCustom
        {
            get
            {
                EquipableWeapon weapon = FindWorkspaceWeapon();
                return isActiveAndEnabled && workspace != null && !paintingMode && weapon != null &&
                    !weapon.ConstructionLocked && weapon.GetComponentInChildren<CubeAssembly>(true) != null;
            }
        }
        public string StickerBrushLabel => stickerBrush != null ? stickerBrush.name : "PNG 스티커를 선택하세요";
        public bool HasSelectedPaintFace => selectedPaintBlock != null;
        public string PaintSelectionLabel => selectedPaintBlock == null
            ? "총기에서 도색할 면을 클릭하세요"
            : $"선택된 모듈: {selectedPaintBlock.name} / 면: {FaceLabel(selectedPaintFace)}";
        public string PaintBrushLabel => paintBrush != null ? paintBrush.name.Replace("Paint - ", "") : "재질을 먼저 선택하세요";

        public void SetWorkspace(Transform root)
        {
            CancelPreview();
            if (workspace != root) undoHistory.Clear();
            workspace = root;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            paintingMode = weapon != null && weapon.ConstructionLocked;
            stickerToolActive = false;
            stickerEraseMode = false;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintFillMode = false;
            paintPointerHeld = false;
            paintBrush = null;
            stickerBrush = null;
            selectedPaintBlock = null;
            selectedPaintFace = Vector3Int.zero;
        }

        private bool CanEdit(CubeAssembly assembly) => assembly != null && workspace != null &&
            assembly.transform.IsChildOf(workspace) &&
            !paintingMode &&
            (assembly.GetComponentInParent<EquipableWeapon>() == null ||
             !assembly.GetComponentInParent<EquipableWeapon>().ConstructionLocked);
        private static readonly Vector3Int[] OutputDirections =
        {
            Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down,
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
        };
        public int SelectedSlot => selectedSlot;
        public bool DeleteMode => deleteMode;
        public bool PaletteOpen => paletteOpen && isActiveAndEnabled;
        public bool ColorPickerOpen => colorPickerOpen && isActiveAndEnabled;
        public Color ModuleColor => moduleColor;
        public ModuleCatalog Catalog => moduleCatalog;
        public Vector2 PointerPosition => controls != null ? AimPoint() : Vector2.zero;
        public bool IsModuleLocked(int moduleId)
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.AllowsModule(moduleId)) return true;
            if (moduleCatalog == null || !moduleCatalog.TryGet(moduleId, out var entry)) return true;
            if (!entry.initiallyLocked) return false;
            if (wallet == null) wallet = PlayerWallet.ForScene(this);
            var unlocks = wallet != null ? wallet.GetComponent<PlayerModuleUnlocks>() : null;
            return unlocks == null || !unlocks.IsUnlocked(moduleId);
        }
        public int FavoriteCount => favorites.Length;
        public int GetFavorite(int index) => index >= 0 && index < favorites.Length ? ValidModuleOrBlocker(favorites[index]) : 1;

        private int ValidModuleOrBlocker(int moduleId) => moduleCatalog != null &&
            moduleCatalog.TryGet(moduleId, out var entry) && entry.prefab != null ? moduleId : 1;

        public void SetFavorite(int index, int moduleId)
        {
            if (index < 0 || index >= favorites.Length) return;
            moduleId = ValidModuleOrBlocker(moduleId);
            if (IsModuleLocked(moduleId)) return;
            int existing = System.Array.IndexOf(favorites, moduleId);
            if (existing >= 0 && existing != index) favorites[existing] = GetFavorite(index);
            favorites[index] = moduleId;
        }

        public void SelectModule(int moduleId)
        {
            if (GameManager.GameplayBlocked || paintingMode) return;
            CancelPreview();
            int slot = ValidModuleOrBlocker(moduleId);
            if (IsModuleLocked(slot)) return;
            // Color module: pick the color first (ColorPickerUI shows while ColorPickerOpen); placing waits.
            if (IsColorModule(slot))
            {
                if (!colorPickerOpen) slotBeforeColorPick = selectedSlot;
                colorPickerOpen = true;
            }
            else colorPickerOpen = false;
            selectedSlot = slot;
            deleteMode = false;
            UpdateEraseModeUI();
        }

        // Every color module placed after this gets the confirmed color, until it is picked again.
        public void ConfirmModuleColor(Color color)
        {
            if (!colorPickerOpen) return;
            color.a = 1f;
            moduleColor = color;
            colorPickerOpen = false;
        }

        public void CancelModuleColor()
        {
            if (!colorPickerOpen) return;
            colorPickerOpen = false;
            selectedSlot = ValidModuleOrBlocker(slotBeforeColorPick);
        }

        private bool IsColorModule(int slot)
        {
            GameObject prefab = moduleCatalog != null ? moduleCatalog.GetPrefab(slot) : null;
            ModuleStats stats = prefab != null ? prefab.GetComponent<ModuleStats>() : null;
            return stats != null && stats.Data != null && stats.Data.ModuleType == StatType.Color;
        }

        private bool PaintPlacedColor(CubeAssembly assembly, Vector3Int cell)
        {
            foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>())
                if (block.transform.parent == assembly.transform && block.Cell == cell)
                    block.GetComponent<ColorModule>()?.SetColor(moduleColor);
            return true;
        }

        private void Awake()
        {
            if (favorites == null || favorites.Length != 10)
            {
                var expanded = new int[10];
                for (int i = 0; i < expanded.Length; i++) expanded[i] = favorites != null && i < favorites.Length ? favorites[i] : i + 1;
                favorites = expanded;
            }
            for (int i = 0; i < favorites.Length; i++) favorites[i] = ValidModuleOrBlocker(favorites[i]);
            selectedSlot = ValidModuleOrBlocker(selectedSlot);
            deleteMode = false;
            paletteOpen = false;
            colorPickerOpen = false;
            paintFillMode = false;
            if (eraseModeUI != null) eraseModeUI.SetActive(false);
            view = GetComponent<Camera>();
            if (inputActions == null) { enabled = false; return; }
            controls = inputActions.FindActionMap("Build", true).Clone();
            for (int i = 1; i <= 10; i++)
            {
                int index = i - 1;
                controls.FindAction("Select" + (i % 10), true).performed += _ => HandleFavoriteKey(index);
            }
            controls.FindAction("TogglePalette", true).performed += _ =>
            {
                if (GameManager.GameplayBlocked || paintingMode) return;
                if (!TutorialManager.ActionAllowed("palette")) return;
                CancelPreview();
                paletteOpen = !paletteOpen; hoveredPaletteModule = 0;
                if (paletteOpen && TutorialManager.Instance != null) TutorialManager.Instance.NotifySignal("palette-opened");
            };
            InputAction attach = controls.FindAction("Attach", true);
            attach.started += _ =>
            {
                paintPointerHeld = false;
                bool canUseSurface = paintingMode && !GameManager.GameplayBlocked &&
                    !controls.FindAction("Rotate", true).IsPressed() && !IsPointerOverUI(AimPoint());
                if (!canUseSurface) return;
                if (stickerToolActive)
                {
                    if (stickerEraseMode) TryEraseStickerAtPointer();
                    else TryPlaceStickerAtPointer();
                    return;
                }
                if (keyringToolActive)
                {
                    TryPlaceKeyringAtPointer();
                    return;
                }
                bool canPaint = paintingMode && !GameManager.GameplayBlocked &&
                    !controls.FindAction("Rotate", true).IsPressed() && !IsPointerOverUI(AimPoint());
                if (!canPaint) return;
                if (paintFillMode)
                {
                    if (TrySelectPaintFace(false)) FillPaintRegionAtSelection();
                }
                else paintPointerHeld = TrySelectPaintFace();
            };
            attach.canceled += _ => paintPointerHeld = false;
            attach.performed += _ =>
            {
                if (GameManager.GameplayBlocked || (!paintingMode && (paletteOpen || colorPickerOpen)) ||
                    IsPointerOverUI(AimPoint())) return;
                if (controls.FindAction("Rotate", true).IsPressed()) return;
                if (!paintingMode && !deleteMode && TutorialManager.Instance != null &&
                    !TutorialManager.Instance.TryAuthorizeAttachAction()) return;
                if (paintingMode)
                {
                    if (!stickerToolActive && !keyringToolActive && !paintFillMode) TrySelectPaintFace();
                }
                else if (deleteMode) TryRemove();
                else TryPlace();
            };
            controls.FindAction("ToggleDelete", true).performed += _ =>
            {
                if (GameManager.GameplayBlocked || paintingMode) return;
                if (!TutorialManager.ActionAllowed("delete")) return;
                CancelPreview();
                CancelModuleColor();
                deleteMode = !deleteMode;
                UpdateEraseModeUI();
                if (TutorialManager.Instance != null)
                    TutorialManager.Instance.NotifySignal(deleteMode ? "delete-mode-enabled" : "delete-mode-disabled");
            };
            controls.FindAction("Undo", true).performed += _ => UndoLastEdit();
        }

        private void OnEnable()
        {
            controls?.Enable();
            EquipableWeapon weapon = FindWorkspaceWeapon();
            paintingMode = weapon != null && weapon.ConstructionLocked;
            stickerToolActive = false;
            stickerEraseMode = false;
            keyringToolActive = false;
            paintFillMode = false;
            UpdateEraseModeUI();
        }
        private void OnDisable()
        {
            CancelPreview();
            CancelModuleColor();
            deleteMode = false;
            paletteOpen = false;
            hoveredPaletteModule = 0;
            paintingMode = false;
            stickerToolActive = false;
            stickerEraseMode = false;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintFillMode = false;
            paintPointerHeld = false;
            paintBrush = null;
            stickerBrush = null;
            selectedPaintBlock = null;
            selectedPaintFace = Vector3Int.zero;
            controls?.Disable();
            UpdateEraseModeUI();
        }
        private void UpdateEraseModeUI()
        {
            if (eraseModeUI != null) eraseModeUI.SetActive(isActiveAndEnabled && deleteMode);
        }
        // The builder lives on the Player prefab camera, so the scene UI hands over its erase-mode indicator.
        public void SetEraseModeUI(GameObject ui)
        {
            if (eraseModeUI == ui) return;
            if (eraseModeUI != null) eraseModeUI.SetActive(false);
            eraseModeUI = ui;
            UpdateEraseModeUI();
        }
        private void OnDestroy()
        {
            controls?.Dispose();
            if (hoverPreview != null) Destroy(hoverPreview);
        }
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            CancelPreview();
            paintPointerHeld = false;
        }

        private void Update()
        {
            UpdateHoverPreview();
            if (GameManager.GameplayBlocked)
            {
                paintPointerHeld = false;
                return;
            }
            if (paintingMode && !stickerToolActive && paintPointerHeld)
            {
                if (controls.FindAction("Rotate", true).IsPressed() || IsPointerOverUI(AimPoint()))
                {
                    paintPointerHeld = false;
                    return;
                }
                TrySelectPaintFace();
                return;
            }
            if (preview == null) return;
            if (pendingAssembly == null || !pendingAssembly.gameObject.activeInHierarchy || !pendingAssembly.CanAttach(pendingCell, pendingSlot))
            { CancelPreview(); return; }
            UpdatePreview();
        }

        private void UpdateHoverPreview()
        {
            if (hoverPreview != null) hoverPreview.SetActive(false);
            if (paintingMode || paletteOpen || colorPickerOpen || preview != null ||
                GameManager.GameplayBlocked || view == null || controls == null ||
                controls.FindAction("Rotate", true).IsPressed() || IsPointerOverUI(AimPoint())) return;
            var pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer) || !TryGetSurfaceHit(pointer, out var hit)) return;
            var assembly = hit.collider.GetComponentInParent<CubeAssembly>();
            if (!CanEdit(assembly)) return;
            Vector3Int cell;
            if (deleteMode)
            {
                var block = hit.collider.GetComponentInParent<PlacedBlock>();
                if (block == null || block.transform.parent != assembly.transform) return;
                cell = block.Cell;
            }
            else
            {
                if (IsModuleLocked(selectedSlot) || !assembly.TryGetAttachCell(hit, out cell, out var face) ||
                    !assembly.CanAttach(cell, selectedSlot)) return;
                bool followsFeed = selectedSlot != 1 && !IsScope(assembly, selectedSlot) && !assembly.IsTriggerModuleSlot(selectedSlot);
                var feed = face;
                if (followsFeed && !assembly.TryGetFeedDirection(cell, out feed)) return;
                bool valid = false;
                foreach (var direction in OutputDirections)
                    if (assembly.CanOrient(cell, selectedSlot, direction, feed)) { valid = true; break; }
                if (!valid) return;
            }
            var material = deleteMode ? erasePreviewMaterial : hoverPreviewMaterial;
            if (material == null) return;
            if (hoverPreview == null)
            {
                hoverPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hoverPreview.name = "BlockHoverPreview";
                hoverPreview.layer = LayerMask.NameToLayer("Ignore Raycast");
                var collider = hoverPreview.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var renderer = hoverPreview.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            hoverPreview.GetComponent<MeshRenderer>().sharedMaterial = material;
            hoverPreview.transform.SetPositionAndRotation(assembly.transform.TransformPoint(cell), assembly.transform.rotation);
            hoverPreview.transform.localScale = assembly.transform.lossyScale * (deleteMode ? 1.015f : .99f);
            hoverPreview.SetActive(true);
        }

        private void UpdatePreview()
        {
            preview.transform.SetPositionAndRotation(pendingAssembly.transform.TransformPoint(pendingCell), pendingAssembly.transform.rotation);
            preview.transform.localScale = pendingAssembly.transform.lossyScale;

            if (!CanSelectDirection(pendingDirection))
                foreach (Vector3Int direction in OutputDirections)
                    if (CanSelectDirection(direction)) { pendingDirection = direction; break; }

            if (TryGetPreviewFace(out Vector3Int face) && CanSelectDirection(face))
            {
                pendingDirection = face;
                preview.GetComponent<ExtensionModule>()?.SetDirection(pendingDirection);
                return;
            }
            bool singleOutput = CubeAssembly.IsSingleOutput(pendingSlot);
            if (singleOutput && Vector3.Dot(pendingDirection, pendingAttachmentDirection) != 0f)
                foreach (Vector3Int direction in OutputDirections)
                    if (CanSelectDirection(direction) && Vector3.Dot(direction, pendingAttachmentDirection) == 0f)
                    { pendingDirection = direction; break; }
            Vector3 center = view.WorldToScreenPoint(preview.transform.position);
            Vector2 mouse = AimPoint() - new Vector2(center.x, center.y);
            float best = singleOutput ? float.NegativeInfinity : 0f;
            if (mouse.sqrMagnitude > 16f)
                foreach (Vector3Int direction in OutputDirections)
                {
                    if (!CanSelectDirection(direction)) continue;
                    // Straight axial directions require an exact face hit for extensions.
                    if (singleOutput && Vector3.Dot(direction, pendingAttachmentDirection) != 0f) continue;
                    Vector3 end = view.WorldToScreenPoint(pendingAssembly.transform.TransformPoint((Vector3)pendingCell + (Vector3)direction));
                    Vector2 projected = new Vector2(end.x - center.x, end.y - center.y);
                    if (end.z <= 0f || projected.sqrMagnitude < 1f) continue;
                    float score = Vector2.Dot(mouse.normalized, projected.normalized);
                    if (score <= best) continue;
                    best = score;
                    pendingDirection = direction;
                }
            preview.GetComponent<ExtensionModule>()?.SetDirection(pendingDirection);
        }

        private bool CanSelectDirection(Vector3Int direction)
        {
            return pendingAssembly != null && pendingAssembly.CanOrient(pendingCell, pendingSlot, direction, -pendingAttachmentDirection);
        }

        private bool TryGetPreviewFace(out Vector3Int face)
        {
            face = Vector3Int.zero;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer)) return false;
            Ray worldRay = view.ScreenPointToRay(pointer);
            // Preview colliders stay disabled; intersect only the cube, excluding the arrow.
            Ray localRay = new Ray(preview.transform.InverseTransformPoint(worldRay.origin),
                preview.transform.InverseTransformVector(worldRay.direction).normalized);
            if (!new Bounds(Vector3.zero, Vector3.one).IntersectRay(localRay, out float distance) || distance <= 0f)
                return false;
            Vector3 point = localRay.GetPoint(distance);
            float worldDistance = Vector3.Distance(worldRay.origin, preview.transform.TransformPoint(point));
            if (Physics.Raycast(worldRay, out RaycastHit obstruction, worldDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                obstruction.distance < worldDistance - 0.001f) return false;
            face = Mathf.Abs(point.x) >= Mathf.Abs(point.y) && Mathf.Abs(point.x) >= Mathf.Abs(point.z)
                ? new Vector3Int(point.x >= 0f ? 1 : -1, 0, 0)
                : Mathf.Abs(point.y) >= Mathf.Abs(point.z)
                    ? new Vector3Int(0, point.y >= 0f ? 1 : -1, 0)
                    : new Vector3Int(0, 0, point.z >= 0f ? 1 : -1);
            return true;
        }

        private void CancelPreview()
        {
            if (hoverPreview != null) hoverPreview.SetActive(false);
            if (preview != null) { preview.SetActive(false); Destroy(preview); }
            preview = null;
            pendingAssembly = null;
            pendingAttachmentDirection = Vector3Int.zero;
        }

        private void TryRemove()
        {
            if (paintingMode || IsPointerOverUI(AimPoint())) return;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer)) return;
            if (!TryGetSurfaceHit(pointer, out RaycastHit hit)) return;
            PlacedBlock block = hit.collider.GetComponentInParent<PlacedBlock>();
            if (block == null) return;
            if (TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizeRemoval(block)) return;
            CubeAssembly assembly = block.GetComponentInParent<CubeAssembly>();
            if (wallet == null) wallet = PlayerWallet.ForScene(this);
            Vector3 soundPosition = block.transform.position;
            if (CanEdit(assembly) && wallet != null &&
                ExecuteUndoableEdit(assembly, () => assembly.RemoveBlock(block, wallet)))
                WorkshopSoundEffects.RaiseBlockRemoved(soundPosition);
        }

        private bool TryPurchaseAttach(CubeAssembly assembly, Vector3Int cell, int slot,
            Vector3Int direction, Vector3Int normal)
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.TryAuthorizePlacement(assembly, cell, slot, direction)) return false;
            EquipableWeapon weapon = assembly.GetComponentInParent<EquipableWeapon>();
            if (weapon == null || weapon.ConstructionLocked || wallet == null || IsModuleLocked(slot)) return false;
            if (assembly.IsTriggerModuleSlot(slot) && assembly.HasTriggerModule)
            {
                ShowCustomLimitMessage("트리거 모듈은 1개만 설치할 수 있습니다");
                return false;
            }
            ModuleCatalog.Entry entry = default;
            int dollars = moduleCatalog != null && moduleCatalog.TryGet(slot, out entry)
                ? Mathf.Max(0, entry.data.BuyPrice) : ModulePrice;
            int materials = entry.data != null ? Mathf.Max(0, entry.moduleCreditCost) : 0;
            bool purchased = wallet.TryPurchaseModule(dollars, materials,
                () => assembly.TryAttach(cell, slot, direction, normal) && PaintPlacedColor(assembly, cell));
            if (purchased)
            {
                assembly.RecordPurchasedModule(cell, dollars);
                assembly.RecordPurchasedModuleCredits(cell, materials);
                WorkshopSoundEffects.RaiseBlockInstalled(assembly.transform.TransformPoint(cell));
            }
            return purchased;
        }
        private void TryPlace()
        {
            if (paintingMode || IsPointerOverUI(AimPoint())) return;
            if (wallet == null) wallet = PlayerWallet.ForScene(this);
            if (wallet == null) return;
            int slotToBuy = preview != null ? pendingSlot : selectedSlot;
            if (IsModuleLocked(slotToBuy)) { CancelPreview(); return; }
            ModuleCatalog.Entry entry = default;
            int dollars = moduleCatalog != null && moduleCatalog.TryGet(slotToBuy, out entry)
                ? Mathf.Max(0, entry.data.BuyPrice) : ModulePrice;
            int materials = entry.data != null ? Mathf.Max(0, entry.moduleCreditCost) : 0;
            if (!wallet.CheckModuleFunds(dollars, materials)) return;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer)) return;
            if (preview != null)
            {
                if (pendingAssembly != null)
                {
                    UpdatePreview();
                    if (!ExecuteUndoableEdit(pendingAssembly,
                            () => TryPurchaseAttach(pendingAssembly, pendingCell, pendingSlot,
                                pendingDirection, -pendingAttachmentDirection))) return;
                }
                CancelPreview();
                return;
            }
            if (!TryGetSurfaceHit(pointer, out RaycastHit hit)) return;
            CubeAssembly assembly = hit.collider.GetComponentInParent<CubeAssembly>();
            if (!CanEdit(assembly)) return;
            if (assembly.IsTriggerModuleSlot(selectedSlot) && assembly.HasTriggerModule)
            {
                ShowCustomLimitMessage("트리거 모듈은 1개만 설치할 수 있습니다");
                return;
            }
            bool singleOutput = CubeAssembly.IsSingleOutput(selectedSlot);
            if (!assembly.TryGetAttachCell(hit, out Vector3Int cell, out Vector3Int face)) return;
            // Energy modules enter through the face energy comes from: they follow the most recently placed
            // neighbor that outputs into the cell, and are not placed when nothing feeds it.
            // Blockers, scopes and triggers keep the clicked face.
            bool followsFeed = selectedSlot != 1 && !IsScope(assembly, selectedSlot) && !assembly.IsTriggerModuleSlot(selectedSlot);
            Vector3Int feed = face;
            if (followsFeed && !assembly.TryGetFeedDirection(cell, out feed))
            {
                GameManager.Instance?.UI?.ShowNotification("총구에만 설치할 수 있습니다.");
                return;
            }
            if (!singleOutput && selectedSlot != 3)
            {
                ExecuteUndoableEdit(assembly, () => TryPurchaseAttach(assembly, cell, selectedSlot, feed, feed));
                return;
            }
            // Stat modules (5+) place in one click: output continues straight from the feed,
            // falling back to any other allowed direction when that one is blocked.
            // Scopes choose their lens direction like an extension (arrow = lens, opposite face = eyepiece).
            if (selectedSlot >= 5 && !IsScope(assembly, selectedSlot))
            {
                if (ExecuteUndoableEdit(assembly,
                        () => TryPurchaseAttach(assembly, cell, selectedSlot, feed, feed))) return;
                foreach (Vector3Int direction in OutputDirections)
                    if ((!followsFeed || direction != -feed) && ExecuteUndoableEdit(assembly,
                            () => TryPurchaseAttach(assembly, cell, selectedSlot, direction, followsFeed ? feed : direction))) return;
                return;
            }
            if (!assembly.CanAttach(cell, selectedSlot)) return;

            pendingAssembly = assembly;
            pendingCell = cell;
            pendingAttachmentDirection = -feed;
            pendingSlot = selectedSlot;
            pendingDirection = singleOutput ? feed : Vector3Int.RoundToInt(Vector3.Cross(Mathf.Abs(feed.y) == 1 ? Vector3.forward : Vector3.up, feed));
            // The preview stays outside the assembly, so it cannot affect energy routes or occupancy.
            preview = Instantiate(assembly.GetPrefab(pendingSlot));
            preview.name = "ModulePreview";
            ModuleDraw previewDraw = preview.GetComponent<ModuleDraw>();
            previewDraw?.SetPreview(true);
            foreach (Collider collider in preview.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
            Transform arrow = previewDraw != null ? previewDraw.Arrow : null;

            foreach (MeshRenderer renderer in preview.GetComponentsInChildren<MeshRenderer>())
            {
                if (arrow != null && renderer.transform.IsChildOf(arrow)) continue;

                renderer.SetPropertyBlock(tint);
            }
            UpdatePreview();
        }

        private static bool IsScope(CubeAssembly assembly, int slot)
        {
            GameObject prefab = assembly.GetPrefab(slot);
            ModuleStats stats = prefab != null ? prefab.GetComponent<ModuleStats>() : null;
            return stats != null && stats.Data != null && stats.Data.ModuleType == StatType.Scope;
        }

        private bool ExecuteUndoableEdit(CubeAssembly assembly, System.Func<bool> edit)
        {
            if (assembly == null || edit == null || !assembly.TryCaptureLayout(out CubeAssembly.LayoutSnapshot snapshot))
                return false;

            int walletBalance = wallet != null ? wallet.Balance : 0;
            int walletModuleCredits = wallet != null ? wallet.ModuleCredits : 0;
            if (!edit()) return false;

            if (undoHistory.Count == UndoLimit) undoHistory.RemoveAt(0);
            undoHistory.Add(new UndoEntry
            {
                assembly = assembly,
                snapshot = snapshot,
                wallet = wallet,
                walletBalance = walletBalance,
                walletModuleCredits = walletModuleCredits
            });
            return true;
        }

        private void UndoLastEdit()
        {
            if (!TutorialManager.ActionAllowed("undo")) return;
            if (GameManager.GameplayBlocked) return;
            if (paintingMode)
            {
                if (stickerToolActive) UndoSticker();
                else if (keyringToolActive) UndoKeyring();
                else UndoPaint();
                return;
            }
            if (paletteOpen || workspace == null || undoHistory.Count == 0) return;
            int last = undoHistory.Count - 1;
            UndoEntry entry = undoHistory[last];
            if (!CanEdit(entry.assembly))
            {
                undoHistory.Clear();
                return;
            }

            CancelPreview();
            if (!entry.assembly.RestoreLayout(entry.snapshot)) return;
            entry.wallet?.RestoreBalance(entry.walletBalance);
            entry.wallet?.RestoreModuleCredits(entry.walletModuleCredits);
            undoHistory.RemoveAt(last);
        }

        public bool BeginCustomMode()
        {
            if (!TutorialManager.ActionAllowed("custom")) return false;
            if (!CanStartCustom) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            if (weapon == null) return false;
            CancelPreview();
            CancelModuleColor();
            weapon.LockConstruction();
            paintingMode = true;
            stickerToolActive = false;
            stickerEraseMode = false;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintFillMode = false;
            paintPointerHeld = false;
            paletteOpen = false;
            deleteMode = false;
            paintBrush = null;
            stickerBrush = null;
            selectedPaintBlock = null;
            selectedPaintFace = Vector3Int.zero;
            UpdateEraseModeUI();
            return true;
        }

        public bool BeginPainting() => BeginCustomMode();

        public bool SelectPaintMaterial(Material material)
        {
            if (!paintingMode || material == null || GameManager.GameplayBlocked) return false;
            paintBrush = material;
            stickerToolActive = false;
            stickerEraseMode = false;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintFillMode = false;
            return true;
        }

        public bool SelectSticker(Texture2D texture)
        {
            if (!paintingMode || texture == null || GameManager.GameplayBlocked) return false;
            stickerBrush = texture;
            stickerToolActive = true;
            stickerEraseMode = false;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintPointerHeld = false;
            paintFillMode = false;
            return true;
        }

        public bool SelectKeyringForPlacement(GameObject modelPrefab)
        {
            if (!paintingMode || modelPrefab == null || GameManager.GameplayBlocked) return false;
            selectedKeyringModel = modelPrefab;
            keyringToolActive = true;
            stickerToolActive = false;
            stickerEraseMode = false;
            paintPointerHeld = false;
            paintFillMode = false;
            return true;
        }

        public void CancelKeyringPlacement()
        {
            keyringToolActive = false;
            selectedKeyringModel = null;
        }

        public bool ToggleStickerHorizontalFlip()
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            WeaponStickerHistory history = CurrentStickerHistory;
            return history != null && history.ToggleLastStickerHorizontalFlip();
        }

        public bool AdjustStickerSize(int direction)
        {
            if (!paintingMode || GameManager.GameplayBlocked || direction == 0) return false;
            WeaponStickerHistory history = CurrentStickerHistory;
            return history != null && history.AdjustLastStickerSize(direction);
        }

        public bool SetStickerEraseMode(bool enabled)
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            stickerEraseMode = enabled;
            stickerToolActive = enabled || stickerBrush != null;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintPointerHeld = false;
            if (enabled) paintFillMode = false;
            return true;
        }

        public bool SetPaintFillMode(bool enabled)
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            paintFillMode = enabled;
            if (enabled)
            {
                stickerToolActive = false;
                stickerEraseMode = false;
                keyringToolActive = false;
                selectedKeyringModel = null;
            }
            paintPointerHeld = false;
            return true;
        }

        public bool UndoPaint()
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            WeaponPaintHistory history = weapon != null ? weapon.GetComponent<WeaponPaintHistory>() : null;
            return history != null && history.Undo();
        }

        public bool UndoSticker()
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            WeaponStickerHistory history = weapon != null ? weapon.GetComponent<WeaponStickerHistory>() : null;
            return history != null && history.Undo();
        }

        public bool UndoKeyring()
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            WeaponKeyringHistory history = weapon != null ? weapon.GetComponent<WeaponKeyringHistory>() : null;
            return history != null && history.Undo();
        }

        public bool RemoveKeyring()
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            WeaponKeyringHistory history = CurrentKeyringHistory;
            if (history == null || history.Count == 0) return false;

            history.ClearAll();
            CancelKeyringPlacement();
            return true;
        }

        public bool RemoveCustomAndResumeEditing()
        {
            if (!paintingMode || GameManager.GameplayBlocked) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (weapon == null || !weapon.ConstructionLocked || assembly == null) return false;

            WeaponPaintHistory history = weapon.GetComponent<WeaponPaintHistory>();
            if (history != null) history.ClearAllPaint(assembly);
            else
                foreach (PlacedBlock block in assembly.GetComponentsInChildren<PlacedBlock>(true))
                    if (block != null && block.transform.parent == assembly.transform)
                        block.GetComponent<ModuleFacePaint>()?.ClearAll();

            weapon.GetComponent<WeaponStickerHistory>()?.ClearAll();
            weapon.GetComponent<WeaponKeyringHistory>()?.ClearAll();

            weapon.UnlockConstruction();
            paintingMode = false;
            stickerToolActive = false;
            stickerEraseMode = false;
            keyringToolActive = false;
            selectedKeyringModel = null;
            paintFillMode = false;
            paintPointerHeld = false;
            paintBrush = null;
            stickerBrush = null;
            selectedPaintBlock = null;
            selectedPaintFace = Vector3Int.zero;
            paletteOpen = false;
            deleteMode = false;
            UpdateEraseModeUI();
            return true;
        }

        public bool RemoveAllPaintAndResumeEditing() => RemoveCustomAndResumeEditing();

        private bool TrySelectPaintFace(bool applyBrush = true)
        {
            if (!paintingMode || GameManager.GameplayBlocked || IsPointerOverUI(AimPoint())) return false;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer) || !TryGetSurfaceHit(pointer, out RaycastHit hit))
            {
                selectedPaintBlock = null;
                selectedPaintFace = Vector3Int.zero;
                return false;
            }

            PlacedBlock block = hit.collider.GetComponentInParent<PlacedBlock>();
            EquipableWeapon weapon = FindWorkspaceWeapon();
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (RejectCoreCustom(hit.collider.transform))
            {
                selectedPaintBlock = null;
                selectedPaintFace = Vector3Int.zero;
                return false;
            }
            if (block == null || assembly == null || block.transform.parent != assembly.transform ||
                block.GetComponent<ColorModule>() != null)
            {
                selectedPaintBlock = null;
                selectedPaintFace = Vector3Int.zero;
                return false;
            }

            Vector3 normal = block.transform.InverseTransformDirection(hit.normal);
            selectedPaintFace = Mathf.Abs(normal.x) >= Mathf.Abs(normal.y) && Mathf.Abs(normal.x) >= Mathf.Abs(normal.z)
                ? new Vector3Int(normal.x >= 0f ? 1 : -1, 0, 0)
                : Mathf.Abs(normal.y) >= Mathf.Abs(normal.z)
                    ? new Vector3Int(0, normal.y >= 0f ? 1 : -1, 0)
                    : new Vector3Int(0, 0, normal.z >= 0f ? 1 : -1);
            selectedPaintBlock = block;
            if (applyBrush && paintBrush != null) ApplyPaintToSelectedFace();
            return true;
        }

        private bool FillPaintRegionAtSelection()
        {
            if (!paintingMode || selectedPaintBlock == null || paintBrush == null) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (weapon == null || !weapon.ConstructionLocked ||
                selectedPaintBlock.transform.parent != assembly?.transform) return false;

            WeaponPaintHistory history = weapon.GetComponent<WeaponPaintHistory>();
            if (history == null) history = weapon.gameObject.AddComponent<WeaponPaintHistory>();
            return history.FillSurfaceRegion(assembly, selectedPaintBlock, selectedPaintFace, paintBrush);
        }

        private bool TryPlaceStickerAtPointer()
        {
            if (!paintingMode || !stickerToolActive || stickerBrush == null ||
                GameManager.GameplayBlocked || IsPointerOverUI(AimPoint())) return false;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer) || !TryGetSurfaceHit(pointer, out RaycastHit hit)) return false;
            if (RejectCoreCustom(hit.collider.transform)) return false;

            PlacedBlock block = hit.collider.GetComponentInParent<PlacedBlock>();
            EquipableWeapon weapon = FindWorkspaceWeapon();
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (block == null || assembly == null || block.transform.parent != assembly.transform) return false;

            WeaponStickerHistory history = weapon.GetComponent<WeaponStickerHistory>();
            if (history == null) history = weapon.gameObject.AddComponent<WeaponStickerHistory>();
            if (history.ActiveStickerCount >= WeaponStickerHistory.MaximumStickerCount)
            {
                ShowCustomLimitMessage("스티커는 10개까지 붙일 수 있습니다");
                return false;
            }
            return history.Place(block, stickerBrush, hit.point, hit.normal,
                assembly.transform.up, assembly.transform.forward);
        }

        private bool TryPlaceKeyringAtPointer()
        {
            if (!paintingMode || !keyringToolActive || selectedKeyringModel == null || GameManager.GameplayBlocked ||
                IsPointerOverUI(AimPoint())) return false;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer) || !TryGetSurfaceHit(pointer, out RaycastHit hit)) return false;
            if (RejectCoreCustom(hit.collider.transform)) return false;

            EquipableWeapon weapon = FindWorkspaceWeapon();
            if (weapon == null || hit.collider.GetComponentInParent<EquipableWeapon>() != weapon) return false;

            WeaponKeyringHistory history = weapon.GetComponent<WeaponKeyringHistory>();
            if (history != null && history.Count >= WeaponKeyringHistory.MaximumKeyringCount)
            {
                ShowCustomLimitMessage("키링은 1개까지 붙일 수 있습니다");
                return false;
            }
            if (history == null) history = weapon.gameObject.AddComponent<WeaponKeyringHistory>();
            return history.Place(weapon, selectedKeyringModel, hit.collider.transform, hit.point, hit.normal);
        }

        private bool RejectCoreCustom(Transform target)
        {
            if (target.GetComponentInParent<PlacedBlock>() != null ||
                target.GetComponentInParent<PowerCore>() == null) return false;
            GameManager.Instance?.UI?.ShowNotification("코어는 커스텀할 수 없습니다");
            return true;
        }

        private void ShowCustomLimitMessage(string message)
        {
            GameManager.Instance?.UI?.ShowNotification(message);
            WorkshopSoundEffects.RaiseCustomLimitReached(view != null ? view.transform.position : transform.position);
        }

        private bool TryEraseStickerAtPointer()
        {
            if (!paintingMode || !stickerEraseMode ||
                GameManager.GameplayBlocked || IsPointerOverUI(AimPoint())) return false;
            Vector2 pointer = AimPoint();
            if (!view.pixelRect.Contains(pointer) ||
                !Physics.Raycast(view.ScreenPointToRay(pointer), out RaycastHit hit, 100f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;

            StickerOverlay sticker = hit.collider.GetComponentInParent<StickerOverlay>();
            EquipableWeapon weapon = FindWorkspaceWeapon();
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (sticker == null || assembly == null || !sticker.transform.IsChildOf(assembly.transform)) return false;
            WeaponStickerHistory history = weapon.GetComponent<WeaponStickerHistory>();
            return history != null && history.Remove(sticker);
        }

        public bool TryGetHoveredModule(out RaycastHit hit)
        {
            hit = default;
            if (!isActiveAndEnabled || controls == null || view == null || GameManager.GameplayBlocked) return false;
            Vector2 pointer = AimPoint();
            return view.pixelRect.Contains(pointer) && !IsPointerOverUI(pointer) && TryGetSurfaceHit(pointer, out hit);
        }

        private bool TryGetSurfaceHit(Vector2 pointer, out RaycastHit closestHit)
        {
            Ray ray = view.ScreenPointToRay(pointer);
            int hitCount = Physics.RaycastNonAlloc(ray, surfaceHits, 100f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = surfaceHits;
            if (hitCount == surfaceHits.Length)
            {
                hits = Physics.RaycastAll(ray, 100f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
                hitCount = hits.Length;
            }

            closestHit = default;
            float closestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.GetComponentInParent<StickerOverlay>() != null ||
                    hit.collider.GetComponentInParent<WeaponKeyring>() != null ||
                    hit.distance >= closestDistance) continue;
                closestHit = hit;
                closestDistance = hit.distance;
                found = true;
            }
            return found;
        }

        private bool ApplyPaintToSelectedFace()
        {
            if (!paintingMode || selectedPaintBlock == null || paintBrush == null) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            if (weapon == null || !weapon.ConstructionLocked ||
                selectedPaintBlock.transform.parent != weapon.GetComponentInChildren<CubeAssembly>(true)?.transform)
                return false;

            WeaponPaintHistory history = weapon.GetComponent<WeaponPaintHistory>();
            if (history == null) history = weapon.gameObject.AddComponent<WeaponPaintHistory>();
            return history.Apply(selectedPaintBlock, selectedPaintFace, paintBrush);
        }

        public bool PaintAllFaces()
        {
            if (!paintingMode || GameManager.GameplayBlocked || paintBrush == null) return false;
            EquipableWeapon weapon = FindWorkspaceWeapon();
            CubeAssembly assembly = weapon != null ? weapon.GetComponentInChildren<CubeAssembly>(true) : null;
            if (weapon == null || !weapon.ConstructionLocked || assembly == null) return false;

            WeaponPaintHistory history = weapon.GetComponent<WeaponPaintHistory>();
            if (history == null) history = weapon.gameObject.AddComponent<WeaponPaintHistory>();
            return history.ApplyToAllExposedFaces(assembly, paintBrush);
        }

        private EquipableWeapon FindWorkspaceWeapon()
            => workspace != null ? workspace.GetComponentInChildren<EquipableWeapon>(true) : null;

        private static string FaceLabel(Vector3Int face)
        {
            if (face.x > 0) return "+X";
            if (face.x < 0) return "-X";
            if (face.y > 0) return "+Y";
            if (face.y < 0) return "-Y";
            return face.z > 0 ? "+Z" : "-Z";
        }

        private static bool IsPointerOverUI(Vector2 pointer)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            var eventData = new PointerEventData(eventSystem) { position = pointer };
            var raycasts = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, raycasts);
            foreach (RaycastResult result in raycasts)
                if (result.module is GraphicRaycaster) return true;
            return false;
        }

        private Vector2 AimPoint() => controls.FindAction("Point").ReadValue<Vector2>();
    }
}
