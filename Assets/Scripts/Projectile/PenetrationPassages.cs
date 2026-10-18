using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Keep hit history for the whole shot, but search cells only while a passage overlaps it.
    internal sealed class PenetrationPassages
    {
        private struct Passage
        {
            public bool carve, covered;
            public Vector3 origin, direction;
            public Quaternion rotation;
            public float length;
            public Matrix4x4 geometry;
        }

        private readonly Dictionary<DestructibleTarget, Passage> history = new();
        private readonly HashSet<DestructibleTarget> active = new();
        private readonly List<DestructibleTarget> retired = new();

        public bool Contains(DestructibleTarget target) => history.ContainsKey(target);

        public void Record(DestructibleTarget target, bool carve, Vector3 start, Quaternion rotation, float coveredDistance)
        {
            history[target] = new Passage
            {
                carve = carve, covered = carve, origin = start, direction = rotation * Vector3.forward,
                rotation = rotation, length = coveredDistance, geometry = target.PenetrationGeometryMatrix
            };
            if (carve) active.Add(target);
        }

        // Casts can reactivate a prior passage if homing or a moving target brings it back.
        public bool Ignore(DestructibleTarget target)
        {
            if (target == null || !history.TryGetValue(target, out var passage)) return false;
            if (passage.carve) active.Add(target);
            return true;
        }

        public void Carve(Vector3 start, Quaternion rotation, Vector3 halfExtents, float distance)
        {
            if (active.Count == 0) return;
            distance = Mathf.Max(0f, distance);
            Vector3 direction = rotation * Vector3.forward;
            Vector3 end = start + direction * distance;
            Vector3 extents = Abs(rotation * Vector3.right * halfExtents.x)
                + Abs(rotation * Vector3.up * halfExtents.y) + Abs(direction * halfExtents.z);
            // Include the same contact tolerance as the exact voxel intersection test.
            extents += Vector3.one * 0.0001f;
            var endBounds = new Bounds(end, extents * 2f);
            var sweepBounds = new Bounds((start + end) * 0.5f, Abs(end - start) + extents * 2f);
            retired.Clear();
            foreach (var target in active)
            {
                if (target == null || !target.TryDebrisBounds(out var bounds) || !bounds.Intersects(sweepBounds))
                {
                    retired.Add(target);
                    continue;
                }
                var passage = history[target];
                var geometry = target.PenetrationGeometryMatrix;
                float skip = 0f;
                Vector3 offset = start - passage.origin;
                float along = Vector3.Dot(offset, passage.direction);
                bool continues = passage.covered && passage.geometry.Equals(geometry)
                    && rotation.Equals(passage.rotation)
                    && (offset - passage.direction * along).sqrMagnitude < 1e-10f
                    && along >= 0f && along <= passage.length;
                if (continues) skip = Mathf.Min(distance, passage.length - along);
                if (!continues || skip < distance)
                {
                    target.DestroyPenetrationCells(start + direction * skip, rotation, halfExtents, distance - skip);
                    if (continues) passage.length = Mathf.Max(passage.length, along + distance);
                    else
                    {
                        passage.covered = true; passage.origin = start; passage.direction = direction;
                        passage.rotation = rotation; passage.length = distance; passage.geometry = geometry;
                    }
                    history[target] = passage;
                }
                if (!bounds.Intersects(endBounds)) retired.Add(target);
            }
            foreach (var target in retired) active.Remove(target);
            retired.Clear();
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        public void Clear()
        {
            history.Clear(); active.Clear(); retired.Clear();
        }
    }
}
