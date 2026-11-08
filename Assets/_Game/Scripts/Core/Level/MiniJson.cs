using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SuitMerge.Core.Level
{
    /// <summary>JSON viết sai cú pháp. Có dòng / cột (đếm từ 1) để người viết level tìm chỗ sửa.</summary>
    public sealed class JsonParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public JsonParseException(string message, int line, int column)
            : base($"JSON lỗi ở dòng {line}, cột {column}: {message}")
        {
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// Bộ đọc JSON nhỏ, không phụ thuộc Unity (JsonUtility không đọc được mảng lồng / mảng trộn kiểu / null của spec 12.1).
    /// Kết quả: object → Dictionary&lt;string, object&gt;, mảng → List&lt;object&gt;, chuỗi → string,
    /// số → double, true/false → bool, null → null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new Reader(text);
            reader.SkipSpace();
            if (reader.AtEnd) throw reader.Error("file rỗng");
            object value = reader.ReadValue();
            reader.SkipSpace();
            if (!reader.AtEnd) throw reader.Error($"thừa ký tự '{reader.Peek}' sau khi đã hết dữ liệu");
            return value;
        }

        private sealed class Reader
        {
            private readonly string _s;
            private int _i;

            public Reader(string s)
            {
                _s = s;
                if (_s.Length > 0 && _s[0] == '﻿') _i = 1; // BOM
            }

            public bool AtEnd => _i >= _s.Length;
            public char Peek => _s[_i];

            public JsonParseException Error(string message) => ErrorAt(_i, message);

            private JsonParseException ErrorAt(int index, string message)
            {
                int line = 1, col = 1;
                for (int k = 0; k < index && k < _s.Length; k++)
                {
                    if (_s[k] == '\n') { line++; col = 1; }
                    else col++;
                }
                return new JsonParseException(message, line, col);
            }

            public void SkipSpace()
            {
                while (!AtEnd && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue()
            {
                SkipSpace();
                if (AtEnd) throw Error("file kết thúc giữa chừng — thiếu dữ liệu hoặc thiếu dấu đóng ']' / '}'");
                char c = Peek;
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ReadWord("true"); return true;
                    case 'f': ReadWord("false"); return false;
                    case 'n': ReadWord("null"); return null;
                }
                if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                if (c == '\'') throw Error("chuỗi phải đặt trong ngoặc kép \"...\", không dùng ngoặc đơn");
                throw Error($"ký tự '{c}' không hợp lệ ở đây");
            }

            private Dictionary<string, object> ReadObject()
            {
                var obj = new Dictionary<string, object>();
                _i++; // {
                SkipSpace();
                if (!AtEnd && Peek == '}') { _i++; return obj; }
                while (true)
                {
                    SkipSpace();
                    if (AtEnd) throw Error("thiếu dấu '}'");
                    if (Peek == '}') throw Error("dấu ',' thừa trước '}'");
                    if (Peek != '"') throw Error("tên trường phải đặt trong ngoặc kép, vd \"rank\"");
                    int keyStart = _i;
                    string key = ReadString();
                    if (obj.ContainsKey(key)) throw ErrorAt(keyStart, $"trường \"{key}\" bị viết 2 lần");
                    SkipSpace();
                    if (AtEnd || Peek != ':') throw Error($"thiếu dấu ':' sau \"{key}\"");
                    _i++;
                    obj[key] = ReadValue();
                    int afterValue = _i;
                    SkipSpace();
                    if (AtEnd) throw Error("thiếu dấu '}'");
                    if (Peek == ',') { _i++; continue; }
                    if (Peek == '}') { _i++; return obj; }
                    throw ErrorAt(afterValue, "thiếu dấu ',' sau giá trị này (hoặc thiếu '}')");
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                _i++; // [
                SkipSpace();
                if (!AtEnd && Peek == ']') { _i++; return list; }
                while (true)
                {
                    SkipSpace();
                    if (!AtEnd && Peek == ']') throw Error("dấu ',' thừa trước ']'");
                    list.Add(ReadValue());
                    int afterValue = _i;
                    SkipSpace();
                    if (AtEnd) throw Error("thiếu dấu ']'");
                    if (Peek == ',') { _i++; continue; }
                    if (Peek == ']') { _i++; return list; }
                    throw ErrorAt(afterValue, "thiếu dấu ',' sau phần tử này (hoặc thiếu ']')");
                }
            }

            private string ReadString()
            {
                int start = _i;
                _i++; // "
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw ErrorAt(start, "chuỗi chưa đóng ngoặc kép");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\n') throw ErrorAt(start, "chuỗi chưa đóng ngoặc kép trước khi xuống dòng");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("chuỗi chưa đóng ngoặc kép");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length ||
                                !int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Error("mã \\u phải có 4 chữ số hex");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default:
                            throw ErrorAt(_i - 2, $"ký tự thoát '\\{e}' không hợp lệ");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _i;
                if (Peek == '-') _i++;
                while (!AtEnd && (char.IsDigit(Peek) || Peek == '.' || Peek == 'e' || Peek == 'E' || Peek == '+' || Peek == '-'))
                    _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw ErrorAt(start, $"số '{token}' không hợp lệ");
                return value;
            }

            private void ReadWord(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                    throw Error("giá trị không hợp lệ — chữ phải đặt trong ngoặc kép, vd \"2H\"");
                _i += word.Length;
            }
        }
    }
}
