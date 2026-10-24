using System;
using UnityEngine;

namespace WeaponWorkshop
{
    public enum TutorialCompletion { Confirmation, Signal, FiveCoreFacesBlocked, ModuleInstalled, CellEmpty, CustomerWeaponReady, WorkshopExited, WeaponHeld, SettlementClosed, CustomerSold, CustomerKilled, TargetNearby, TargetScanned, RaidTankSpawned, RetirementIntroduction, RaidTicketCollected, RaidMaterialCollected, WeaponTagged }
    public enum TutorialTarget { None, Shutter, Customer, MainCoreDispenser, Workbench, ShootingTarget, WorkbenchWeapon, CloseShop, RaidEntrance, RaidLoot, RaidExit, RaidMaterialCrate, RaidTank }
    public enum TutorialRotationGuide { None, Tilt, Roll }
    public enum TutorialButtonGuide { None, Recipe, PresetFirst, RaidHouse, LoanConfirm }

    [Serializable]
    public sealed class TutorialStep
    {
        public string id;
        [Min(0)] public int setBalanceOnStart = -1;
        public bool restoreWithheldBalanceOnComplete;
        public string title = "튜토리얼";
        [TextArea(2, 6)] public string description;
        [TextArea, Tooltip("좌상단 행동 목표. 비우면 해당 단계의 목표 패널을 숨깁니다.")] public string objective;
        [Tooltip("Signal 단계에서 설명을 먼저 확인한 뒤, 같은 단계의 행동 목표를 시작합니다.")]
        public bool showDescriptionBeforeObjective;
        [TextArea] public string objectiveWhileChoiceOpen;
        [TextArea] public string choiceGuidance;
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
        [Tooltip("WeaponTagged 단계에서 작업대 무기가 충족해야 할 CustomerRequestData.tags의 id(무기 타입 자산 GUID). tag/... 요청 id가 아니다.")] public string requiredTagId;
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
        [Tooltip("퀵슬롯 교체는 requiredModuleId 모듈을 지정 번호 칸으로 옮길 때만 허용합니다.")]
        public bool restrictQuickslotChange;
        [Range(1, 10), Tooltip("교체 목표 퀵슬롯 번호. 10은 숫자키 0 칸입니다.")]
        public int requiredQuickslotNumber = 2;
        [Tooltip("Signal 단계에서 기다릴 게임 행동 신호. NotifySignal로 전달합니다.")] public string requiredSignal;
        [Tooltip("발사·작업대 회전 등 기존 예외 없이 allowedActions에 지정한 행동만 허용합니다. 빈 목록도 모두 차단합니다.")]
        public bool strictAllowedActions;
        [TextArea, Tooltip("엄격 제한 단계에서 금지된 행동을 시도했을 때 표시할 알림.")]
        public string blockedActionMessage = "지금은 안내된 행동만 진행해 주세요.";
        [Tooltip("허용 행동 ID. strictAllowedActions가 꺼져 있으면 빈 목록은 제한 없음, none은 작업 조작 금지. 설치·삭제·회전·퇴장·주요 창에서 매니저를 조회합니다.")]
        public string[] allowedActions = Array.Empty<string>();
    }

    [CreateAssetMenu(menuName = "Weapon Workshop/Tutorial Sequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        public TutorialStep[] steps = Array.Empty<TutorialStep>();
    }
}
