using System;
using System.Collections.Generic;
using SuitMerge.Core.Localization;
using UnityEngine;

namespace SuitMerge.View.Localization
{
    /// <summary>
    /// Chữ 2 ngôn ngữ cho View (bước 4.5). Tự nạp Data/Localization/Resources/strings.json lần đầu dùng.
    /// Ngôn ngữ mặc định: tiếng Việt; lựa chọn được lưu (PlayerPrefs) khi tắt game.
    /// </summary>
    public static class Loc
    {
        private const string ResourceName = "strings";
        private const string PrefKey = "suitmerge.language";

        private static StringTable _table;
        private static string _language;
        private static readonly HashSet<string> WarnedKeys = new HashSet<string>();

        /// <summary>Đổi ngôn ngữ → mọi chữ đang hiện tự cập nhật.</summary>
        public static event Action Changed;

        public static string Language
        {
            get
            {
                if (_language == null)
                {
                    string saved = PlayerPrefs.GetString(PrefKey, StringTable.Vietnamese);
                    _language = saved == StringTable.English ? StringTable.English : StringTable.Vietnamese;
                }
                return _language;
            }
        }

        public static bool IsEnglish => Language == StringTable.English;

        public static void SetLanguage(string language)
        {
            language = language == StringTable.English ? StringTable.English : StringTable.Vietnamese;
            if (language == Language) return;
            _language = language;
            PlayerPrefs.SetString(PrefKey, language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static StringTable Table
        {
            get
            {
                if (_table != null) return _table;
                var asset = Resources.Load<TextAsset>(ResourceName);
                if (asset == null)
                {
                    Debug.LogWarning("[Suit Merge] Không tìm thấy bảng chữ Resources/strings.json — chữ sẽ hiện tên key.");
                    _table = StringTable.Parse("{}");
                }
                else
                {
                    try
                    {
                        _table = StringTable.Parse(asset.text);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[Suit Merge] strings.json lỗi: " + e.Message);
                        _table = StringTable.Parse("{}");
                    }
                }
                return _table;
            }
        }

        /// <summary>Chữ theo ngôn ngữ đang chọn; không có key → trả về chính key (và cảnh báo 1 lần).</summary>
        public static string Get(string key)
        {
            string text = Table.Get(key, Language);
            if (text != null) return text;
            if (key != null && WarnedKeys.Add(key)) Debug.LogWarning($"[Suit Merge] Bảng chữ thiếu key \"{key}\"");
            return key ?? "";
        }

        public static string Format(string key, params object[] args)
        {
            try
            {
                return string.Format(Get(key), args);
            }
            catch (FormatException)
            {
                return Get(key);
            }
        }

        /// <summary>Số có dấu phân cách: tiếng Việt "1.500", tiếng Anh "1,500".</summary>
        public static string Number(long value) => StringTable.FormatNumber(value, Language);
    }
}
