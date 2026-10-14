using System.Collections.Generic;
using UnityEngine;
namespace WeaponWorkshop
{
    // Static walls use a captured 2D grid; live destructible targets use the shared spatial index.
    internal sealed class DebrisWallCache
    {
        private struct Wall { public Vector3 center, half; public Quaternion rotation, inverse; public Bounds bounds; }
        private readonly List<Wall> walls = new List<Wall>();
        private readonly HashSet<DestructibleTarget> targets = new HashSet<DestructibleTarget>();
        private List<int>[] cells = System.Array.Empty<List<int>>();
        private int[] stamps = System.Array.Empty<int>();
        private int columns, stamp;
        private Vector2 minimum;
        private const float CellSize = 2f;
        private bool hasPriorityWalls;
        internal bool HasNearbyObstacles => hasPriorityWalls || DebrisTargetIndex.HasObstacles;
        public void Clear() { walls.Clear(); targets.Clear(); hasPriorityWalls=false; foreach(var cell in cells) cell?.Clear(); }
        internal bool Near(Vector3 position, float radius)
        {
            if (DebrisTargetIndex.Near(position,radius)) return true;
            Range(position-Vector3.one*radius,position+Vector3.one*radius,out int x0,out int x1,out int z0,out int z1);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                var list=cells[z*columns+x]; if(list==null)continue;
                foreach(int index in list) if(walls[index].half.y>.1f && walls[index].bounds.SqrDistance(position)<=radius*radius)return true;
            }
            return false;
        }
        public void Capture(Transform reference, float floor, float radius)
        {
            Clear();
            if (reference == null || radius <= 0f) return;
            columns = Mathf.Max(1, Mathf.CeilToInt(radius * 2f / CellSize));
            if (cells.Length < columns * columns) System.Array.Resize(ref cells, columns * columns);
            minimum = new Vector2(reference.position.x - radius, reference.position.z - radius);
            var center = reference.position; center.y = (center.y + floor) * 0.5f;
            var half = new Vector3(radius, Mathf.Abs(reference.position.y - floor) * 0.5f + radius, radius);
            var area = new Bounds(center, half * 2f);
            // Only static walls are kept; scanning thousands of destructible colliders per cloud is avoided.
            foreach (var box in StaticDebrisWalls.Boxes)
            {
                if (box == null || !box.enabled || !box.gameObject.activeInHierarchy || box.attachedRigidbody != null ||
                    !area.Intersects(box.bounds) || box.transform.IsChildOf(reference)) continue;
                Vector3 scale = box.transform.lossyScale;
                var wall = new Wall {center=box.transform.TransformPoint(box.center), rotation=box.transform.rotation,
                    inverse=Quaternion.Inverse(box.transform.rotation),
                    half=Vector3.Scale(box.size*0.5f,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))),
                    bounds=box.bounds};
                int index=walls.Count; walls.Add(wall);
                if(wall.half.y>.1f)hasPriorityWalls=true;
                Range(wall.bounds.min,wall.bounds.max,out int x0,out int x1,out int z0,out int z1);
                for(int z=z0;z<=z1;z++) for(int x=x0;x<=x1;x++)
                {int cell=z*columns+x; if(cells[cell]==null) cells[cell]=new List<int>(); cells[cell].Add(index);}
            }
            if(stamps.Length<walls.Count) System.Array.Resize(ref stamps,walls.Count);
        }
        private void Range(Vector3 min,Vector3 max,out int x0,out int x1,out int z0,out int z1)
        {
            x0=Mathf.Max(0,Mathf.FloorToInt((min.x-minimum.x)/CellSize));
            x1=Mathf.Min(columns-1,Mathf.FloorToInt((max.x-minimum.x)/CellSize));
            z0=Mathf.Max(0,Mathf.FloorToInt((min.z-minimum.y)/CellSize));
            z1=Mathf.Min(columns-1,Mathf.FloorToInt((max.z-minimum.y)/CellSize));
        }
        public void Resolve(Vector3 previous,ref Vector3 position,ref Vector3 velocity,float radius,float dt,float bounce)
        {
            if(dt<=0f) return;
            var from=previous; var to=position; float remainingTime=dt;
            for(int contact=0;contact<2;contact++)
            {
                if(!FindHit(from,to,radius,out float time,out Vector3 normal,out Vector3 point)) break;
                from=point+normal*0.002f;
                float incoming=Vector3.Dot(velocity,normal);
                if(incoming<0f) velocity-=normal*((1f+bounce)*incoming);
                velocity*=0.8f; remainingTime*=1f-time;
                to=from+velocity*remainingTime;
            }
            position=to;
        }
        private bool FindHit(Vector3 from,Vector3 to,float radius,out float best,out Vector3 normal,out Vector3 point)
        {
            best=2f;normal=point=default;
            var pad=Vector3.one*radius;
            Range(Vector3.Min(from,to)-pad,Vector3.Max(from,to)+pad,out int x0,out int x1,out int z0,out int z1);
            if(++stamp==int.MaxValue){System.Array.Clear(stamps,0,stamps.Length);stamp=1;}
            for(int z=z0;z<=z1;z++) for(int x=x0;x<=x1;x++)
            {
                var candidates=cells[z*columns+x]; if(candidates==null)continue;
                foreach(int index in candidates)
                {
                    if(stamps[index]==stamp)continue;stamps[index]=stamp;
                    var wall=walls[index]; var bounds=wall.bounds; bounds.Expand(radius*2f);
                    if(!bounds.Intersects(new Bounds((from+to)*0.5f, new Vector3(Mathf.Abs(to.x-from.x),Mathf.Abs(to.y-from.y),Mathf.Abs(to.z-from.z)))))continue;
                    if(!Sweep(wall,from,to,radius,out float t,out Vector3 n,out Vector3 hit) ||
                        Mathf.Abs(n.y)>0.5f || t>=best)continue;
                    best=t;normal=n;point=hit;
                }
            }
            var search = new Bounds((from+to)*.5f, Vector3.Max(from,to)-Vector3.Min(from,to)+pad*2f);
            DebrisTargetIndex.Query(search,targets);
            foreach (var target in targets)
            {
                if (target == null || !target.isActiveAndEnabled) continue;
                if (!target.SweepDebris(from, to, radius, out float t, out var n, out var hit) || t >= best) continue;
                best = t; normal = n; point = hit;
            }
            return best<=1f;
        }
        // Static non-destructible box colliders, collected once per loaded scene set.
        private static class StaticDebrisWalls
        {
            private static readonly List<BoxCollider> boxes = new List<BoxCollider>();
            private static bool dirty = true;
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void Reset()
            {
                boxes.Clear(); dirty = true;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Loaded;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += Loaded;
                UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= Unloaded;
                UnityEngine.SceneManagement.SceneManager.sceneUnloaded += Unloaded;
            }
            private static void Loaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => dirty = true;
            private static void Unloaded(UnityEngine.SceneManagement.Scene scene) => dirty = true;
            internal static List<BoxCollider> Boxes
            {
                get
                {
                    if (!dirty) return boxes;
                    dirty = false; boxes.Clear();
                    int excluded = LayerMask.GetMask("Energy", "Weapon");
                    foreach (var box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (!box.isTrigger && (excluded & (1 << box.gameObject.layer)) == 0 &&
                            box.GetComponentInParent<DestructibleTarget>(true) == null) boxes.Add(box);
                    return boxes;
                }
            }
        }
        internal static bool SweepBounds(Bounds bounds, Vector3 from, Vector3 to,
            out float time, out Vector3 normal, out Vector3 point)
        {
            return Sweep(new Wall { center = bounds.center, half = bounds.extents,
                rotation = Quaternion.identity, inverse = Quaternion.identity }, from, to, 0f,
                out time, out normal, out point);
        }
        internal static bool ClipBounds(Bounds bounds,Vector3 from,Vector3 to,out float enter,out float exit)
        {
            enter=0f;exit=1f;var delta=to-from;
            for(int axis=0;axis<3;axis++)
            {
                if(Mathf.Abs(delta[axis])<.000001f){if(from[axis]<bounds.min[axis]||from[axis]>bounds.max[axis])return false;continue;}
                float a=(bounds.min[axis]-from[axis])/delta[axis],b=(bounds.max[axis]-from[axis])/delta[axis];
                if(a>b){float swap=a;a=b;b=swap;}
                enter=Mathf.Max(enter,a);exit=Mathf.Min(exit,b);if(enter>exit)return false;
            }
            return true;
        }
        private static bool Sweep(Wall wall,Vector3 from,Vector3 to,float radius,out float time,out Vector3 normal,out Vector3 point)
        {
            var start=wall.inverse*(from-wall.center);var delta=wall.inverse*(to-from);
            var half=wall.half+Vector3.one*radius;
            time=0f;normal=point=default;float exit=1f;
            bool inside=Mathf.Abs(start.x)<half.x&&Mathf.Abs(start.y)<half.y&&Mathf.Abs(start.z)<half.z;
            if(inside)
            {
                int axis=0;float distance=half.x-Mathf.Abs(start.x);
                for(int i=1;i<3;i++) if(half[i]-Mathf.Abs(start[i])<distance){axis=i;distance=half[i]-Mathf.Abs(start[i]);}
                Vector3 n=Vector3.zero;n[axis]=start[axis]>=0f?1f:-1f;
                var projected=start;projected[axis]=n[axis]*half[axis];
                normal=wall.rotation*n;point=wall.center+wall.rotation*projected;return true;
            }
            Vector3 entryNormal=Vector3.zero;
            for(int axis=0;axis<3;axis++)
            {
                if(Mathf.Abs(delta[axis])<0.000001f){if(Mathf.Abs(start[axis])>half[axis])return false;continue;}
                float a=(-half[axis]-start[axis])/delta[axis],b=(half[axis]-start[axis])/delta[axis];
                if(a>b){float swap=a;a=b;b=swap;}
                if(a>=time){time=a;entryNormal=Vector3.zero;entryNormal[axis]=delta[axis]>0f?-1f:1f;}
                exit=Mathf.Min(exit,b);if(time>exit)return false;
            }
            if(time<0f||time>1f||entryNormal.sqrMagnitude<0.5f)return false;
            normal=wall.rotation*entryNormal;point=Vector3.LerpUnclamped(from,to,time);return true;
        }
    }
}
