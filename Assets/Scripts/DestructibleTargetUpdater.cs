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
        private readonly List<DestructibleTarget> occupancyPrewarm = new List<DestructibleTarget>();
        private readonly HashSet<DestructibleTarget> occupancyPrewarmSet = new HashSet<DestructibleTarget>();
        private const double OccupancyPrewarmBudgetMilliseconds = 0.75;
        // 공용 예산이 바닥난 프레임에도 지지 판정에 쓰는 최소 시간.
        private const double SupportMinSliceMilliseconds = 1.0;
        private int supportCursor;
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
            // Build cell occupancy during load so the first hit only activates prebuilt state.
            if (target.NeedsOccupancyPrewarm)
            {
                DestructibleTargetUpdater prewarmOwner = GetOrCreate();
                prewarmOwner.QueueOccupancyPrewarm(target);
            }
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

        private void QueueOccupancyPrewarm(DestructibleTarget target)
        {
            if (target == null || !target.NeedsOccupancyPrewarm) return;
            if (occupancyPrewarmSet.Add(target)) occupancyPrewarm.Add(target);
        }

        // Spread cell-table construction across a few frames instead of one load spike.
        private void ProcessOccupancyPrewarm()
        {
            if (occupancyPrewarm.Count == 0) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            double budgetTicks = System.Diagnostics.Stopwatch.Frequency * OccupancyPrewarmBudgetMilliseconds / 1000d;
            while (occupancyPrewarm.Count > 0)
            {
                DestructibleTarget target = occupancyPrewarm[0];
                if (target == null || !target.NeedsOccupancyPrewarm)
                {
                    occupancyPrewarmSet.Remove(target);
                    RemovePrewarmAt(0);
                    continue;
                }
                target.PrepareOccupancyTemplate();
                occupancyPrewarmSet.Remove(target);
                RemovePrewarmAt(0);
                if (System.Diagnostics.Stopwatch.GetTimestamp() - started >= budgetTicks) break;
            }
        }

        private void RemovePrewarmAt(int index)
        {
            int last = occupancyPrewarm.Count - 1;
            if (index != last) occupancyPrewarm[index] = occupancyPrewarm[last];
            occupancyPrewarm.RemoveAt(last);
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
            occupancyPrewarm.Clear(); occupancyPrewarmSet.Clear();
        }

        private void LateUpdate()
        {
            // 같은 프레임 이웃 파츠의 콜라이더 재빌드를 먼저 마쳐, 이미 사라진 이웃 셀이 지지를 붙들지 못하게 한다.
            PoolManager.FlushExplosionFinish();
            ProcessOccupancyPrewarm();
            using (updateMarker.Auto())
            {
                updating = true;
                try
                {
                    // 지지 판정은 공용 파괴 예산을 따른다. 예산이 바닥나도 SupportMinSliceMilliseconds와
                    // 표적 하나는 보장하고, 미룬 표적부터 다음 프레임에 시작해 뒤쪽 표적이 굶지 않게 한다.
                    int count = targets.Count;
                    int start = count > 0 ? supportCursor % count : 0;
                    int deferredAt = -1;
                    bool supportResolved = false;
                    long supportStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    long supportSliceTicks = (long)(System.Diagnostics.Stopwatch.Frequency * SupportMinSliceMilliseconds / 1000d);
                    for (int n = 0; n < count; n++)
                    {
                        int i = (start + n) % count;
                        DestructibleTarget target = targets[i];
                        if (target == null || !target.isActiveAndEnabled)
                        {
                            pendingRefresh.Add(target);
                            continue;
                        }

                        bool supportPending = target.SupportPending;
                        bool allowSupport = !supportResolved || !DestructionFrameBudget.IsExhausted ||
                            System.Diagnostics.Stopwatch.GetTimestamp() - supportStarted < supportSliceTicks;
                        if (supportPending)
                        {
                            if (allowSupport) supportResolved = true;
                            else if (deferredAt < 0) deferredAt = i;
                        }
                        try
                        {
                            using (targetUpdateMarker.Auto()) target.ProcessLateUpdate(allowSupport);
                        }
                        catch (System.Exception exception) { Debug.LogException(exception, target); }
                    }
                    supportCursor = deferredAt >= 0 ? deferredAt : 0;
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
