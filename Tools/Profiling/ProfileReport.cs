// Unity CLI eval_file 본문 (using 불가, 정규화된 이름 사용). 녹화된 프로파일러 프레임을 짧은 텍스트로 요약한다.
// 실행: unity --no-banner cmd eval_file --file Tools/Profiling/ProfileReport.cs --result-only
// 조정값
int spikeCount = 5;      // 가장 느린 프레임 몇 개를 자세히 볼지
int topSelf = 12;        // 스파이크 프레임마다 메인 스레드 자체 시간 상위 몇 개
int topOther = 6;        // 렌더 스레드 상위 몇 개
string[] watch = {       // 가설 검증용 마커(이름 앞부분 일치, 모든 스레드 합산)
    "ExpandingExplosion.", "DestructibleTarget.", "ParticleSystem", "Physics.SyncTransforms", "Physics.Simulate",
    "GC.Alloc", "GC.Collect", "Gfx.WaitForPresent", "RenderLoop", "Drawing", "Transparent", "Shadows",
    "PoolManager", "VisualDebrisCloud", "EquipableWeapon.", "GameSaveController.", "NightRaidReturnOverlay.", "Instantiate", "Destroy", "Batch", "SRPBatcher" };
string[] counters = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count",
    "Shadow Casters Count", "Dynamic Batched Draw Calls Count", "GC Allocated In Frame", "Rigidbody Count" };

// 저장 파일(Profiler 창 Save)이 있으면 그것을 불러와 분석한다. 없으면 현재 버퍼를 쓴다.
string capturePath = System.IO.Path.GetFullPath("Tools/Profiling/capture.data");
// Profiler 창 기본 저장 위치(ProfilerCaptures)의 최신 파일이 더 새로우면 그것을 쓴다.
if (System.IO.Directory.Exists("ProfilerCaptures"))
    foreach (string file in System.IO.Directory.GetFiles("ProfilerCaptures", "*.data"))
        if (!System.IO.File.Exists(capturePath) || System.IO.File.GetLastWriteTime(file) > System.IO.File.GetLastWriteTime(capturePath))
            capturePath = System.IO.Path.GetFullPath(file);
string source = "buffer";
// 1GB 캡처는 불러오기가 CLI 5초 제한을 넘는다. 이미 불러온 파일(경로+수정시각)은 다시 불러오지 않는다.
// 제한 시간 오류가 나도 불러오기는 계속되므로 잠시 뒤 같은 명령을 다시 실행하면 된다.
// 불러오기는 Profiler 창의 현재 녹화를 덮어쓴다. 녹화 버퍼가 비어 있을 때만 파일을 불러온다.
if (UnityEditorInternal.ProfilerDriver.lastFrameIndex >= 0) { source = "buffer(Profiler 창 현재 데이터)"; capturePath = ""; }
if (capturePath != "" && System.IO.File.Exists(capturePath))
{
    string key = capturePath + "|" + System.IO.File.GetLastWriteTimeUtc(capturePath).Ticks;
    if (UnityEditor.SessionState.GetString("ProfileReport.loaded", "") != key)
    {
        UnityEditor.SessionState.SetString("ProfileReport.loaded", key);
        UnityEditorInternal.ProfilerDriver.LoadProfile(capturePath, false);
    }
    source = System.IO.Path.GetFileName(capturePath);
}
int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex, last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
if (first < 0 || last < first) return "NO_DATA: 녹화된 프레임이 없습니다. Profiler Record를 켜고 플레이 후 다시 실행하세요.";
var VM = UnityEditor.Profiling.HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName;
int colSelf = UnityEditor.Profiling.HierarchyFrameDataView.columnSelfTime;
int colTotal = UnityEditor.Profiling.HierarchyFrameDataView.columnTotalTime;
int colCalls = UnityEditor.Profiling.HierarchyFrameDataView.columnCalls;
int colGc = UnityEditor.Profiling.HierarchyFrameDataView.columnGcMemory;
var sb = new System.Text.StringBuilder();
sb.AppendLine("SOURCE " + source);

