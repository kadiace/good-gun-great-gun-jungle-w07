using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // Shared broadphase: one transform scan per frame, never one scan per fragment.
    internal static class DebrisTargetIndex
    {
        private sealed class Entry
        {
            public Bounds bounds;
            public Vector3Int min, max;
        }
        private const float Size = 3f;
        private static readonly Dictionary<DestructibleTarget, Entry> entries = new Dictionary<DestructibleTarget, Entry>();
        private static readonly Dictionary<Vector3Int, HashSet<DestructibleTarget>> bins = new Dictionary<Vector3Int, HashSet<DestructibleTarget>>();
        private static readonly Stack<HashSet<DestructibleTarget>> freeBins = new Stack<HashSet<DestructibleTarget>>();
        private static int frame = -1;
        internal static bool HasObstacles => bins.Count > 0;
        private static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x/Size), Mathf.FloorToInt(p.y/Size), Mathf.FloorToInt(p.z/Size));
        internal static void Sync()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            DestructibleTargetUpdater.RefreshMovingTargets();
        }
        internal static void Update(DestructibleTarget target)
        {
            if (target == null || !target.TryDebrisBounds(out var bounds)) { Remove(target); return; }
            var min = Key(bounds.min); var max = Key(bounds.max);
            if (!entries.TryGetValue(target, out var entry))
            {
                entry = new Entry(); entries.Add(target, entry);
            }
            else if (entry.min == min && entry.max == max)
            {
                entry.bounds = bounds; return;
            }
            else Unlink(target, entry);
            entry.bounds = bounds; entry.min = min; entry.max = max;
            for (int x=min.x;x<=max.x;x++) for(int y=min.y;y<=max.y;y++) for(int z=min.z;z<=max.z;z++)
            {
                var key = new Vector3Int(x,y,z);
                if (!bins.TryGetValue(key,out var bin)) { bin=freeBins.Count>0?freeBins.Pop():new HashSet<DestructibleTarget>(); bins.Add(key,bin); }
                bin.Add(target);
            }
        }
        private static void Unlink(DestructibleTarget target, Entry entry)
        {
            for(int x=entry.min.x;x<=entry.max.x;x++) for(int y=entry.min.y;y<=entry.max.y;y++) for(int z=entry.min.z;z<=entry.max.z;z++)
            {
                var key=new Vector3Int(x,y,z);
                if(!bins.TryGetValue(key,out var bin))continue;
                bin.Remove(target);
                if(bin.Count==0){bins.Remove(key);freeBins.Push(bin);}
            }
        }
        internal static void Remove(DestructibleTarget target)
        {
            if (ReferenceEquals(target,null) || !entries.TryGetValue(target,out var entry)) return;
            Unlink(target,entry); entries.Remove(target);
        }
        internal static void Query(Bounds bounds, HashSet<DestructibleTarget> result)
        {
            Sync(); result.Clear(); var min=Key(bounds.min);var max=Key(bounds.max);
            // Avoid enormous empty-bin walks for an unusually long delayed segment.
            long volume=(long)(max.x-min.x+1)*(max.y-min.y+1)*(max.z-min.z+1);
            if(volume>4096)
            {
                foreach(var pair in entries) if(pair.Value.bounds.Intersects(bounds))result.Add(pair.Key);
                return;
            }
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)
                if(bins.TryGetValue(new Vector3Int(x,y,z),out var bin))
                    foreach(var target in bin) if(entries[target].bounds.Intersects(bounds))result.Add(target);
        }
        internal static bool Near(Vector3 position,float radius)
        {
            var min=Key(position-Vector3.one*radius);var max=Key(position+Vector3.one*radius);
            for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)for(int z=min.z;z<=max.z;z++)
                if(bins.TryGetValue(new Vector3Int(x,y,z),out var bin))
                    foreach(var target in bin) if(entries[target].bounds.SqrDistance(position)<=radius*radius)return true;
            return false;
        }
    }
}
