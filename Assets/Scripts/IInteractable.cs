using UnityEngine;

namespace WeaponWorkshop
{
    public interface IInteractable
    {
        Transform OutlineRoot { get; }
        bool CanInteract(Interaction interactor);
        bool Interact(Interaction interactor);
    }
}
