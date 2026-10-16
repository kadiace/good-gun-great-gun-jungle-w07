using System;
using UnityEngine;

namespace WeaponWorkshop
{
    public enum TutorialCompletion { Confirmation, Signal, FiveCoreFacesBlocked, ModuleInstalled, CellEmpty, CustomerWeaponReady, WorkshopExited, WeaponHeld, SettlementClosed, CustomerSold, CustomerKilled, TargetNearby, TargetScanned, RaidTankSpawned }
    public enum TutorialTarget { None, Shutter, Customer, MainCoreDispenser, Workbench, ShootingTarget, WorkbenchWeapon, CloseShop, RaidEntrance, RaidLoot, RaidExit }
    public enum TutorialRotationGuide { None, Tilt, Roll }
    public enum TutorialButtonGuide { None, Recipe, PresetFirst, RaidHouse }

    [Serializable]
    public sealed class TutorialStep
    {
        public string id;
        public string title = "튜토리얼";
        [TextArea(2, 6)] public string description;
        [TextArea, Tooltip("좌상단 행동 목표. 비우면 해당 단계의 목표 패널을 숨깁니다.")] public string objective;
        public TutorialCompletion completion;
        public TutorialTarget target;
        public string targetLabel;
        [Tooltip("작업대 밖에서는 작업대로 유도하며 재진입을 허용합니다.")] public bool requiresWorkbenchEditing;
        [Min(5f)] public float requiredRotationDegrees = 45f;
        public TutorialRotationGuide rotationGuide;
        public TutorialButtonGuide buttonGuide;
        public bool showPlacementHints;
        public Vector3Int openCoreFace = Vector3Int.forward;
        [Tooltip("비우면 기존 5면 안내. 지정하면 해당 셀만 표시합니다.")] public Vector3Int[] hintCells = Array.Empty<Vector3Int>();
        public int requiredModuleId;
        public int requiredCustomerCount;
        [Min(0.1f), Tooltip("대상 접근·스캔 단계의 최대 거리(m)")] public float requiredTargetDistance = 5f;
        public Vector3Int requiredCell = Vector3Int.forward;
        public bool requireOutputDirection;
        public bool excludeRequiredOutputDirection;
        public Vector3Int requiredOutputDirection = Vector3Int.forward;
        public bool MatchesOutputDirection(Vector3Int direction) => !requireOutputDirection ||
            (excludeRequiredOutputDirection ? direction != requiredOutputDirection : direction == requiredOutputDirection);
        public bool restrictPlacement;
        [TextArea] public string blockedPlacementMessage = "안내된 위치와 방향에 모듈을 설치해 보세요.";
        [TextArea] public string wrongModuleMessage;
        [Tooltip("이미 배운 기본 5면 막기는 다음 주문에서 연습용 몸체로 준비합니다.")] public bool prepareBlockedCore;
        [Tooltip("이 단계에서 작업대에 준비할 프리셋. 비우면 일반 코어를 놓습니다.")] public EquipableWeapon preparedWeaponPrefab;
        [Tooltip("프리셋에서 플레이어가 채울 빈 모듈 자리.")] public Vector3Int[] preparedWeaponOpenCells = Array.Empty<Vector3Int>();
        [Tooltip("연습에만 쓰인 손의 무기를 정리합니다.")] public bool discardHeldWeapon;
        public int[] allowedModuleIds = Array.Empty<int>();
        [Tooltip("Signal 단계에서 기다릴 게임 행동 신호. NotifySignal로 전달합니다.")] public string requiredSignal;
        [Tooltip("허용 행동 ID. 비우면 제한 없음, none이면 작업 조작 금지. 설치·삭제·회전·퇴장·주요 창에서 매니저를 조회합니다.")]
        public string[] allowedActions = Array.Empty<string>();
    }

    [CreateAssetMenu(menuName = "Weapon Workshop/Tutorial Sequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        public TutorialStep[] steps = Array.Empty<TutorialStep>();
    }
}
