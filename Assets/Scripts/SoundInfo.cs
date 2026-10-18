using System;
using UnityEngine;

namespace WeaponWorkshop
{
    // 테이블 조회는 배열 순서 대신 씬 이름·곡 식별자·명시한 습격 번호를 사용한다.
    public static class SoundInfo
    {
        public static BgmId Parse(string value) => Enum.TryParse(value, out BgmId id) && Enum.IsDefined(typeof(BgmId), id)
            ? id : BgmId.DayFirst;

        public static SoundData.Track Track(BgmId id)
        {
            foreach (var row in TableManager.Instance.Sound.tracks ?? Array.Empty<SoundData.Track>())
                if (row != null && row.id == id.ToString()) return row;
            return null;
        }

        /// <summary>
        /// 지정한 습격 씬을 클리어할 때 보상으로 해금할 곡을 조회한다.
        /// scene 이름과 SoundData의 unlockScene 키를 비교해 일치하는 곡 행을 반환한다.
        /// </summary>
        public static SoundData.Track RaidRewardTrack(string scene)
        {
            scene = TableManager.Instance.RaidMap.Find(scene)?.CanonicalScene() ?? scene;
            SoundData.Track reward = null;
            foreach (var row in TableManager.Instance.Sound.tracks ?? Array.Empty<SoundData.Track>())
                if (row != null && row.selectableInMain && row.unlockScene == scene)
                {
                    if (reward != null) return null;
                    reward = row;
                }
            return reward;
        }

        public static SoundData.Raid Raid(string scene)
        {
            scene = TableManager.Instance.RaidMap.Find(scene)?.CanonicalScene() ?? scene;
            foreach (var row in TableManager.Instance.Sound.raids ?? Array.Empty<SoundData.Raid>())
                if (row != null && row.scene == scene) return row;
            return null;
        }

        public static SoundData.Raid Raid(int number)
        {
            foreach (var row in TableManager.Instance.Sound.raids ?? Array.Empty<SoundData.Raid>())
                if (row != null && row.number == number) return row;
            return null;
        }

        // 테이블의 해금 정보와 무관한 안전 경계다. 공격곡은 Main에서 사용할 수 없다.
        public static bool IsAttackTrack(BgmId id) => id == BgmId.DayAttack0 || id == BgmId.DayAttack1 || id == BgmId.DayAttack2;
        // 표 로드 실패가 이미 저장된 진행도를 지우지 않게 한다. 알 수 없는 단계의 재생만 기본곡으로 돌린다.
        public static int ClampProgress(int progress) => Mathf.Max(0, progress);

    }
}
