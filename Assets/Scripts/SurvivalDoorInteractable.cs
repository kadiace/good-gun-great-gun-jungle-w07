using System.Collections;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드에서 F 상호작용으로 90도 회전 개폐되고, 파손 시 즉시 잠기는 문 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionLabel))]
    public sealed class SurvivalDoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("힌지 및 회전 설정")]
        [SerializeField] private Transform _hingeTransform;
        [SerializeField] private DestructibleTarget _destructible;
        [SerializeField] private SurvivalDoorInteractable _pairedDoor;
        [SerializeField] private Vector3 _hingeAxis = Vector3.up;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _rotateSpeed = 6f;
        [SerializeField] private bool _isOpen;

        [Header("상호작용 문구 설정")]
        [SerializeField] private string _openTitle = "문 열기";
        [SerializeField] private string _closeTitle = "문 닫기";
        [SerializeField] private string _damagedTitle = "문 파손됨";
        [SerializeField] private string _damagedCondition = "문이 파손되어 더 이상 개폐할 수 없습니다.";

        private InteractionLabel _label;
        private Quaternion _closedLocalRotation;
        private Quaternion _openedLocalRotation;
        private Coroutine _rotationCoroutine;
        private bool _isInitialized;

        public Transform OutlineRoot => _hingeTransform != null ? _hingeTransform : transform;
        public bool IsOpen => _isOpen;

        void Awake()
        {
            InitializeDoor();
        }

        void Start()
        {
            SetOpenState(_isOpen, true);
        }

        /// <summary>
        /// 힌지 회전값과 기본 참조 컴포넌트를 초기화합니다.
        /// </summary>
        private void InitializeDoor()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _label = GetComponent<InteractionLabel>();
            if (_hingeTransform == null)
            {
                _hingeTransform = transform;
            }

            if (_destructible == null)
            {
                _destructible = GetComponentInChildren<DestructibleTarget>();
            }

            _closedLocalRotation = _hingeTransform.localRotation;
            _openedLocalRotation = _closedLocalRotation * Quaternion.AngleAxis(_openAngle, _hingeAxis);
            UpdateLabel();
        }

        /// <summary>
        /// 문짝 파괴 대상의 셀 손실 여부를 판별합니다.
        /// </summary>
        /// <returns>단 1셀이라도 파손되었거나 전체가 파괴되었으면 true를 반환합니다.</returns>
        public bool IsDamaged()
        {
            if (_destructible == null) return false;
            if (_destructible.IsDestroyed) return true;
            if (_destructible.InitialCellCount > 0 && _destructible.RemainingCellCount < _destructible.InitialCellCount)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 현재 플레이어가 문과 상호작용 가능한지 검사합니다.
        /// </summary>
        /// <param name="interactor">상호작용 주체 컴포넌트</param>
        /// <returns>온전한 상태에서만 true, 파손되었거나 비활성 상태면 false를 반환합니다.</returns>
        public bool CanInteract(Interaction interactor)
        {
            if (!isActiveAndEnabled) return false;
            if (IsDamaged())
            {
                UpdateLabel();
                return false;
            }
            return true;
        }

        /// <summary>
        /// F키 입력 시 문을 열거나 닫습니다.
        /// </summary>
        /// <param name="interactor">상호작용 주체 컴포넌트</param>
        /// <returns>개폐 처리가 정상 수행되었으면 true를 반환합니다.</returns>
        public bool Interact(Interaction interactor)
        {
            if (!CanInteract(interactor)) return false;

            ToggleDoor(false);
            if (_pairedDoor != null && !_pairedDoor.IsDamaged() && _pairedDoor._isOpen != _isOpen)
            {
                _pairedDoor.SetOpenState(_isOpen, false);
            }

            return true;
        }

        /// <summary>
        /// 문의 개폐 상태를 반전하고 회전을 시작합니다.
        /// </summary>
        /// <param name="immediate">즉시 회전 완료 여부</param>
        public void ToggleDoor(bool immediate = false)
        {
            SetOpenState(!_isOpen, immediate);
        }

        /// <summary>
        /// 문의 개폐 상태를 명시적으로 설정합니다.
        /// </summary>
        /// <param name="open">열림 여부</param>
        /// <param name="immediate">즉시 회전 완료 여부</param>
        public void SetOpenState(bool open, bool immediate = false)
        {
            InitializeDoor();
            _isOpen = open;
            Quaternion targetRotation = _isOpen ? _openedLocalRotation : _closedLocalRotation;

            if (_rotationCoroutine != null)
            {
                StopCoroutine(_rotationCoroutine);
                _rotationCoroutine = null;
            }

            if (immediate)
            {
                _hingeTransform.localRotation = targetRotation;
            }
            else
            {
                _rotationCoroutine = StartCoroutine(AnimateDoorRotation(targetRotation));
            }

            UpdateLabel();
        }

        /// <summary>
        /// 문짝의 상호작용 안내 문구를 현재 상태(열기, 닫기, 파손)에 맞게 갱신합니다.
        /// </summary>
        private void UpdateLabel()
        {
            if (_label == null) _label = GetComponent<InteractionLabel>();
            if (_label == null) return;

            if (IsDamaged())
            {
                _label.SetTitle(_damagedTitle);
            }
            else
            {
                _label.SetTitle(_isOpen ? _closeTitle : _openTitle);
            }
        }

        /// <summary>
        /// 힌지 회전을 부드럽게 보간하는 코루틴입니다.
        /// </summary>
        /// <param name="targetRotation">목표 로컬 회전</param>
        private IEnumerator AnimateDoorRotation(Quaternion targetRotation)
        {
            while (Quaternion.Angle(_hingeTransform.localRotation, targetRotation) > 0.1f)
            {
                _hingeTransform.localRotation = Quaternion.Slerp(
                    _hingeTransform.localRotation,
                    targetRotation,
                    Time.deltaTime * _rotateSpeed);
                yield return null;
            }

            _hingeTransform.localRotation = targetRotation;
            _rotationCoroutine = null;
        }
    }
}
