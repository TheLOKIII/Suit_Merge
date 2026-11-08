using System;
using System.Linq;
using NUnit.Framework;
using SuitMerge.Core.Config;
using SuitMerge.Core.Simulation;

namespace SuitMerge.Tests
{
    /// <summary>Bộ mô phỏng Endless (bước 2.6) — công cụ playtest, không phải luật chơi.</summary>
    public class EndlessSimulatorTests
    {
        [TestCase(BotKind.Random)]
        [TestCase(BotKind.Greedy)]
        public void SameSeed_SameResult(BotKind bot)
        {
            GameRunStats a = EndlessSimulator.PlayOne(77, bot, GameConfig.Default);
            GameRunStats b = EndlessSimulator.PlayOne(77, bot, GameConfig.Default);
            Assert.AreEqual(a.Turns, b.Turns);
            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.HighestRank, b.HighestRank);
        }

        [TestCase(BotKind.Random)]
        [TestCase(BotKind.Greedy)]
        public void EveryGame_EndsByLosing(BotKind bot)
        {
            SimulationReport report = EndlessSimulator.Run(bot, 20, 1, GameConfig.Default);
            Assert.AreEqual(20, report.Games);
            Assert.AreEqual(0, report.Unfinished, "Mọi ván phải kết thúc vì bàn đầy, không chạm giới hạn lượt");
            Assert.IsTrue(report.Runs.All(r => r.Turns > 0));
        }

        [Test]
        public void Greedy_ScoresAtLeastAsWellAsRandom()
        {
            SimulationReport random = EndlessSimulator.Run(BotKind.Random, 30, 1, GameConfig.Default);
            SimulationReport greedy = EndlessSimulator.Run(BotKind.Greedy, 30, 1, GameConfig.Default);
            Assert.GreaterOrEqual(greedy.AverageScore, random.AverageScore);
            Assert.GreaterOrEqual(greedy.AverageTurns, random.AverageTurns);
        }

        [Test]
        public void Report_AggregatesMatchRuns()
        {
            SimulationReport report = EndlessSimulator.Run(BotKind.Random, 9, 100, GameConfig.Default);
            Assert.AreEqual(report.Runs.Average(r => r.Turns), report.AverageTurns, 1e-9);
            Assert.AreEqual(report.Runs.Max(r => r.Score), report.MaxScore);
            double[] sorted = report.Runs.Select(r => (double)r.Turns).OrderBy(t => t).ToArray();
            Assert.AreEqual(sorted[4], report.MedianTurns, 1e-9);

            // Từng ván chạy riêng phải ra cùng số liệu
            GameRunStats single = EndlessSimulator.PlayOne(103, BotKind.Random, GameConfig.Default);
            Assert.AreEqual(single.Score, report.Runs[3].Score);
        }

        [Test]
        public void Markdown_ContainsConfigAndRows()
        {
            var reports = new[]
            {
                EndlessSimulator.Run(BotKind.Random, 3, 1, GameConfig.Default),
                EndlessSimulator.Run(BotKind.Greedy, 3, 1, GameConfig.Default)
            };
            string md = SimulationReport.ToMarkdown(reports, GameConfig.Default, 1, 3.0, new DateTime(2026, 10, 5));
            StringAssert.Contains("endlessLowOffset", md);
            StringAssert.Contains("Số lượt trung vị", md);
            StringAssert.Contains("Tham lam", md);
            StringAssert.Contains("Ngẫu nhiên", md);
        }
    }
}
