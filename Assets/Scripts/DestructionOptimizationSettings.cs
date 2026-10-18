using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // Explicit per-scene opt-in; the original raid maps keep their existing destruction path.
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class DestructionOptimizationSettings : MonoBehaviour
    {
        private static readonly Dictionary<int, HashSet<DestructionOptimizationSettings>> scenes =
            new Dictionary<int, HashSet<DestructionOptimizationSettings>>();
        private int registeredScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => scenes.Clear();

        private void OnEnable()
        {
            registeredScene = gameObject.scene.handle;
            if (!scenes.TryGetValue(registeredScene, out var owners))
                scenes.Add(registeredScene, owners = new HashSet<DestructionOptimizationSettings>());
            owners.Add(this);
        }

        private void OnDisable()
        {
            if (!scenes.TryGetValue(registeredScene, out var owners)) return;
            owners.Remove(this);
            if (owners.Count == 0) scenes.Remove(registeredScene);
        }

        public static bool EnabledFor(Scene scene) => scene.IsValid() &&
            scenes.TryGetValue(scene.handle, out var owners) && owners.Count > 0;
    }
}
