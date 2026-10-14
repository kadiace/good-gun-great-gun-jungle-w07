using UnityEngine;

namespace WeaponWorkshop
{
    [RequireComponent(typeof(PlacedBlock))]
    public sealed class SplitModule : MonoBehaviour
    {
        public Vector3Int[] GetOutputDirections()
        {
            PlacedBlock block = GetComponent<PlacedBlock>();
            if (block.Kind == BlockKind.SplitTwo)
            {
                ExtensionModule orientation = GetComponent<ExtensionModule>();
                Vector3Int axis = orientation != null ? orientation.OutputDirection : Vector3Int.right;
                return new[] { axis, -axis };
            }
            Vector3Int normal = block.OrientationNormal;
            Vector3 up = Mathf.Abs(normal.y) == 1 ? Vector3.forward : Vector3.up;
            Vector3Int right = Vector3Int.RoundToInt(Vector3.Cross(up, normal).normalized);
            Vector3Int vertical = Vector3Int.RoundToInt(Vector3.Cross(normal, right).normalized);
            return new[] { right, -right, vertical, -vertical };
        }
    }
}