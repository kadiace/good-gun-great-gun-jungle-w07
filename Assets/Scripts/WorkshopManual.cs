using UnityEngine;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(60)]
    public sealed class WorkshopManual : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject deskDocument;
        [SerializeField, Min(.1f)] private float documentHeight = .57f;
        private PlayerManualReader reader;
        private Vector3 restingPosition;
        private Quaternion restingRotation;
        private Vector3 restingScale;
        private float zoom = 1f;
        private Vector2 pan;
        private Collider[] documentColliders;
        private bool initialized;
        public Transform OutlineRoot => deskDocument != null ? deskDocument.transform : transform;

        private void Awake()
        {
            if (deskDocument == null) return;
            restingPosition = deskDocument.transform.localPosition;
            restingRotation = deskDocument.transform.localRotation;
            restingScale = deskDocument.transform.localScale;
            documentColliders = deskDocument.GetComponentsInChildren<Collider>();
            initialized = true;
            PutDown();
        }

        private void OnDisable()
        {
            if (reader != null) reader.Close();
            // SetActive is not allowed while this object is being deactivated; restore the pose only.
            PutDown(false);
        }

        public bool CanInteract(Interaction interactor) => isActiveAndEnabled && reader == null &&
            !GameManager.GameplayBlocked && interactor != null && interactor.Player != null &&
            interactor.Player.GetComponent<PlayerManualReader>() != null;

        public bool Interact(Interaction interactor)
            => CanInteract(interactor) && interactor.Player.GetComponent<PlayerManualReader>().Open(this);

        public void Show(PlayerManualReader owner)
        {
            reader = owner;
            zoom = 1f;
            pan = Vector2.zero;
            foreach (var collider in documentColliders) collider.enabled = false;
            SoundManager.PlayPaperSound(deskDocument.transform.position);
        }

        private void LateUpdate()
        {
            if (reader == null || reader.ReadingCamera == null) return;
            var camera = reader.ReadingCamera;
            // Fit the physical sheet within both camera dimensions.
            float distance = documentHeight / (1.5f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            distance *= Mathf.Max(1f, .8f / camera.aspect);
            distance = Mathf.Max(distance, camera.nearClipPlane + .12f);
            var targetPosition = camera.transform.TransformPoint(new Vector3(pan.x, pan.y, distance));
            var targetRotation = camera.transform.rotation * Quaternion.Euler(-90, 0, 0);
            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            deskDocument.transform.localScale = Vector3.Lerp(deskDocument.transform.localScale, restingScale * zoom, blend);
            deskDocument.transform.SetPositionAndRotation(
                Vector3.Lerp(deskDocument.transform.position, targetPosition, blend),
                Quaternion.Slerp(deskDocument.transform.rotation, targetRotation, blend));
        }

        public void AdjustReading(float scroll, Vector2 dragPixels)
        {
            if (reader == null || reader.ReadingCamera == null) return;
            // Input System scroll values may be wheel ticks or high-resolution deltas.
            if (Mathf.Abs(scroll) > .01f)
                zoom = Mathf.Clamp(zoom * Mathf.Exp(Mathf.Sign(scroll) * .15f), .75f, 3f);
            var camera = reader.ReadingCamera;
            float distance = documentHeight / (1.5f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            distance *= Mathf.Max(1f, .8f / camera.aspect);
            distance = Mathf.Max(distance, camera.nearClipPlane + .12f);
            float unitsPerPixel = 2f * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, camera.pixelHeight);
            pan += dragPixels * unitsPerPixel;
            float limit = documentHeight * zoom * .45f;
            pan.x = Mathf.Clamp(pan.x, -limit, limit);
            pan.y = Mathf.Clamp(pan.y, -limit, limit);
        }

        public void PutDown() => PutDown(true);

        private void PutDown(bool activateDocument)
        {
            bool wasBeingRead = reader != null;
            reader = null;
            if (!initialized) return;
            if (activateDocument) deskDocument.SetActive(true);
            deskDocument.transform.localPosition = restingPosition;
            deskDocument.transform.localRotation = restingRotation;
            deskDocument.transform.localScale = restingScale;
            zoom = 1f;
            pan = Vector2.zero;
            foreach (var collider in documentColliders) collider.enabled = true;
            if (wasBeingRead) SoundManager.PlayPaperSound(deskDocument.transform.position);
        }
    }
}

