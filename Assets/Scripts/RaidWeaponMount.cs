using UnityEngine;

namespace WeaponWorkshop
{
    // An enemy launcher: fires from this transform while its parts keep enough of their cubes.
    public sealed class RaidWeaponMount : MonoBehaviour
    {
        [SerializeField, Tooltip("이 발사구를 이루는 파괴 가능 부위(포드·포신 등).")]
        private DestructibleTarget[] parts = new DestructibleTarget[0];
        [SerializeField, Range(0f, 1f), Tooltip("부위 큐브가 이 비율 이상 떨어져 나가면 발사 불가.")]
        private float disableAtLost = 0.7f;
        [SerializeField, Tooltip("발사 불가가 되는 순간 부위 중앙에서 한 번 터지는 폭발 연출(피해 없음).")]
        private GameObject breakBurstPrefab;
        [SerializeField, Min(0.01f)] private float breakBurstScale = 1.5f;
        [SerializeField, Tooltip("발사 불가가 되면 부위 중앙에서 켜는 연기.")]
        private ParticleSystem damagedSmokePrefab;
        private const float CheckInterval = 0.2f;
        private int[] initialCells;
        private bool disabled;
        private ParticleSystem smoke;
        private RaidEnemyCore core;
        private bool hasCore;
        private float checkRemaining;

        public Transform Muzzle => transform;

        public bool IsOperational
        {
            get
            {
                if (!disabled && LostRatio() >= disableAtLost) Disable();
                return !disabled;
            }
        }

        private void Awake()
        {
            disabled = false;
            smoke = null;
            initialCells = null;
            checkRemaining = CheckInterval;
        }

        private void Start()
        {
            core = RaidEnemyCore.FindForPart(transform);
            hasCore = core != null;
        }

        // Breakage shows as soon as it happens, not only when the owner next tries to fire.
        private void Update()
        {
            if (disabled) return;
            // A destroyed core breaks every part (its object may already be gone); that wreck needs no launcher effect.
            if (hasCore && (core == null || core.IsDestroyed)) { enabled = false; return; }
            checkRemaining -= Time.deltaTime;
            if (checkRemaining > 0f) return;
            checkRemaining = CheckInterval;
            if (LostRatio() >= disableAtLost) Disable();
        }

        // Parts subdivide in their own Awake, so the starting cube counts are read on first use.
        private float LostRatio()
        {
            if (parts == null || parts.Length == 0) return 0f;
            if (initialCells == null)
            {
                initialCells = new int[parts.Length];
                for (int i = 0; i < parts.Length; i++) initialCells[i] = parts[i] != null ? parts[i].InitialCellCount : 0;
            }
            int initial = 0, attached = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                initial += initialCells[i];
                if (parts[i] != null) attached += Mathf.Min(initialCells[i], parts[i].AttachedCellCount);
            }
            return initial > 0 ? 1f - (float)attached / initial : 0f;
        }

        private void Disable()
        {
            disabled = true;
            Vector3 center = PartsCenter();
            if (breakBurstPrefab != null)
            {
                var burst = Instantiate(breakBurstPrefab, center, Quaternion.identity, transform);
                burst.transform.localScale = Vector3.one * breakBurstScale;
            }
            if (damagedSmokePrefab == null || smoke != null) return;
            smoke = Instantiate(damagedSmokePrefab, center, Quaternion.identity, transform);
            smoke.Play();
        }

        private Vector3 PartsCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var part in parts)
                if (part != null) { sum += part.transform.position; count++; }
            return count > 0 ? sum / count : transform.position;
        }
    }
}
