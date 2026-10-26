using System;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class ShootingTestTableData
    {
        [Serializable]
        public sealed class ModuleRequirement
        {
            public string kind;
            public int count = 1;
        }
        [Serializable]
        public sealed class Entry
        {
            public string id, name, instructions, failureHint;
            public bool enabled = true;
            public float timeLimit, requiredDestruction;
            public string[] requiredUnlockModules = Array.Empty<string>();
            // 구매비 추정용. 해금만 필요한 조건은 requiredUnlockModules에 둔다.
            public ModuleRequirement[] requiredModules = Array.Empty<ModuleRequirement>();
        }
        public Entry[] tests = Array.Empty<Entry>();
    }
}
