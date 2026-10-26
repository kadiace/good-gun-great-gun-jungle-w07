namespace WeaponWorkshop
{
    // 기본 프리셋 코드 조회. 슬롯 번호는 0 기반이다.
    public static class PresetDefaultInfo
    {
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
