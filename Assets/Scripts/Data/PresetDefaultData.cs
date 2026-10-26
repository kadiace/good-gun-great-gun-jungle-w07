using System;

namespace WeaponWorkshop
{
    // Resources/PresetDefaultData.json을 그대로 옮긴 직렬화 자료다. 여기에 로직을 두지 않는다.
    // 조회는 PresetDefaultInfo, 로딩과 보유는 TableManager가 담당한다.
    [Serializable]
    public sealed class PresetDefaultData
    {
        [Serializable] public sealed class Entry
        {
            // 0 기반 프리셋 슬롯 번호.
            public int slot;
            public string name;
            // 공유 코드(WSC:...). 비어 있으면 해당 슬롯은 기본값 없이 시작한다.
            public string code;
        }
        public Entry[] presets = Array.Empty<Entry>();
    }
}
