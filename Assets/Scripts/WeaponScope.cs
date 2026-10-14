using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    // 장착 무기에 스코프 모듈이 있으면 Player/Aim(우클릭)을 누르는 동안 스코프를 통해 줌.
    // 독립된 시야는 휠로 선택하고, 같은 방향으로 이어진 스코프는 배율을 높인다.
    // 줌 시간·감도는 ProjectileTuning.
    [RequireComponent(typeof(WeaponHolder))]
    [DefaultExecutionOrder(20)]
    public sealed class WeaponScope : MonoBehaviour
    {
        private const float FullLengthReticleMinMagnification = 3f;
        // 조준경 오버레이(원형 렌즈·십자선)는 UIManager 프리팹에 있고 이 태그로 찾는다.
        // 태그 검색은 비활성 오브젝트를 찾지 못하므로 오버레이는 켜 둔 채 투명도로 숨긴다.
        public const string OverlayTag = "ScopeOverlay";

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField, Min(0.001f)] private float scopeNearClipPlane = 0.01f;
        private CanvasGroup scopeOverlay;
        private ScopeOverlayGraphic overlayGraphic;
        private WeaponHolder holder;
        private FirstPersonPlayer player;
        private InputActionMap controls;
        private InputAction aim;
        private InputAction cycleScope;
        private Camera zoomCamera;
        private float baseFieldOfView;
        // 0 = normal view, 1 = fully zoomed; advances at a fixed rate regardless of magnification.
        private float progress;
        private bool hasLastScopeCameraPose;
        private EquipableWeapon lastScopeWeapon;
        private Vector3Int lastScopeCell;
        private Vector3 lastScopeCameraPosition;
        private Quaternion lastScopeCameraRotation;
        private float lastScopeCameraFieldOfView;
        private bool scopeCameraTransitionActive;
        private float scopeCameraTransitionElapsed;
        private float scopeCameraTransitionDuration = 0.15f;
        private Vector3 scopeCameraTransitionStartPosition;
        private Quaternion scopeCameraTransitionStartRotation;
        private float scopeCameraTransitionStartFieldOfView;

        // Camera state before zooming; the main camera shows the first view and returns here.
        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation = Quaternion.identity;
        private Rect baseRect = new Rect(0f, 0f, 1f, 1f);
        private float baseNearClipPlane = 0.3f;
        private bool zoomApplied;

        private readonly List<EquipableWeapon.ScopeView> views = new List<EquipableWeapon.ScopeView>();
        private readonly Dictionary<Renderer, bool> weaponRendererStates = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Transform, ScopeVisualPose> scopeVisualPoses = new Dictionary<Transform, ScopeVisualPose>();
        private EquipableWeapon scopeOcclusionWeapon;
        private Vector3Int scopeOcclusionCell;
        private bool scopeOcclusionActive;

        private struct ScopeVisualPose
        {
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        public bool IsAiming => progress > 0f;

        private void Awake()
        {
            holder = GetComponent<WeaponHolder>();
            player = GetComponent<FirstPersonPlayer>();
            GameObject overlay = GameObject.FindWithTag(OverlayTag);
            if (overlay != null)
            {
                scopeOverlay = overlay.GetComponent<CanvasGroup>();
                overlayGraphic = overlay.GetComponent<ScopeOverlayGraphic>();
            }
            SetOverlay(0f, false);
            if (inputActions == null) return;
            controls = inputActions.FindActionMap("Player", true).Clone();
            aim = controls.FindAction("Aim", true);
            cycleScope = controls.FindAction("CycleScope", true);
        }

        private void OnEnable() => controls?.Enable();

        private void OnDisable()
        {
            controls?.Disable();
            ResetZoom();
        }

        private void OnDestroy()
        {
            controls?.Dispose();
        }

        private void LateUpdate()
        {
            RestoreScopeVisuals(false);
            Camera view = holder.AimCamera;
            if (view == null)
            {
                RestoreWeaponRenderers();
                RestoreScopeVisuals(true);
                return;
            }
            if (zoomCamera != view)
            {
                ResetZoom();
                zoomCamera = view;
            }
            // Keep following other camera changes while the zoom has not touched the camera.
            if (!zoomApplied)
            {
                baseFieldOfView = view.fieldOfView;
                baseLocalPosition = view.transform.localPosition;
                baseLocalRotation = view.transform.localRotation;
                baseRect = view.rect;
                baseNearClipPlane = view.nearClipPlane;
            }

            EquipableWeapon weapon = holder.EquippedWeapon;
            ProjectileTuning tuning = weapon != null ? weapon.Tuning : null;
            bool canAim = aim != null && aim.IsPressed() && tuning != null && weapon.IsEquipped &&
                          !weapon.IsMoving && !GameManager.GameplayBlocked && player != null && player.isActiveAndEnabled;
            // Each independent scope position is a selectable sight; aligned scopes form one magnification group.
            if (weapon != null && weapon.IsEquipped) weapon.GetScopeViews(views);
            else views.Clear();
            int selectedIndex = weapon != null ? weapon.ResolveSelectedScopeIndex(views) : -1;
            bool canCycleScope = weapon != null && weapon.IsEquipped && !weapon.IsMoving &&
                                 !GameManager.GameplayBlocked && player != null && player.isActiveAndEnabled;
            if (canCycleScope && views.Count > 1 && cycleScope != null && cycleScope.WasPerformedThisFrame())
            {
                float scroll = cycleScope.ReadValue<Vector2>().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // Hidden scope renderers are omitted from GetScopeEyePosition. Restore them before
                    // measuring the newly selected sight, or its temporary fallback can move the camera.
                    if (scopeOcclusionActive)
                    {
                        RestoreWeaponRenderers();
                        weapon.GetScopeViews(views);
                        selectedIndex = weapon.ResolveSelectedScopeIndex(views);
                    }
                    weapon.SetSelectedScopeIndex(views, selectedIndex + (scroll > 0f ? 1 : -1));
                    selectedIndex = weapon.ResolveSelectedScopeIndex(views);
                }
            }
            bool aiming = canAim && selectedIndex >= 0;
            if ((aiming || progress > 0f) && selectedIndex >= 0 && hasLastScopeCameraPose &&
                (lastScopeWeapon != weapon || lastScopeCell != views[selectedIndex].Cell))
                BeginScopeCameraTransition(tuning != null ? tuning.scopeZoomDuration : 0.15f);
            if ((aiming || progress > 0f) && selectedIndex >= 0)
                SetScopeOcclusionRenderers(weapon, views[selectedIndex]);
            else
                RestoreWeaponRenderers();
            if (!aiming && progress <= 0f) return;

            float duration = tuning != null ? tuning.scopeZoomDuration : 0.15f;
            progress = Mathf.MoveTowards(progress, aiming ? 1f : 0f, Time.unscaledDeltaTime / duration);
            if (progress <= 0f || selectedIndex < 0) { ResetZoom(); return; }
            zoomApplied = true;

            EquipableWeapon.ScopeView selectedView = views[selectedIndex];
            float oneScopeMagnification = tuning != null ? tuning.scopeMagnificationPerModule : 2f;
            float targetMagnification = 1f + Mathf.Max(0f, oneScopeMagnification - 1f) * selectedView.Count;
            float blend = Mathf.SmoothStep(0f, 1f, progress);
            Transform cameraTransform = zoomCamera.transform;
            cameraTransform.localPosition = baseLocalPosition;
            cameraTransform.localRotation = baseLocalRotation;
            Vector3 basePosition = cameraTransform.position;
            Quaternion baseRotation = cameraTransform.rotation;
            float zoom = Mathf.Pow(targetMagnification, progress);
            zoomCamera.rect = baseRect;
            zoomCamera.ResetAspect();
            float targetFieldOfView = baseFieldOfView / zoom;
            zoomCamera.nearClipPlane = Mathf.Lerp(baseNearClipPlane,
                Mathf.Min(baseNearClipPlane, scopeNearClipPlane), blend);
            Vector3 aimDirection = holder.GetScopeAimPoint() - selectedView.EyePosition;
            Vector3 scopeForward = selectedView.Rotation * Vector3.forward;
            Vector3 scopeRight = selectedView.Rotation * Vector3.right;
            Vector3 pitchCorrectedDirection = Vector3.ProjectOnPlane(aimDirection, scopeRight);
            Quaternion correctedScopeRotation = selectedView.Rotation;
            bool scopeFacesAim = aimDirection.sqrMagnitude > 0.0001f &&
                                 Vector3.Dot(scopeForward, aimDirection.normalized) > 0f;
            if (scopeFacesAim && pitchCorrectedDirection.sqrMagnitude > 0.0001f)
            {
                float pitchCorrection = Vector3.SignedAngle(selectedView.Rotation * Vector3.forward,
                    pitchCorrectedDirection, scopeRight);
                correctedScopeRotation = Quaternion.AngleAxis(pitchCorrection, scopeRight) * selectedView.Rotation;
            }
            Vector3 targetCameraPosition = Vector3.Lerp(basePosition, selectedView.EyePosition, blend);
            Quaternion targetCameraRotation = Quaternion.Slerp(baseRotation, correctedScopeRotation, blend);
            bool transitioning = scopeCameraTransitionActive;
            float transitionBlend = GetScopeCameraTransitionBlend();
            Vector3 cameraPosition = transitioning
                ? Vector3.Lerp(scopeCameraTransitionStartPosition, targetCameraPosition, transitionBlend)
                : targetCameraPosition;
            Quaternion cameraRotation = transitioning
                ? Quaternion.Slerp(scopeCameraTransitionStartRotation, targetCameraRotation, transitionBlend)
                : targetCameraRotation;
            float cameraFieldOfView = transitioning
                ? Mathf.Lerp(scopeCameraTransitionStartFieldOfView, targetFieldOfView, transitionBlend)
                : targetFieldOfView;
            zoomCamera.fieldOfView = cameraFieldOfView;
            zoomCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            ApplyScopeVisualCorrection(weapon, selectedView, cameraPosition,
                cameraRotation * Quaternion.Inverse(selectedView.Rotation));
            if (player != null)
                player.LookSensitivityScale = tuning == null || tuning.scopeScalesSensitivity
                    ? cameraFieldOfView / baseFieldOfView : 1f;
            hasLastScopeCameraPose = true;
            lastScopeWeapon = weapon;
            lastScopeCell = selectedView.Cell;
            lastScopeCameraPosition = cameraPosition;
            lastScopeCameraRotation = cameraRotation;
            lastScopeCameraFieldOfView = cameraFieldOfView;
            SetOverlay(Mathf.InverseLerp(0.6f, 1f, progress),
                targetMagnification >= FullLengthReticleMinMagnification);
        }

        private void BeginScopeCameraTransition(float duration)
        {
            scopeCameraTransitionStartPosition = lastScopeCameraPosition;
            scopeCameraTransitionStartRotation = lastScopeCameraRotation;
            scopeCameraTransitionStartFieldOfView = lastScopeCameraFieldOfView;
            scopeCameraTransitionDuration = Mathf.Max(0.001f, duration);
            scopeCameraTransitionElapsed = 0f;
            scopeCameraTransitionActive = true;
        }

        private float GetScopeCameraTransitionBlend()
        {
            if (!scopeCameraTransitionActive) return 1f;
            scopeCameraTransitionElapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(scopeCameraTransitionElapsed / scopeCameraTransitionDuration);
            if (normalizedTime >= 1f) scopeCameraTransitionActive = false;
            return Mathf.SmoothStep(0f, 1f, normalizedTime);
        }

        private void SetOverlay(float alpha, bool fullLengthReticle)
        {
            if (scopeOverlay != null)
            {
                scopeOverlay.alpha = alpha;
                scopeOverlay.blocksRaycasts = false;
                scopeOverlay.interactable = false;
            }
            if (overlayGraphic == null) return;
            overlayGraphic.SetView(baseRect, fullLengthReticle);
            bool visible = alpha > 0f;
            if (overlayGraphic.enabled != visible) overlayGraphic.enabled = visible;
        }

        private void ResetZoom()
        {
            // Restore whenever the zoom touched the camera, even if progress already reached 0,
            // so a leftover tilt never becomes the next base pose.
            if (zoomCamera != null && zoomApplied)
            {
                zoomCamera.fieldOfView = baseFieldOfView;
                zoomCamera.nearClipPlane = baseNearClipPlane;
                zoomCamera.rect = baseRect;
                zoomCamera.ResetAspect();
                zoomCamera.transform.localPosition = baseLocalPosition;
                zoomCamera.transform.localRotation = baseLocalRotation;
            }
            zoomApplied = false;
            progress = 0f;
            hasLastScopeCameraPose = false;
            lastScopeWeapon = null;
            scopeCameraTransitionActive = false;
            scopeCameraTransitionElapsed = 0f;
            RestoreWeaponRenderers();
            RestoreScopeVisuals(true);
            if (player != null) player.LookSensitivityScale = 1f;
            SetOverlay(0f, false);
        }

        private void ApplyScopeVisualCorrection(EquipableWeapon weapon, EquipableWeapon.ScopeView view,
            Vector3 pivot, Quaternion correction)
        {
            if (weapon == null || Quaternion.Angle(Quaternion.identity, correction) < 0.001f) return;
            PlacedBlock[] blocks = weapon.GetComponentsInChildren<PlacedBlock>(true);
            PlacedBlock start = null;
            foreach (PlacedBlock block in blocks)
            {
                if (block != null && block.IsSight && block.Cell == view.Cell)
                {
                    start = block;
                    break;
                }
            }

            ExtensionModule startExtension = start != null ? start.GetComponent<ExtensionModule>() : null;
            if (startExtension == null) return;
            Vector3Int axis = startExtension.OutputDirection;
            for (int i = 0; i < view.Count; i++)
            {
                Vector3Int cell = view.Cell + axis * i;
                foreach (PlacedBlock block in blocks)
                {
                    if (block == null || !block.IsSight || block.Cell != cell) continue;
                    ExtensionModule extension = block.GetComponent<ExtensionModule>();
                    if (extension == null || extension.OutputDirection != axis) continue;
                    ModuleDraw moduleDraw = block.GetComponent<ModuleDraw>();
                    Transform drawRoot = moduleDraw != null ? moduleDraw.DrawRoot : null;
                    if (drawRoot == null || drawRoot == block.transform) continue;

                    if (!scopeVisualPoses.TryGetValue(drawRoot, out ScopeVisualPose pose))
                    {
                        pose = new ScopeVisualPose
                        {
                            LocalPosition = drawRoot.localPosition,
                            LocalRotation = drawRoot.localRotation
                        };
                        scopeVisualPoses[drawRoot] = pose;
                    }

                    Transform parent = drawRoot.parent;
                    Vector3 originalPosition = parent != null ? parent.TransformPoint(pose.LocalPosition) : pose.LocalPosition;
                    Quaternion originalRotation = parent != null ? parent.rotation * pose.LocalRotation : pose.LocalRotation;
                    drawRoot.SetPositionAndRotation(pivot + correction * (originalPosition - pivot),
                        correction * originalRotation);
                }
            }
        }

        private void RestoreScopeVisuals(bool clear)
        {
            foreach (KeyValuePair<Transform, ScopeVisualPose> entry in scopeVisualPoses)
            {
                Transform drawRoot = entry.Key;
                if (drawRoot == null) continue;
                drawRoot.localPosition = entry.Value.LocalPosition;
                drawRoot.localRotation = entry.Value.LocalRotation;
            }
            if (clear) scopeVisualPoses.Clear();
        }

        private void SetScopeOcclusionRenderers(EquipableWeapon weapon, EquipableWeapon.ScopeView view)
        {
            if (weapon == null)
            {
                RestoreWeaponRenderers();
                return;
            }
            if (scopeOcclusionActive && scopeOcclusionWeapon == weapon && scopeOcclusionCell == view.Cell) return;
            RestoreWeaponRenderers();

            PlacedBlock[] blocks = weapon.GetComponentsInChildren<PlacedBlock>(true);
            PlacedBlock start = null;
            foreach (PlacedBlock block in blocks)
            {
                if (block != null && block.IsSight && block.Cell == view.Cell)
                {
                    start = block;
                    break;
                }
            }

            ExtensionModule startExtension = start != null ? start.GetComponent<ExtensionModule>() : null;
            if (startExtension != null)
            {
                Vector3Int axis = startExtension.OutputDirection;
                HashSet<Vector3Int> selectedScopeCells = new HashSet<Vector3Int>();
                for (int i = 0; i < view.Count; i++)
                {
                    selectedScopeCells.Add(view.Cell + axis * i);
                }

                foreach (PlacedBlock block in blocks)
                {
                    if (block == null) continue;
                    Vector3Int offset = block.Cell - view.Cell;
                    int forwardDistance = offset.x * axis.x + offset.y * axis.y + offset.z * axis.z;
                    if (forwardDistance <= 0) continue;
                    if (selectedScopeCells.Contains(block.Cell) && block.IsSight)
                    {
                        ExtensionModule extension = block.GetComponent<ExtensionModule>();
                        if (extension != null && extension.OutputDirection == axis) continue;
                    }

                    foreach (Renderer renderer in block.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer == null || weaponRendererStates.ContainsKey(renderer)) continue;
                        weaponRendererStates[renderer] = renderer.enabled;
                        renderer.enabled = false;
                    }
                }
            }

            scopeOcclusionWeapon = weapon;
            scopeOcclusionCell = view.Cell;
            scopeOcclusionActive = true;
        }

        private void RestoreWeaponRenderers()
        {
            foreach (KeyValuePair<Renderer, bool> state in weaponRendererStates)
                if (state.Key != null) state.Key.enabled = state.Value;
            weaponRendererStates.Clear();
            scopeOcclusionWeapon = null;
            scopeOcclusionActive = false;
        }
    }
}
