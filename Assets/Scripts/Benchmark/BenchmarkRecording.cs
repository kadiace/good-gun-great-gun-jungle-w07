using System;
using System.IO;
using UnityEngine;

namespace WeaponWorkshop
{
    // One recorded benchmark route. Frame i is the state at the end of frame i (LateUpdate),
    // captured while Time.captureDeltaTime == fixedDeltaTime, so frame index == game time.
    [Serializable]
    public sealed class BenchmarkRecording
    {
        public const int CurrentVersion = 1;
        public const byte AttackBit = 1 << 0;
        public const byte AimBit = 1 << 1;

        public int version = CurrentVersion;
        public string sourceScene;
        public int seed;
        public float fixedDeltaTime = 1f / 60f;
        public bool enemiesDisabled = true;
        public WeaponWorldSave weapon;
        public BenchmarkFrame[] frames = Array.Empty<BenchmarkFrame>();
    }

    [Serializable]
    public struct BenchmarkFrame
    {
        public Vector3 position;
        public float yaw;
        public float pitch;
        public byte buttons;
    }

    public static class BenchmarkFiles
    {
        public static string Root => Path.Combine(Application.persistentDataPath, "Benchmark");
        public static string RecordingsFolder => Path.Combine(Root, "Recordings");
        public static string ResultsFolder => Path.Combine(Root, "Results");

        public static string Save(BenchmarkRecording recording)
        {
            Directory.CreateDirectory(RecordingsFolder);
            string path = Path.Combine(RecordingsFolder,
                $"{recording.sourceScene}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, JsonUtility.ToJson(recording));
            return path;
        }

        public static bool TryLoad(string path, out BenchmarkRecording recording)
        {
            recording = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            recording = JsonUtility.FromJson<BenchmarkRecording>(File.ReadAllText(path));
            return recording != null && recording.version == BenchmarkRecording.CurrentVersion &&
                recording.frames != null && recording.frames.Length > 0;
        }

        public static string LatestRecording()
        {
            if (!Directory.Exists(RecordingsFolder)) return null;
            string latest = null;
            DateTime latestTime = DateTime.MinValue;
            foreach (string file in Directory.GetFiles(RecordingsFolder, "*.json"))
            {
                DateTime time = File.GetLastWriteTimeUtc(file);
                if (time > latestTime) { latestTime = time; latest = file; }
            }
            return latest;
        }
    }
}
