using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent, RequireComponent(typeof(Collider), typeof(InteractionLabel))]
    public sealed class JungleGiftReward : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameSaveController _saveController;
        [SerializeField] private Transform _gunSpawnPoint;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private bool[] _rendererEnabled;
        private bool[] _colliderEnabled;
        private DeveloperGiftStagedWeapon _stagedWeapon;

        public Transform OutlineRoot => transform;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _rendererEnabled = new bool[_renderers.Length];
            _colliderEnabled = new bool[_colliders.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererEnabled[i] = _renderers[i].enabled;
            for (int i = 0; i < _colliders.Length; i++) _colliderEnabled[i] = _colliders[i].enabled;
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (_saveController != null) SyncFromSaveState(_saveController);
        }

        /// <summary>
        /// Updates gift visibility and creates or removes the hidden reward weapon for the restored save state.
        /// controller owns the persisted discovery and claimed state.
        /// </summary>
        public void SyncFromSaveState(GameSaveController controller)
        {
            _saveController = controller;
            bool pending = controller != null && controller.HasPendingDeveloperGift;
            SetVisible(pending);
            if (pending)
            {
                if (_stagedWeapon == null || !_stagedWeapon.gameObject.activeInHierarchy)
                {
                    _stagedWeapon = null;
                    TryCreateStagedWeapon(out _);
                }
            }
            else if (_stagedWeapon != null)
            {
                if (_stagedWeapon.IsStaged) Destroy(_stagedWeapon.gameObject);
                _stagedWeapon = null;
            }
        }

        /// <summary>
        /// Reports whether the gift can be interacted with while an unclaimed discovery is pending.
        /// interactor is the player's shared Interaction component.
        /// </summary>
        public bool CanInteract(Interaction interactor)
            => isActiveAndEnabled && _saveController != null && _saveController.HasPendingDeveloperGift;

        /// <summary>
        /// Equips the prepared gift weapon, marks it claimed, and plays the gift clip only after both steps succeed.
        /// interactor supplies the player's hand and current held-weapon state.
        /// </summary>
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor) || interactor.Player == null) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            if (holder == null) return false;
            if (holder.EquippedWeapon != null)
            {
                FeedbackUI.For(this)?.ShowNotification("총을 내려놓고 열어주세요!");
                return true;
            }
            if (_stagedWeapon == null && !TryCreateStagedWeapon(out string createError))
            {
                FeedbackUI.For(this)?.ShowNotification(createError);
                return true;
            }
            EquipableWeapon rewardWeapon = _stagedWeapon.GetComponent<EquipableWeapon>();
            if (rewardWeapon == null || !_stagedWeapon.PrepareForEquip()) return false;
            if (!holder.Equip(rewardWeapon))
            {
                _stagedWeapon.BeginStaging();
                return false;
            }
            if (!_saveController.TryMarkDeveloperGiftClaimed())
            {
                holder.TakeWeapon();
                _stagedWeapon.BeginStaging();
                return false;
            }
            SoundManager.PlayJungleGiftOpen(transform.position);
            _stagedWeapon = null;
            SetVisible(false);
            return true;
        }

        /// <summary>
        /// Builds one hidden reward weapon with its table-owned paint and stickers at the assigned gun marker.
        /// error explains missing references or a code, layout, or appearance failure.
        /// </summary>
        private bool TryCreateStagedWeapon(out string error)
        {
            error = null;
            if (_saveController == null || _saveController.CorePrefab == null || _gunSpawnPoint == null)
            { error = "보상 총 생성 위치를 확인할 수 없습니다."; return false; }
            EquipableWeapon rewardWeapon = Instantiate(_saveController.CorePrefab, _gunSpawnPoint.position, _gunSpawnPoint.rotation);
            if (!DeveloperGiftInfo.TryBuildWeapon(rewardWeapon, _saveController, out error))
            {
                Destroy(rewardWeapon.gameObject);
                return false;
            }
            _stagedWeapon = rewardWeapon.gameObject.AddComponent<DeveloperGiftStagedWeapon>();
            _stagedWeapon.BeginStaging();
            return true;
        }

        /// <summary>
        /// Applies the requested gift state to its cached renderers and interaction colliders.
        /// visible is true only while the discovered gift remains unclaimed.
        /// </summary>
        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = visible && _rendererEnabled[i];
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = visible && _colliderEnabled[i];
        }
    }
}
