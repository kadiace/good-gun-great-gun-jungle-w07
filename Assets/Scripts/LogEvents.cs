using System;

namespace WeaponWorkshop
{
    [Serializable] public sealed class SceneBootLog { public string scene, installId, saveId; }
    [Serializable] public sealed class RaidResultLog
    {
        public string scene;
        public bool survived;
        public float time;
        public float duration;
        public int[] moduleCrateRewards;
        public int materialCrateCredits;
        public int enemyKills;
        public float health;
        // extracted / died / ending_quit(엔드 상자 END 화면의 그만하기).
        public string exitReason = "";
        public bool endChestOpened;
        // 출발 공방 일차(직접 재생 -1). training은 반영되지 않는 튜토리얼 연습 습격.
        public int day = -1;
        public bool training;
        // 주운 재료 픽업 개수와 지갑에 넣은 달러(탈출 성공만). 주운 양 필드는 사망·종료에도 그대로 남는다.
        public int materialPickups, dollarReward;
        // 이번 습격의 획득물이 공방에 남는지(탈출 성공 && 연습 아님).
        public bool rewardsApplied;
    }
    [Serializable] public sealed class RaidBlockDestroyedLog { public string stageId; public float time; public string blockId; }
    [Serializable] public sealed class RaidEnemyLog { public float time; public string enemyId; }
    [Serializable] public sealed class RaidPlayerHitLog
    {
        public string stageId, enemyId, enemyType;
        public float time, duration, damage, health;
        public UnityEngine.Vector3 position;
    }
    [Serializable] public sealed class RaidPlayerStateLog
    {
        public float time;
        public UnityEngine.Vector3 position;
        public UnityEngine.Vector3 cameraRotation;
        public float health;
        public int remainingModuleCrates;
        public int remainingMaterialCrates;
        public int tanks;
        public int helicopters;
    }
    [Serializable] public sealed class RaidStartLog
    {
        public string scene;
        // 출발한 공방 일차. 습격 씬을 직접 재생하면 -1.
        public int day = -1;
        public string weaponCode = "", weaponCodeStatus = "no_weapon";
        public string weaponAssemblyId = "", weaponAssemblyIdStatus = "no_weapon";
        // 총구 중 최댓값(코어 기본 + 경로 모듈, 산탄 절반 반영, 차지 배율 제외). 무기가 없으면 0.
        public float penetration, explosion;
        // 출발 시 지갑과 획득한 해금 모듈 id(처음부터 열린 모듈 제외).
        public int balance, moduleCredits;
        public int[] unlockedModules;
        // 입장 비용(balance는 차감 후), 연습 여부, 만들어진 보상 상자. replacedModuleSpots는 이미 해금해 재료 상자로 바뀐 모듈 자리(spawnedMaterialCrates에 포함).
        public int entryCost;
        public bool training;
        public int spawnedModuleCrates, spawnedMaterialCrates, replacedModuleSpots, materialPerCrate;
    }
    [Serializable] public sealed class RaidWeaponConfigLog
    {
        public float time;
        public string weaponCode = "", weaponCodeStatus = "no_weapon";
        public string weaponAssemblyId = "", weaponAssemblyIdStatus = "no_weapon";
    }
    [Serializable] public sealed class RaidWeaponFiredLog
    {
        public float time;
        public UnityEngine.Vector3 position;
        public string weaponAssemblyId = "", weaponAssemblyIdStatus = "no_weapon";
    }
    [Serializable] public sealed class SessionStartLog { public string installId; }
    [Serializable] public sealed class GameStartLog { public string installId, saveId, savedScene = ""; public bool newGame, retired, endChestOpened; }
    [Serializable] public sealed class GameDayLog { public int day; }
    [Serializable] public sealed class RaidFirstClearLog { public string scene; public int day; public float saleBonusAdded, saleBonusTotal; }
    [Serializable] public sealed class RaidSkippedLog { public int day, nextDay, moduleCreditsGranted, moduleCreditsAfter; }
    [Serializable] public sealed class GameBankruptLog { public int day, balance, rent; }
    [Serializable] public sealed class GameDaySettledLog
    {
        public int day, balanceBefore, income, constructionCost, soldConstructionCost, rent, balanceAfter;
        public int completedGuests, dissatisfiedGuests, killedGuests, moduleCredits;
        public bool bankrupt, retired, endChestOpened;
    }
    [Serializable] public sealed class TutorialStepLog
    {
        public string sequence, stepId;
        public int stepIndex;
        // time은 게임 실행 후 실제 시간(초, 씬 이동·일시정지와 무관). duration은 단계 시작부터 완료까지.
        public float time, duration;
        // 에디터 F3 치트로 넘긴 단계.
        public bool debugSkipped;
        // 가이드 표시 정책 실험 이름. TutorialManager 인스펙터 값, 미정이면 빈 문자열.
        public string guideMode = "";
    }
    [Serializable] public sealed class TutorialEndLog
    {
        public string sequence, lastStepId, reason;
        public int lastStepIndex;
        public float time;
    }
    // 엔드 상자(정글 캠퍼스). day는 출발 공방 일차(직접 재생 -1), time은 씬 진입 후, duration은 습격 시작 후 게임 시간.
    [Serializable] public class EndChestLog { public string scene; public int day = -1; public float time, duration; }
    [Serializable] public sealed class EndChestScannedLog : EndChestLog { public float requiredGravity, requiredHoming; }
    [Serializable] public sealed class EndChestHitLog : EndChestLog
    {
        public float gravity, homing;
        public string weaponAssemblyId = "", weaponAssemblyIdStatus = "no_weapon";
        public bool scanned;
    }
    [Serializable] public sealed class EndChestOpenedLog : EndChestLog { public float absorbSeconds; }
    // 은퇴. playSeconds는 Time.time.
    [Serializable] public class RetireLog
    {
        public int day;
        public string installId, saveId, period;
        public bool isNight;
        public float playSeconds;
    }
    [Serializable] public sealed class RetireFailedLog : RetireLog { public string reason; public int balance; }
    [Serializable] public sealed class RetireCompletedLog : RetireLog
    {
        public int balanceBefore, balanceAfter, cost, outstandingLoans, moduleCredits;
        public bool endless, firstEnding;
    }
    // 엔딩 화면 버튼. endingType retire / end_chest, choice lobby / continue.
    [Serializable] public sealed class EndingChoiceLog
    {
        public string endingType, choice, installId, saveId;
        public int day;
        public float panelSeconds;
        public bool firstEnding;
    }
}
