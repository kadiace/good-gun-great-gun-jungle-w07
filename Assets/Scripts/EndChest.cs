using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // 게임을 끝낼 상자. 코어가 맞으면 작은 구체들이 천천히 코어로 빨려 들어간 뒤 뚜껑이 열린다.
    // 적 탱크의 물리 충돌 무시를 위해 인스턴스 목록을 공개한다(RaidTank 참고).
    [DisallowMultipleComponent]
    public sealed class EndChest : MonoBehaviour
    {
        // 순서가 의미를 가진다: WhiteOut부터는 게임플레이가 막힌 연출 구간.
        private enum State { Closed, Absorbing, WhiteOut, Opening, Open, Done }

        private static readonly List<EndChest> instances = new List<EndChest>();
        public static IReadOnlyList<EndChest> Instances => instances;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => instances.Clear();

        [SerializeField, Tooltip("구형 범위 중심. 코어와 작은 구체들의 부모.")] private Transform zone;
        [SerializeField] private Transform core;
        [SerializeField] private Transform[] orbs;
        [SerializeField, Tooltip("뚜껑 힌지. +X 회전으로 열린다.")] private Transform lid;
        [SerializeField] private GameObject innerGlow;
        [SerializeField] private Light innerLight;
        [SerializeField, Min(0f)] private float orbitSpeed = 25f;
        [SerializeField, Min(0.1f)] private float absorbDuration = 7f;
        [SerializeField, Min(0.1f)] private float whiteInDuration = 1f;
        [SerializeField, Min(0.1f)] private float whiteOutDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float openDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float endFadeDuration = 1.5f;
        [SerializeField, Tooltip("흡수 후 하얀 화면 뒤에서 켜지는 상자 위 내려다보는 카메라.")] private Camera endCamera;
        [SerializeField] private EndChestEndingUI endingUI;
        [SerializeField, Range(0f, 170f)] private float lidOpenAngle = 105f;
        [SerializeField, Min(0f)] private float openLightIntensity = 8f;

        private State state;
        private float stateTime, orbitTime;
        private Vector3[] orbStart, orbAxis, orbScale;
        private float[] orbSpeed;
        private Vector3 coreScale;
        private Quaternion lidClosed;
        private float savedListenerVolume;
        private bool audioMuted;

        public bool IsClosed => state == State.Closed;
        public bool IsOpen => state >= State.Open;
        public bool AwaitingChoice => state == State.Open;
        public EndChestCore CoreGate => core != null ? core.GetComponent<EndChestCore>() : null;

        private void Awake()
        {
            int count = orbs != null ? orbs.Length : 0;
            orbStart = new Vector3[count]; orbAxis = new Vector3[count];
            orbScale = new Vector3[count]; orbSpeed = new float[count];
            for (int i = 0; i < count; i++)
            {
                orbStart[i] = orbs[i].localPosition;
                orbScale[i] = orbs[i].localScale;
                orbAxis[i] = new Vector3(Mathf.Sin(i * 2.4f), 1f, Mathf.Cos(i * 1.7f)).normalized;
                orbSpeed[i] = orbitSpeed * (0.6f + 0.8f * Mathf.Repeat(i * 0.37f, 1f));
                orbs[i].gameObject.SetActive(true);
            }
            if (core != null) { coreScale = core.localScale; core.gameObject.SetActive(true); }
            if (lid != null) { lidClosed = lid.localRotation; }
            // 방어막은 탄 판정 전용: 플레이어·탱크 등과 물리 충돌하지 않는다.
            int shieldLayer = LayerMask.NameToLayer("EndChestShield");
            if (shieldLayer >= 0)
                for (int layer = 0; layer < 32; layer++) Physics.IgnoreLayerCollision(shieldLayer, layer, true);
            // 씬에 저장된 상태와 무관하게 닫힌 상태로 시작한다.
            state = State.Closed; stateTime = orbitTime = 0f;
            if (innerGlow != null) innerGlow.SetActive(false);
            if (innerLight != null) innerLight.intensity = 0f;
            if (endCamera != null) endCamera.enabled = false;
        }

        private void OnEnable() => instances.Add(this);
        private void OnDisable() { instances.Remove(this); RestoreAudio(); }

        public void BeginAbsorb()
        {
            if (state != State.Closed) return;
            state = State.Absorbing; stateTime = 0f;
        }

        // END 화면에서 '계속하기': 연출 UI·카메라를 걷고 조작과 HUD를 되돌린다. 상자는 열린 채 남는다.
        public void Continue()
        {
            if (state != State.Open) return;
            state = State.Done; stateTime = 0f;
            if (endingUI != null) { endingUI.SetWhite(0f); endingUI.SetEnd(0f); }
            if (endCamera != null) endCamera.enabled = false;
            var manager = GameManager.Instance;
            if (manager == null) return;
            var hud = manager.UI != null ? manager.UI.Screen<Canvas>() : null;
            if (hud != null) hud.enabled = true;
            manager.SetDialogue(false);
        }

        private void Update()
        {
            if (GameManager.GameplayBlocked && state < State.WhiteOut) return;
            float deltaTime = Time.deltaTime;
            stateTime += deltaTime;
            switch (state)
            {
                case State.Closed:
                    Orbit(deltaTime, 1f, 1f);
                    break;
                case State.Absorbing:
                    float t = Mathf.Clamp01(stateTime / absorbDuration);
                    // 처음에는 천천히, 끝으로 갈수록 빠르게 빨려 든다.
                    Orbit(deltaTime, 1f + t * 3f, 1f - t * t);
                    PulseCore(t);
                    if (t >= 1f) BeginWhiteOut();
                    break;
                case State.WhiteOut:
                    float white = Mathf.Clamp01(stateTime / whiteInDuration);
                    if (endingUI != null) endingUI.SetWhite(white);
                    if (white >= 1f) BeginOpen();
                    break;
                case State.Opening:
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTime / openDuration));
                    if (lid != null) lid.localRotation = lidClosed * Quaternion.Euler(lidOpenAngle * k, 0f, 0f);
                    if (innerLight != null) innerLight.intensity = openLightIntensity * k;
                    if (endingUI != null) endingUI.SetWhite(1f - Mathf.Clamp01(stateTime / whiteOutDuration));
                    if (stateTime >= openDuration)
                    {
                        state = State.Open; stateTime = 0f; RestoreAudio();
                        NightRaidController.Instance?.NotifyEndChestOpened();
                    }
                    break;
                case State.Open:
                    if (endingUI != null) endingUI.SetEnd(stateTime / endFadeDuration);
                    break;
            }
        }

        private void Orbit(float deltaTime, float speedScale, float radiusFactor)
        {
            orbitTime += deltaTime * speedScale;
            float shrink = radiusFactor < 0.25f ? radiusFactor / 0.25f : 1f;
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                orbs[i].localPosition = Quaternion.AngleAxis(orbSpeed[i] * orbitTime, orbAxis[i]) * (orbStart[i] * radiusFactor);
                orbs[i].localScale = orbScale[i] * shrink;
            }
        }

        private void PulseCore(float t)
        {
            if (core == null) return;
            float shiver = 1f + 0.05f * Mathf.Sin(stateTime * 30f) * t;
            core.localScale = coreScale * (1f + 0.4f * t) * shiver;
        }

        // 흡수가 끝나면 조작·적 행동을 막고 HUD를 숨긴 채 화면을 하얗게 덮는다.
        private void BeginWhiteOut()
        {
            state = State.WhiteOut; stateTime = 0f;
            var manager = GameManager.Instance;
            if (manager == null) return;
            manager.SetDialogue(true);
            var hud = manager.UI != null ? manager.UI.Screen<Canvas>() : null;
            if (hud != null) hud.enabled = false;
        }

        // 화면이 완전히 하얀 동안 구체·코어·방어막을 치우고 카메라를 상자 위로 옮긴다.
        private void BeginOpen()
        {
            state = State.Opening; stateTime = 0f;
            if (zone != null) zone.gameObject.SetActive(false);
            if (innerGlow != null) innerGlow.SetActive(true);
            if (endCamera != null) endCamera.enabled = true;
            // 뚜껑이 열리는 동안 주변 소리를 차단한다.
            savedListenerVolume = AudioListener.volume; audioMuted = true;
            AudioListener.volume = 0f;
        }

        private void RestoreAudio()
        {
            if (!audioMuted) return;
            audioMuted = false;
            AudioListener.volume = savedListenerVolume;
        }
    }
}
