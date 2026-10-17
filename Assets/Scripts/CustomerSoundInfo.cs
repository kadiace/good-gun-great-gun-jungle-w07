using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    // CustomerSoundData를 profileId로 조회하고 로드된 분류표를 검증한다.
    public static class CustomerSoundInfo
    {
        public const string CLASSIFICATION_MAN = "man";
        public const string CommonSelection = "random-one-of-available-clips";
        private const string LegacyCommonSelection = "random-one-of-two-clips";

        public static CustomerSoundData.FixedGuest FindFixedGuest(this CustomerSoundData table, string profileId)
        {
            if (table == null || string.IsNullOrEmpty(profileId)) return null;
            foreach (var row in table.fixedGuests ?? System.Array.Empty<CustomerSoundData.FixedGuest>())
                if (row != null && row.profileId == profileId) return row;
            return null;
        }

        /// <summary>분류표의 프로젝트 버전, 비명 선택 정책, profileId와 분류 값을 검사하고 잘못된 항목을 로그에 남긴다.</summary>
        public static void Validate(CustomerSoundData table)
        {
            if (table == null) return;
            if (table.projectId != "good-gun-great-gun-jungle-w07" || table.formatVersion != 1)
                Debug.LogError("CustomerSoundData의 프로젝트 식별자 또는 형식 버전이 맞지 않습니다.");
            if (table.commonScream == null ||
                (table.commonScream.selection != CommonSelection && table.commonScream.selection != LegacyCommonSelection))
                Debug.LogError("CustomerSoundData의 공통 비명 선택 방식은 사용 가능한 클립 중 무작위 선택이어야 합니다.");

            var ids = new HashSet<string>();
            foreach (var row in table.fixedGuests ?? System.Array.Empty<CustomerSoundData.FixedGuest>())
            {
                if (row == null || string.IsNullOrEmpty(row.profileId) || !ids.Add(row.profileId))
                {
                    Debug.LogError("CustomerSoundData에 비었거나 중복된 profileId가 있습니다.");
                    continue;
                }
                if (row.classification != "common" && row.classification != "individual" && row.classification != CLASSIFICATION_MAN)
                    Debug.LogError($"CustomerSoundData의 분류 값이 잘못되었습니다: {row.profileId}");
            }
            if (table.generatedGuests == null || !table.generatedGuests.commonScreamEnabled)
                Debug.LogError("조합 생성 손님은 공통 비명을 사용하도록 설정해야 합니다.");
        }
    }
}
