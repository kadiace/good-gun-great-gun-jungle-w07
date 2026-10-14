using UnityEngine;
using UnityEngine.UI;

namespace WeaponWorkshop
{
    // 간판처럼 벽에 붙은 표시물: 자기 위치 뒤의 벽 칸이 사라지면 같이 사라지고, 벽이 복구되면 다시 보인다.
    public sealed class BreakWithWall : MonoBehaviour
    {
        [SerializeField] private DestructibleTarget wall;
        private Graphic graphic;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            if (graphic != null) graphic.enabled = true;
        }

        private void LateUpdate()
        {
            if (wall == null || graphic == null || !wall.isActiveAndEnabled) return;
            bool attached = wall.HasCellAt(transform.position);
            if (graphic.enabled != attached) graphic.enabled = attached;
        }
    }
}
