using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 생존 모드에서 하부 서랍(Draw) 피격으로 내구도가 30% 미만이 되면 작업대를 붕괴시키고 거치 총기를 낙하시키는 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivalWorkbenchDestruction : MonoBehaviour
    {
        private const float DEFAULT_COLLAPSE_THRESHOLD = 0.3f;

        [Header("작업대 및 부품 참조")]
        [SerializeField] private Workbench _workbench;
        [SerializeField] private DestructibleTarget _drawDestructible;
        [SerializeField] private DestructibleTarget _tableTopDestructible;

        [Header("붕괴 임계값 설정")]
        [SerializeField, Range(0.05f, 0.95f)] private float _collapseThresholdRatio = DEFAULT_COLLAPSE_THRESHOLD;

        private bool _collapsed;
        private bool _isInitialized;

        public bool IsCollapsed => _collapsed;
        public float CollapseThresholdRatio => _collapseThresholdRatio;

        void Awake()
        {
            EnsureComponents();
        }

        void Start()
        {
            EnsureComponents();
        }

        void LateUpdate()
        {
            if (_collapsed) return;

            if (_drawDestructible != null && _drawDestructible.InitialCellCount > 0)
            {
                float ratio = (float)_drawDestructible.RemainingCellCount / _drawDestructible.InitialCellCount;
                if (ratio < _collapseThresholdRatio || _drawDestructible.IsDestroyed)
                {
                    TriggerCollapse();
                }
            }
        }

        /// <summary>
        /// 작업대 본체 및 서랍/상판의 DestructibleTarget 컴포넌트를 탐색하고 보장합니다.
        /// </summary>
        private void EnsureComponents()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (_workbench == null)
            {
                _workbench = GetComponent<Workbench>();
            }

            Transform drawTransform = null;
            if (_workbench != null && _workbench.OutlineRoot != null)
            {
                drawTransform = _workbench.OutlineRoot;
            }
            else
            {
                drawTransform = transform.Find("Draw");
            }

            if (drawTransform != null && _drawDestructible == null)
            {
                _drawDestructible = drawTransform.GetComponent<DestructibleTarget>();
                if (_drawDestructible == null)
                {
                    _drawDestructible = drawTransform.gameObject.AddComponent<DestructibleTarget>();
                }
            }

            Transform tableTopTransform = null;
            if (drawTransform != null)
            {
                tableTopTransform = drawTransform.Find("Table_1");
            }
            if (tableTopTransform == null)
            {
                tableTopTransform = transform.Find("Table_1");
            }

            if (tableTopTransform != null && _tableTopDestructible == null)
            {
                _tableTopDestructible = tableTopTransform.GetComponent<DestructibleTarget>();
                if (_tableTopDestructible == null)
                {
                    _tableTopDestructible = tableTopTransform.gameObject.AddComponent<DestructibleTarget>();
                }
            }
        }

        /// <summary>
        /// 하부 서랍(Draw)의 현재 남은 셀 비율을 계산합니다.
        /// </summary>
        /// <returns>남은 셀 비율(0.0 ~ 1.0)을 반환합니다.</returns>
        public float GetRemainingRatio()
        {
            if (_drawDestructible == null || _drawDestructible.InitialCellCount <= 0) return 1f;
            if (_drawDestructible.IsDestroyed) return 0f;
            return (float)_drawDestructible.RemainingCellCount / _drawDestructible.InitialCellCount;
        }

        /// <summary>
        /// 작업대 내구도 고갈 시 즉시 편집 모드를 종료시키고 거치 총기를 월드 바닥으로 떨어뜨린 뒤 작업대를 영구 비활성화합니다.
        /// </summary>
        public void TriggerCollapse()
        {
            if (_collapsed) return;
            _collapsed = true;

            EnsureComponents();

            WorkshopSession activeSession = FindAnyObjectByType<WorkshopSession>();
            EquipableWeapon weaponToDrop = _workbench != null ? _workbench.CurrentWeapon : null;

            if (activeSession != null && activeSession.IsInWorkshop && activeSession.ActiveWorkbench == _workbench)
            {
                activeSession.LeaveImmediately();
            }

            if (weaponToDrop != null)
            {
                weaponToDrop.DropFromWorkbench();
            }

            if (_workbench != null)
            {
                _workbench.enabled = false;

                Collider[] colliders = _workbench.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null && !colliders[i].transform.IsChildOf(weaponToDrop != null ? weaponToDrop.transform : transform))
                    {
                        colliders[i].enabled = false;
                    }
                }
            }
        }
    }
}
