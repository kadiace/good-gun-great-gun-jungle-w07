using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace WeaponWorkshop
{
    // 값은 진행도 키이며 배열 순서와 무관하다.
    public enum RaidBgmStage
    {
        [InspectorName("01 목조 주택")] WoodenHouse = 1,
        [InspectorName("02 은행")] Bank = 2,
        [InspectorName("03 정글 캠퍼스")] JungleCampus = 3,
        [InspectorName("04 군사기지")] MilitaryBase = 4
    }

    public enum RaidBgmPhase { Travel = 0, FirstWall = 1, Clear = 2 }

    [Serializable]
    public sealed class RaidBgmSet
    {
        public RaidBgmStage stage;
        [InspectorName("입장 BGM (필수)")] public AudioClip travel;
        [InspectorName("첫 벽 파괴 BGM (필수)")] public AudioClip firstWall;
        // 이전 프리팹의 연결은 보존하지만 현재 클리어 보상 BGM 설정에는 사용하지 않는다.
        [SerializeField, HideInInspector, FormerlySerializedAs("clear")] private AudioClip _legacyClear;
    }

    public sealed partial class SoundManager
    {
        private RaidBgmSet FindRaidMusic(int number)
        {
            foreach (var set in raidMusicSets ?? Array.Empty<RaidBgmSet>())
                if (set != null && (int)set.stage == number) return set;
            return null;
        }

        // 조건 식별자의 연속 값만 계산한다. 실제 음원 선택은 인스펙터의 개별 슬롯이 담당한다.
        private static BgmId RaidMusicId(int number, RaidBgmPhase phase) => number >= 1 && number <= 4
            ? (BgmId)((int)BgmId.Raid01Travel + (number - 1) * 3 + (int)phase) : BgmId.DayFirst;

        // 습격 조건 BGM은 전투 중 음량을 사용한다.
        /// <summary>
        /// 습격 입장·전투 곡과 Main 곡에 적용할 음량 채널을 구분한다.
        /// id가 입장·첫 벽 곡이면 true를 반환한다.
        /// </summary>
        private static bool UsesRaidVolume(BgmId id)
        {
            int cue = (int)id - (int)BgmId.Raid01Travel;
            return id == BgmId.Night || (cue >= 0 && cue <= 11 && cue % 3 != (int)RaidBgmPhase.Clear);
        }

        // 조건 슬롯에 공격곡을 직접 넣어도 enum뿐 아니라 실제 AudioClip으로 Main 재생을 막는다.
        private bool IsAttackClip(AudioClip clip) => clip != null &&
            (clip == dayAttack0MusicClip || clip == dayAttack1MusicClip || clip == dayAttack2MusicClip);
        private bool IsAttackMusic(BgmId id) => SoundInfo.IsAttackTrack(id) || IsAttackClip(GetMusicClip(id));

        // 자동 재생 조건과 곡 선택 슬롯이 같은 음원일 때 선택 표시를 이어 준다.
        public static bool IsPlayingTrack(BgmId id) => instance != null && instance.playingBgm != BgmId.None &&
            instance.GetMusicClip(id) != null && instance.GetMusicClip(id) == instance.GetMusicClip(instance.playingBgm);

        // 인스펙터/플레이 시작/빌드/런타임에서 공통으로 사용하는 필수 연결 검사다.
        // 빈 슬롯을 폴백으로 감춰 정상 설정처럼 실행하지 않는다.
        /// <summary>
        /// 기본·선택·전투 BGM 연결이 비었거나 Main 금지 음원인지 검사한다.
        /// 오류 메시지를 error에 담고 전체 연결이 유효하면 true를 반환한다.
        /// </summary>
        public bool ValidateBgmConfiguration(out string error)
        {
            var errors = new StringBuilder();
            RequireClip(defaultMusicClip, "Main 기본/폴백 day-first", errors, true);
            RequireClip(daySecondMusicClip, "곡 선택 day-second", errors, true);
            RequireClip(dayThirdMusicClip, "곡 선택 day-third", errors, true);
            RequireClip(dayFourthMusicClip, "곡 선택 day-fourth", errors, true);
            RequireClip(dayLastMusicClip, "곡 선택 day-last", errors, true);
            RequireClip(dayAttack0MusicClip, "Main 차단 기준 day-attack0", errors);
            RequireClip(dayAttack1MusicClip, "Main 차단 기준 day-attack1", errors);
            RequireClip(dayAttack2MusicClip, "Main 차단 기준 day-attack2", errors);
            RequireClip(tutorialRainMusicClip, "튜토리얼 공방/귀환", errors, true);
            RequireClip(nightMusicClip, "튜토리얼 훈련 습격", errors);
            var found = new HashSet<RaidBgmStage>();
            foreach (var set in raidMusicSets ?? Array.Empty<RaidBgmSet>())
            {
                if (set == null) { errors.AppendLine("습격 BGM 설정 행이 비어 있습니다."); continue; }
                if (!Enum.IsDefined(typeof(RaidBgmStage), set.stage)) errors.AppendLine("알 수 없는 습격 BGM 키입니다.");
                if (!found.Add(set.stage)) errors.AppendLine($"{set.stage}: 습격 BGM 설정이 중복되었습니다.");
                string title = $"습격 {(int)set.stage:00}";
                RequireClip(set.travel, title + " 입장", errors);
                RequireClip(set.firstWall, title + " 첫 벽 파괴", errors);
            }
            foreach (RaidBgmStage stage in Enum.GetValues(typeof(RaidBgmStage)))
                if (!found.Contains(stage)) errors.AppendLine($"습격 {(int)stage:00} BGM 설정이 없습니다.");
            error = errors.Length == 0 ? null : "SoundManager BGM 필수 연결을 확인하세요.\n" + errors;
            return error == null;
        }

        private void RequireClip(AudioClip clip, string label, StringBuilder errors, bool main = false)
        {
            if (clip == null) errors.AppendLine(label + ": 음원을 넣어 주세요.");
            else if (main && IsAttackClip(clip)) errors.AppendLine(label + ": Main에서 공격곡은 사용할 수 없습니다.");
        }
    }
}
