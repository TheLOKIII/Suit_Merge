using System;
using System.IO;
using SuitMerge.Core.Config;
using SuitMerge.Core.Simulation;
using UnityEditor;
using UnityEngine;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Mô phỏng Endless: máy tự chơi 200 ván / bot (Ngẫu nhiên + Tham lam) với cấu hình
    /// trong Data/Config/game_config.json, ghi bảng số liệu vào Docs/Playtest/endless-simulation.md (TẠO LẠI mỗi lần chạy).
    /// </summary>
    public static class EndlessSimulationMenu
    {
        private const string ConfigPath = GameSceneBuilder.Root + "Data/Config/game_config.json";
        private const string OutputFolder = GameSceneBuilder.Root + "Docs/Playtest";
        private const string OutputPath = OutputFolder + "/endless-simulation.md";
        private const int GamesPerBot = 200;
        private const uint FirstSeed = 1;
        private const double SecondsPerTurn = 3.0;

        [MenuItem("Suit Merge/Mô phỏng Endless", priority = 40)]
        private static void Run()
        {
            var config = new GameConfig();
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (json != null) JsonUtility.FromJsonOverwrite(json.text, config);
            else Debug.LogWarning($"[Suit Merge] Không thấy {ConfigPath} — mô phỏng với giá trị mặc định.");

            SimulationReport random, greedy;
            try
            {
                random = EndlessSimulator.Run(BotKind.Random, GamesPerBot, FirstSeed, config,
                    done => EditorUtility.DisplayProgressBar("Mô phỏng Endless", $"Bot Ngẫu nhiên: ván {done}/{GamesPerBot}", done / (2f * GamesPerBot)));
                greedy = EndlessSimulator.Run(BotKind.Greedy, GamesPerBot, FirstSeed, config,
                    done => EditorUtility.DisplayProgressBar("Mô phỏng Endless", $"Bot Tham lam: ván {done}/{GamesPerBot}", 0.5f + done / (2f * GamesPerBot)));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string md = SimulationReport.ToMarkdown(new[] { random, greedy }, config, FirstSeed, SecondsPerTurn, DateTime.Now);
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllText(OutputPath, md, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutputPath);

            string summary =
                $"Ngẫu nhiên: trung vị {random.MedianTurns:0} lượt (~{random.MedianSeconds(SecondsPerTurn) / 60:0.0} phút), điểm TB {random.AverageScore:0}\n" +
                $"Tham lam: trung vị {greedy.MedianTurns:0} lượt (~{greedy.MedianSeconds(SecondsPerTurn) / 60:0.0} phút), điểm TB {greedy.AverageScore:0}";
            Debug.Log($"[Suit Merge] Mô phỏng Endless xong ({GamesPerBot} ván/bot) → {OutputPath}\n{summary}");
            EditorUtility.DisplayDialog("Mô phỏng Endless", summary + $"\n\nBảng đầy đủ: {OutputPath}", "OK");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(OutputPath));
        }
    }
}
