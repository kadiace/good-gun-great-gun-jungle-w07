using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace WeaponWorkshop
{
    // Per-frame benchmark sampler. BeginMeasuring starts the wall clock, EndFrame appends one row of
    // numbers in memory (no IO, no string work per frame), Finish writes the CSV and JSON summary once.
    public sealed class BenchmarkMetrics : IDisposable
    {
        // Marker columns in contract order; project markers use the Scripts category, Physics.* uses Physics.
        private static readonly string[] MarkerNames =
        {
            "ExpandingExplosion.Create",
            "ExpandingExplosion.CollectPieces",
            "ExpandingExplosion.Step",
            "ExpandingExplosion.Merge",
            "ExpandingExplosion.Build",
            "DestructibleTarget.Subdivide",
            "DestructibleTarget.ResolveSupport",
            "DestructibleTarget.UpdateCollisionRuns",
            "DestructibleTarget.RebuildChunkMeshes",
            "DestructibleTarget.FinishExplosionStep",
            "DestructibleTargetUpdater.LateUpdate",
            "VisualDebrisCloud.ReserveBuffers",
            "ProjectileMotor.CollisionQuery",
            "Physics.SyncTransforms",
            "Physics.Simulate",
        };

        private const int MarkerCount = 15;
        private const int ValueStride = 4 + MarkerCount; // wall, main thread, gpu, gc alloc + markers
        private const int InitialCapacity = 8192;
        private const float FrameOver33Ms = 33f;
        private const float FrameOver50Ms = 50f;

        private static readonly string CsvHeader =
            "frame,wall_ms,main_thread_ms,gpu_ms,gc_alloc_kb," + string.Join(",", MarkerNames);

        private readonly Stopwatch wallClock = new Stopwatch();
        private readonly ProfilerRecorder mainThreadRecorder;
        private readonly ProfilerRecorder gcAllocRecorder;
        private readonly ProfilerRecorder[] markerRecorders;
        private readonly FrameTiming[] frameTimingBuffer = new FrameTiming[1];

        private readonly string sceneName;
        private readonly string recordingName;
        private readonly int runIndex;

        private int[] frameIndices;
        private float[] values;
        private int rowCount;
        private bool finished;
        private bool disposed;
        private string csvPath;

        public BenchmarkMetrics(string sceneName, string recordingPath, int runIndex, int minFrameCapacity = 0)
        {
            this.sceneName = sceneName;
            this.runIndex = runIndex;
            recordingName = string.IsNullOrEmpty(recordingPath)
                ? string.Empty
                : Path.GetFileNameWithoutExtension(recordingPath);

            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            markerRecorders = new ProfilerRecorder[MarkerNames.Length];
            for (int i = 0; i < MarkerNames.Length; i++)
            {
                ProfilerCategory category = MarkerNames[i].StartsWith("Physics.", StringComparison.Ordinal)
                    ? ProfilerCategory.Physics
                    : ProfilerCategory.Scripts;
                markerRecorders[i] = ProfilerRecorder.StartNew(category, MarkerNames[i]);
            }

            // 녹화 길이를 알면 처음부터 필요한 크기를 잡아 측정 중 버퍼 확장(할당 스파이크)이 없게 한다.
            int capacity = Math.Max(InitialCapacity, minFrameCapacity);
            frameIndices = new int[capacity];
            values = new float[ValueStride * capacity];
        }

        // Resets the wall clock; call once just before frame 0.
        public void BeginMeasuring()
        {
            wallClock.Restart();
        }

        // Call once per frame from LateUpdate; appends one CSV row worth of numbers, no IO.
        public void EndFrame(int frameIndex)
        {
            if (disposed || finished) return;
            float wallMs = wallClock.IsRunning ? (float)wallClock.Elapsed.TotalMilliseconds : 0f;
            wallClock.Restart();
            AppendRow(frameIndex, wallMs, RecorderMs(mainThreadRecorder), SampleGpuMs(), GcAllocKb());
        }

        // Writes the CSV and JSON summary into BenchmarkFiles.ResultsFolder; returns the CSV path. Idempotent.
        public string Finish()
        {
            if (finished) return csvPath;
            finished = true;

            csvPath = BuildCsvPath();
            WriteCsv(csvPath);
            File.WriteAllText(Path.ChangeExtension(csvPath, ".json"), JsonUtility.ToJson(BuildSummary(), true));
            Debug.Log($"[Benchmark] Wrote {rowCount} frames to {csvPath}");
            return csvPath;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            StopRecorder(mainThreadRecorder);
            StopRecorder(gcAllocRecorder);
            for (int i = 0; i < markerRecorders.Length; i++)
                StopRecorder(markerRecorders[i]);
        }

        private void AppendRow(int frameIndex, float wallMs, float mainThreadMs, float gpuMs, float gcAllocKb)
        {
            if (rowCount == frameIndices.Length)
            {
                int newCapacity = frameIndices.Length * 2;
                Array.Resize(ref frameIndices, newCapacity);
                Array.Resize(ref values, newCapacity * ValueStride);
            }
            frameIndices[rowCount] = frameIndex;
            int offset = rowCount * ValueStride;
            values[offset] = wallMs;
            values[offset + 1] = mainThreadMs;
            values[offset + 2] = gpuMs;
            values[offset + 3] = gcAllocKb;
            for (int i = 0; i < MarkerCount; i++)
                values[offset + 4 + i] = RecorderMs(markerRecorders[i]);
            rowCount++;
        }

        // LastValue is nanoseconds; a recorder that is invalid in this build (release player) reads 0.
        private static float RecorderMs(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue * 0.000001f : 0f;
        }

        private float GcAllocKb()
        {
            return gcAllocRecorder.Valid ? gcAllocRecorder.LastValue / 1024f : 0f;
        }

        private float SampleGpuMs()
        {
            try
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, frameTimingBuffer) > 0)
                {
                    float gpuMs = (float)frameTimingBuffer[0].gpuFrameTime;
                    return gpuMs > 0f ? gpuMs : 0f;
                }
            }
            catch { }
            return 0f;
        }

        private static void StopRecorder(ProfilerRecorder recorder)
        {
            try
            {
                if (recorder.IsRunning) recorder.Stop();
                recorder.Dispose();
            }
            catch { }
        }

        private string BuildCsvPath()
        {
            Directory.CreateDirectory(BenchmarkFiles.ResultsFolder);
            string fileName = $"{Sanitize(sceneName)}__{Sanitize(recordingName)}__run{runIndex}__{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return Path.Combine(BenchmarkFiles.ResultsFolder, fileName);
        }

        private void WriteCsv(string path)
        {
            var sb = new StringBuilder(4096 + rowCount * 160);
            sb.Append(CsvHeader).Append('\n');
            for (int i = 0; i < rowCount; i++)
            {
                int offset = i * ValueStride;
                sb.Append(frameIndices[i]);
                for (int c = 0; c < ValueStride; c++)
                {
                    sb.Append(',');
                    sb.Append(values[offset + c].ToString("F3", CultureInfo.InvariantCulture));
                }
                sb.Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
        }

        private BenchmarkRunSummary BuildSummary()
        {
            var summary = new BenchmarkRunSummary
            {
                scene = sceneName,
                recording = recordingName,
                runIndex = runIndex,
                frames = rowCount,
                buildType = GetBuildType(),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                markerNames = MarkerNames,
                markerTotalMs = new float[MarkerNames.Length],
            };
            if (rowCount == 0) return summary;

            var wall = new float[rowCount];
            double total = 0;
            int over33 = 0;
            int over50 = 0;
            for (int i = 0; i < rowCount; i++)
            {
                float wallMs = values[i * ValueStride];
                wall[i] = wallMs;
                total += wallMs;
                if (wallMs > FrameOver33Ms) over33++;
                if (wallMs > FrameOver50Ms) over50++;
                int offset = i * ValueStride;
                for (int m = 0; m < MarkerNames.Length; m++)
                    summary.markerTotalMs[m] += values[offset + 4 + m];
            }
            Array.Sort(wall);
            summary.totalWallMs = (float)total;
            summary.avgMs = (float)(total / rowCount);
            summary.p50Ms = Percentile(wall, 0.50f);
            summary.p95Ms = Percentile(wall, 0.95f);
            summary.p99Ms = Percentile(wall, 0.99f);
            summary.maxMs = wall[rowCount - 1];
            summary.framesOver33Ms = over33;
            summary.framesOver50Ms = over50;
            return summary;
        }

        private static float Percentile(float[] sorted, float fraction)
        {
            if (sorted.Length == 0) return 0f;
            float rank = fraction * (sorted.Length - 1);
            int low = (int)rank;
            int high = Math.Min(low + 1, sorted.Length - 1);
            return Mathf.Lerp(sorted[low], sorted[high], rank - low);
        }

        private static string GetBuildType()
        {
            if (Application.isEditor) return "Editor";
            return Debug.isDebugBuild ? "Development" : "Release";
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }
    }

    // JsonUtility summary of one benchmark run; all timings are wall-clock ms.
    [Serializable]
    public class BenchmarkRunSummary
    {
        public string scene;
        public string recording;
        public int runIndex;
        public int frames;
        public string buildType;
        public string timestamp;
        public float avgMs;
        public float p50Ms;
        public float p95Ms;
        public float p99Ms;
        public float maxMs;
        public int framesOver33Ms;
        public int framesOver50Ms;
        public float totalWallMs;
        public string[] markerNames;
        public float[] markerTotalMs;
    }
}
