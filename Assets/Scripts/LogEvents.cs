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
    [Serializable] public sealed class GameStartLog { public string installId, saveId, savedScene = ""; public bool newGame; }
    [Serializable] public sealed class GameDayLog { public int day; }
    [Serializable] public sealed class GameBankruptLog { public int day, balance, rent; }
    [Serializable] public sealed class GameDaySettledLog
    {
        public int day, balanceBefore, income, constructionCost, soldConstructionCost, rent, balanceAfter;
        public int completedGuests, dissatisfiedGuests, killedGuests, moduleCredits;
        public bool bankrupt;
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
}
