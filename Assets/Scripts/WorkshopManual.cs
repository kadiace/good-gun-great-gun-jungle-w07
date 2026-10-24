using UnityEngine;

namespace WeaponWorkshop
{
    // 월드 매뉴얼 문서의 호환 브리지 겸 View. 기존 직렬화 참조(deskDocument, documentHeight)와
    // 상호작용 API를 유지하고, 카메라 정렬·줌·팬 계산은 ManualDocumentPresenter에 위임한다.
    [DefaultExecutionOrder(60)]
    public sealed class WorkshopManual : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameObject deskDocument;
        [SerializeField, Min(.1f)] private float documentHeight = .57f;
        private PlayerManualReader reader;
        private Vector3 restingPosition;
        private Quaternion restingRotation;
        private Vector3 restingScale;
        private Collider[] documentColliders;
        private ManualDocumentPresenter m_document;
        private bool initialized;
        public Transform OutlineRoot => deskDocument != null ? deskDocument.transform : transform;

        public Transform DocumentTransform => deskDocument != null ? deskDocument.transform : transform;

        private void Awake()
        {
            if (deskDocument == null) return;
            restingPosition = deskDocument.transform.localPosition;
            restingRotation = deskDocument.transform.localRotation;
            restingScale = deskDocument.transform.localScale;
            documentColliders = deskDocument.GetComponentsInChildren<Collider>();
            m_document = new ManualDocumentPresenter(this, documentHeight, restingScale);
            if (!m_document.Initialize())
            {
                m_document = null;
                return;
            }

            initialized = true;
            PutDown();
        }

        private void OnDisable()
        {
            if (reader != null) reader.Close();
            // SetActive is not allowed while this object is being deactivated; restore the pose only.
            PutDown(false);
        }

        private void OnDestroy()
        {
            m_document?.Dispose();
            m_document = null;
        }

        public bool CanInteract(Interaction interactor) => isActiveAndEnabled && reader == null &&
            !GameManager.GameplayBlocked && interactor != null && interactor.Player != null &&
            interactor.Player.GetComponent<PlayerManualReader>() != null;

        public bool Interact(Interaction interactor)
            => CanInteract(interactor) && interactor.Player.GetComponent<PlayerManualReader>().Open(this);

        public void Show(PlayerManualReader owner)
        {
            reader = owner;
            m_document?.Reset();
            foreach (var collider in documentColliders) collider.enabled = false;
            SoundManager.PlayPaperSound(deskDocument.transform.position);
        }

        private void LateUpdate()
        {
            if (reader == null || reader.ReadingCamera == null) return;
            m_document?.Tick(reader.ReadingCamera, Time.unscaledDeltaTime);
        }

        public void AdjustReading(float scroll, Vector2 dragPixels)
        {
            if (reader == null || reader.ReadingCamera == null) return;
            m_document?.Adjust(reader.ReadingCamera, scroll, dragPixels);
        }

        /// <summary>
        /// Presenter가 계산한 읽기 자세를 종이 Transform에 적용합니다.
        /// </summary>
        public void ApplyReadingPose(Vector3 _position, Quaternion _rotation, Vector3 _scale)
        {
            Transform document = DocumentTransform;
            document.localScale = _scale;
            document.SetPositionAndRotation(_position, _rotation);
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
            m_document?.Reset();
            foreach (var collider in documentColliders) collider.enabled = true;
            if (wasBeingRead) SoundManager.PlayPaperSound(deskDocument.transform.position);
        }
    }
}
