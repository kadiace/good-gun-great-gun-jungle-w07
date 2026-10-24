using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private FirstPersonPlayer player;
        [SerializeField]
        private CubeBuilder builder;
        [SerializeField]
        private GameObject eraseModeUI;
        private readonly Dictionary<System.Type, Component> m_screenCache = new Dictionary<System.Type, Component>();
        [SerializeField] private AimUI aimUI;
        [SerializeField] private HoveringUI hoveringUI;
        [SerializeField] private NightRaidUI nightRaidUI;
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
        public T Screen<T>() where T : Component
        {
            if (m_screenCache.TryGetValue(typeof(T), out Component cached) && cached != null) return (T)cached;
            T found = GetComponentInChildren<T>(true);
            m_screenCache[typeof(T)] = found;
            return found;
        }
        public T[] Screens<T>() where T : Component => GetComponentsInChildren<T>(true);
        private FirstPersonPlayer ResolvePlayer() => player != null ? player : (gameManager != null ? gameManager.Player : null);

        private void Awake()
        {
            transform.localScale = Vector3.one;
            GameManager contextGame = gameManager != null ? gameManager : GameManager.Instance;
            FirstPersonPlayer contextPlayer = contextGame != null ? contextGame.Player : player;
            if (contextPlayer == null) contextPlayer = ResolvePlayer();
            if (builder == null && contextPlayer != null) builder = contextPlayer.GetComponentInChildren<CubeBuilder>(true);
            Interaction interaction = contextPlayer != null ? contextPlayer.GetComponentInChildren<Interaction>(true) : null;
            UIContext context = new UIContext { Game = contextGame, Ui = this, Player = contextPlayer, Builder = builder, Interaction = interaction };
            foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IUIScreen uiScreen) uiScreen.InitializeScreen(context);
            if (eraseModeUI != null) eraseModeUI.SetActive(false);
            if (builder != null && eraseModeUI != null) builder.SetEraseModeUI(eraseModeUI);
            if (frontMark != null)
            {
                frontGraphics = frontMark.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
                frontAlphas = new float[frontGraphics.Length];
                for (int i = 0; i < frontGraphics.Length; i++) frontAlphas[i] = frontGraphics[i].color.a;
            }
            SetWorkshopUI(false);
            workshopSession = ResolvePlayer() != null ? ResolvePlayer().GetComponent<WorkshopSession>() : null;
            if (workshopSession != null) workshopSession.BindWorkshopUI(this);
        }
        private void LateUpdate()
        {
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
            if (core == null || view == null || planar)
            {
                if (frontMark != null) frontMark.gameObject.SetActive(false);
                if (upMark != null) upMark.gameObject.SetActive(false);
                return;
            }
            // 2D와 같은 최외곽 면을 사용한다. 모듈의 출력 방향과는 무관하다.
            var assembly = core.GetComponent<CubeAssembly>();
            UpdateDirectionMark(frontMark, Vector3Int.forward, core, view, assembly, frontGraphics, frontAlphas);
            UpdateDirectionMark(upMark, Vector3Int.up, core, view, assembly, upGraphics, upAlphas);
        }
        private void UpdateDirectionMark(RectTransform mark, Vector3Int axis, PowerCore core, Camera view,
            CubeAssembly assembly, UnityEngine.UI.Graphic[] graphics, float[] alphas)
        {
            if (mark == null) return;
            Vector3 localFace = assembly != null ? assembly.GetOutermostFace(axis) : (Vector3)axis * .501f;
            Transform space = assembly != null ? assembly.transform : core.transform;
            Vector3 facePoint = space.TransformPoint(localFace);
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
        private void OnDisable()
        {
            Screen<FeedbackUI>()?.ForceClose();
            SetWorkshopUI(false);
        }
    }
}
