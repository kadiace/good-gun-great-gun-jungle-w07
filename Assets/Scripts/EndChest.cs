using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class EndChest : MonoBehaviour
    {
        private enum State { Closed, Absorbing, WhiteOut, Opening, Open, Done }

        private static readonly List<EndChest> _instances = new List<EndChest>();

        [Header("상자 구성")]
        [FormerlySerializedAs("zone"), SerializeField] private Transform _zone;
        [FormerlySerializedAs("core"), SerializeField] private Transform _core;
        [FormerlySerializedAs("orbs"), SerializeField] private Transform[] _orbs;
        [FormerlySerializedAs("lid"), SerializeField] private Transform _lid;
        [FormerlySerializedAs("innerGlow"), SerializeField] private GameObject _innerGlow;
        [FormerlySerializedAs("innerLight"), SerializeField] private Light _innerLight;
        [SerializeField] private Renderer[] _discoveredRenderers;
        [SerializeField] private DestructibleTarget[] _discoveredTargets;
        [SerializeField] private Material _discoveredMaterial;

        [Header("발견 연출")]
        [FormerlySerializedAs("orbitSpeed"), SerializeField, Min(0f)] private float _orbitSpeed = 25f;
        [FormerlySerializedAs("absorbDuration"), SerializeField, Min(0.1f)] private float _absorbDuration = 7f;
        [FormerlySerializedAs("whiteInDuration"), SerializeField, Min(0.1f)] private float _whiteInDuration = 1f;
        [FormerlySerializedAs("whiteOutDuration"), SerializeField, Min(0.1f)] private float _whiteOutDuration = 1.5f;
        [FormerlySerializedAs("openDuration"), SerializeField, Min(0.1f)] private float _openDuration = 2.5f;
        [FormerlySerializedAs("endFadeDuration"), SerializeField, Min(0.1f)] private float _endFadeDuration = 1.5f;
        [FormerlySerializedAs("endCamera"), SerializeField] private Camera _endCamera;
        [FormerlySerializedAs("endingUI"), SerializeField] private EndChestEndingUI _endingUI;
        [SerializeField] private JungleEndChestNotice _notice;
        [FormerlySerializedAs("lidOpenAngle"), SerializeField, Range(0f, 170f)] private float _lidOpenAngle = 105f;
        [FormerlySerializedAs("openLightIntensity"), SerializeField, Min(0f)] private float _openLightIntensity = 8f;

        private State _state;
        private float _stateTime;
        private float _orbitTime;
        private Vector3[] _orbStart;
        private Vector3[] _orbAxis;
        private Vector3[] _orbScale;
        private float[] _orbSpeed;
        private Vector3 _coreScale;
        private Quaternion _lidClosed;

        public static IReadOnlyList<EndChest> Instances => _instances;
        public bool IsClosed => _state == State.Closed;
        public bool IsOpen => _state >= State.Open;
        public EndChestCore CoreGate => _core != null ? _core.GetComponent<EndChestCore>() : null;

        /// <summary>Reports whether an active discovery presentation should pause helicopter rotors.</summary>
        public static bool IsDiscoveryPresentationActive
        {
            get
            {
                foreach (EndChest chest in _instances)
                    if (chest != null && chest._state >= State.WhiteOut && chest._state <= State.Open) return true;
                return false;
            }
        }

        /// <summary>Clears the scene chest registry before a new play session starts.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => _instances.Clear();

        private void Awake()
        {
            int count = _orbs != null ? _orbs.Length : 0;
            _orbStart = new Vector3[count];
            _orbAxis = new Vector3[count];
            _orbScale = new Vector3[count];
            _orbSpeed = new float[count];
            for (int i = 0; i < count; i++)
            {
                _orbStart[i] = _orbs[i].localPosition;
                _orbScale[i] = _orbs[i].localScale;
                _orbAxis[i] = new Vector3(Mathf.Sin(i * 2.4f), 1f, Mathf.Cos(i * 1.7f)).normalized;
                _orbSpeed[i] = _orbitSpeed * (0.6f + 0.8f * Mathf.Repeat(i * 0.37f, 1f));
                _orbs[i].gameObject.SetActive(true);
            }
            if (_core != null) { _coreScale = _core.localScale; _core.gameObject.SetActive(true); }
            if (_lid != null) _lidClosed = _lid.localRotation;
            int shieldLayer = LayerMask.NameToLayer("EndChestShield");
            if (shieldLayer >= 0)
                for (int layer = 0; layer < 32; layer++) Physics.IgnoreLayerCollision(shieldLayer, layer, true);
            _state = State.Closed;
            _stateTime = _orbitTime = 0f;
            if (_innerGlow != null) _innerGlow.SetActive(false);
            if (_innerLight != null) { _innerLight.intensity = 0f; _innerLight.enabled = false; }
            if (_endCamera != null) _endCamera.enabled = false;
            if (_notice == null) _notice = GetComponent<JungleEndChestNotice>();
        }

        private void OnEnable() => _instances.Add(this);
        private void OnDisable()
        {
            _instances.Remove(this);
            if (_innerGlow != null) _innerGlow.SetActive(false);
        }

        private void Start()
        {
            _notice?.ResetForRaid();
            bool discovered = NightRaidTransfer.Workshop != null && NightRaidTransfer.Workshop.jungleEasterEggDiscovered;
            SetDamageProtection(!discovered);
            if (discovered) RestoreDiscoveredState();
        }

        /// <summary>Starts the core absorption and dismisses any blocked-shot notice.</summary>
        public void BeginAbsorb()
        {
            if (_state != State.Closed) return;
            _notice?.Dismiss();
            _state = State.Absorbing;
            _stateTime = 0f;
        }

        /// <summary>Displays the Jungle-only notice after a blocked projectile hits the shield or core.</summary>
        public void NotifyBlockedHit()
        {
            if (CanShowNotice()) _notice?.ShowBlockedHit();
        }

        /// <summary>Starts the discovery animation after both projectile stats qualify.</summary>
        public void NotifyDiscoveryHit() => BeginAbsorb();

        /// <summary>Saves the confirmed Jungle discovery and prepares the successful workshop return.</summary>
        /// <param name="error">Receives the reason when the checkpoint or return cannot be prepared.</param>
        /// <returns>True when the durable return starts; otherwise the current scene remains active.</returns>
        public bool TryFindGift(out string error)
        {
            error = null;
            if (_state != State.Open) { error = "상자 개방 연출이 끝난 뒤 다시 시도하세요."; return false; }
            NightRaidController controller = NightRaidController.Instance;
            if (controller == null) { error = "정글 복귀 연결을 찾을 수 없습니다."; return false; }
            if (!controller.TryReturnWithJungleGift(out error)) return false;
            if (_innerGlow != null) _innerGlow.SetActive(false);
            if (_innerLight != null) { _innerLight.intensity = 0f; _innerLight.enabled = false; }
            _state = State.Done;
            if (_endCamera != null) _endCamera.enabled = false;
            return true;
        }

        /// <summary>Determines whether a blocked-shot notice is valid for the current closed chest.</summary>
        public bool CanShowNotice()
        {
            if (!IsClosed || _core == null || !_core.gameObject.activeInHierarchy || _orbs == null) return false;
            foreach (Transform orb in _orbs)
                if (orb != null && orb.gameObject.activeInHierarchy) return true;
            return false;
        }

        private void Update()
        {
            if (GameManager.GameplayBlocked && _state < State.WhiteOut) return;
            float deltaTime = Time.deltaTime;
            _stateTime += deltaTime;
            switch (_state)
            {
                case State.Closed:
                    Orbit(deltaTime, 1f, 1f);
                    break;
                case State.Absorbing:
                    float t = Mathf.Clamp01(_stateTime / _absorbDuration);
                    Orbit(deltaTime, 1f + t * 3f, 1f - t * t);
                    PulseCore(t);
                    if (t >= 1f) BeginWhiteOut();
                    break;
                case State.WhiteOut:
                    float white = Mathf.Clamp01(_stateTime / _whiteInDuration);
                    if (_endingUI != null) _endingUI.SetWhite(white);
                    if (white >= 1f) BeginOpen();
                    break;
                case State.Opening:
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_stateTime / _openDuration));
                    if (_lid != null) _lid.localRotation = _lidClosed * Quaternion.Euler(_lidOpenAngle * k, 0f, 0f);
                    if (_innerLight != null) _innerLight.intensity = _openLightIntensity * k;
                    if (_endingUI != null) _endingUI.SetWhite(1f - Mathf.Clamp01(_stateTime / _whiteOutDuration));
                    if (_stateTime >= _openDuration)
                    {
                        _state = State.Open;
                        _stateTime = 0f;
                    }
                    break;
                case State.Open:
                    float endAlpha = Mathf.Clamp01(_stateTime / _endFadeDuration);
                    if (_endingUI != null) _endingUI.SetEnd(endAlpha);
                    break;
            }
        }

        /// <summary>Updates orbiting spheres during absorption using the supplied time and scale factors.</summary>
        /// <param name="deltaTime">Frame time to advance the orbit.</param>
        /// <param name="speedScale">Multiplier applied to the orbit speed.</param>
        /// <param name="radiusFactor">Multiplier applied to the orbit radius and sphere scale.</param>
        private void Orbit(float deltaTime, float speedScale, float radiusFactor)
        {
            _orbitTime += deltaTime * speedScale;
            float shrink = radiusFactor < 0.25f ? radiusFactor / 0.25f : 1f;
            for (int i = 0; i < _orbs.Length; i++)
            {
                if (_orbs[i] == null) continue;
                _orbs[i].localPosition = Quaternion.AngleAxis(_orbSpeed[i] * _orbitTime, _orbAxis[i]) * (_orbStart[i] * radiusFactor);
                _orbs[i].localScale = _orbScale[i] * shrink;
            }
        }

        /// <summary>Scales and shakes the core according to normalized absorption progress.</summary>
        /// <param name="t">Absorption progress from zero to one.</param>
        private void PulseCore(float t)
        {
            if (_core == null) return;
            float shiver = 1f + 0.05f * Mathf.Sin(_stateTime * 30f) * t;
            _core.localScale = _coreScale * (1f + 0.4f * t) * shiver;
        }

        /// <summary>Blocks ordinary audio, plays the one-shot discovery clip, hides the workshop HUD, and starts the white-screen transition.</summary>
        private void BeginWhiteOut()
        {
            _state = State.WhiteOut;
            _stateTime = 0f;
            SoundManager.BeginJungleEasterEggAudio();
            GameManager manager = GameManager.Instance;
            if (manager == null) return;
            manager.SetDialogue(true);
            Canvas hud = manager.UI != null ? manager.UI.GetComponent<Canvas>() : null;
            if (hud != null) hud.enabled = false;
        }

        /// <summary>Removes the shield and starts the existing camera and lid animation.</summary>
        private void BeginOpen()
        {
            _state = State.Opening;
            _stateTime = 0f;
            if (_zone != null) _zone.gameObject.SetActive(false);
            if (_core != null) _core.gameObject.SetActive(false);
            if (_orbs != null)
                foreach (Transform orb in _orbs)
                    if (orb != null) orb.gameObject.SetActive(false);
            if (_innerGlow != null) _innerGlow.SetActive(true);
            if (_innerLight != null) _innerLight.enabled = true;
            if (_endCamera != null) _endCamera.enabled = true;
        }

        /// <summary>Restores the opened chest state and black appearance from a persisted discovery.</summary>
        private void RestoreDiscoveredState()
        {
            _state = State.Done;
            if (_zone != null) _zone.gameObject.SetActive(false);
            if (_core != null) _core.gameObject.SetActive(false);
            if (_orbs != null)
                foreach (Transform orb in _orbs)
                    if (orb != null) orb.gameObject.SetActive(false);
            if (_innerGlow != null) _innerGlow.SetActive(false);
            if (_innerLight != null) { _innerLight.intensity = 0f; _innerLight.enabled = false; }
            if (_endCamera != null) _endCamera.enabled = false;
            if (_lid != null) _lid.localRotation = _lidClosed * Quaternion.Euler(_lidOpenAngle, 0f, 0f);
            if (_discoveredMaterial != null && _discoveredRenderers != null)
                foreach (Renderer targetRenderer in _discoveredRenderers)
                    if (targetRenderer != null) targetRenderer.sharedMaterial = _discoveredMaterial;
            if (_endingUI != null) { _endingUI.SetWhite(0f); _endingUI.SetEnd(0f); }
        }

        /// <summary>Protects chest targets before discovery and allows their normal destruction afterward.</summary>
        /// <param name="value">True while the discovery is not confirmed.</param>
        private void SetDamageProtection(bool value)
        {
            if (_discoveredTargets == null) return;
            foreach (DestructibleTarget target in _discoveredTargets)
                if (target != null) target.DamageProtected = value;
        }

    }
}
