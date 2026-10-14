using UnityEngine;
using UnityEngine.InputSystem;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerWallet))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        private const float JumpMoveSpeedMultiplier = 0.75f;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Transform viewPoint;
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float sprintSpeed = 10f;
        [SerializeField, Min(0.05f)] private float walkingSoundInterval = 0.45f;
        [SerializeField, Min(0.1f)] private float sprintSoundIntervalMultiplier = 2.5f;
        [SerializeField, Min(0f), Tooltip("점프 시작 시 위쪽 속도(m/s). 0이면 점프하지 않습니다.")]
        private float jumpForce = 7f;
        [SerializeField, Min(0f)] private float lookSensitivity = 0.15f;
        [SerializeField] private float gravity = -20f;
        private InputActionMap controls;
        private CharacterController controller;
        private InputAction move;
        private InputAction sprint;
        private InputAction jump;
        private InputAction look;
        private float pitch;
        private float recoilTargetPitch;
        private float recoilPitchVelocity;
        private float recoilSmoothTime = 0.18f;
        private float verticalSpeed;
        private float walkingSoundElapsed;
        private bool jumpInProgress;
        public Transform ViewPoint => viewPoint;
        // Scope zoom lowers look speed; 1 = normal.
        public float LookSensitivityScale { get; set; } = 1f;
        public void SetViewRotation(Quaternion rotation)
        {
            if(viewPoint==null)return;
            pitch=Mathf.Clamp(Mathf.DeltaAngle(0f,rotation.eulerAngles.x),-89f,89f);
            recoilTargetPitch = pitch;
            recoilPitchVelocity = 0f;
            ApplyViewRotation();
            verticalSpeed=0f;
        }

        public void RestorePose(Vector3 position, Vector3 euler, Vector3 viewEuler)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewEuler.x), -89f, 89f);
            recoilTargetPitch = pitch;
            recoilPitchVelocity = 0f;
            ApplyViewRotation();
            verticalSpeed = 0f;
            jumpInProgress = false;
            walkingSoundElapsed = 0f;
            if (controller != null) controller.enabled = wasEnabled;
        }

        private void Awake()
        {
            PlayerWallet.ForPlayer(this);
            controller = GetComponent<CharacterController>();
            foreach (var weapon in Object.FindObjectsByType<EquipableWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (weapon.gameObject.scene == gameObject.scene) weapon.IgnorePlayerCollisions();
            if (inputActions == null || viewPoint == null) { enabled = false; return; }
            controls = inputActions.FindActionMap("Player", true).Clone();
            move = controls.FindAction("Move", true);
            sprint = controls.FindAction("Sprint", true);
            jump = controls.FindAction("Jump", true);
            look = controls.FindAction("Look", true);
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewPoint.localEulerAngles.x), -89f, 89f);
            recoilTargetPitch = pitch;
        }

        private void OnEnable() => controls?.Enable();
        private void OnDisable()
        {
            controls?.Disable();
            recoilTargetPitch = pitch;
            recoilPitchVelocity = 0f;
            verticalSpeed = 0f;
            jumpInProgress = false;
            walkingSoundElapsed = 0f;
        }
        private void OnDestroy() => controls?.Dispose();

        private void Update()
        {
            if (GameManager.GameplayBlocked || !Application.isFocused)
            {
                walkingSoundElapsed = 0f;
                return;
            }

            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            if (!enabled || controls == null)
            {
                walkingSoundElapsed = 0f;
                return;
            }
            Vector2 delta = look.ReadValue<Vector2>() * LookSensitivityScale * MouseSensitivityUI.Multiplier;
            transform.Rotate(Vector3.up, delta.x * lookSensitivity, Space.World);
            float pitchInput = -delta.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch + pitchInput, -89f, 89f);
            recoilTargetPitch = Mathf.Clamp(recoilTargetPitch + pitchInput, -89f, 89f);
            pitch = Mathf.SmoothDamp(pitch, recoilTargetPitch, ref recoilPitchVelocity,
                Mathf.Max(0.001f, recoilSmoothTime), Mathf.Infinity, deltaTime);
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            ApplyViewRotation();
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
                jumpInProgress = false;
            }
            bool canJump = NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid;
            if (canJump && controller.isGrounded && jump.WasPressedThisFrame() && jumpForce > 0f)
            {
                verticalSpeed = jumpForce;
                jumpInProgress = true;
                WorkshopSoundEffects.RaisePlayerJumped(transform.position);
            }

            float speed = sprint.IsPressed() ? sprintSpeed : moveSpeed;
            if (jumpInProgress) speed *= JumpMoveSpeedMultiplier;
            Vector3 motion = (transform.right * input.x + transform.forward * input.y) * speed;
            Vector3 previousPosition = transform.position;
            verticalSpeed += gravity * deltaTime;
            motion.y = verticalSpeed;
            CollisionFlags collisions = controller.Move(motion * deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
            if (controller.isGrounded && verticalSpeed <= 0f)
                jumpInProgress = false;

            float horizontalDistance = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude;
            float horizontalSpeed = deltaTime > 0f ? horizontalDistance / deltaTime : 0f;
            if (controller.isGrounded && horizontalSpeed > 0.05f)
            {
                walkingSoundElapsed += deltaTime;
                float baseInterval = Mathf.Max(0.05f, walkingSoundInterval);
                float referenceSpeed = Mathf.Max(0.01f, moveSpeed);
                float interval = Mathf.Clamp(baseInterval * referenceSpeed / horizontalSpeed,
                    Mathf.Min(0.12f, baseInterval), baseInterval * 2f);
                if (sprint.IsPressed())
                    interval *= Mathf.Max(0.1f, sprintSoundIntervalMultiplier);
                if (walkingSoundElapsed >= interval)
                {
                    walkingSoundElapsed = 0f;
                    WorkshopSoundEffects.RaiseFootstepPlayed(transform.position);
                }
            }
            else
            {
                walkingSoundElapsed = 0f;
            }
        }

        public void AddVerticalRecoil(float degrees, float maximumDegrees, float smoothTime)
        {
            if (viewPoint == null || degrees <= 0f) return;
            float impulse = Mathf.Min(degrees, Mathf.Max(0f, maximumDegrees));
            recoilTargetPitch = Mathf.Clamp(recoilTargetPitch - impulse, -89f, 89f);
            recoilSmoothTime = Mathf.Max(0.001f, smoothTime);
        }

        private void ApplyViewRotation()
        {
            if (viewPoint == null) return;
            viewPoint.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -89f, 89f), 0f, 0f);
        }
    }
}
