using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class DeveloperGiftStagedWeapon : MonoBehaviour
    {
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Rigidbody[] _rigidbodies;
        private bool[] _rendererEnabled;
        private bool[] _colliderEnabled;
        private bool[] _rigidbodyKinematic;
        private bool _captured;

        public bool IsStaged { get; private set; }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            _rigidbodies = GetComponentsInChildren<Rigidbody>(true);
            _rendererEnabled = new bool[_renderers.Length];
            _colliderEnabled = new bool[_colliders.Length];
            _rigidbodyKinematic = new bool[_rigidbodies.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererEnabled[i] = _renderers[i].enabled;
            for (int i = 0; i < _colliders.Length; i++) _colliderEnabled[i] = _colliders[i].enabled;
            for (int i = 0; i < _rigidbodies.Length; i++) _rigidbodyKinematic[i] = _rigidbodies[i].isKinematic;
            _captured = true;
        }

        /// <summary>
        /// Hides the reward weapon from rendering, physics, interaction, firing, and world save collection.
        /// </summary>
        public void BeginStaging()
        {
            if (!_captured) return;
            IsStaged = true;
            SetStagedState();
        }

        /// <summary>
        /// Restores the reward weapon's prefab component states immediately before the standard equip call.
        /// </summary>
        public bool PrepareForEquip()
        {
            if (!IsStaged) return false;
            IsStaged = false;
            RestoreComponentStates();
            return true;
        }

        /// <summary>
        /// Disables cached renderers and colliders and makes cached rigidbodies kinematic.
        /// </summary>
        private void SetStagedState()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = false;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = false;
            for (int i = 0; i < _rigidbodies.Length; i++)
                if (_rigidbodies[i] != null) _rigidbodies[i].isKinematic = true;
        }

        /// <summary>
        /// Restores the renderer, collider, and rigidbody settings captured from the core prefab.
        /// </summary>
        private void RestoreComponentStates()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = _rendererEnabled[i];
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = _colliderEnabled[i];
            for (int i = 0; i < _rigidbodies.Length; i++)
                if (_rigidbodies[i] != null) _rigidbodies[i].isKinematic = _rigidbodyKinematic[i];
        }
    }
}
