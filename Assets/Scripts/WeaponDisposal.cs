using System;
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
        private Action tutorialFailedCallback;
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
            TriggerTutorialFailed();
        }

        private void TriggerTutorialFailed()
        {
            Action callback = tutorialFailedCallback;
            tutorialFailedCallback = null;
            callback?.Invoke();
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

        /// <summary>
        /// 튜토리얼 무기를 지정된 경유 지점을 거쳐 쓰레기통으로 이동시킨 뒤 폐기 및 정산합니다.
        /// </summary>
        /// <param name="weapon">폐기할 무기입니다.</param>
        /// <param name="viaPoint">쓰레기통 돌진 전 거쳐 갈 경유 지점 Transform입니다.</param>
        /// <param name="wallet">폐기 정산을 지급받을 플레이어 지갑입니다.</param>
        /// <param name="onCompleted">실제 폐기 애니메이션과 정산이 모두 완료된 뒤 호출될 콜백입니다.</param>
        /// <param name="onFailed">무기 소실, 폐기 실패 또는 도중 중단 시 호출될 실패 콜백입니다.</param>
        /// <returns>폐기 시퀀스를 성공적으로 시작했으면 true, 이미 진행 중이거나 인자가 유효하지 않으면 false입니다.</returns>
        public bool DisposeTutorialWeapon(EquipableWeapon weapon, Transform viaPoint, PlayerWallet wallet, Action onCompleted, Action onFailed = null)
        {
            if (!isActiveAndEnabled || busy || weapon == null || viaPoint == null || wallet == null) return false;
            if (weapon.IsMoving) return false;
            Transform destination = disposalPoint != null ? disposalPoint : transform;
            if (destination == null || destination.IsChildOf(weapon.transform) || viaPoint.IsChildOf(weapon.transform)) return false;
            if (!weapon.MoveToPoint(viaPoint)) return false;

            busy = true;
            tutorialFailedCallback = onFailed;
            lidMovement = StartCoroutine(DisposeTutorialSequence(weapon, destination, wallet, onCompleted));
            return true;
        }

        private IEnumerator DisposeTutorialSequence(EquipableWeapon weapon, Transform destination, PlayerWallet wallet, Action onCompleted)
        {
            while (weapon != null && weapon.IsMoving) yield return null;
            if (weapon == null || !weapon.DisposeTo(destination, wallet))
            {
                busy = false;
                lidMovement = null;
                TriggerTutorialFailed();
                yield break;
            }

            WorkshopSoundEffects.RaiseWeaponDisposed(destination.position);
            yield return DisposeAnimation(weapon);
            tutorialFailedCallback = null;
            onCompleted?.Invoke();
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
