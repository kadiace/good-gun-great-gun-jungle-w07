#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using WeaponWorkshop;

// Explicit, isolated benchmark. Never runs automatically in a user's scene.
[InitializeOnLoad]
public static class DestructionProfile
{
    static readonly List<DestructibleTarget> targets = new();
    static readonly List<double> cpu = new(), gpu = new(), work = new();
    static readonly List<ExpandingExplosion.Piece> pieces = new();
    static readonly FrameTiming[] timings = new FrameTiming[1];
    static MethodInfo collect;
    static int frame;
    static string label;
    static RenderTexture output;
    static double previous;
    static bool validationOnly;
    [Serializable] class Setup { public SceneSetup[] scenes; }
    static DestructionProfile() { EditorApplication.playModeStateChanged += State; EditorApplication.update += Poll; }
    static void Poll()
    {
        const string request = "Library/DestructionProfileRequest.txt";
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
        string requested = File.ReadAllText(request).Trim(); File.Delete(request);
        if (requested == "analyze") { Analyze(); return; }
        if (requested == "variants") { DestructibleDamageShaderVariants.Rebuild(); return; }
        SessionState.SetString("DestructionProfile.Label", requested);
        try { Run(); } catch (Exception ex) { File.WriteAllText("Library/Destruction-profile-error.txt", ex.ToString()); }
    }
    public static void Run()
    {
        foreach (var scene in EnumerableScenes())
            if (scene.isDirty) throw new InvalidOperationException("Unsaved scene: " + scene.name);
        SessionState.SetString("DestructionProfile.Setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        SessionState.SetBool("DestructionProfile.Run", true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static IEnumerable<UnityEngine.SceneManagement.Scene> EnumerableScenes()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            yield return UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("DestructionProfile.Run", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            label = SessionState.GetString("DestructionProfile.Label", "unknown");
            validationOnly = label == "validate";
            targets.Clear(); cpu.Clear(); gpu.Clear(); work.Clear(); frame = 0;
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            var camera = new GameObject("ProfileCamera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -22); camera.orthographic = true; camera.orthographicSize = 9;
            output = new RenderTexture(1280, 720, 24); camera.targetTexture = output;
            var light = new GameObject("ProfileLight").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35, -25, 0);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            for (int i = 0; i < 96; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.position = new Vector3((i % 12 - 5.5f) * 1.4f, (i / 12 - 3.5f) * 1.4f, 0);
                go.GetComponent<Renderer>().sharedMaterial = material;
                var target = go.AddComponent<DestructibleTarget>();
                typeof(DestructibleTarget).GetField("standaloneProp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, true);
                typeof(DestructibleTarget).GetField("explosionResistance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, 100f);
                targets.Add(target);
            }
            collect = typeof(DestructibleTarget).GetMethod("CollectExplosionPiecesCore", BindingFlags.Instance | BindingFlags.NonPublic);
            Profiler.logFile = "Library/Destruction-" + label + ".raw";
            Profiler.enableBinaryLog = true; Profiler.enabled = true;
            previous = EditorApplication.timeSinceStartup;
            if (!validationOnly)
            {
            EditorApplication.update += Tick;
            }
            else EditorApplication.delayCall += Validate;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("DestructionProfile.Run", false);
            var setup = JsonUtility.FromJson<Setup>(SessionState.GetString("DestructionProfile.Setup", "{}"));
            if (setup.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
        }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        if (Time.frameCount == frame) return;
        frame = Time.frameCount;
        double now = EditorApplication.timeSinceStartup;
        if (frame > 90) cpu.Add((now - previous) * 1000);
        previous = now;
        FrameTimingManager.CaptureFrameTimings();
        if (frame > 90 && FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
            gpu.Add(timings[0].gpuFrameTime);
        if (frame == 120 || frame == 240 || frame == 360)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var target in targets)
            {
                pieces.Clear();
                collect.Invoke(target, new object[] { target.transform.position, 1f, .2f, pieces });
                foreach (var piece in pieces) target.ApplyExplosionDamage(piece.coordinate, 1f, target.transform.position, 0f);
            }
            watch.Stop(); work.Add(watch.Elapsed.TotalMilliseconds);
        }
        if (frame < 480) return;
        Profiler.enabled = false; Profiler.enableBinaryLog = false;
        cpu.Sort(); gpu.Sort();
        string summary = "label=" + label + "\nUnity=" + Application.unityVersion + "\nGPU=" + SystemInfo.graphicsDeviceName +
            "\n96 cubes, 125 cells each, three non-destructive area hits, 1280x720; Editor isolated benchmark\n" +
            "Editor frame interval mean_ms=" + Mean(cpu) + " p95_ms=" + cpu[(int)(cpu.Count * .95)] +
            "\nGPU samples=" + gpu.Count + " mean_ms=" + Mean(gpu) + "\nDamage work ms=" + string.Join(",", work);
        File.WriteAllText("Library/Destruction-" + label + ".txt", summary);
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }
    static double Mean(List<double> values) { double sum = 0; foreach (double v in values) sum += v; return values.Count == 0 ? 0 : sum / values.Count; }
    static object Field(DestructibleTarget target, string name) => typeof(DestructibleTarget).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static DestructibleTarget Target(Vector3 position, float resistance = 3f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.position = position;
        go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var target = go.AddComponent<DestructibleTarget>();
        typeof(DestructibleTarget).GetField("standaloneProp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, true);
        typeof(DestructibleTarget).GetField("explosionResistance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, resistance);
        return target;
    }
    static void Validate()
    {
        var results = new List<string>();
        try
        {
            var target = Target(new Vector3(100, 0, 0));
            var cell = new Vector3Int(2, 2, 0);
            Check(target.ApplyExplosionDamage(cell, 1, target.transform.position, 0) == DestructibleTarget.ExplosionDamageResult.Damaged, "first partial hit");
            Check(!(bool)Field(target, "subdivided") && target.GetComponent<Renderer>().enabled && target.GetComponents<Collider>().Length == 1,
                "partial hit must preserve original geometry and collider");
            var saved = target.CaptureSave();
            Check(!saved.subdivided && saved.explosionDamage.Length == 1 && Math.Abs(saved.explosionDamage[0].health - 2d / 3d) < 1e-8, "intact save health");
            results.Add("PASS partial hit: original renderer/collider, no cell subdivision; sparse health save");
            var restored = Target(new Vector3(103, 0, 0));
            saved.position = restored.transform.localPosition;
            restored.RestoreSave(saved);
            Check(!(bool)Field(restored, "subdivided"), "save restore forced subdivision");
            Check(restored.ApplyExplosionDamage(cell, 1, restored.transform.position, 0) == DestructibleTarget.ExplosionDamageResult.Damaged, "second hit after load");
            Check(restored.ApplyExplosionDamage(cell, 1, restored.transform.position, 0) == DestructibleTarget.ExplosionDamageResult.Destroyed, "third hit after load");
            Check(restored.AttachedCellCount == 124, "only requested cell should disappear");
            results.Add("PASS save/load and resistance deficit 2: third hit removes exactly one cell");
            var equal = Target(new Vector3(106, 0, 0));
            Check(equal.ApplyExplosionDamage(cell, 3, equal.transform.position, 0) == DestructibleTarget.ExplosionDamageResult.Destroyed && equal.AttachedCellCount == 124, "equal power one hit");
            target.ResetTarget(); Check(target.CaptureSave().explosionDamage.Length == 0, "reset partial health");
            results.Add("PASS equal explosion power destroys once; reset clears intact damage");
            var wall = Target(new Vector3(110, 0, 0));
            Physics.SyncTransforms();
            var start = wall.transform.position + Vector3.back * 2;
            var half = Vector3.one * .04f;
            typeof(DestructibleTarget).GetMethod("DestroyPenetrationCells", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(wall, new object[] { start, Quaternion.identity, half, 4f });
            Check(wall.GetComponents<Collider>().Length == 1 && wall.GetComponent<Collider>().enabled, "collider rebuilt during penetration");
            var queryType = typeof(DestructibleTarget).Assembly.GetType("WeaponWorkshop.CombatPhysicsQueries");
            var cast = queryType.GetMethod("BoxCast", BindingFlags.Static | BindingFlags.Public);
            object[] args = { start, half, Vector3.forward, Quaternion.identity, 4f, ~0, null, null, null };
            int throughHole = (int)cast.Invoke(null, args);
            Check(throughHole == 0, "stale collider blocks cleared hole");
            args[0] = start + Vector3.right * .3f;
            Check((int)cast.Invoke(null, args) > 0, "adjacent intact cells missed before collision flush");
            var finish = typeof(DestructibleTarget).Assembly.GetType("WeaponWorkshop.PoolManager").GetMethod("FlushExplosionFinish", BindingFlags.NonPublic | BindingFlags.Static);
            finish.Invoke(null, null);
            args[0] = start;
            Check((int)cast.Invoke(null, args) == 0, "hole changed after collision flush");
            results.Add("PASS deferred collision: a second bullet crosses the hole, adjacent cells still hit, same result after flush");
        }
        catch (Exception exception) { results.Add("FAIL " + exception); }
        File.WriteAllLines("Library/Destruction-validation.txt", results);
        Profiler.enabled = false; Profiler.enableBinaryLog = false;
        EditorApplication.ExitPlaymode();
    }
    static void Analyze()
    {
        try
        {
            var lines = new List<string>();
            foreach (var capture in new[] { "baseline", "optimized", "final" })
            {
                string path = "Library/Destruction-" + capture + ".raw";
                if (!File.Exists(path)) continue;
                UnityEditorInternal.ProfilerDriver.ClearAllFrames();
                UnityEditorInternal.ProfilerDriver.LoadProfile(path, false);
                var sums = new Dictionary<string, double>();
                var maxima = new Dictionary<string, double>();
                int frames = 0;
                for (int frameIndex = UnityEditorInternal.ProfilerDriver.firstFrameIndex;
                    frameIndex <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frameIndex++)
                {
                    using (var data = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frameIndex, 0))
                    {
                        if (!data.valid) continue;
                        frames++;
                        var frameSums = new Dictionary<string, double>();
                        for (int sample = 0; sample < data.sampleCount; sample++)
                        {
                            string name = data.GetSampleName(sample);
                            if (!name.StartsWith("DestructibleTarget") && !name.StartsWith("Physics.") &&
                                name != "PlayerLoop" && name != "Gfx.WaitForPresentOnGfxThread") continue;
                            frameSums.TryGetValue(name, out double value);
                            frameSums[name] = value + data.GetSampleTimeMs(sample);
                        }
                        foreach (var pair in frameSums)
                        {
                            sums.TryGetValue(pair.Key, out double sum); sums[pair.Key] = sum + pair.Value;
                            maxima.TryGetValue(pair.Key, out double maximum); maxima[pair.Key] = Math.Max(maximum, pair.Value);
                        }
                    }
                }
                lines.Add(capture + " CPU raw frames=" + frames);
                foreach (var pair in sums) lines.Add(pair.Key + " mean_ms=" + pair.Value / Math.Max(1, frames) + " max_ms=" + maxima[pair.Key]);
            }
            File.WriteAllLines("Library/Destruction-cpu-analysis.txt", lines);
        }
        catch (Exception exception) { File.WriteAllText("Library/Destruction-profile-error.txt", exception.ToString()); }
    }
}
#endif
