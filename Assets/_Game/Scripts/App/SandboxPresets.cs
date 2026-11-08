using System;
using System.Collections.Generic;
using SuitMerge.Core.Model;
using UnityEngine;

namespace SuitMerge.App
{
    /// <summary>Một tình huống test (Data/Sandbox/sandbox_presets.json).</summary>
    [Serializable]
    public class SandboxPreset
    {
        public string name;
        /// <summary>Kết quả mong đợi (hiện trên thanh công cụ).</summary>
        public string expect;
        /// <summary>Chất đã mở: chuỗi mã D C S H (trống = chưa mở chất nào).</summary>
        public string suits;
        /// <summary>Lá trên tay lúc nạp, vd "4D".</summary>
        public string hand;
        /// <summary>Từng hàng bàn: "." trống, "#" ô khoá, "2H" lá.</summary>
        public string[] board;

        public Board BuildBoard() => Board.FromText(string.Join("\n", board ?? Array.Empty<string>()));

        public List<Suit> EnabledSuits()
        {
            var list = new List<Suit>();
            if (string.IsNullOrEmpty(suits)) return list;
            foreach (char ch in suits)
                if (SuitUtil.TryParseCode(ch, out Suit s) && !list.Contains(s)) list.Add(s);
            return list;
        }
    }

    [Serializable]
    public class SandboxPresetFile
    {
        public SandboxPreset[] presets;

        public static SandboxPreset[] Load(TextAsset json)
        {
            if (json == null) return Array.Empty<SandboxPreset>();
            var file = JsonUtility.FromJson<SandboxPresetFile>(json.text);
            return file?.presets ?? Array.Empty<SandboxPreset>();
        }
    }
}
