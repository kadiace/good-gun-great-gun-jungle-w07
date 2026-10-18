using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // 습격 씬마다 하나 붙는 벤치마크 드라이버. F9로 플레이 경로를 녹화하고, F10(최신 녹화) 또는
    // BenchmarkReplayRequest.Active로 재생한다. 재생은 플레이어 자세를 프레임마다 강제하고
    // 공격·조준은 가상 마우스 상태 이벤트로 눌러 기존 WeaponHolder 발사 경로를 그대로 태운다.
    [DefaultExecutionOrder(12000)]
    [DisallowMultipleComponent]
    public sealed class BenchmarkSession : MonoBehaviour
    {
        private enum Mode { Idle, Record, ReplayWaitRaid, ReplayWarmup, ReplayMeasure }

        private const int FrameCapacity = 36000;

        // 재생 중에는 창 포커스와 무관하게 발사 입력을 처리한다(WeaponHolder가 참조).
        public static bool ReplayActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ReplayActive = false;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField, Min(0.0001f)] private float fixedDeltaTime = 1f / 60f;
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool disableEnemies = true;
        [SerializeField, Min(0)] private int warmupFrames = 120;

        private InputActionMap benchmarkControls;
        private InputActionMap playerControls;
        private InputAction toggleRecord;
        private InputAction startReplay;
        private InputAction attackAction;
        private InputAction aimAction;

        private FirstPersonPlayer player;
        private WeaponHolder weaponHolder;
        private Mode mode = Mode.Idle;
        private BenchmarkRecording recording;
        private WeaponWorldSave recordedWeapon;
        private readonly List<BenchmarkFrame> frames = new List<BenchmarkFrame>(FrameCapacity);

        private BenchmarkReplayRequest.Data replayRequest;
        private BenchmarkRecording replayRecording;
        private PaintMaterialLibrary replayPaints;
        private BenchmarkMetrics metrics;
        private Mouse virtualMouse;
        private InputSettings originalInputSettings;
        private InputSettings replayInputSettings;
        private int warmupRemaining;
        private int frameIndex;

        private void Awake()
        {
            if (inputActions == null) { Debug.LogError("[Benchmark] 벤치마크 입력 에셋 연결이 없습니다."); enabled = false; return; }
            benchmarkControls = inputActions.FindActionMap("Benchmark", true).Clone();
            toggleRecord = benchmarkControls.FindAction("ToggleRecord", true);
            startReplay = benchmarkControls.FindAction("StartReplay", true);
            playerControls = inputActions.FindActionMap("Player", true).Clone();
            attackAction = playerControls.FindAction("Attack", true);
            aimAction = playerControls.FindAction("Aim", true);
            // 시작 상태를 명시한다: 아이들, 녹화·재생 없음, 라벨 없음(OnGUI는 Idle에서 조기 반환).
            mode = Mode.Idle;
            recording = null;
            recordedWeapon = null;
            frames.Clear();
            replayRequest = null;
            replayRecording = null;
            metrics = null;
            virtualMouse = null;
        }

        private void Start()
        {
            FindPlayer();
            if (player != null) player.ExternallyDriven = false;
        }

        private void OnEnable()
        {
            benchmarkControls?.Enable();
            playerControls?.Enable();
        }

        private void OnDisable()
        {
            benchmarkControls?.Disable();
            playerControls?.Disable();
            RestoreBenchmarkState();
        }

        private void OnDestroy()
        {
            benchmarkControls?.Dispose();
            playerControls?.Dispose();
            RestoreBenchmarkState();
            replayPaints?.Dispose();
            replayPaints = null;
        }

        private void LateUpdate()
        {
            bool inRaid = NightRaidController.Instance != null && NightRaidController.Instance.IsInRaid;
            if (mode == Mode.Idle)
            {
                if (toggleRecord != null && toggleRecord.WasPressedThisFrame())
                {
                    if (inRaid) StartRecording();
                }
                else if (startReplay != null && startReplay.WasPressedThisFrame() && BenchmarkReplayRequest.Active == null)
                {
                    StartReplayFromKey();
                }
                else if (BenchmarkReplayRequest.Active != null)
                {
                    StartReplayFromRequest();
                }
                return;
            }
            if (mode == Mode.Record)
            {
                if (toggleRecord.WasPressedThisFrame() || !inRaid) StopRecording();
                else AppendRecordFrame();
                return;
            }
            // 재생은 습격이 시작되기를 프레임마다 기다렸다가 한 번만 준비한다.
            if (mode == Mode.ReplayWaitRaid)
            {
                if (inRaid) SetupReplay();
                return;
            }
            if (mode == Mode.ReplayWarmup) TickReplayWarmup();
            else TickReplayMeasure();
        }

        private void OnGUI()
        {
            if (mode == Mode.Idle) return;
            int total = replayRecording?.frames.Length ?? 0;
            string text = mode == Mode.Record
                ? $"BENCH REC {seed}"
                : $"BENCH REPLAY {Mathf.Min(Mathf.Max(0, frameIndex), total)}/{total} run {(replayRequest?.runIndex ?? 0) + 1}/{replayRequest?.runs ?? 1}";
            GUI.Label(new Rect(10f, 10f, 380f, 24f), text);
        }

        // ---- 녹화 ----

        private void StartRecording()
        {
            if (!TryGetPlayer())
            {
                Debug.LogWarning("[Benchmark] 녹화를 시작하려면 씬에 플레이어가 필요합니다.");
                return;
            }
            EquipableWeapon weapon = weaponHolder.EquippedWeapon;
            if (weapon == null)
            {
                Debug.LogWarning("[Benchmark] 녹화를 시작하려면 장착한 무기가 필요합니다.");
                return;
            }
            if (!GameSaveController.TryCaptureCarried(weapon, out recordedWeapon, out string error, true))
            {
                Debug.LogWarning($"[Benchmark] 장착한 무기를 녹화에 담지 못했습니다: {error}");
                recordedWeapon = null;
                return;
            }
            frames.Clear();
            recording = new BenchmarkRecording
            {
                sourceScene = gameObject.scene.name,
                seed = seed,
                fixedDeltaTime = fixedDeltaTime,
                enemiesDisabled = disableEnemies,
                weapon = recordedWeapon,
            };
            mode = Mode.Record;
            Time.captureDeltaTime = fixedDeltaTime;
            if (disableEnemies) DisableEnemies();
        }

        private void AppendRecordFrame()
        {
            if (player == null || player.ViewPoint == null) return;
            frames.Add(new BenchmarkFrame
            {
                position = player.transform.position,
                yaw = player.transform.eulerAngles.y,
                pitch = Mathf.DeltaAngle(0f, player.ViewPoint.localEulerAngles.x),
                buttons = (byte)((attackAction != null && attackAction.IsPressed() ? BenchmarkRecording.AttackBit : 0) |
                    (aimAction != null && aimAction.IsPressed() ? BenchmarkRecording.AimBit : 0)),
            });
        }

        private void StopRecording()
        {
            Time.captureDeltaTime = 0f;
            if (recording != null && frames.Count > 0)
            {
                recording.frames = frames.ToArray();
                string path = BenchmarkFiles.Save(recording);
                Debug.Log($"[Benchmark] 녹화 저장: {path} ({recording.frames.Length}프레임)");
            }
            else
            {
                Debug.LogWarning("[Benchmark] 녹화된 프레임이 없어 저장하지 않았습니다.");
            }
            recording = null;
            recordedWeapon = null;
            mode = Mode.Idle;
        }

        // ---- 재생 ----

        // F10: 현재 씬에서 가장 최근 녹화를 한 번 재생한다.
        private void StartReplayFromKey()
        {
            string path = BenchmarkFiles.LatestRecording();
            if (!BenchmarkFiles.TryLoad(path, out replayRecording))
            {
                Debug.LogError("[Benchmark] 재생할 녹화를 찾지 못했습니다.");
                replayRecording = null;
                return;
            }
            replayRequest = new BenchmarkReplayRequest.Data
            { recordingPath = path, sceneName = string.Empty, runs = 1, runIndex = 0, quitWhenDone = false };
            mode = Mode.ReplayWaitRaid;
        }

        private void StartReplayFromRequest()
        {
            replayRequest = BenchmarkReplayRequest.Active;
            if (!BenchmarkFiles.TryLoad(replayRequest.recordingPath, out replayRecording))
            {
                Debug.LogError($"[Benchmark] 재생할 녹화를 읽을 수 없습니다: {replayRequest.recordingPath}");
                BenchmarkReplayRequest.Active = null;
                replayRequest = null;
                replayRecording = null;
                return;
            }
            mode = Mode.ReplayWaitRaid;
        }

        private void SetupReplay()
        {
            if (!TryGetPlayer() || NightRaidController.Instance == null)
            {
                Debug.LogError("[Benchmark] 재생 준비에 플레이어·습격 컨트롤러가 필요합니다.");
                CancelReplay();
                return;
            }
            if (replayRecording.enemiesDisabled) DisableEnemies();
            // 현재 장착 무기를 치우고 녹화에 담긴 무기로 갈아끼운다.
            EquipableWeapon previous = weaponHolder.TakeWeapon();
            if (previous != null) Destroy(previous.gameObject);
            replayPaints = PaintMaterialLibrary.CreateDefaults();
            if (!GameSaveController.TryRestoreRaidWeapon(replayRecording.weapon,
                NightRaidController.Instance.CarriedWeaponPrefab, player, replayPaints, true, out _, out string error))
            {
                Debug.LogError($"[Benchmark] 녹화된 무기를 복원하지 못했습니다: {error}");
                CancelReplay();
                return;
            }
            UseBackgroundInput();
            virtualMouse = InputSystem.AddDevice<Mouse>("BenchmarkMouse");
            ReplayActive = true;
            player.ExternallyDriven = true;
            Random.InitState(replayRecording.seed);
            Time.captureDeltaTime = replayRecording.fixedDeltaTime;
            warmupRemaining = warmupFrames;
            frameIndex = 0;
            mode = Mode.ReplayWarmup;
        }

        private void TickReplayWarmup()
        {
            // 측정 전 프레임 동안 첫 자세를 유지하며 버튼은 모두 뗀 상태로 둔다.
            BenchmarkFrame hold = replayRecording.frames[0];
            player.RestorePose(hold.position, new Vector3(0f, hold.yaw, 0f), new Vector3(hold.pitch, 0f, 0f));
            QueueButtons(0);
            if (--warmupRemaining > 0) return;
            metrics = new BenchmarkMetrics(gameObject.scene.name, replayRequest.recordingPath, replayRequest.runIndex);
            metrics.BeginMeasuring();
            frameIndex = 0;
            mode = Mode.ReplayMeasure;
        }

        private void TickReplayMeasure()
        {
            BenchmarkFrame frame = replayRecording.frames[frameIndex];
            player.RestorePose(frame.position, new Vector3(0f, frame.yaw, 0f), new Vector3(frame.pitch, 0f, 0f));
            // 프레임 i+1의 버튼을 여기서 쌓아 다음 프레임 입력으로 쓰게 한다.
            byte buttons = frameIndex + 1 < replayRecording.frames.Length
                ? replayRecording.frames[frameIndex + 1].buttons
                : (byte)0;
            QueueButtons(buttons);
            metrics.EndFrame(frameIndex);
            frameIndex++;
            if (frameIndex < replayRecording.frames.Length) return;
            FinishReplay();
        }

        private void FinishReplay()
        {
            metrics?.Finish();
            metrics?.Dispose();
            metrics = null;
            QueueButtons(0);
            ReleaseVirtualMouse();
            if (player != null) player.ExternallyDriven = false;
            Time.captureDeltaTime = 0f;

            if (replayRequest != null && replayRequest.runIndex + 1 < replayRequest.runs)
            {
                // 다음 런은 씬을 다시 불러온 뒤 Active를 읽은 새 BenchmarkSession이 맡는다.
                replayRequest.runIndex++;
                BenchmarkReplayRequest.Active = replayRequest;
                Random.InitState(replayRecording.seed);
                SceneManager.LoadScene(gameObject.scene.name);
                return;
            }
            bool quit = replayRequest != null && replayRequest.quitWhenDone;
            BenchmarkReplayRequest.Active = null;
            replayRequest = null;
            replayRecording = null;
            mode = Mode.Idle;
            if (quit) QuitApplication();
        }

        private void CancelReplay()
        {
            ReleaseVirtualMouse();
            if (player != null) player.ExternallyDriven = false;
            Time.captureDeltaTime = 0f;
            metrics?.Dispose();
            metrics = null;
            BenchmarkReplayRequest.Active = null;
            replayRequest = null;
            replayRecording = null;
            mode = Mode.Idle;
        }

        private void RestoreBenchmarkState()
        {
            Time.captureDeltaTime = 0f;
            if (player != null) player.ExternallyDriven = false;
            ReleaseVirtualMouse();
            metrics?.Dispose();
            metrics = null;
            mode = Mode.Idle;
            recording = null;
            recordedWeapon = null;
            frames.Clear();
            replayRequest = null;
            replayRecording = null;
        }

        // ---- 입력 장치·플레이어 ----

        private void QueueButtons(byte buttons)
        {
            if (virtualMouse == null) return;
            var state = new MouseState()
                .WithButton(MouseButton.Left, (buttons & BenchmarkRecording.AttackBit) != 0)
                .WithButton(MouseButton.Right, (buttons & BenchmarkRecording.AimBit) != 0);
            InputSystem.QueueStateEvent(virtualMouse, state);
        }

        private void ReleaseVirtualMouse()
        {
            ReplayActive = false;
            if (virtualMouse != null)
            {
                InputSystem.RemoveDevice(virtualMouse);
                virtualMouse = null;
            }
            RestoreInputSettings();
        }

        // 프로젝트 설정 에셋은 건드리지 않고 런타임 복제본만 포커스 무시로 바꿔 끼운다.
        private void UseBackgroundInput()
        {
            if (replayInputSettings != null) return;
            originalInputSettings = InputSystem.settings;
            replayInputSettings = Instantiate(originalInputSettings);
            replayInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            replayInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = replayInputSettings;
        }

        private void RestoreInputSettings()
        {
            if (replayInputSettings == null) return;
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            Destroy(replayInputSettings);
            replayInputSettings = null;
            originalInputSettings = null;
        }

        private void FindPlayer()
        {
            if (player != null && player.gameObject.scene == gameObject.scene) return;
            FirstPersonPlayer candidate = FindFirstObjectByType<FirstPersonPlayer>();
            player = candidate != null && candidate.gameObject.scene == gameObject.scene ? candidate : null;
            if (player != null && weaponHolder == null) weaponHolder = player.GetComponent<WeaponHolder>();
        }

        private bool TryGetPlayer()
        {
            FindPlayer();
            return player != null && weaponHolder != null;
        }

        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- 적 비활성화 ----

        private void DisableEnemies()
        {
            int managers = 0;
            foreach (RaidEnemySpawnManager manager in FindObjectsByType<RaidEnemySpawnManager>(FindObjectsSortMode.None))
            {
                if (!manager.enabled) continue;
                manager.enabled = false;
                managers++;
            }
            int enemies = 0;
            foreach (RaidEnemyCore core in FindObjectsByType<RaidEnemyCore>(FindObjectsSortMode.None))
            {
                Transform root = EnemyRootOf(core);
                if (root == null || !root.gameObject.activeSelf) continue;
                if (ContainsEnvironmentTargets(root.gameObject))
                {
                    Debug.LogWarning($"[Benchmark] 파츠에 건물이 섞인 적이라 비활성화하지 않았습니다: {root.name}");
                    continue;
                }
                root.gameObject.SetActive(false);
                enemies++;
            }
            Debug.Log($"[Benchmark] 벤치마크용 적 비활성화: 출격 관리자 {managers}개, 배치 적 {enemies}개");
        }

        // 적 루트 컴포넌트가 붙은 가장 바깥 계층. 건물 부모까지 올라가지 않도록 적 컴포넌트에서만 확장한다.
        private static Transform EnemyRootOf(RaidEnemyCore core)
        {
            Transform outermost = null;
            for (Transform current = core.transform; current != null; current = current.parent)
                if (current.GetComponent<RaidTank>() != null || current.GetComponent<RaidHelicopter>() != null ||
                    current.GetComponent<RaidTankTransport>() != null)
                    outermost = current;
            return outermost;
        }

        // 적 루트 아래에 적 소속이 아닌 파괴 대상(건물)이 섞여 있으면 통째로 끄지 않는다.
        private static bool ContainsEnvironmentTargets(GameObject root)
        {
            foreach (DestructibleTarget part in root.GetComponentsInChildren<DestructibleTarget>(true))
                if (RaidEnemyCore.FindForPart(part.transform) == null) return true;
            return false;
        }
    }
}
