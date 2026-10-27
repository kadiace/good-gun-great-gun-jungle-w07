namespace WeaponWorkshop
{
    // 기본 프리셋 코드 조회. 슬롯 번호는 0 기반이다.
    public static class PresetDefaultInfo
    {
        // 세이브에 남은 이전 기본값 코드를 현재 기본값으로 바꾼다. 그 밖의 코드는 그대로 둔다.
        public static string Migrate(PresetDefaultData _table, int _slot, string _code)
        {
            if (_table == null || _table.legacy == null || string.IsNullOrEmpty(_code)) return _code;
            foreach (var entry in _table.legacy)
            {
                if (entry != null && entry.slot == _slot && entry.code == _code)
                {
                    return CodeFor(_table, _slot) ?? _code;
                }
            }
            return _code;
        }

        public static string CodeFor(PresetDefaultData _table, int _slot)
        {
            if (_table == null || _table.presets == null) return null;
            foreach (var entry in _table.presets)
            {
                if (entry != null && entry.slot == _slot && !string.IsNullOrEmpty(entry.code)) return entry.code;
            }
            return null;
        }
    }
}
