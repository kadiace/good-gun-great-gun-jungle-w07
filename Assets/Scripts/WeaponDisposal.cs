using System.Collections;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class WeaponDisposal : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("총이 날아가서 사라질 위치. 비어 있으면 오브젝트 중심 사용.")] private Transform disposalPoint;
        [SerializeField] private Transform lidPivot;
        [SerializeField] private Vector3 openLidEuler = new Vector3(105f, 0f, 0f);
        [SerializeField, Min(0.01f)] private float lidMoveDuration = 0.15f;
        private Quaternion closedLidRotation;
        private Coroutine lidMovement;
        private bool busy;
        public Transform OutlineRoot => transform;

        private void Awake()
        {
            if (lidPivot != null) closedLidRotation = lidPivot.localRotation;
        }

        private void OnDisable()
        {
            if (lidMovement != null) StopCoroutine(lidMovement);
            lidMovement = null;
            busy = false;
            if (lidPivot != null) lidPivot.localRotation = closedLidRotation;
        }

        public bool CanInteract(Interaction interactor)
        {
            if (!isActiveAndEnabled || busy || GameManager.GameplayBlocked ||
                interactor == null || interactor.Player == null) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            return holder != null && holder.isActiveAndEnabled &&
                holder.EquippedWeapon != null && !holder.EquippedWeapon.IsMoving;
        }

        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;
            WeaponHolder holder = interactor.Player.GetComponent<WeaponHolder>();
            EquipableWeapon weapon = holder.EquippedWeapon;
            PlayerWallet wallet = PlayerWallet.ForPlayer(interactor.Player);
            if (wallet == null || !weapon.DisposeTo(disposalPoint != null ? disposalPoint : transform, wallet)) return false;
            holder.TakeWeapon();
            WorkshopSoundEffects.RaiseWeaponDisposed(disposalPoint != null ? disposalPoint.position : transform.position);
            busy = true;
            lidMovement = StartCoroutine(DisposeAnimation(weapon));
            return true;
        }

        private IEnumerator DisposeAnimation(EquipableWeapon weapon)
        {
            yield return MoveLid(closedLidRotation * Quaternion.Euler(openLidEuler));
            while (weapon != null && weapon.IsMoving) yield return null;
            yield return MoveLid(closedLidRotation);
            busy = false;
            lidMovement = null;
        }

        private IEnumerator MoveLid(Quaternion target)
        {
            if (lidPivot == null) yield break;
            Quaternion start = lidPivot.localRotation;
            float elapsed = 0f;
            while (elapsed < lidMoveDuration)
            {
                elapsed += Time.deltaTime;
                lidPivot.localRotation = Quaternion.Slerp(start, target,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / lidMoveDuration)));
                yield return null;
            }
            lidPivot.localRotation = target;
        }
    }
}
