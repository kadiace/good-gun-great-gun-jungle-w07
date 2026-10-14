using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class ModuleDraw : MonoBehaviour
    {
        [SerializeField] private Transform drawRoot;
        public Transform DrawRoot => drawRoot;
        private Vector3Int direction = Vector3Int.forward;
        [SerializeField, Tooltip("설치 미리보기에서만 표시할 Arrow. Draw의 로컬 +Z를 출력 기준으로 사용합니다.")]
        private GameObject arrow;
        public Transform Arrow => arrow != null ? arrow.transform : null;

        private void Awake() => SetPreview(false);

        public void SetPreview(bool visible)
        {
            if (arrow != null) arrow.SetActive(visible);
        }

        public void SetDirection(Vector3Int direction)
        {
            if (direction.sqrMagnitude != 1) return;
            this.direction = direction;
            if (drawRoot == null) return;
            Vector3 up = Mathf.Abs(direction.y) == 1 ? Vector3.forward : Vector3.up;
            drawRoot.localRotation = Quaternion.LookRotation(direction, up);
        }

        public Vector3Int ArrowDirection => direction;

        public void SetSplitAxis(Vector3Int axis)
        {
            if (axis.sqrMagnitude != 1) return;
            direction = axis;
            if (drawRoot == null) return;
            // The two authored arrows point along Draw's local +/-X.
            drawRoot.localRotation = Quaternion.FromToRotation(Vector3.right, axis);
        }
    }
}
