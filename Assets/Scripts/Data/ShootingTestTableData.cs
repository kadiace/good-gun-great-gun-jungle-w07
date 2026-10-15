using System;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class ShootingTestTableData
    {
        [Serializable]
        public sealed class Entry
        {
            public string id, name, instructions, failureHint;
            public bool enabled = true;
            public float timeLimit, requiredDestruction;
        }
        public Entry[] tests = Array.Empty<Entry>();
    }
}
