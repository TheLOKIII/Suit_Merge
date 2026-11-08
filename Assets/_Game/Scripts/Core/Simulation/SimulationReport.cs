using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SuitMerge.Core.Config;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Simulation
{
    /// <summary>Tổng hợp số liệu của nhiều ván mô phỏng cùng 1 kiểu bot.</summary>
    public sealed class SimulationReport
    {
        /// <summary>Spec mục 1: một phiên chơi 1–3 phút.</summary>
        public const double TargetMinSeconds = 60;
        public const double TargetMaxSeconds = 180;

        public BotKind Bot { get; }
        public IReadOnlyList<GameRunStats> Runs { get; }

        public SimulationReport(BotKind bot, IReadOnlyList<GameRunStats> runs)
        {
            Bot = bot;
            Runs = runs;
        }

        public int Games => Runs.Count;
        public double AverageTurns => Runs.Count == 0 ? 0 : Runs.Average(r => r.Turns);
        public double MedianTurns => Median(Runs.Select(r => (double)r.Turns));
        public double AverageScore => Runs.Count == 0 ? 0 : Runs.Average(r => r.Score);
        public double MedianScore => Median(Runs.Select(r => (double)r.Score));
        public int MaxScore => Runs.Count == 0 ? 0 : Runs.Max(r => r.Score);
        public int TotalAces => Runs.Sum(r => r.Aces);
        public int Unfinished => Runs.Count(r => !r.Finished);

        /// <summary>Tỉ lệ (0–1) số ván có lá đạt ít nhất <paramref name="rank"/>.</summary>
        public double ReachRate(Rank rank) =>
            Runs.Count == 0 ? 0 : Runs.Count(r => r.HighestRank >= (int)rank) / (double)Runs.Count;

        /// <summary>Thời gian ván trung vị ước tính, với <paramref name="secondsPerTurn"/> giây mỗi lượt.</summary>
        public double MedianSeconds(double secondsPerTurn) => MedianTurns * secondsPerTurn;

        public bool WithinTargetSession(double secondsPerTurn)
        {
            double s = MedianSeconds(secondsPerTurn);
            return s >= TargetMinSeconds && s <= TargetMaxSeconds;
        }

        public static string BotName(BotKind bot) =>
            bot == BotKind.Greedy ? "Tham lam (người chơi khá)" : "Ngẫu nhiên (người chơi kém)";

        /// <summary>Bảng markdown so sánh nhiều kiểu bot (dùng cho Docs/Playtest/endless-simulation.md).</summary>
        public static string ToMarkdown(IReadOnlyList<SimulationReport> reports, GameConfig config, uint firstSeed,
            double secondsPerTurn, DateTime now)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Mô phỏng Endless — số liệu độ khó");
            sb.AppendLine();
            sb.AppendLine($"> Tạo lúc {now.ToString("dd/MM/yyyy HH:mm", ci)} · menu **Suit Merge → Mô phỏng Endless** · file này được TẠO LẠI mỗi lần chạy.");
            sb.AppendLine();
            sb.AppendLine("## Cấu hình lúc chạy (`Data/Config/game_config.json`)");
            sb.AppendLine("| Khoá | Giá trị |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| endlessStartHighest | {config.endlessStartHighest} |");
            sb.AppendLine($"| endlessLowOffset | {config.endlessLowOffset} |");
            sb.AppendLine($"| endlessHighOffset | {config.endlessHighOffset} |");
            sb.AppendLine($"| endlessMinHigh | {config.endlessMinHigh} |");
            sb.AppendLine($"| chainBonusPerStep | {config.chainBonusPerStep.ToString(ci)} |");
            sb.AppendLine();
            int games = reports.Count > 0 ? reports[0].Games : 0;
            sb.AppendLine($"Mỗi bot chơi **{games} ván**, seed {firstSeed} → {firstSeed + (uint)Math.Max(0, games - 1)}. " +
                          $"Thời gian ước tính = số lượt × **{secondsPerTurn.ToString(ci)} giây/lượt**. " +
                          "Endless mở đủ 4 hiệu ứng chất (♦ ×3 · ♣ sinh lá · ♠ dọn 3×3 · ♥ nhảy rank).");
            sb.AppendLine();
            sb.AppendLine("## Kết quả");
            sb.Append("| Chỉ số |");
            foreach (SimulationReport r in reports) sb.Append($" {BotName(r.Bot)} |");
            sb.AppendLine();
            sb.Append("|---|");
            foreach (SimulationReport _ in reports) sb.Append("---|");
            sb.AppendLine();

            Row(sb, reports, "Số lượt trung bình", r => r.AverageTurns.ToString("0.0", ci));
            Row(sb, reports, "Số lượt trung vị", r => r.MedianTurns.ToString("0", ci));
            Row(sb, reports, "Thời gian ván trung vị (ước tính)", r => FormatTime(r.MedianSeconds(secondsPerTurn)));
            Row(sb, reports, "Trong khoảng spec 1–3 phút?", r => r.WithinTargetSession(secondsPerTurn) ? "✓ đạt" : "✗ chưa");
            Row(sb, reports, "Điểm trung bình", r => r.AverageScore.ToString("0", ci));
            Row(sb, reports, "Điểm trung vị", r => r.MedianScore.ToString("0", ci));
            Row(sb, reports, "Điểm cao nhất", r => r.MaxScore.ToString(ci));
            Row(sb, reports, "Ván đạt lá 8", r => Percent(r.ReachRate(Rank.Eight)));
            Row(sb, reports, "Ván đạt lá 10", r => Percent(r.ReachRate(Rank.Ten)));
            Row(sb, reports, "Ván đạt lá Q", r => Percent(r.ReachRate(Rank.Queen)));
            Row(sb, reports, "Ván đạt lá K", r => Percent(r.ReachRate(Rank.King)));
            Row(sb, reports, "Tổng số Ace", r => r.TotalAces.ToString(ci));
            Row(sb, reports, "Ván dừng vì giới hạn lượt", r => r.Unfinished.ToString(ci));
            sb.AppendLine();
            sb.AppendLine("## Cách đọc");
            sb.AppendLine("- **Ngẫu nhiên** ≈ người chơi không suy nghĩ; **Tham lam** ≈ người chơi khá (luôn chọn nước nhiều điểm nhất trước mắt). Người thật thường nằm giữa hoặc trên mức Tham lam.");
            sb.AppendLine("- **Độ dài ván** chủ yếu do `endlessHighOffset`: tăng (vd 1 → 2) → lá rút ra thấp hơn lá cao nhất nhiều hơn → khó nối tiếp → ván **ngắn lại** (khó hơn); giảm → ván dài hơn.");
            sb.AppendLine("- **Độ sâu** (lên được lá cao: K, Ace) chủ yếu do `endlessLowOffset`: nhỏ hơn → khoảng rank hẹp → dễ lên lá cao; lớn hơn → khó lên lá cao. Độ dài ván gần như không đổi.");
            sb.AppendLine("- Bot **Ngẫu nhiên** gần như không bị ảnh hưởng (thua sớm khi bàn còn toàn lá thấp) → dùng bot **Tham lam** để so sánh cấu hình.");
            sb.AppendLine("- **Điểm trung bình** bị vài ván ra nhiều Ace (+100,000 mỗi Ace) kéo lên rất cao → so sánh bằng **điểm trung vị**.");
            sb.AppendLine("- Chỉnh `game_config.json` → chạy lại menu → so sánh bảng mới với bảng cũ (file này bị ghi đè — sao lưu bảng cũ nếu cần).");
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, IReadOnlyList<SimulationReport> reports, string label,
            Func<SimulationReport, string> value)
        {
            sb.Append($"| {label} |");
            foreach (SimulationReport r in reports) sb.Append($" {value(r)} |");
            sb.AppendLine();
        }

        private static string Percent(double rate) => (rate * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        private static string FormatTime(double seconds)
        {
            int total = (int)Math.Round(seconds);
            return $"{total / 60}:{total % 60:00}";
        }

        private static double Median(IEnumerable<double> values)
        {
            double[] sorted = values.OrderBy(v => v).ToArray();
            if (sorted.Length == 0) return 0;
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        }
    }
}
