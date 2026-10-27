using System.Collections.Generic;
using System.Linq;

namespace WeaponWorkshop
{
    // 플레이어가 직접 만든 무기에서 한 번이라도 만족한 태그를 기억한다.
    public static class TagExperience
    {
        private static readonly HashSet<string> experienced = new HashSet<string>(System.StringComparer.Ordinal);

        public static void Mark(string _tagId)
        {
            if (!string.IsNullOrEmpty(_tagId))
            {
                experienced.Add(_tagId);
            }
        }

        public static bool Has(string _tagId)
        {
            return !string.IsNullOrEmpty(_tagId) && experienced.Contains(_tagId);
        }

        public static string[] Capture()
        {
            return experienced.ToArray();
        }

        public static void Restore(string[] _tagIds)
        {
            experienced.Clear();
            if (_tagIds == null)
            {
                return;
            }

            foreach (string tagId in _tagIds)
            {
                if (!string.IsNullOrEmpty(tagId))
                {
                    experienced.Add(tagId);
                }
            }
        }

        public static void Reset()
        {
            experienced.Clear();
        }
    }
}
