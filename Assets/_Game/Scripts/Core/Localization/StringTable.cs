using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using SuitMerge.Core.Level;

namespace SuitMerge.Core.Localization
{
    /// <summary>
    /// Bảng chữ 2 ngôn ngữ (bước 4.5): file JSON dạng <c>{ "key": { "vi": "...", "en": "..." } }</c>.
    /// Chữ có thể chứa chỗ trống {0}, {1}… (string.Format). Không phụ thuộc Unity → test được.
    /// </summary>
    public sealed class StringTable
    {
        public const string Vietnamese = "vi";
        public const string English = "en";
        public static readonly string[] Languages = { Vietnamese, English };

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}");

        private readonly Dictionary<string, Dictionary<string, string>> _entries =
            new Dictionary<string, Dictionary<string, string>>();

        public IEnumerable<string> Keys => _entries.Keys;
        public int Count => _entries.Count;

        /// <summary>Đọc bảng chữ; JSON sai cú pháp → JsonParseException (có dòng / cột).</summary>
        public static StringTable Parse(string json)
        {
            var table = new StringTable();
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
                throw new FormatException("Bảng chữ phải là 1 object { \"key\": { \"vi\": \"...\", \"en\": \"...\" } }");
            foreach (KeyValuePair<string, object> entry in root)
            {
                if (entry.Key.StartsWith("_")) continue; // ghi chú
                var texts = new Dictionary<string, string>();
                if (entry.Value is Dictionary<string, object> obj)
                    foreach (KeyValuePair<string, object> t in obj)
                        if (t.Value is string s) texts[t.Key] = s;
                table._entries[entry.Key] = texts;
            }
            return table;
        }

        public bool Contains(string key) => key != null && _entries.ContainsKey(key);

        /// <summary>Chữ theo ngôn ngữ; thiếu bản ngôn ngữ đó → tiếng Việt; không có key → null.</summary>
        public string Get(string key, string language)
        {
            if (key == null || !_entries.TryGetValue(key, out Dictionary<string, string> texts)) return null;
            if (texts.TryGetValue(language ?? Vietnamese, out string text) && !string.IsNullOrEmpty(text)) return text;
            return texts.TryGetValue(Vietnamese, out string vi) ? vi : null;
        }

        /// <summary>Lỗi của bảng chữ: thiếu ngôn ngữ, chữ rỗng, chỗ trống {n} khác nhau giữa vi và en.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            foreach (KeyValuePair<string, Dictionary<string, string>> entry in _entries)
            {
                foreach (string lang in Languages)
                    if (!entry.Value.TryGetValue(lang, out string t) || string.IsNullOrWhiteSpace(t))
                        errors.Add($"\"{entry.Key}\": thiếu chữ \"{lang}\"");
                if (entry.Value.TryGetValue(Vietnamese, out string vi) && entry.Value.TryGetValue(English, out string en)
                    && PlaceholderSet(vi) != PlaceholderSet(en))
                    errors.Add($"\"{entry.Key}\": chỗ trống {{n}} của vi ({PlaceholderSet(vi)}) và en ({PlaceholderSet(en)}) khác nhau");
            }
            return errors;
        }

        private static string PlaceholderSet(string text)
        {
            var found = new SortedSet<int>();
            foreach (Match m in Placeholder.Matches(text)) found.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            return string.Join(",", found);
        }

        /// <summary>Số có dấu phân cách hàng nghìn: vi "1.500", en "1,500".</summary>
        public static string FormatNumber(long value, string language)
        {
            string s = value.ToString("N0", CultureInfo.InvariantCulture); // 1,500
            return language == English ? s : s.Replace(',', '.');
        }
    }
}
