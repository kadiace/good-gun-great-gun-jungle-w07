using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop.Editor
{
    // One-shot, explicitly requested authoring for the comparison campus scene. It never opens
    // or saves the source scene; all destructive edits are made in a separately copied scene.
    [InitializeOnLoad]
    internal static class CampusImproveAuthoringCoordinator
    {
        private const string SourcePath = "Assets/Scenes/NightRaid/NightRaid_JungleCampus.unity";
        private const string ImprovedPath = "Assets/Scenes/NightRaid/NightRaid_JungleCampus_improve.unity";
        private const string RequestPath = "Library/CampusImprove.request";
        private const string ResultPath = "Library/CampusImproveAuthoring.completed";
        private const int CellsPerAxis = 8;
        private const int MaximumCreatedChunks = 12000;
        private static double nextPoll;

        private sealed class Candidate
        {
            public DestructibleTarget target;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
            public BoxCollider collider;
            public Bounds bounds;
            public Vector3Int cells;
            public Vector3 localStep;
            public Vector3 parentLossyScale;
            public int chunks;
        }

        static CampusImproveAuthoringCoordinator() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 2d;
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;

            File.Delete(RequestPath);
            try
            {
                string report = Author();
                File.WriteAllText(ResultPath, report);
                Debug.Log("Campus improve authoring finished. See Library/CampusImproveAuthoring.completed.");
            }
            catch (Exception exception)
            {
                Scene failedScene = SceneManager.GetSceneByPath(ImprovedPath);
                if (failedScene.IsValid()) EditorSceneManager.CloseScene(failedScene, true);
                AssetDatabase.DeleteAsset(ImprovedPath);
                File.WriteAllText(ResultPath, "FAILED\n" + exception);
                Debug.LogException(exception);
            }
        }

        private static string Author()
        {
            if (!File.Exists(SourcePath)) throw new FileNotFoundException("Campus source scene is missing.", SourcePath);
            // Recovery for a prior failed run of this tool: its copied scene is the only asset at this
            // destination because the tool refuses to start when the destination exists initially.
            Scene previousAttempt = SceneManager.GetSceneByPath(ImprovedPath);
            if (previousAttempt.IsValid()) EditorSceneManager.CloseScene(previousAttempt, true);
            if (File.Exists(ImprovedPath)) AssetDatabase.DeleteAsset(ImprovedPath);
            if (File.Exists(ImprovedPath)) File.Delete(ImprovedPath);
            if (!AssetDatabase.CopyAsset(SourcePath, ImprovedPath)) throw new IOException("Could not copy the campus scene.");

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ImprovedPath, OpenSceneMode.Additive);
            var candidates = FindCandidates(scene, out int scanned, out int skipped);
            int outputChunks = 0;
            foreach (var candidate in candidates) outputChunks += candidate.chunks;
            if (outputChunks > MaximumCreatedChunks)
                throw new InvalidOperationException($"Preflight requires {outputChunks} chunks, over the safety limit {MaximumCreatedChunks}; copied scene was not modified.");

            int sourceCount = candidates.Count;
            foreach (var candidate in candidates) Split(candidate);
            var settingsObject = new GameObject("Destruction Optimization Settings");
            SceneManager.MoveGameObjectToScene(settingsObject, scene);
            settingsObject.AddComponent<DestructionOptimizationSettings>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ImprovedPath)) throw new IOException("Could not save the improved scene.");
            AddToBuildSettings(ImprovedPath);
            if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
            return $"scene={ImprovedPath}\nscannedTargets={scanned}\nsplitTargets={sourceCount}\ncreatedChunks={outputChunks}\nskippedTargets={skipped}\nchunkCellsPerAxis={CellsPerAxis}\nsourceSceneUntouched=true\n";
        }

        private static List<Candidate> FindCandidates(Scene scene, out int scanned, out int skipped)
        {
            scanned = 0;
            skipped = 0;
            var result = new List<Candidate>();
            foreach (var target in Resources.FindObjectsOfTypeAll<DestructibleTarget>())
            {
                if (target == null || target.gameObject.scene != scene) continue;
                scanned++;
                if (!TryCandidate(target, out var candidate)) { skipped++; continue; }
                result.Add(candidate);
            }
            return result;
        }

        private static bool TryCandidate(DestructibleTarget target, out Candidate candidate)
        {
            candidate = null;
            if (!target.enabled || !target.gameObject.activeInHierarchy || target.transform.parent == null ||
                target.GetComponent<Rigidbody>() != null || target.GetComponentInParent<Rigidbody>() != null ||
                PrefabUtility.IsPartOfPrefabInstance(target.gameObject)) return false;

            var transform = target.transform;
            var targetData = new SerializedObject(target);
            var filter = targetData.FindProperty("targetMesh").objectReferenceValue as MeshFilter;
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            var collider = filter != null ? filter.GetComponent<BoxCollider>() : null;
            if (filter == null || renderer == null || collider == null || filter.sharedMesh == null ||
                filter.GetComponents<Collider>().Length != 1 || !IsCubeMesh(filter.sharedMesh) ||
                !collider.enabled || collider.isTrigger) return false;

            // Do not detach gameplay scripts or clone any child hierarchy. A child mesh is eligible
            // only when its transform is aligned with the DestructibleTarget root.
            foreach (var component in target.GetComponents<Component>())
                if (component != null && !(component is Transform) && !(component is DestructibleTarget) &&
                    component != filter && component != renderer && component != collider) return false;
            foreach (var component in filter.GetComponents<Component>())
                if (component != null && !(component is Transform) && !(component is MeshFilter) &&
                    !(component is MeshRenderer) && !(component is BoxCollider)) return false;
            if (Quaternion.Angle(transform.rotation, filter.transform.rotation) > 0.01f) return false;

            // Keep the generated chunks affine and predictable; uncommon hierarchy transforms stay intact.
            Vector3 parentScale = transform.parent.lossyScale;
            if (!ApproximatelyUniform(parentScale) || transform.localScale.x <= 0f ||
                transform.localScale.y <= 0f || transform.localScale.z <= 0f ||
                filter.transform.lossyScale.x <= 0f || filter.transform.lossyScale.y <= 0f || filter.transform.lossyScale.z <= 0f) return false;

            Bounds bounds = filter.sharedMesh.bounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f || bounds.size.z <= 0f ||
                (collider.center - bounds.center).sqrMagnitude > 0.0001f ||
                (collider.size - bounds.size).sqrMagnitude > 0.0001f) return false;

            float partSize = Mathf.Max(0.01f, targetData.FindProperty("partSize").floatValue);
            Vector3 worldScale = filter.transform.lossyScale;
            Vector3 worldSize = Vector3.Scale(bounds.size, worldScale);
            Vector3Int cells = new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(worldSize.x / partSize - 0.00001f)),
                Mathf.Max(1, Mathf.CeilToInt(worldSize.y / partSize - 0.00001f)),
                Mathf.Max(1, Mathf.CeilToInt(worldSize.z / partSize - 0.00001f)));
            if (cells.x <= CellsPerAxis && cells.y <= CellsPerAxis && cells.z <= CellsPerAxis) return false;

            Vector3 localStep = new Vector3(partSize / worldScale.x, partSize / worldScale.y, partSize / worldScale.z);
            int nx = DivideRoundUp(cells.x, CellsPerAxis), ny = DivideRoundUp(cells.y, CellsPerAxis), nz = DivideRoundUp(cells.z, CellsPerAxis);
            candidate = new Candidate
            {
                target = target, meshFilter = filter, meshRenderer = renderer, collider = collider,
                bounds = bounds, cells = cells, localStep = localStep, parentLossyScale = transform.lossyScale,
                chunks = nx * ny * nz
            };
            return true;
        }

        private static bool IsCubeMesh(Mesh mesh)
        {
            if (mesh.vertexCount < 8 || mesh.vertexCount > 32 || mesh.triangles.Length != 36) return false;
            Bounds b = mesh.bounds;
            return (b.center).sqrMagnitude < 0.0001f && (b.size - Vector3.one).sqrMagnitude < 0.0001f;
        }

        private static bool ApproximatelyUniform(Vector3 value) =>
            Mathf.Abs(Mathf.Abs(value.x) - Mathf.Abs(value.y)) < 0.0001f &&
            Mathf.Abs(Mathf.Abs(value.y) - Mathf.Abs(value.z)) < 0.0001f;

        private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;

        private static void Split(Candidate candidate)
        {
            DestructibleTarget source = candidate.target;
            Bounds bounds = candidate.bounds;
            Vector3 step = candidate.localStep;
            Vector3Int cells = candidate.cells;
            var sourceSerialized = new SerializedObject(source);
            float partSize = Mathf.Max(0.01f, sourceSerialized.FindProperty("partSize").floatValue);
            int index = 0;
            for (int x = 0; x < cells.x; x += CellsPerAxis)
                for (int y = 0; y < cells.y; y += CellsPerAxis)
                    for (int z = 0; z < cells.z; z += CellsPerAxis)
                    {
                        int countX = Mathf.Min(CellsPerAxis, cells.x - x);
                        int countY = Mathf.Min(CellsPerAxis, cells.y - y);
                        int countZ = Mathf.Min(CellsPerAxis, cells.z - z);
                        Vector3 offset = new Vector3(x * step.x, y * step.y, z * step.z);
                        Vector3 size = Vector3.Min(Vector3.Scale(new Vector3(countX, countY, countZ), step), bounds.size - offset);
                        Vector3 center = bounds.min + offset + size * 0.5f;

                        var chunk = new GameObject($"{source.name}_Chunk_{index++:D3}");
                        chunk.layer = source.gameObject.layer;
                        chunk.tag = source.gameObject.tag;
                        chunk.transform.SetParent(source.transform, false);
                        chunk.transform.localPosition = source.transform.InverseTransformPoint(candidate.meshFilter.transform.TransformPoint(center));
                        chunk.transform.localRotation = Quaternion.identity;
                        Vector3 ratio = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
                        Vector3 worldChunkScale = Vector3.Scale(candidate.meshFilter.transform.lossyScale, ratio);
                        chunk.transform.localScale = new Vector3(worldChunkScale.x / candidate.parentLossyScale.x,
                            worldChunkScale.y / candidate.parentLossyScale.y, worldChunkScale.z / candidate.parentLossyScale.z);
                        chunk.isStatic = source.gameObject.isStatic;
                        GameObjectUtility.SetStaticEditorFlags(chunk, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
                        var filter = chunk.AddComponent<MeshFilter>();
                        filter.sharedMesh = candidate.meshFilter.sharedMesh;
                        var renderer = chunk.AddComponent<MeshRenderer>();
                        EditorUtility.CopySerialized(candidate.meshRenderer, renderer);
                        var collider = chunk.AddComponent<BoxCollider>();
                        EditorUtility.CopySerialized(candidate.collider, collider);
                        collider.center = Vector3.zero;
                        collider.size = Vector3.one;
                        var target = chunk.AddComponent<DestructibleTarget>();
                        EditorUtility.CopySerialized(source, target);
                        var targetData = new SerializedObject(target);
                        targetData.FindProperty("targetMesh").objectReferenceValue = filter;
                        targetData.FindProperty("targetId").stringValue = Guid.NewGuid().ToString("N");
                        targetData.FindProperty("partSize").floatValue = partSize;
                        targetData.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(chunk);
                    }

            source.enabled = false;
            candidate.meshRenderer.enabled = false;
            candidate.collider.enabled = false;
            EditorUtility.SetDirty(source.gameObject);
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(entry => entry.path == scenePath);
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
