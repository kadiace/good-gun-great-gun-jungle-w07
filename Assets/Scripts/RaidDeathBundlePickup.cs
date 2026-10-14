using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(InteractionLabel))]
    public sealed class RaidDeathBundlePickup : MonoBehaviour, IInteractable
    {
        private RaidDeathDropSave drop;
        private PlayerRaidDeathLoot owner;
        public Transform OutlineRoot => transform;
        public void Initialize(RaidDeathDropSave data, PlayerRaidDeathLoot recovery)
        {
            drop = data;
            owner = recovery;
            GetComponent<InteractionLabel>().SetTitle("유실한 총 · 획득 모듈 회수");
        }
        public bool CanInteract(Interaction interactor) => drop != null && owner != null && interactor != null &&
            interactor.Player == owner.GetComponent<FirstPersonPlayer>() && owner.CanRecover(drop);
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor) || !owner.Recover(drop)) return false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
