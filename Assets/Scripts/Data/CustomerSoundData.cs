using System;

namespace WeaponWorkshop
{
    // Resources/CustomerSoundData.json의 순수 직렬화 자료. 조회·검증 로직은 CustomerSoundInfo에 둔다.
    [Serializable]
    public sealed class CustomerSoundData
    {
        [Serializable]
        public sealed class CommonScream
        {
            public string selection;
        }

        [Serializable]
        public sealed class FixedGuest
        {
            public string profileId;
            public string classification;
        }

        [Serializable]
        public sealed class GeneratedGuests
        {
            public bool commonScreamEnabled;
            public string[] poolIds = Array.Empty<string>();
        }

        public string projectId;
        public int formatVersion;
        public CommonScream commonScream = new CommonScream();
        public FixedGuest[] fixedGuests = Array.Empty<FixedGuest>();
        public GeneratedGuests generatedGuests = new GeneratedGuests();
    }
}
