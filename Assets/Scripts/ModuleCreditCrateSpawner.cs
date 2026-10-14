using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class ModuleCreditCrateSpawner : MonoBehaviour
    {
        [SerializeField] private ModuleCreditCrate cratePrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField, Min(0)] private int spawnCount = 3;
        private readonly List<ModuleCreditCrate> spawnedCrates = new List<ModuleCreditCrate>();
        public int RemainingCrateCount
        {
            get
            {
                int count = 0;
                foreach (var crate in spawnedCrates)
                    if (crate != null && crate.isActiveAndEnabled && !crate.IsClaimed) count++;
                return count;
            }
        }

        private void Start()
        {
            if (cratePrefab == null || spawnPoints == null) return;
            var available = new System.Collections.Generic.List<Transform>();
            foreach (Transform point in spawnPoints)
                if (point != null && !available.Contains(point)) available.Add(point);
            int count = Mathf.Min(spawnCount, available.Count);
            for (int i = 0; i < count; i++)
            {
                int index = Random.Range(i, available.Count);
                Transform point = available[index];
                available[index] = available[i];
                available[i] = point;
                var crate = Instantiate(cratePrefab, point.position, point.rotation, transform);
                spawnedCrates.Add(crate);
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (spawnPoints == null) return;
            Gizmos.color = Color.cyan;
            foreach (Transform point in spawnPoints)
                if (point != null) Gizmos.DrawWireCube(point.position, Vector3.one * 0.8f);
        }
    }
}