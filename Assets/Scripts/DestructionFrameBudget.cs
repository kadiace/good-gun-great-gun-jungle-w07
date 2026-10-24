using System.Diagnostics;
using UnityEngine;

namespace WeaponWorkshop
{
    // Destruction work shares one frame window. Logical cell changes stay immediate; deferred
    // presentation work checks this budget so independent systems cannot stack their own limits
    // into one spike.
    internal static class DestructionFrameBudget
    {
        internal const float FrameBudgetMilliseconds = 4f;
        // Explosion steps leave this much for support, mesh, and log work before they stop.
        internal const float PresentationReserveMilliseconds = 1f;
        private static int frame = -1;
        private static long frameStartTicks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            frame = -1;
            frameStartTicks = 0;
        }

        private static void BeginFrameIfNeeded()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            frameStartTicks = Stopwatch.GetTimestamp();
        }

        internal static float RemainingMilliseconds
        {
            get
            {
                BeginFrameIfNeeded();
                double elapsed = (Stopwatch.GetTimestamp() - frameStartTicks) * 1000d / Stopwatch.Frequency;
                return Mathf.Max(0f, FrameBudgetMilliseconds - (float)elapsed);
            }
        }

        internal static bool IsExhausted => RemainingMilliseconds <= 0f;
    }
}
