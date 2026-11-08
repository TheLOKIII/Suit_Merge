using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Localization;

namespace SuitMerge.Tests
{
    /// <summary>Bước 4.5 — bảng chữ 2 ngôn ngữ (Data/Localization/Resources/strings.json).</summary>
    public class StringTableTests
    {
        private static string ProjectFile(string relative)
        {
            var roots = new List<string> { Directory.GetCurrentDirectory() };
            string env = Environment.GetEnvironmentVariable("SUITMERGE_PROJECT");
            if (!string.IsNullOrEmpty(env)) roots.Add(env);
            return roots.Select(r => Path.Combine(r, relative)).FirstOrDefault(File.Exists);
        }

        private static StringTable LoadProjectTable()
        {
            string path = ProjectFile("Assets/_Game/Data/Localization/Resources/strings.json");
            if (path == null) Assert.Inconclusive("Không tìm thấy strings.json (chạy từ thư mục project Unity).");
            return StringTable.Parse(File.ReadAllText(path));
        }

        [Test]
        public void ProjectTable_EveryKeyHasBothLanguages_AndSamePlaceholders()
        {
            StringTable table = LoadProjectTable();
            Assert.Greater(table.Count, 30);
            List<string> errors = table.Validate();
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        [Test]
        public void ProjectTable_HasKeysUsedByCode()
        {
            StringTable table = LoadProjectTable();
            string[] used =
            {
                "hud.menu", "hud.up_next", "hud.drag_hint", "hud.best", "hud.level", "hud.goals", "hud.deck", "goal.score", "goal.rank", "hud.pair_hint", "fail.no_space_pair",
                "pair.title", "pair.no_space", "btn.swap_next", "btn.hammer_one",
                "btn.resume", "btn.restart", "btn.settings", "btn.home", "btn.play_again", "btn.continue", "btn.give_up",
                "btn.got_it", "btn.endless", "pause.title", "gameover.title", "label.score", "label.best", "label.new_best",
                "complete.title", "complete.done", "complete.reward", "fail.out_of_cards", "fail.board_full", "fail.subtitle",
                "unlock.title", "unlock.D.name", "unlock.D.desc", "unlock.C.name", "unlock.C.desc", "unlock.S.name",
                "unlock.S.desc", "unlock.H.name", "unlock.H.desc", "select.title", "settings.title", "settings.language",
                "lang.vi", "lang.en", "settings.effects", "btn.fx_full", "btn.fx_reduced", "settings.sound", "settings.music", "btn.on", "btn.off", "fx.diamond", "fx.heart", "fx.club", "fx.spade", "fx.combo", "fx.ace"
            };
            foreach (string key in used) Assert.IsTrue(table.Contains(key), $"Thiếu key \"{key}\"");
            Assert.AreEqual("TẠM DỪNG", table.Get("pause.title", StringTable.Vietnamese), "Chữ Việt lấy đúng Figma");
            Assert.AreEqual("PAUSED", table.Get("pause.title", StringTable.English));
        }

        [Test]
        public void Get_FallsBackToVietnamese_AndUnknownKeyIsNull()
        {
            StringTable t = StringTable.Parse("{ \"a\": { \"vi\": \"Xin chào\" }, \"_note\": \"bỏ qua\" }");
            Assert.AreEqual("Xin chào", t.Get("a", StringTable.English), "Thiếu tiếng Anh → tiếng Việt");
            Assert.IsNull(t.Get("b", StringTable.Vietnamese));
            Assert.IsFalse(t.Contains("_note"));
            Assert.AreEqual(1, t.Validate().Count, "Báo thiếu \"en\"");
        }

        [Test]
        public void Validate_PlaceholderMismatch()
        {
            StringTable t = StringTable.Parse("{ \"a\": { \"vi\": \"LEVEL {0}\", \"en\": \"LEVEL\" } }");
            StringAssert.Contains("chỗ trống", t.Validate().Single());
        }

        [Test]
        public void FormatNumber_ByLanguage()
        {
            Assert.AreEqual("1.500", StringTable.FormatNumber(1500, StringTable.Vietnamese));
            Assert.AreEqual("1,500", StringTable.FormatNumber(1500, StringTable.English));
            Assert.AreEqual("100.000", StringTable.FormatNumber(100000, StringTable.Vietnamese));
            Assert.AreEqual("0", StringTable.FormatNumber(0, StringTable.English));
        }
    }
}
