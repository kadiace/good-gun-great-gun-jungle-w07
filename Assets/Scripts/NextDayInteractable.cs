using System.Collections;
using UnityEngine;

namespace WeaponWorkshop
{
    // 월드 셔터 및 영업 시작/다음 날 진행 상호작용 브리지.
    // 셔터·상태 패널 물리 연출과 도메인 진행은 이 컴포넌트가 유지하고,
    // 라벨 문자열 갱신과 스킵 확인 다이얼로그 요청은 NextDayInteractablePresenter가 전담한다.
    public sealed class NextDayInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private _DayManager dayManager;
        [SerializeField] private Transform drawRoot;

        [Header("Shutter")]
        [SerializeField, Tooltip("Draw 기준 닫힘 Y=0, 열림 Y=Open Height")] private Transform shutter;
        [SerializeField] private TMPro.TMP_Text statusLabel;
        [SerializeField, Min(0f)] private float openHeight = 2.35f;
        [SerializeField, Min(.01f)] private float moveDuration = .8f;

        [Header("Status Panel")]
        [SerializeField] private Transform statusPanel;
        [SerializeField, Min(0f)] private float panelPushDistance = .16f;
        [SerializeField, Min(.01f)] private float panelMoveDuration = .12f;
        [SerializeField, Min(.01f)] private float panelFlipDuration = .35f;

        private readonly NextDayInteractablePresenter m_presenter = new NextDayInteractablePresenter();

        private Vector3 panelPosition;
        private Renderer[] shutterRenderers;
        private Quaternion panelRotation;
        private Coroutine opening;
        private Vector3 closedPosition;
        private float startY;
        private float targetY;
        private float elapsed;
        private bool closed;

        public Vector3 TutorialMarkerPosition
        {
            get
            {
                bool found = false;
                Bounds bounds = default;
                if (shutterRenderers != null)
                {
                    foreach (var renderer in shutterRenderers)
                    {
                        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        if (!found)
                        {
                            bounds = renderer.bounds;
                            found = true;
                        }
                        else
                        {
                            bounds.Encapsulate(renderer.bounds);
                        }
                    }
                }

                return found
                    ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)
                    : (shutter != null ? shutter.position : transform.position);
            }
        }

        public Transform OutlineRoot => drawRoot != null ? drawRoot : transform;

        private void Awake()
        {
            shutterRenderers = shutter != null ? shutter.GetComponentsInChildren<Renderer>(true) : null;
            if (dayManager == null)
            {
                dayManager = FindFirstObjectByType<_DayManager>();
            }

            if (shutter != null)
            {
                closedPosition = shutter.localPosition;
                closedPosition.y = 0f;
            }

            if (statusPanel != null)
            {
                panelPosition = statusPanel.localPosition;
                panelRotation = statusPanel.localRotation;
            }
        }

        private void OnEnable()
        {
            if (dayManager != null)
            {
                dayManager.Changed += RefreshShutter;
            }

            InitializeShutter();
        }

        private void Start()
        {
            InitializeShutter();
        }

        private void OnDisable()
        {
            if (dayManager != null)
            {
                dayManager.Changed -= RefreshShutter;
            }

            m_presenter.CancelChoice(this);
            StopOpening();
        }

        private void InitializeShutter()
        {
            closed = dayManager != null && (dayManager.IsNight || dayManager.IsAwaitingOpening);
            targetY = closed ? 0f : openHeight;
            startY = targetY;
            elapsed = moveDuration;
            SetShutterY(targetY);
            UpdateLabel();
            SetPanelPose(closed ? 0f : 180f, panelPosition);
        }

        private void RefreshShutter()
        {
            bool nextClosed = dayManager != null && (dayManager.IsNight || dayManager.IsAwaitingOpening);
            if (closed == nextClosed)
            {
                UpdateLabel();
                return;
            }

            closed = nextClosed;
            if (shutter != null)
            {
                WorkshopSoundEffects.RaiseShopShutterMoved(shutter.position);
            }

            StopOpening();
            startY = shutter != null ? shutter.localPosition.y : 0f;
            targetY = closed ? 0f : openHeight;
            elapsed = 0f;

            if (!closed && statusPanel != null)
            {
                opening = StartCoroutine(OpenSequence());
            }
            else
            {
                SetPanelPose(0f, panelPosition);
            }

            UpdateLabel();
        }

        private void UpdateLabel()
        {
            bool isAwaitingOpening = dayManager != null && dayManager.IsAwaitingOpening;
            var label = GetComponent<InteractionLabel>();
            m_presenter.UpdateLabels(statusLabel, label, closed, statusPanel != null, isAwaitingOpening);
        }

        private void StopOpening()
        {
            if (opening != null)
            {
                StopCoroutine(opening);
                opening = null;
            }
        }

        private void SetPanelPose(float angle, Vector3 position)
        {
            if (statusPanel == null)
            {
                return;
            }

            statusPanel.localPosition = position;
            statusPanel.localRotation = panelRotation * Quaternion.Euler(0f, angle, 0f);
        }

        private IEnumerator OpenSequence()
        {
            Vector3 forward = panelPosition + Vector3.forward * panelPushDistance;
            yield return AnimatePanel(panelPosition, forward, 0f, 0f, panelMoveDuration);
            yield return AnimatePanel(forward, forward, 0f, 180f, panelFlipDuration);
            yield return AnimatePanel(forward, panelPosition, 180f, 180f, panelMoveDuration);
            startY = shutter != null ? shutter.localPosition.y : 0f;
            elapsed = 0f;
            opening = null;
        }

        private IEnumerator AnimatePanel(Vector3 from, Vector3 to, float fromAngle, float toAngle, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / Mathf.Max(.01f, duration)));
                SetPanelPose(Mathf.Lerp(fromAngle, toAngle, t), Vector3.Lerp(from, to, t));
                yield return null;
            }

            SetPanelPose(toAngle, to);
        }

        private void SetShutterY(float y)
        {
            if (shutter == null)
            {
                return;
            }

            var position = closedPosition;
            position.y = y;
            shutter.localPosition = position;
        }

        private void Update()
        {
            if (shutter == null || opening != null || elapsed >= moveDuration)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            SetShutterY(Mathf.Lerp(startY, targetY, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(.01f, moveDuration)))));
        }

        public bool CanInteract(Interaction interactor)
        {
            return isActiveAndEnabled &&
                !GameManager.GameplayBlocked &&
                dayManager != null &&
                !dayManager.IsSettlementOpen &&
                (dayManager.IsAwaitingOpening || (dayManager.IsNight && dayManager.CanStartNextDay)) &&
                interactor != null &&
                interactor.Player != null;
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor))
            {
                return false;
            }

            if (dayManager.IsAwaitingOpening)
            {
                if (!dayManager.OpenShop())
                {
                    return false;
                }

                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.NotifySignal("shop-opened", this);
                }

                return true;
            }

            var ui = GameManager.Instance != null ? GameManager.Instance.UI : null;
            var transition = ui != null ? ui.Screen<DayTransitionUI>() : null;
            if (transition == null)
            {
                return false;
            }

            return m_presenter.RequestSkipNightRaid(
                this,
                transition,
                () =>
                {
                    if (isActiveAndEnabled &&
                        dayManager != null &&
                        dayManager.IsNight &&
                        !dayManager.IsSettlementOpen &&
                        dayManager.CanStartNextDay)
                    {
                        transition.BeginSkippedNightRaid(dayManager);
                    }
                });
        }
    }
}
