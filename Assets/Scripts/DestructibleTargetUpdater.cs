using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace WeaponWorkshop
{
    [DefaultExecutionOrder(0)]
    [DisallowMultipleComponent]
    internal sealed class DestructibleTargetUpdater : MonoBehaviour
    {
        private static readonly ProfilerMarker updateMarker = new ProfilerMarker("DestructibleTargetUpdater.LateUpdate");
        // Calls per frame are the active target count shown in the Profiler's Calls column.
        private static readonly ProfilerMarker targetUpdateMarker = new ProfilerMarker("DestructibleTargetUpdater.TargetUpdate");
        private static DestructibleTargetUpdater instance;

        private readonly List<DestructibleTarget> targets = new List<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> registeredTargets = new HashSet<DestructibleTarget>();
        private readonly List<DestructibleTarget> movingTargets = new List<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> registeredMovingTargets = new HashSet<DestructibleTarget>();
        private readonly List<DestructibleTarget> renderTargets = new List<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> registeredRenderTargets = new HashSet<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> pendingRefresh = new HashSet<DestructibleTarget>();
        private bool updating;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebuildAfterSceneLoad()
        {
            DestructibleTargetUpdater updater = FindFirstObjectByType<DestructibleTargetUpdater>();
            if (updater == null) return;
            instance = updater;
            updater.ClearRegistry();
            foreach (DestructibleTarget target in FindObjectsByType<DestructibleTarget>(FindObjectsSortMode.None))
                if (target.isActiveAndEnabled) RefreshRegistration(target);
        }

        private void OnEnable()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            foreach (DestructibleTarget target in FindObjectsByType<DestructibleTarget>(FindObjectsSortMode.None))
                if (target.isActiveAndEnabled) RefreshRegistration(target);
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
            ClearRegistry();
        }

        internal static void RefreshRegistration(DestructibleTarget target)
        {
            if (!Application.isPlaying || ReferenceEquals(target, null)) return;
            if (!target.NeedsLateUpdate && !target.NeedsRenderUpdate)
            {
                if (instance != null) instance.QueueRefresh(target);
                return;
            }
            DestructibleTargetUpdater updater = GetOrCreate();
            if (updater != null) updater.QueueRefresh(target);
        }

        internal static void Unregister(DestructibleTarget target)
        {
            if (ReferenceEquals(target, null) || instance == null) return;
            instance.QueueRefresh(target);
        }

        internal static void RefreshMovingTargets()
        {
            if (instance == null) return;
            List<DestructibleTarget> movers = instance.movingTargets;
            for (int i = 0; i < movers.Count; i++)
            {
                DestructibleTarget target = movers[i];
                if (target != null && target.TracksDebrisTransform) target.RefreshDebrisIndex();
            }
        }

        private static DestructibleTargetUpdater GetOrCreate()
        {
            if (instance != null && instance.isActiveAndEnabled) return instance;
            instance = FindFirstObjectByType<DestructibleTargetUpdater>();
            if (instance != null) return instance;
            GameObject updaterObject = new GameObject("DestructibleTargetUpdater");
            DontDestroyOnLoad(updaterObject);
            instance = updaterObject.AddComponent<DestructibleTargetUpdater>();
            return instance;
        }

        private void QueueRefresh(DestructibleTarget target)
        {
            if (ReferenceEquals(target, null)) return;
            if (updating) pendingRefresh.Add(target);
            else ApplyRefresh(target);
        }

        private void ApplyRefresh(DestructibleTarget target)
        {
            bool active = target != null && target.NeedsLateUpdate;
            UpdateMembership(target, active, targets, registeredTargets);

            bool moving = active && target.TracksDebrisTransform;
            UpdateMembership(target, moving, movingTargets, registeredMovingTargets);
            // Drawing is independent of state updates: a settled subdivided target still has to draw its cells.
            UpdateMembership(target, target != null && target.NeedsRenderUpdate, renderTargets, registeredRenderTargets);
        }

        private static void UpdateMembership(DestructibleTarget target, bool shouldContain,
            List<DestructibleTarget> list, HashSet<DestructibleTarget> set)
        {
            if (shouldContain)
            {
                if (set.Add(target)) list.Add(target);
            }
            else if (set.Remove(target)) list.Remove(target);
        }

        private void ApplyPendingRefreshes()
        {
            foreach (DestructibleTarget target in pendingRefresh) ApplyRefresh(target);
            pendingRefresh.Clear();
        }

        private void ClearRegistry()
        {
            targets.Clear();
            registeredTargets.Clear();
            movingTargets.Clear();
            registeredMovingTargets.Clear();
            renderTargets.Clear(); registeredRenderTargets.Clear();
            pendingRefresh.Clear();
        }

        private void LateUpdate()
        {
            using (updateMarker.Auto())
            {
                updating = true;
                try
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        DestructibleTarget target = targets[i];
                        if (target == null || !target.isActiveAndEnabled)
                        {
                            pendingRefresh.Add(target);
                            continue;
                        }

                        try
                        {
                            using (targetUpdateMarker.Auto()) target.ProcessLateUpdate();
                        }
                        catch (System.Exception exception) { Debug.LogException(exception, target); }
                    }
                    for (int i = 0; i < renderTargets.Count; i++)
                    {
                        DestructibleTarget target = renderTargets[i];
                        if (target == null || !target.isActiveAndEnabled) { pendingRefresh.Add(target); continue; }
                        using (drawTargetMarker.Auto()) target.DrawLateUpdate();
                    }
                }
                finally
                {
                    updating = false;
                    ApplyPendingRefreshes();
                }
            }
        }
        private static readonly ProfilerMarker drawTargetMarker = new ProfilerMarker("DestructibleTargetUpdater.DrawRemainingChunks");
    }
}
