using TMPro;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class TutorialTargetUI : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI marker;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float bounceHeight = 8f;
        [SerializeField, Min(0f)] private float bounceSpeed = 2f;
        [SerializeField, Min(1f)] private float arrowSize = 48f;

        private void Awake()
        {
            Hide();
            if (viewCamera == null) viewCamera = Camera.main;
        }
        private void LateUpdate()
        {
            var tutorial = TutorialManager.Instance;
            var step = tutorial != null ? tutorial.CurrentStep : null;
            var target = tutorial != null ? tutorial.CurrentInteractionTarget : null;
            if (tutorial != null && tutorial.IsEditingTutorialWorkbench) { Hide(); return; }
            if (step == null || target == null ||
                viewCamera == null || panel == null || marker == null || GameManager.GameplayBlocked)
            { Hide(); return; }
            panel.gameObject.SetActive(true);
            var guide = target.GetComponent<InteractionGuidePoints>();
            Vector3 world = target.transform.position;
            Vector3 topWorld = world + Vector3.up;
            if (guide != null) { world = guide.Center; topWorld = guide.Top; }
            else
            {
                // Customers change portrait size by profile; follow their actual visible bounds.
                bool found = false;
                Bounds bounds = default;
                foreach (var renderer in target.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || renderer.GetComponent<TMP_Text>() != null) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (found) { world = bounds.center; topWorld = new Vector3(world.x, bounds.max.y + .1f, world.z); }
            }
            Vector3 viewport = viewCamera.WorldToViewportPoint(world);
            Vector2 direction = new Vector2(viewport.x - .5f, viewport.y - .5f);
            bool behind = viewport.z <= 0f;
            if (behind)
            {
                direction = -direction;
                if (direction.sqrMagnitude < .001f) direction = Vector2.down;
            }
            Vector2 half = panel.rect.size * .5f;
            Vector2 limit = new Vector2(Mathf.Max(1f, half.x - 155f), Mathf.Max(1f, half.y - 105f));
            Vector2 point = Vector2.Scale(direction, panel.rect.size);
            float edge = Mathf.Max(Mathf.Abs(point.x) / limit.x, Mathf.Abs(point.y) / limit.y);
            bool offscreen = behind || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * bounceSpeed)) * bounceHeight;
            if (offscreen)
            {
                point /= Mathf.Max(edge, .001f);
            }
            else
            {
                Vector3 top = viewCamera.WorldToViewportPoint(topWorld);
                point = Vector2.Scale(new Vector2(top.x - .5f, top.y - .5f), panel.rect.size);
                point.y += bounce;
                point.x = Mathf.Clamp(point.x, -half.x + arrowSize, half.x - arrowSize);
                point.y = Mathf.Clamp(point.y, -half.y + arrowSize, half.y - arrowSize);
            }
            marker.rectTransform.anchoredPosition = point;
            string arrow = offscreen ? (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? (direction.x > 0 ? "→ " : "← ") : (direction.y > 0 ? "↑ " : "↓ ")) : "";
            string title = target is Customer ? "손님" : (string.IsNullOrEmpty(step.targetLabel) ? step.title : step.targetLabel);
            marker.text = offscreen
                ? $"<size={arrowSize:0}>{arrow}</size>{title}\n{Vector3.Distance(viewCamera.transform.position, world):0}m"
                : $"<size={arrowSize:0}>▼</size>";
        }
        private void Hide() { if (panel != null) panel.gameObject.SetActive(false); }
        private void OnDisable() => Hide();
    }
}
