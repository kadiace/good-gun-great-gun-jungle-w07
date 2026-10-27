using UnityEngine;

namespace WeaponWorkshop
{
    public sealed partial class DestructibleTarget
    {
        internal bool HasPendingCellCollisions => subdivided && collisionDirty;

        // Collider geometry can lag until the batch boundary. Resolve hits against current
        // occupied cells meanwhile; removed cells can never stop a following bullet.
        internal bool SweepOccupiedCells(Vector3 start, Quaternion rotation, Vector3 halfExtents, float distance,
            out float hitDistance, out Vector3 hitNormal, out Vector3 hitPoint)
        {
            hitDistance = float.PositiveInfinity; hitNormal = hitPoint = default;
            var candidates = CollectPenetrationCells(start, rotation, halfExtents, distance);
            PrepareCellQuery(start, rotation, halfExtents, out var query, out _, out _, out _);
            Vector3 direction = rotation * Vector3.forward;
            // Cheap reject before the 15-axis SAT: the cell bounding sphere versus the swept box
            // bounding sphere anywhere along the travel segment. Conservative only; cells that pass
            // keep the exact SAT result.
            float boxRadius = query.a.magnitude + query.b.magnitude + query.c.magnitude;
            foreach (int index in candidates)
            {
                var cell = preparedCells[index];
                Vector3 x = query.cellX * cell.localScale.x, y = query.cellY * cell.localScale.y, z = query.cellZ * cell.localScale.z;
                Vector3 offset = query.matrix.MultiplyPoint3x4(cell.localPosition) - start;
                float reach = boxRadius + x.magnitude + y.magnitude + z.magnitude;
                float along = Vector3.Dot(offset, direction);
                if (along < 0f) along = 0f; else if (along > distance) along = distance;
                if ((offset - direction * along).sqrMagnitude > reach * reach) continue;
                float enter = 0f, exit = distance;
                Vector3 normal = -direction;
                bool intersects = true;
                for (int axisIndex = 0; axisIndex < 15 && intersects; axisIndex++)
                {
                    Vector3 axis;
                    switch (axisIndex)
                    {
                        case 0: axis = Vector3.Cross(query.a, query.b); break;
                        case 1: axis = Vector3.Cross(query.b, query.c); break;
                        case 2: axis = Vector3.Cross(query.c, query.a); break;
                        case 3: axis = Vector3.Cross(x, y); break;
                        case 4: axis = Vector3.Cross(y, z); break;
                        case 5: axis = Vector3.Cross(z, x); break;
                        default:
                            int n = axisIndex - 6;
                            axis = Vector3.Cross(n / 3 == 0 ? query.a : n / 3 == 1 ? query.b : query.c,
                                n % 3 == 0 ? x : n % 3 == 1 ? y : z); break;
                    }
                    float magnitude = axis.magnitude;
                    if (magnitude < 1e-12f) continue;
                    axis /= magnitude;
                    float extent = Mathf.Abs(Vector3.Dot(axis, query.a)) + Mathf.Abs(Vector3.Dot(axis, query.b)) +
                        Mathf.Abs(Vector3.Dot(axis, query.c)) + Mathf.Abs(Vector3.Dot(axis, x)) +
                        Mathf.Abs(Vector3.Dot(axis, y)) + Mathf.Abs(Vector3.Dot(axis, z)) + .00001f;
                    float center = Vector3.Dot(axis, offset), speed = Vector3.Dot(axis, direction);
                    if (Mathf.Abs(speed) < 1e-8f) { intersects = Mathf.Abs(center) <= extent; continue; }
                    float a = (center - extent) / speed, b = (center + extent) / speed;
                    if (a > b) { float swap = a; a = b; b = swap; }
                    if (a > enter) { enter = a; normal = speed > 0f ? -axis : axis; }
                    exit = Mathf.Min(exit, b);
                    intersects = enter <= exit;
                }
                if (!intersects || enter >= hitDistance) continue;
                hitDistance = enter; hitNormal = normal;
            }
            if (float.IsPositiveInfinity(hitDistance)) return false;
            float support = Mathf.Abs(Vector3.Dot(hitNormal, query.a)) + Mathf.Abs(Vector3.Dot(hitNormal, query.b)) +
                Mathf.Abs(Vector3.Dot(hitNormal, query.c));
            hitPoint = start + direction * hitDistance - hitNormal * support;
            return true;
        }
    }
}
