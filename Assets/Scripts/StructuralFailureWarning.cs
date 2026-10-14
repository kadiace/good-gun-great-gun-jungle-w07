using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class StructuralFailureWarning : MonoBehaviour
    {
        [SerializeField] private CubeAssembly assembly;
        [SerializeField] private string message = "연결되지 않은 모듈이 있습니다.";
        private bool wasVisible;

        private void Awake() => SetVisible(false);
        private void OnDisable() => SetVisible(false);
        private void Update() => SetVisible(assembly != null && assembly.isActiveAndEnabled && assembly.HasStructuralFailure());

        private void SetVisible(bool visible)
        {
            if (visible && !wasVisible) FeedbackUI.For(this)?.ShowNotification(message);
            wasVisible = visible;
        }
    }
}
