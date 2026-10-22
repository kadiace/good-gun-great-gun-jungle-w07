using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private FeedbackUI feedback;
        public FeedbackUI Feedback => feedback;
        public bool IsChoiceOpen => feedback != null && feedback.IsChoiceOpen;
        [SerializeField] private TMPro.TextMeshProUGUI notificationText;
        [SerializeField] private CanvasGroup notificationGroup;
        [SerializeField, Min(0.01f)] private float notificationEnterDuration = .18f;
        [SerializeField, Min(0f)] private float notificationHoldDuration = 2f;
        [SerializeField, Min(0.01f)] private float notificationExitDuration = .4f;
        private float notificationElapsed = -1f;
        public void ShowNotification(string message)
        {
            if (feedback != null) { feedback.ShowNotification(message); return; }
            if (!isActiveAndEnabled || notificationText == null || notificationGroup == null) return;
            if (string.IsNullOrEmpty(message)) { HideNotification(); return; }
            notificationText.text = message;
            notificationElapsed = 0f;
            notificationGroup.alpha = 0f;
            notificationGroup.transform.localScale = Vector3.one * .92f;
            notificationGroup.gameObject.SetActive(true);
        }
        private void HideNotification()
        {
            notificationElapsed = -1f;
            if (notificationText != null) notificationText.text = string.Empty;
            if (notificationGroup == null) return;
            notificationGroup.alpha = 0f;
            notificationGroup.interactable = notificationGroup.blocksRaycasts = false;
            notificationGroup.transform.localScale = Vector3.one;
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = Player != null ? Player.GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this);
            notificationGroup.gameObject.SetActive(false);
        }
        private void UpdateNotification()
        {
            if (notificationElapsed < 0f || notificationGroup == null) return;
            notificationElapsed += Time.unscaledDeltaTime;
            float enter = Mathf.Max(.01f, notificationEnterDuration);
            float exit = Mathf.Max(.01f, notificationExitDuration);
            if (notificationElapsed < enter)
            {
                float t = Mathf.SmoothStep(0f, 1f, notificationElapsed / enter);
                notificationGroup.alpha = t;
                notificationGroup.transform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, t);
            }
            else if (notificationElapsed < enter + notificationHoldDuration)
            { notificationGroup.alpha = 1f; notificationGroup.transform.localScale = Vector3.one;
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = Player != null ? Player.GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this); }
            else
            {
                float t = (notificationElapsed - enter - notificationHoldDuration) / exit;
                if (t >= 1f) { HideNotification(); return; }
                notificationGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            }
        }
        [SerializeField] private GameManager gameManager;
        [SerializeField] private FirstPersonPlayer player;
        [Header("Workshop UI")]
        [SerializeField] private RotationCircleGraphic rotationCircle;
        [SerializeField] private RectTransform frontMark;
        [SerializeField] private RectTransform upMark;
        [SerializeField, Range(0f, 1f), Tooltip("이 값보다 확실히 뒤를 향할 때만 표시를 흐리게 합니다.")]
        private float directionBackThreshold = .05f;
        private UnityEngine.UI.Graphic[] upGraphics;
        private float[] upAlphas;
        private UnityEngine.UI.Graphic[] frontGraphics;
        private float[] frontAlphas;
        private void SetFrontOpacity(float opacity)
        {
            if (frontGraphics == null) return;
            for (int i = 0; i < frontGraphics.Length; i++)
            {
                if (frontGraphics[i] == null) continue;
                Color color = frontGraphics[i].color;
                color.a = frontAlphas[i] * opacity;
                frontGraphics[i].color = color;
            }
        }
        [SerializeField, Range(0f, 1f)] private float rotationCircleRadius = .5f;
        private WorkshopSession workshopSession;
        public float GetWorkshopRotationRadius(Rect viewport) => Mathf.Min(viewport.width, viewport.height) * rotationCircleRadius;
        public void SetWorkshopUI(bool editing)
        {
            if (upMark != null && upGraphics == null)
            {
                upGraphics = upMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                upAlphas = new float[upGraphics.Length];
                for (int i = 0; i < upGraphics.Length; i++) upAlphas[i] = upGraphics[i].color.a;
            }
            if (upMark != null) upMark.gameObject.SetActive(editing);
            if (rotationCircle != null) rotationCircle.gameObject.SetActive(editing &&
                (workshopSession == null || workshopSession.PlanarView == null || !workshopSession.PlanarView.Is2D));
            if (frontMark != null) frontMark.gameObject.SetActive(editing);
            if (!editing) SetFrontOpacity(1f);
        }
        [SerializeField] private AimUI aimUI;
        [SerializeField] private HoveringUI hoveringUI;
        private WeaponHolder weaponHolder;


        [SerializeField] private NightRaidUI nightRaidUI;
        public NightRaidUI RaidUI => nightRaidUI;


        public FirstPersonPlayer Player
        {
            get
            {
                if (player == null) player = FindFirstObjectByType<FirstPersonPlayer>();
                return player;
            }
        }

        public WeaponHolder PlayerWeaponHolder
        {
            get
            {
                FirstPersonPlayer currentPlayer = Player;
                if (currentPlayer == null) return null;
                if (weaponHolder == null || weaponHolder.gameObject != currentPlayer.gameObject)
                    weaponHolder = currentPlayer.GetComponent<WeaponHolder>();
                return weaponHolder;
            }
        }
        [SerializeField, Tooltip("비우면 플레이어 카메라의 CubeBuilder를 찾음.")]
        private CubeBuilder builder;
        [SerializeField, Tooltip("삭제 모드 표시. 시작 시 CubeBuilder에 넘겨줌.")]
        private GameObject eraseModeUI;
        [SerializeField] private TMPro.TextMeshProUGUI[] moduleLabels;
        [SerializeField] private Color selectedModuleColor = new Color(1f, 0.8f, 0.2f, 1f);
        private int lastSelectedSlot = -1;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private UnityEngine.UI.Button resumeButton;
        private void Awake()
        {
            nightRaidUI?.Initialize();
            foreach (var slots in GetComponentsInChildren<SaveSlotsUI>(true)) slots.Initialize();
            // 사운드 설정도 비활성 상태에서 초기 연결하고 닫힌 화면으로 시작한다.
            foreach (var sound in GetComponentsInChildren<SoundSettingsUI>(true)) sound.Initialize();
            // CanvasScaler controls UI sizing; a zero authored root scale hides every panel.
            transform.localScale = Vector3.one;
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = Player != null ? Player.GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this);
            if (aimUI != null) aimUI.Initialize(this);
            weaponHolder = PlayerWeaponHolder;
            if (builder == null && Player != null) builder = Player.GetComponentInChildren<CubeBuilder>(true);
            foreach (var palette in GetComponentsInChildren<ModulePaletteUI>(true)) palette.Initialize(builder);
            if (eraseModeUI != null) eraseModeUI.SetActive(false);
            if (builder != null && eraseModeUI != null) builder.SetEraseModeUI(eraseModeUI);
            if (hoveringUI != null) hoveringUI.Initialize(builder, Player != null ? Player.GetComponentInChildren<Interaction>(true) : null);

            HideNotification();
            pausePanel?.SetActive(false);
            UpdateModuleSelection();
        }
        public void Resume() => gameManager?.Resume();
        public void Restart() => gameManager?.Restart();
        public void LeaveWorkshop() => gameManager?.LeaveWorkshop();
        private void LateUpdate()
        {
            UpdateModuleSelection();

            UpdateWorkshopDirectionUI();
        }
        private void UpdateWorkshopDirectionUI()
        {
            bool editing = workshopSession != null && workshopSession.IsEditing;
            PowerCore core = editing && workshopSession.ActiveWorkbench != null ? workshopSession.ActiveWorkbench.CurrentCore : null;
            Camera view = editing ? workshopSession.WorkshopCamera : null;
            bool planar = editing && workshopSession.PlanarView != null && workshopSession.PlanarView.Is2D;
            if (rotationCircle != null && rotationCircle.gameObject.activeSelf != (editing && !planar))
                rotationCircle.gameObject.SetActive(editing && !planar);
            if (editing && !planar && rotationCircle != null)
            {
                Rect viewport = workshopSession.RotationViewport;
                rotationCircle.SetGeometry(viewport, GetWorkshopRotationRadius(viewport));
            }
            if (core == null || view == null)
            {
                if (frontMark != null) frontMark.gameObject.SetActive(false);
                if (upMark != null) upMark.gameObject.SetActive(false);
                return;
            }
            // Follow the connected blocks on the core's local forward axis, independent of their output direction.
            var assembly = core.GetComponent<CubeAssembly>();
            var occupied = new System.Collections.Generic.HashSet<Vector3Int>();
            if (assembly != null)
                foreach (var block in assembly.GetComponentsInChildren<PlacedBlock>())
                    if (block.transform.parent == assembly.transform) occupied.Add(block.Cell);
            UpdateDirectionMark(frontMark, Vector3Int.forward, core, view, occupied, frontGraphics, frontAlphas);
            UpdateDirectionMark(upMark, Vector3Int.up, core, view, occupied, upGraphics, upAlphas);
        }
        private void UpdateDirectionMark(RectTransform mark, Vector3Int axis, PowerCore core, Camera view,
            System.Collections.Generic.HashSet<Vector3Int> occupied, UnityEngine.UI.Graphic[] graphics, float[] alphas)
        {
            if (mark == null) return;
            Vector3Int cell = Vector3Int.zero;
            int furthest = 0;
            int closestToCoreAxis = 0;
            foreach (var candidate in occupied)
            {
                int depth = (candidate.x * axis.x + candidate.y * axis.y + candidate.z * axis.z);
                int axisDistance = candidate.sqrMagnitude - depth * depth;
                if (depth > furthest || (depth == furthest && axisDistance < closestToCoreAxis))
                {
                    cell = candidate;
                    furthest = depth;
                    closestToCoreAxis = axisDistance;
                }
                else if (depth == furthest && axisDistance == closestToCoreAxis &&
                    (candidate.x < cell.x || (candidate.x == cell.x &&
                    (candidate.y < cell.y || (candidate.y == cell.y && candidate.z < cell.z)))))
                    cell = candidate;
            }
            Vector3 facePoint = core.transform.TransformPoint((Vector3)cell + (Vector3)axis * .501f);
            Vector3 screenPoint = view.WorldToScreenPoint(facePoint);
            bool visible = screenPoint.z > view.nearClipPlane;
            mark.gameObject.SetActive(visible);
            float opacity = Vector3.Dot(view.transform.forward, core.transform.TransformDirection((Vector3)axis)) > directionBackThreshold ? .2f : 1f;
            if (graphics != null && alphas != null)
                for (int i = 0; i < graphics.Length; i++)
                {
                    if (graphics[i] == null) continue;
                    var color = graphics[i].color; color.a = alphas[i] * opacity; graphics[i].color = color;
                }
            if (!visible) return;
            if (mark.parent is RectTransform parent)
            {
                Canvas canvas = mark.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out Vector2 position))
                    mark.localPosition = new Vector3(position.x, position.y, 0f);
            }
        }
        private void UpdateModuleSelection()
        {
            int slot = builder != null && !builder.DeleteMode ? builder.SelectedSlot : 0;
            if (lastSelectedSlot == slot) return;
            lastSelectedSlot = slot;
            if (moduleLabels == null) return;
            for (int i = 0; i < moduleLabels.Length; i++)
            {
                if (moduleLabels[i] == null) continue;
                bool selected = i + 1 == slot;
                moduleLabels[i].color = selected ? selectedModuleColor : Color.white;
                UIFonts.ApplyWeight(moduleLabels[i], selected ? 700 : 400);
            }
        }
        private void OnDisable() { feedback?.Cancel(); SetWorkshopUI(false); }
        public void Refresh(GameManager game)
        {
            bool pauseChanged = pausePanel != null && pausePanel.activeSelf != game.IsPaused;
            // 일시정지를 닫으면 하위 설정 화면도 닫아 다음 열기에서 메뉴가 겹치지 않게 한다.
            if (pauseChanged && !game.IsPaused) GetComponent<SoundSettingsUI>()?.Close();
            pausePanel?.SetActive(game.IsPaused);
            if (pauseChanged && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
