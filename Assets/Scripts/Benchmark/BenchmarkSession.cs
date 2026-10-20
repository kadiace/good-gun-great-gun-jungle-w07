using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
using UnityEngine.Profiling;
#endif
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
        // 측정 구간 프로파일러 기록 한도(MB). .data 저장은 에디터 전용이라 빌드에서는 동작하지 않는다.
        [SerializeField, Min(128)] private int profilerMaxMemoryMb = 1024;

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
        // InputSystem.settings 대입은 기존 설정이 HideAndDontSave 임시 오브젝트면 DestroyImmediate로 지운다.
        // 되돌릴 설정(프로젝트 에셋 원본 또는 임시 설정의 복제본)을 교체 전에 따로 잡아 둔다.
        private InputSettings replayInputSettings;
        private InputSettings restorableInputSettings;
        // 재생 중에만 꺼 두는 실제 마우스·키보드 장치 목록. 복원 때 같은 장치만 다시 켠다.
        private readonly List<InputDevice> reenableDevices = new List<InputDevice>();
        // OnGUI는 프레임당 여러 번 불리므로 라벨 문자열을 프레임마다 한 번만 만들어 둔다.
        private string benchLabel;
        private int warmupRemaining;
        private int frameIndex;
#if UNITY_EDITOR
        // 측정 구간 동안만 켠 프로파일러 기록 상태. 되돌릴 때 사용자가 켜 둔 상태를 보존한다.
        private bool profilerCapturing;
        private bool profilerWasEnabled;
        private bool driverWasEnabled;
#endif

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
            replayInputSettings = null;
            restorableInputSettings = null;
            reenableDevices.Clear();
            benchLabel = null;
#if UNITY_EDITOR
            profilerCapturing = false;
            profilerWasEnabled = false;
