using UnityEngine;
namespace WeaponWorkshop
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(InteractionLabel))]
    public sealed class ModuleUnlockPickup : MonoBehaviour, IInteractable
    {
        public static event System.Action<FirstPersonPlayer, int> RewardCollected;
        [SerializeField, Min(0f)] private float pickupDelay = 0.35f;
        private Rigidbody body;
        private int moduleId;
        private bool collected;
        private float availableAt;
        public Transform OutlineRoot => transform;
        private void Awake()
        {
            gameObject.AddComponent<PickupOutline>();
            collected = false; availableAt = Time.time + pickupDelay;
            body = GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            GetComponent<BoxCollider>().isTrigger = false;
        }
        public void Release(int id, Vector3 velocity, GameObject modulePrefab, string moduleName)
        {
            moduleId = id;
            GetComponent<InteractionLabel>().SetTitle(moduleName);
            var draw = modulePrefab.GetComponent<ModuleDraw>();
            CopyVisual(modulePrefab.transform, transform, draw != null ? draw.Arrow : null);
            body.linearVelocity = velocity;
        }

        private static void CopyVisual(Transform source, Transform parent, Transform arrow)
        {
            if (source == arrow || !source.gameObject.activeSelf) return;
            var visual = new GameObject(source.name).transform;
            visual.SetParent(parent, false);
            visual.localPosition = source.localPosition;
            visual.localRotation = source.localRotation;
            visual.localScale = source.localScale;
            visual.gameObject.layer = parent.gameObject.layer;
            var sourceFilter = source.GetComponent<MeshFilter>();
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter != null && sourceRenderer != null)
            {
                visual.gameObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                var renderer = visual.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.enabled = sourceRenderer.enabled;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = sourceRenderer.receiveShadows;
            }
            foreach (Transform child in source) CopyVisual(child, visual, arrow);
        }

        public bool CanInteract(Interaction interactor) => !collected && moduleId > 0 &&
            Time.time >= availableAt && !GameManager.GameplayBlocked && interactor != null &&
            interactor.Player != null && interactor.Player.GetComponent<PlayerModuleUnlocks>() != null;

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            var player = interactor.Player;
            var unlocks = player.GetComponent<PlayerModuleUnlocks>();
            collected = true;
            if (NightRaidController.Instance == null || !NightRaidController.Instance.TryCollectTrainingModule(moduleId))
                unlocks.UnlockModule(moduleId);
            RewardCollected?.Invoke(player, moduleId);
            if (TutorialManager.Instance != null) TutorialManager.Instance.NotifyTrainingLoot(moduleId);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
