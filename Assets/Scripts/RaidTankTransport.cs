using UnityEngine;

namespace WeaponWorkshop
{
    public sealed class RaidTankTransport : MonoBehaviour
    {
        [SerializeField] private RaidTank tankPrefab;
        [SerializeField] private Transform cargoAnchor;
        [SerializeField] private GameObject suspensionDraw;
        [SerializeField, Min(1f)] private float flightSpeed = 15f;
        [SerializeField, Min(1f)] private float dropHeight = 50f;
        private RaidTank cargo;
        private Vector3 destination, exit;
        private bool released, initialized;
        public void Initialize(FirstPersonPlayer player, Vector3 landingPoint, Vector3 outward)
        {
            if (tankPrefab == null || cargoAnchor == null) { Destroy(gameObject); return; }
            outward.y = 0f; if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
            outward.Normalize();
            float height = dropHeight - cargoAnchor.localPosition.y;
            destination = landingPoint + Vector3.up * height;
            transform.position = destination + outward * 50f;
            transform.rotation = Quaternion.LookRotation(-outward);
            exit = destination - outward * 80f;
            cargo = Instantiate(tankPrefab, cargoAnchor.position, transform.rotation, cargoAnchor);
            cargo.Initialize(player); initialized = true;
            WorkshopSoundEffects.RaiseTankAppeared(transform.position);
        }
        private void ReleaseCargo()
        {
            if (released) return;
            released = true;
            if (cargo != null) cargo.Release();
            if (suspensionDraw != null) suspensionDraw.SetActive(false);
        }
        private void Update()
        {
            if (!initialized || GameManager.GameplayBlocked) return;
            Vector3 target = released ? exit : destination;
            transform.position = Vector3.MoveTowards(transform.position, target, flightSpeed * Time.deltaTime);
            if ((transform.position-target).sqrMagnitude > 0.01f) return;
            if (!released) ReleaseCargo(); else Destroy(gameObject);
        }
    }
}