#endif
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
            if (mode == Mode.Idle || benchLabel == null) return;
            GUI.Label(new Rect(10f, 10f, 380f, 24f), benchLabel);
        }

        private void UpdateBenchLabel()
        {
            int total = replayRecording?.frames.Length ?? 0;
            benchLabel = $"BENCH REPLAY {Mathf.Min(Mathf.Max(0, frameIndex), total)}/{total} run {(replayRequest?.runIndex ?? 0) + 1}/{replayRequest?.runs ?? 1}";
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
            benchLabel = $"BENCH REC {seed}";
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
            benchLabel = null;
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
            // 준비 중 예외가 나면 WaitRaid에서 같은 준비를 매 프레임 반복해 녹화 재파싱·적 검색 비용이
            // 계속 쌓인다. 실패는 한 번 기록하고 재생을 접는다.
            try
            {
                if (replayRecording.enemiesDisabled) DisableEnemies();
                // 현재 장착 무기를 치우고 녹화에 담긴 무기로 갈아끼운다.
                EquipableWeapon previous = weaponHolder.TakeWeapon();
                if (previous != null) Destroy(previous.gameObject);
                // 이전 런의 도색 라이브러리는 그 라이브러리로 만든 무기를 치운 뒤에만 정리할 수 있다.
                replayPaints?.Dispose();
                replayPaints = PaintMaterialLibrary.CreateDefaults();
                if (!GameSaveController.TryRestoreRaidWeapon(replayRecording.weapon,
                    NightRaidController.Instance.CarriedWeaponPrefab, player, replayPaints, true, out _, out string error))
                {
                    Debug.LogError($"[Benchmark] 녹화된 무기를 복원하지 못했습니다: {error}");
                    CancelReplay();
                    return;
                }
                // 측정 구간 밖(준비 프레임)에서 샘플 버퍼와 레코더를 만든다.
                if (metrics == null)
                    metrics = new BenchmarkMetrics(gameObject.scene.name, replayRequest.recordingPath,
                        replayRequest.runIndex, replayRecording.frames.Length);
                if (!UseBackgroundInput())
                {
                    CancelReplay();
                    return;
                }
                // 실제 마우스·키보드는 재생 내내 끈다. 포커스 무시 설정으로 입력을 계속 받는 동안
                // 남아 있던 실제 버튼 상태(눌린 채로 남은 클릭 등)가 Attack·Aim에 반영되면 녹화와
                // 다른 프레임에 발사된다. DisableDevice는 장치 상태를 기본값으로 되돌리므로 그 눌림도 지워진다.
                DisableRealInputDevices();
                virtualMouse = InputSystem.AddDevice<Mouse>("BenchmarkMouse");
                ReplayActive = true;
                player.ExternallyDriven = true;
                Random.InitState(replayRecording.seed);
                Time.captureDeltaTime = replayRecording.fixedDeltaTime;
                warmupRemaining = warmupFrames;
                frameIndex = 0;
                mode = Mode.ReplayWarmup;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                CancelReplay();
            }
        }

        private void TickReplayWarmup()
        {
            // 측정 전 프레임 동안 첫 자세를 유지하며 버튼은 모두 뗀 상태로 둔다.
            BenchmarkFrame hold = replayRecording.frames[0];
            player.RestorePose(hold.position, new Vector3(0f, hold.yaw, 0f), new Vector3(hold.pitch, 0f, 0f));
            QueueButtons(0);
            UpdateBenchLabel();
            if (--warmupRemaining > 0) return;
            // 샘플 버퍼·레코더는 SetupReplay에서 이미 만들어 두었고, 여기서 벽시계만 0으로 맞춘다.
            metrics?.BeginMeasuring();
#if UNITY_EDITOR
            StartProfilerCaptureIfRequested();
#endif
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
            UpdateBenchLabel();
            metrics.EndFrame(frameIndex);
            frameIndex++;
            if (frameIndex < replayRecording.frames.Length) return;
            FinishReplay();
        }

        private void FinishReplay()
        {
#if UNITY_EDITOR
            FinishProfilerCapture();
#endif
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
            benchLabel = null;
            mode = Mode.Idle;
            if (quit) QuitApplication();
        }

        private void CancelReplay()
        {
#if UNITY_EDITOR
            AbortProfilerCapture();
#endif
            ReleaseVirtualMouse();
            if (player != null) player.ExternallyDriven = false;
            Time.captureDeltaTime = 0f;
            metrics?.Dispose();
            metrics = null;
            BenchmarkReplayRequest.Active = null;
            replayRequest = null;
            replayRecording = null;
            benchLabel = null;
            mode = Mode.Idle;
        }

        private void RestoreBenchmarkState()
        {
#if UNITY_EDITOR
            AbortProfilerCapture();
#endif
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
            benchLabel = null;
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
            EnableRealInputDevices();
            if (virtualMouse != null)
            {
                InputSystem.RemoveDevice(virtualMouse);
                virtualMouse = null;
            }
            RestoreInputSettings();
        }

        // 실제 마우스·키보드(포인터·텍스트 입력 장치)만 골라 끄고, 복원 때 같은 장치만 다시 켠다.
        private void DisableRealInputDevices()
        {
            reenableDevices.Clear();
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device == null || !device.added || !device.enabled || device == virtualMouse) continue;
                if (device is Mouse || device is Keyboard || device is Touchscreen || device is Pen)
                {
                    InputSystem.DisableDevice(device);
                    reenableDevices.Add(device);
                }
            }
        }

        private void EnableRealInputDevices()
        {
            foreach (InputDevice device in reenableDevices)
                if (device != null && device.added) InputSystem.EnableDevice(device);
            reenableDevices.Clear();
        }

        // 프로젝트 설정 에셋은 건드리지 않고 런타임 복제본만 포커스 무시로 바꿔 끼운다.
        // InputSystem.settings 대입은 기존 설정이 HideAndDontSave 임시 오브젝트면 DestroyImmediate 한다.
        // 그런 경우에 대비해 되돌릴 복제본을 미리 만들어 두고, 복원 대입이 재생용 설정을 대신 파괴하게 한다.
        private bool UseBackgroundInput()
        {
            if (replayInputSettings != null) return true;
            InputSettings current = InputSystem.settings;
            if (current == null)
            {
                Debug.LogError("[Benchmark] 입력 설정 오브젝트가 이미 파괴되어 재생 입력 설정을 교체할 수 없습니다.");
                return false;
            }
            replayInputSettings = Instantiate(current);
            replayInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            replayInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // 재생용 설정은 복원 대입 때 입력 시스템이 파괴하도록 임시 표시를 단다.
            replayInputSettings.hideFlags = HideFlags.HideAndDontSave;
            // 프로젝트 에셋이면 원본을 그대로 되돌리고, 파괴될 임시 설정일 때만 복제본을 남긴다.
            if (current.hideFlags == HideFlags.HideAndDontSave)
            {
                restorableInputSettings = Instantiate(current);
                restorableInputSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            else restorableInputSettings = current;
            InputSystem.settings = replayInputSettings;
            return true;
        }

        private void RestoreInputSettings()
        {
            if (replayInputSettings == null) return;
            // 설정 교체 대입은 기존 오브젝트를 직접 파괴하므로 수동 Destroy 하지 않는다.
            // 파괴된 오브젝트를 대입하면 복원이 생략되고, 설치된 재생용 설정만 파괴되어
            // 다음 런 준비가 매 프레임 실패하는 고장의 원인이었다.
            if (restorableInputSettings != null) InputSystem.settings = restorableInputSettings;
            restorableInputSettings = null;
            replayInputSettings = null;
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

#if UNITY_EDITOR
        // 측정 구간 프로파일러 기록. 재생 요청에 captureProfiler가 있을 때만 켠다(BenchmarkMenu 토글).
        // 프로파일러 버퍼에는 프레임 수 한도가 없고 maxUsedMemory(에디터 기본 256MB)를 넘어가면
        // 오래된 프레임부터 버려져 런 앞부분이 잘리므로, 기록 전에 인스펙터 값(기본 1024MB)으로 올린다.
        // maxUsedMemory가 바이트 단위 int라 2047MB가 표현 한도다.
        private void StartProfilerCaptureIfRequested()
        {
            profilerCapturing = false;
            profilerWasEnabled = Profiler.enabled;
            driverWasEnabled = UnityEditorInternal.ProfilerDriver.enabled;
            if (replayRequest == null || !replayRequest.captureProfiler) return;
            Profiler.maxUsedMemory = Mathf.Clamp(profilerMaxMemoryMb, 128, 2047) * 1024 * 1024;
            // 저장 파일은 프로파일러 창 버퍼(ProfilerDriver)에서 나오므로, 워밍업 프레임을 비우고 창 기록도 켠다.
            // 창 설정 "메모리 부족 시 프레임 버림"이 켜져 있으면 메모리 압박 때 앞 프레임이 빠질 수 있다.
            UnityEditorInternal.ProfilerDriver.ClearAllFrames();
            UnityEditorInternal.ProfilerDriver.enabled = true;
            Profiler.enabled = true;
            profilerCapturing = true;
            Debug.Log($"[Benchmark] 측정 구간 프로파일러 기록 시작 (maxUsedMemory {Profiler.maxUsedMemory / (1024 * 1024)}MB)");
        }

        // 정상 종료: 기록을 끄고 현재 버퍼를 ProfilerCaptures/<씬>_<시각>.data로 저장한다. 런마다 한 파일.
        private void FinishProfilerCapture()
        {
            if (!profilerCapturing) return;
            Profiler.enabled = profilerWasEnabled;
            UnityEditorInternal.ProfilerDriver.enabled = driverWasEnabled;
            profilerCapturing = false;
            try
            {
                string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProfilerCaptures");
                Directory.CreateDirectory(folder);
                string scene = gameObject.scene.name;
                foreach (char c in Path.GetInvalidFileNameChars()) scene = scene.Replace(c, '_');
                string path = Path.Combine(folder, $"{scene}_{System.DateTime.Now:yyyyMMdd_HHmmss}.data");
                UnityEditorInternal.ProfilerDriver.SaveProfile(path);
                Debug.Log($"[Benchmark] 프로파일러 캡처 저장: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        // 중단: 저장 없이 기록만 끈고 사용자가 켜 둔 프로파일러 상태는 되돌린다.
        private void AbortProfilerCapture()
        {
            if (!profilerCapturing) return;
            Profiler.enabled = profilerWasEnabled;
            UnityEditorInternal.ProfilerDriver.enabled = driverWasEnabled;
            profilerCapturing = false;
        }
#endif

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
