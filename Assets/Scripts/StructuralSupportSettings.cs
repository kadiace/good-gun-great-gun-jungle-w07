using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WeaponWorkshop
{
    // One per map scene. Maps without it keep the original support behavior.
    public sealed class StructuralSupportSettings : MonoBehaviour
    {
        private static readonly Dictionary<Scene, StructuralSupportSettings> byScene = new Dictionary<Scene, StructuralSupportSettings>();
        [SerializeField, Tooltip("파츠 안에서 자기 바닥줄과 끊긴 조각은 아래에서 받치는 파츠가 있을 때만 버팁니다. 옆·위 접촉은 지지로 보지 않습니다.")]
        private bool requireSupportFromBelow = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => byScene.Clear();
        private void Awake() => byScene[gameObject.scene] = this;
        private void OnDestroy()
        {
            if (byScene.TryGetValue(gameObject.scene, out var current) && current == this) byScene.Remove(gameObject.scene);
        }

        internal static bool RequireSupportFromBelow(GameObject target) =>
            byScene.TryGetValue(target.scene, out var settings) && settings != null && settings.requireSupportFromBelow;
    }
}
