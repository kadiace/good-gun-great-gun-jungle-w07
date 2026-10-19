using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WeaponWorkshop.EditorTools
{
    // 벤치마크 리플레이 실행과 결과 CSV 비교 리포트 생성 메뉴. 런타임 스크립트(BenchmarkReplayRequest,
    // BenchmarkMetrics)의 결과물만 읽고 씬을 열어 플레이 모드로 넘긴다.
    internal static class BenchmarkMenu
    {
        private const string MenuRoot = "Tools/Benchmark/";
        private const string RunsPrefKey = "WeaponWorkshop.Benchmark.Runs";
        // 측정 구간 프로파일러 기록 토글. 켜면 리플레이 요청에 captureProfiler를 실어 보내고,
        // BenchmarkSession이 측정 시작 때 Profiler를 켜 끝날 때 ProfilerCaptures/<씬>_<시각>.data로 저장한다.
        private const string CaptureProfilerPrefKey = "WeaponWorkshop.Benchmark.CaptureProfiler";
        private const int DefaultRuns = 3;
        private const string OriginalScene = "Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity";
        private const string ImproveScene = "Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity";
        private const string OriginalSceneName = "NightRaid_JungleCampus";
        private const string ImproveSceneName = "NightRaid_JungleCampus_improve";
        private const string ReportPath = "Tools/Profiling/benchmark_compare.md";
        private const int FramesPerWindow = 60;
        private const float FrameOver33Ms = 33f;
        private const float FrameOver50Ms = 50f;

        private static int RunsPerReplay => EditorPrefs.GetInt(RunsPrefKey, DefaultRuns);

        private static bool CaptureProfiler => EditorPrefs.GetBool(CaptureProfilerPrefKey, false);

        [MenuItem(MenuRoot + "Capture Profiler To .data")]
        private static void ToggleCaptureProfiler()
        {
            EditorPrefs.SetBool(CaptureProfilerPrefKey, !CaptureProfiler);
        }

        [MenuItem(MenuRoot + "Capture Profiler To .data", true)]
        private static bool ValidateCaptureProfiler()
        {
            Menu.SetChecked(MenuRoot + "Capture Profiler To .data", CaptureProfiler);
            return true;
        }

        [MenuItem(MenuRoot + "Replay Latest In Original")]
        private static void ReplayLatestInOriginal() => ReplayLatest(OriginalScene);

        [MenuItem(MenuRoot + "Replay Latest In Improve")]
        private static void ReplayLatestInImprove() => ReplayLatest(ImproveScene);

        [MenuItem(MenuRoot + "Runs Per Replay/1")]
        private static void SetRuns1() => EditorPrefs.SetInt(RunsPrefKey, 1);

        [MenuItem(MenuRoot + "Runs Per Replay/3")]
        private static void SetRuns3() => EditorPrefs.SetInt(RunsPrefKey, 3);

        [MenuItem(MenuRoot + "Runs Per Replay/5")]
        private static void SetRuns5() => EditorPrefs.SetInt(RunsPrefKey, 5);

        [MenuItem(MenuRoot + "Runs Per Replay/1", true)]
        private static bool ValidateRuns1()
        {
            Menu.SetChecked(MenuRoot + "Runs Per Replay/1", RunsPerReplay == 1);
            return true;
        }

        [MenuItem(MenuRoot + "Runs Per Replay/3", true)]
        private static bool ValidateRuns3()
        {
            Menu.SetChecked(MenuRoot + "Runs Per Replay/3", RunsPerReplay == 3);
            return true;
        }

        [MenuItem(MenuRoot + "Runs Per Replay/5", true)]
        private static bool ValidateRuns5()
        {
            Menu.SetChecked(MenuRoot + "Runs Per Replay/5", RunsPerReplay == 5);
            return true;
        }

        [MenuItem(MenuRoot + "Compare Latest Results")]
        private static void CompareLatestResults() => Compare();

        [MenuItem(MenuRoot + "Open Benchmark Folder")]
        private static void OpenBenchmarkFolder()
        {
            Directory.CreateDirectory(BenchmarkFiles.Root);
            EditorUtility.RevealInFinder(BenchmarkFiles.Root);
        }

        private static void ReplayLatest(string scenePath)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("벤치마크 리플레이",
                    "플레이 모드에서는 리플레이를 시작할 수 없습니다. 플레이를 종료한 뒤 다시 실행하세요.", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string recording = BenchmarkFiles.LatestRecording();
            if (string.IsNullOrEmpty(recording))
            {
                EditorUtility.DisplayDialog("벤치마크 리플레이",
                    "녹화 파일이 없습니다. 먼저 게임에서 루트를 녹화하세요.", "확인");
                return;
            }
            BenchmarkReplayRequest.WritePending(recording, "", RunsPerReplay, true, CaptureProfiler);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private sealed class CsvEntry
        {
            public string Path;
            public string Scene;
            public string Recording;
            public DateTime Time;
        }

        private sealed class RunData
        {
            public string CsvPath;
            public string[] MarkerNames;
            public float[] WallMs;
            public float[] MarkerTotals;
            public string BuildType;
            public RunStats Stats;
        }

        private sealed class RunStats
        {
            public int Frames;
            public float AvgMs;
            public float P50Ms;
            public float P95Ms;
            public float P99Ms;
            public float MaxMs;
            public float TotalWallMs;
            public float FramesOver33;
            public float FramesOver50;
        }

        private sealed class SceneData
        {
            public RunData[] Runs;
            public string[] MarkerNames;
            public float[] FrameMedian;
            public float[] MarkerTotals;
            public RunStats MedianStats;
        }

        private static void Compare()
        {
            string resultsFolder = BenchmarkFiles.ResultsFolder;
            if (!Directory.Exists(resultsFolder))
            {
                EditorUtility.DisplayDialog("벤치마크 비교", $"결과 폴더가 없습니다.\n{resultsFolder}", "확인");
                return;
            }
            List<CsvEntry> entries = ScanResults(resultsFolder);
            if (entries.Count == 0)
            {
                EditorUtility.DisplayDialog("벤치마크 비교", "결과 폴더에 CSV가 없습니다. 먼저 리플레이를 실행하세요.", "확인");
                return;
            }

            var groups = new List<List<CsvEntry>>();
            var byRecording = new Dictionary<string, List<CsvEntry>>();
            foreach (var entry in entries)
            {
                if (!byRecording.TryGetValue(entry.Recording, out var group))
                {
                    group = new List<CsvEntry>();
                    byRecording[entry.Recording] = group;
                    groups.Add(group);
                }
                group.Add(entry);
            }
            groups.Sort((a, b) => LatestTime(b).CompareTo(LatestTime(a)));

            List<CsvEntry> chosen = groups[0];
            foreach (var group in groups)
            {
                if (HasScene(group, OriginalSceneName) && HasScene(group, ImproveSceneName))
                {
                    chosen = group;
                    break;
                }
            }
            if (!HasScene(chosen, OriginalSceneName) || !HasScene(chosen, ImproveSceneName))
            {
                string missing = !HasScene(chosen, OriginalSceneName) ? OriginalSceneName : ImproveSceneName;
                EditorUtility.DisplayDialog("벤치마크 비교",
                    $"가장 최근 녹화 '{chosen[0].Recording}'에 '{missing}' 씬의 결과가 없습니다.\n" +
                    "두 씬을 같은 녹화로 리플레이한 뒤 다시 비교하세요.", "확인");
                return;
            }

            RunData[] originalRuns = LoadRuns(chosen, OriginalSceneName);
            RunData[] improveRuns = LoadRuns(chosen, ImproveSceneName);
            if (originalRuns.Length == 0 || improveRuns.Length == 0)
            {
                EditorUtility.DisplayDialog("벤치마크 비교", "CSV를 읽지 못했습니다. 결과 파일 형식을 확인하세요.", "확인");
                return;
            }

            SceneData original = BuildSceneData(originalRuns);
            SceneData improve = BuildSceneData(improveRuns);
            WriteReport(chosen[0].Recording, original, improve);
        }

        private static List<CsvEntry> ScanResults(string folder)
        {
            var entries = new List<CsvEntry>();
            foreach (string path in Directory.GetFiles(folder, "*.csv"))
            {
                // <scene>__<recording>__run<N>__<timestamp>.csv
                string[] parts = Path.GetFileNameWithoutExtension(path).Split(new[] { "__" }, StringSplitOptions.None);
                if (parts.Length < 4) continue;
                if (!parts[parts.Length - 2].StartsWith("run", StringComparison.Ordinal)) continue;
                entries.Add(new CsvEntry
                {
                    Path = path,
                    Scene = parts[0],
                    Recording = string.Join("__", parts, 1, parts.Length - 3),
                    Time = File.GetLastWriteTimeUtc(path),
                });
            }
            return entries;
        }

        private static DateTime LatestTime(List<CsvEntry> group)
        {
            DateTime latest = DateTime.MinValue;
            foreach (var entry in group)
                if (entry.Time > latest) latest = entry.Time;
            return latest;
        }

        private static bool HasScene(List<CsvEntry> group, string sceneName)
        {
            foreach (var entry in group)
                if (entry.Scene == sceneName) return true;
            return false;
        }

        // 같은 녹화의 해당 씬 런을 최신 순으로 최대 설정 런 수만큼 파싱한다.
        private static RunData[] LoadRuns(List<CsvEntry> group, string sceneName)
        {
            var sceneEntries = new List<CsvEntry>();
            foreach (var entry in group)
                if (entry.Scene == sceneName) sceneEntries.Add(entry);
            sceneEntries.Sort((a, b) => b.Time.CompareTo(a.Time));

            int count = Math.Min(RunsPerReplay, sceneEntries.Count);
            var runs = new List<RunData>(count);
            for (int i = 0; i < count; i++)
            {
                RunData run = ParseCsv(sceneEntries[i].Path);
                if (run != null) runs.Add(run);
                else Debug.LogWarning($"[Benchmark] CSV 파싱 실패, 건너뜀: {sceneEntries[i].Path}");
            }
            return runs.ToArray();
        }

        // 헤더 이름으로 열을 찾아 wall_ms와 마커 합계를 읽는다.
        private static RunData ParseCsv(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2) return null;
            string[] header = lines[0].Split(',');
            int wallCol = IndexOf(header, "wall_ms");
            int gcCol = IndexOf(header, "gc_alloc_kb");
            if (wallCol < 0 || gcCol < 0 || gcCol + 1 >= header.Length) return null;

            int markerCount = header.Length - (gcCol + 1);
            var markerNames = new string[markerCount];
            for (int i = 0; i < markerCount; i++) markerNames[i] = header[gcCol + 1 + i];

            var wall = new List<float>(lines.Length - 1);
            var markerTotals = new float[markerCount];
            for (int l = 1; l < lines.Length; l++)
            {
                if (string.IsNullOrWhiteSpace(lines[l])) continue;
                string[] fields = lines[l].Split(',');
                if (fields.Length != header.Length) continue;
                if (!float.TryParse(fields[wallCol], NumberStyles.Float, CultureInfo.InvariantCulture, out float wallMs))
                    continue;
                wall.Add(wallMs);
                for (int m = 0; m < markerCount; m++)
                {
                    float.TryParse(fields[gcCol + 1 + m], NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
                    markerTotals[m] += value;
                }
            }
            if (wall.Count == 0) return null;

            var run = new RunData
            {
                CsvPath = path,
                MarkerNames = markerNames,
                WallMs = wall.ToArray(),
                MarkerTotals = markerTotals,
                BuildType = ReadBuildType(path),
            };
            run.Stats = ComputeStats(run.WallMs);
            return run;
        }

        private static string ReadBuildType(string csvPath)
        {
            string jsonPath = Path.ChangeExtension(csvPath, ".json");
            if (!File.Exists(jsonPath)) return null;
            var summary = JsonUtility.FromJson<BenchmarkRunSummary>(File.ReadAllText(jsonPath));
            return summary != null ? summary.buildType : null;
        }

        private static RunStats ComputeStats(float[] wallMs)
        {
            var sorted = (float[])wallMs.Clone();
            Array.Sort(sorted);
            double total = 0;
            int over33 = 0;
            int over50 = 0;
            foreach (float value in wallMs)
            {
                total += value;
                if (value > FrameOver33Ms) over33++;
                if (value > FrameOver50Ms) over50++;
            }
            return new RunStats
            {
                Frames = wallMs.Length,
                AvgMs = (float)(total / wallMs.Length),
                P50Ms = Percentile(sorted, 0.5f),
                P95Ms = Percentile(sorted, 0.95f),
                P99Ms = Percentile(sorted, 0.99f),
                MaxMs = sorted[sorted.Length - 1],
                TotalWallMs = (float)total,
                FramesOver33 = over33,
                FramesOver50 = over50,
            };
        }

        private static SceneData BuildSceneData(RunData[] runs)
        {
            int frameCount = int.MaxValue;
            foreach (var run in runs)
                frameCount = Math.Min(frameCount, run.WallMs.Length);

            var frameMedian = new float[frameCount];
            var frameBuffer = new float[runs.Length];
            for (int i = 0; i < frameCount; i++)
            {
                for (int r = 0; r < runs.Length; r++) frameBuffer[r] = runs[r].WallMs[i];
                frameMedian[i] = Median(frameBuffer);
            }

            int markerCount = runs[0].MarkerNames.Length;
            var markerTotals = new float[markerCount];
            var markerBuffer = new float[runs.Length];
            for (int m = 0; m < markerCount; m++)
            {
                for (int r = 0; r < runs.Length; r++)
                    markerBuffer[r] = m < runs[r].MarkerTotals.Length ? runs[r].MarkerTotals[m] : 0f;
                markerTotals[m] = Median(markerBuffer);
            }

            return new SceneData
            {
                Runs = runs,
                MarkerNames = runs[0].MarkerNames,
                FrameMedian = frameMedian,
                MarkerTotals = markerTotals,
                MedianStats = new RunStats
                {
                    Frames = Mathf.RoundToInt(MedianSelect(runs, r => r.Stats.Frames)),
                    AvgMs = MedianSelect(runs, r => r.Stats.AvgMs),
                    P50Ms = MedianSelect(runs, r => r.Stats.P50Ms),
                    P95Ms = MedianSelect(runs, r => r.Stats.P95Ms),
                    P99Ms = MedianSelect(runs, r => r.Stats.P99Ms),
                    MaxMs = MedianSelect(runs, r => r.Stats.MaxMs),
                    TotalWallMs = MedianSelect(runs, r => r.Stats.TotalWallMs),
                    FramesOver33 = MedianSelect(runs, r => r.Stats.FramesOver33),
                    FramesOver50 = MedianSelect(runs, r => r.Stats.FramesOver50),
                },
            };
        }

        private static float MedianSelect(RunData[] runs, Func<RunData, float> select)
        {
            var values = new float[runs.Length];
            for (int i = 0; i < runs.Length; i++) values[i] = select(runs[i]);
            return Median(values);
        }

        private static float Median(float[] values)
        {
            var sorted = (float[])values.Clone();
            Array.Sort(sorted);
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }

        private static float Percentile(float[] sorted, float fraction)
        {
            float rank = fraction * (sorted.Length - 1);
            int low = (int)rank;
            int high = Math.Min(low + 1, sorted.Length - 1);
            return Mathf.Lerp(sorted[low], sorted[high], rank - low);
        }

        private static int IndexOf(string[] values, string target)
        {
            for (int i = 0; i < values.Length; i++)
                if (string.Equals(values[i], target, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static void WriteReport(string recording, SceneData original, SceneData improve)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string reportPath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            int compareFrames = Math.Min(original.FrameMedian.Length, improve.FrameMedian.Length);
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine("# 벤치마크 비교 결과");
            sb.AppendLine();
            sb.AppendLine($"- 생성 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- 녹화: {recording}");
            sb.AppendLine($"- 런 수: {OriginalSceneName} {original.Runs.Length}개 / {ImproveSceneName} {improve.Runs.Length}개 (설정 최대 {RunsPerReplay}개)");
            string frameNote = original.FrameMedian.Length != improve.FrameMedian.Length
                ? $" (프레임 수가 달라 앞 {compareFrames}프레임만 비교)"
                : "";
            sb.AppendLine($"- 프레임 수: {original.FrameMedian.Length} / {improve.FrameMedian.Length}{frameNote}");
            sb.AppendLine($"- buildType: {DisplayBuildType(original)} / {DisplayBuildType(improve)}");
            sb.AppendLine();

            AppendSummary(sb, original, improve);
            AppendMarkers(sb, original, improve);
            AppendWorstFrames(sb, original, improve, compareFrames);
            AppendRegressions(sb, original, improve, compareFrames);
            AppendWorstWindows(sb, original, improve, compareFrames);
            AppendSources(sb, original, improve);

            File.WriteAllText(reportPath, sb.ToString());
            EditorUtility.RevealInFinder(reportPath);
            Debug.Log($"[Benchmark] 비교 리포트 저장: {reportPath}");
        }

        private static string DisplayBuildType(SceneData scene)
        {
            string first = scene.Runs[0].BuildType;
            if (string.IsNullOrEmpty(first)) return "알 수 없음";
            foreach (var run in scene.Runs)
                if (run.BuildType != first) return first + " 외 혼재";
            return first;
        }

        private static void AppendSummary(StringBuilder sb, SceneData original, SceneData improve)
        {
            var o = original.MedianStats;
            var i = improve.MedianStats;
            sb.AppendLine("## 요약 (런 중앙값)");
            sb.AppendLine();
            sb.AppendLine("| 항목 | Original | Improve | Δ | Δ% |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            Row(sb, "avg wall_ms", o.AvgMs, i.AvgMs);
            Row(sb, "p50 wall_ms", o.P50Ms, i.P50Ms);
            Row(sb, "p95 wall_ms", o.P95Ms, i.P95Ms);
            Row(sb, "p99 wall_ms", o.P99Ms, i.P99Ms);
            Row(sb, "max wall_ms", o.MaxMs, i.MaxMs);
            Row(sb, "frames > 33ms", o.FramesOver33, i.FramesOver33);
            Row(sb, "frames > 50ms", o.FramesOver50, i.FramesOver50);
            Row(sb, "total wall ms", o.TotalWallMs, i.TotalWallMs);
            sb.AppendLine();
        }

        private static void AppendMarkers(StringBuilder sb, SceneData original, SceneData improve)
        {
            sb.AppendLine("## 프로파일러 마커 누적 (전체 구간 합계 ms, 런 중앙값)");
            sb.AppendLine();
            sb.AppendLine("| 마커 | Original | Improve | Δ | Δ% |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            for (int m = 0; m < original.MarkerNames.Length; m++)
            {
                float o = original.MarkerTotals[m];
                float i = m < improve.MarkerTotals.Length ? improve.MarkerTotals[m] : 0f;
                Row(sb, original.MarkerNames[m], o, i);
            }
            sb.AppendLine();
        }

        private static void AppendWorstFrames(StringBuilder sb, SceneData original, SceneData improve, int compareFrames)
        {
            sb.AppendLine("## 최악 프레임 Top 15 (Original 프레임 중앙값 기준)");
            sb.AppendLine();
            sb.AppendLine("| 프레임 | Original ms | Improve ms | Δ ms |");
            sb.AppendLine("|---:|---:|---:|---:|");
            int[] order = new int[compareFrames];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => original.FrameMedian[b].CompareTo(original.FrameMedian[a]));
            for (int n = 0; n < order.Length && n < 15; n++)
            {
                int frame = order[n];
                float o = original.FrameMedian[frame];
                float i = improve.FrameMedian[frame];
                sb.AppendLine($"| {frame} | {F(o)} | {F(i)} | {F(i - o)} |");
            }
            sb.AppendLine();
        }

        private static void AppendRegressions(StringBuilder sb, SceneData original, SceneData improve, int compareFrames)
        {
            var regressions = new List<(int Frame, float Original, float Improve, float Delta)>();
            for (int i = 0; i < compareFrames; i++)
            {
                float delta = improve.FrameMedian[i] - original.FrameMedian[i];
                if (delta > 0f) regressions.Add((i, original.FrameMedian[i], improve.FrameMedian[i], delta));
            }
            regressions.Sort((a, b) => b.Delta.CompareTo(a.Delta));

            sb.AppendLine("## 후퇴 프레임 Top 15 (Δ = Improve − Original > 0)");
            sb.AppendLine();
            if (regressions.Count == 0)
            {
                sb.AppendLine("후퇴한 프레임이 없습니다.");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("| 프레임 | Original ms | Improve ms | Δ ms |");
            sb.AppendLine("|---:|---:|---:|---:|");
            for (int n = 0; n < regressions.Count && n < 15; n++)
            {
                var r = regressions[n];
                sb.AppendLine($"| {r.Frame} | {F(r.Original)} | {F(r.Improve)} | {F(r.Delta)} |");
            }
            sb.AppendLine();
        }

        private static void AppendWorstWindows(StringBuilder sb, SceneData original, SceneData improve, int compareFrames)
        {
            int windowCount = (compareFrames + FramesPerWindow - 1) / FramesPerWindow;
            var windows = new List<(int Index, int Frame, float OriginalMax, float ImproveMax)>(windowCount);
            for (int w = 0; w < windowCount; w++)
            {
                int start = w * FramesPerWindow;
                int end = Math.Min(start + FramesPerWindow, compareFrames);
                int worstFrame = start;
                float originalMax = -1f;
                float improveMax = -1f;
                for (int i = start; i < end; i++)
                {
                    if (original.FrameMedian[i] > originalMax)
                    {
                        originalMax = original.FrameMedian[i];
                        worstFrame = i;
                    }
                    if (improve.FrameMedian[i] > improveMax) improveMax = improve.FrameMedian[i];
                }
                windows.Add((w, worstFrame, originalMax, improveMax));
            }
            windows.Sort((a, b) => b.OriginalMax.CompareTo(a.OriginalMax));

            sb.AppendLine($"## 최악 1초 구간 Top 10 (Original 최대 프레임 기준, 1구간 = {FramesPerWindow}프레임)");
            sb.AppendLine();
            sb.AppendLine("| 구간 | 최악 프레임 | Original 최대 ms | Improve 최대 ms | Δ ms |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            for (int n = 0; n < windows.Count && n < 10; n++)
            {
                var w = windows[n];
                sb.AppendLine($"| {w.Index} | {w.Frame} | {F(w.OriginalMax)} | {F(w.ImproveMax)} | {F(w.ImproveMax - w.OriginalMax)} |");
            }
            sb.AppendLine();
        }

        private static void AppendSources(StringBuilder sb, SceneData original, SceneData improve)
        {
            sb.AppendLine("## 소스 파일");
            sb.AppendLine();
            foreach (var run in original.Runs)
                sb.AppendLine($"- {OriginalSceneName}: {Path.GetFileName(run.CsvPath)} ({run.Stats.Frames}프레임)");
            foreach (var run in improve.Runs)
                sb.AppendLine($"- {ImproveSceneName}: {Path.GetFileName(run.CsvPath)} ({run.Stats.Frames}프레임)");
            sb.AppendLine();
        }

        private static void Row(StringBuilder sb, string label, float original, float improve)
        {
            sb.AppendLine($"| {label} | {F(original)} | {F(improve)} | {F(improve - original)} | {Pct(original, improve)} |");
        }

        private static string F(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

        private static string Pct(float original, float improve)
        {
            if (original <= 0f) return "-";
            double pct = (improve - original) / original * 100.0;
            return pct.ToString("F1", CultureInfo.InvariantCulture) + "%";
        }
    }
}