// 트리 순회: 이름별 자체 시간·호출·GC 합산
System.Action<UnityEditor.Profiling.HierarchyFrameDataView, System.Collections.Generic.Dictionary<string, float[]>> collect = (v, acc) =>
{
    var stack = new System.Collections.Generic.Stack<int>(); var children = new System.Collections.Generic.List<int>();
    int root = v.GetRootItemID();
    stack.Push(root);
    while (stack.Count > 0)
    {
        int id = stack.Pop();
        if (id != root)
        {
            string name = v.GetItemName(id);
            if (!acc.TryGetValue(name, out var a)) acc[name] = a = new float[4];
            a[0] += v.GetItemColumnDataAsFloat(id, colSelf); a[1] += v.GetItemColumnDataAsFloat(id, colCalls);
            a[2] += v.GetItemColumnDataAsFloat(id, colGc); a[3] = System.Math.Max(a[3], v.GetItemColumnDataAsFloat(id, colTotal));
        }
        v.GetItemChildren(id, children);
        foreach (int c in children) stack.Push(c);
    }
};
// 대기 마커(Semaphore·Idle)는 실제 작업이 아니므로 순위에서 뺀다.
System.Func<System.Collections.Generic.Dictionary<string, float[]>, int, string> top = (acc, n) =>
{
    var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, float[]>>();
    foreach (var e in acc) if (!e.Key.Contains("Semaphore") && !e.Key.Contains("WaitForSignal") && !e.Key.Contains("Idle")) list.Add(e);
    list.Sort((a, b) => b.Value[0].CompareTo(a.Value[0]));
    var s = new System.Text.StringBuilder();
    for (int i = 0; i < System.Math.Min(n, list.Count); i++)
        s.AppendLine($"    {list[i].Value[0],7:F2}ms self  calls {list[i].Value[1],5:F0}  gc {list[i].Value[2] / 1024f,7:F1}KB  {list[i].Key}");
    return s.ToString();
};
System.Func<int, string> counterLine = f =>
{
    using (var raw = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(f, 0))
    {
        var cs = new System.Text.StringBuilder();
        foreach (string c in counters)
        {
            int id = raw.GetMarkerId(c);
            if (id != UnityEditor.Profiling.FrameDataView.invalidMarkerId) cs.Append($"{c.Replace(" Count", "")}={raw.GetCounterValueAsLong(id)}  ");
        }
        return cs.Length > 0 ? cs.ToString() : "(없음 - Rendering/Memory 모듈 확인)";
    }
};

// 프레임 시간 목록
var frames = new System.Collections.Generic.List<System.Tuple<int, float, float>>();
double sum = 0;
for (int f = first; f <= last; f++)
    using (var v = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, 0, VM, colTotal, false))
    {
        if (!v.valid) continue;
        frames.Add(System.Tuple.Create(f, v.frameTimeMs, v.frameGpuTimeMs));
        sum += v.frameTimeMs;
    }
if (frames.Count == 0) return "NO_DATA: 유효한 프레임이 없습니다.";
var sorted = new System.Collections.Generic.List<System.Tuple<int, float, float>>(frames);
sorted.Sort((a, b) => b.Item2.CompareTo(a.Item2));
var times = new System.Collections.Generic.List<float>(); foreach (var t in frames) times.Add(t.Item2); times.Sort();
sb.AppendLine($"FRAMES {frames.Count} ({first}..{last}) avg {sum / frames.Count:F1}ms median {times[times.Count / 2]:F1}ms p95 {times[(int)(times.Count * 0.95f)]:F1}ms max {sorted[0].Item2:F1}ms");
int over33 = 0; foreach (var t in times) if (t > 33.3f) over33++;
sb.AppendLine($"frames>33ms: {over33}  (GPU 시간 0이면 GPU 모듈 미녹화)");
// 평상시 기준: 프레임 시간 중앙값 프레임의 카운터
var median = sorted[sorted.Count / 2];
sb.AppendLine($"BASELINE frame {median.Item1}: cpu {median.Item2:F1}ms gpu {median.Item3:F1}ms  counters: {counterLine(median.Item1)}");

for (int k = 0; k < System.Math.Min(spikeCount, sorted.Count); k++)
{
    int f = sorted[k].Item1;
    sb.AppendLine($"\n== SPIKE #{k + 1} frame {f}: cpu {sorted[k].Item2:F1}ms gpu {sorted[k].Item3:F1}ms");
    sb.AppendLine("  counters: " + counterLine(f));
    var watchSum = new System.Collections.Generic.Dictionary<string, float[]>();
    for (int thread = 0; ; thread++)
        using (var v = UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(f, thread, VM, colSelf, false))
        {
            if (!v.valid) break;
            var acc = new System.Collections.Generic.Dictionary<string, float[]>();
            collect(v, acc);
            foreach (var e in acc)
                foreach (string w in watch)
                    if (e.Key.StartsWith(w) && !e.Key.EndsWith(" Count"))
                    {
                        if (!watchSum.TryGetValue(e.Key, out var a)) watchSum[e.Key] = a = new float[4];
                        a[0] += e.Value[0]; a[1] += e.Value[1]; a[2] += e.Value[2]; break;
                    }
            string tn = v.threadName ?? "";
            if (thread == 0) sb.Append("  [Main] top self\n" + top(acc, topSelf));
            else if (tn.Contains("Render")) sb.Append($"  [{tn}] top self\n" + top(acc, topOther));
            else if (tn.Contains("Job") || tn.Contains("Worker"))
            {
                float busy = 0; foreach (var e in acc) if (!e.Key.Contains("Idle") && !e.Key.Contains("Semaphore")) busy += e.Value[0];
                if (busy > 1f) sb.AppendLine($"  [{tn}] busy {busy:F1}ms");
            }
        }
    sb.Append("  [watch, all threads]\n" + top(watchSum, 30));
}
return sb.ToString();
