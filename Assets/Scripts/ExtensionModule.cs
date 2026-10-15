using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class ExtensionModule : MonoBehaviour
    {
        [SerializeField] private Vector3Int outputDirection = Vector3Int.forward;
        public Vector3Int OutputDirection
        {
            get
            {
                ModuleDraw draw = GetComponent<ModuleDraw>();
                return GetComponent<SplitModule>() == null && draw != null ? draw.ArrowDirection : outputDirection;
            }
        }

        private void OnEnable() => SetDirection(outputDirection);

        public void SetDirection(Vector3Int direction)
        {
            if (direction.sqrMagnitude != 1) return;
            bool changed = OutputDirection != direction;
            outputDirection = direction;
            ModuleDraw draw = GetComponent<ModuleDraw>();
            if (GetComponent<SplitModule>() != null) draw?.SetSplitAxis(direction);
            else draw?.SetDirection(direction);
            if (changed) GetComponentInParent<CubeAssembly>()?.NotifyStructureChanged();
        }
    }
}
