using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SuitMerge.Core.Config;
using SuitMerge.Core.Level;
using SuitMerge.Core.Simulation;
using UnityEditor;
using UnityEngine;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Mô phỏng Level: bot Ngẫu nhiên + Tham lam chơi mọi Data/Levels/*.json với seed trong file
    /// và vài seed thay thế, ghi bảng tỉ lệ thắng vào Docs/Playtest/level-simulation.md (TẠO LẠI mỗi lần chạy).
    /// Không sửa file level nào.
    /// </summary>
    public static class LevelSimulationMenu
    {
        private const string ConfigPath = GameSceneBuilder.Root + "Data/Config/game_config.json";
        private const string LevelFolder = GameSceneBuilder.Root + "Data/Levels";
        private const string OutputFolder = GameSceneBuilder.Root + "Docs/Playtest";
        private const string OutputPath = OutputFolder + "/level-simulation.md";
        private const int RandomGames = 200;
        private const int GreedyGames = 50;
        private const int AlternativeSeeds = 5;

        [MenuItem("Suit Merge/Mô phỏng Level", priority = 42)]
        private static void Run()
        {
            var config = new GameConfig();
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (json != null) JsonUtility.FromJsonOverwrite(json.text, config);

            var levels = new List<LevelData>();
            foreach (string path in Directory.Exists(LevelFolder) ? Directory.GetFiles(LevelFolder, "*.json").OrderBy(f => f) : Enumerable.Empty<string>())
            {
                LevelLoadResult r = LevelLoader.Load(File.ReadAllText(path, Encoding.UTF8), Path.GetFileNameWithoutExtension(path));
                if (r.Ok) levels.Add(r.Level);
                else Debug.LogWarning($"[Suit Merge] Bỏ qua {Path.GetFileName(path)}: file lỗi — chạy menu Kiểm tra file level để xem.");
            }
            if (levels.Count == 0)
            {
                EditorUtility.DisplayDialog("Mô phỏng Level", $"Không có file level hợp lệ trong {LevelFolder}", "OK");
                return;
            }

            var main = new List<LevelSimulationResult>();
            var alternatives = new List<LevelSimulationResult>();
            try
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    LevelData level = levels[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Mô phỏng Level", $"Level {level.Id}", i / (float)levels.Count)) return;
                    try
                    {
                        main.Add(LevelSimulator.Run(level, BotKind.Random, RandomGames, config));
                        main.Add(LevelSimulator.Run(level, BotKind.Greedy, GreedyGames, config));
                        if (level.Deck.Random == null || level.Deck.RandomCardCount == 0) continue;
                        for (uint k = 1; k <= AlternativeSeeds; k++)
                        {
                            LevelData alt = LevelSimulator.WithDeckSeed(level, level.Deck.Random.Seed + k);
                            alternatives.Add(LevelSimulator.Run(alt, BotKind.Random, RandomGames, config));
                            alternatives.Add(LevelSimulator.Run(alt, BotKind.Greedy, GreedyGames, config));
                        }
                    }
                    catch (NotSupportedException e)
                    {
                        Debug.LogWarning($"[Suit Merge] Bỏ qua level {level.Id}: {e.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Directory.CreateDirectory(OutputFolder);
            File.WriteAllText(OutputPath, LevelSimulator.ToMarkdown(main, alternatives, DateTime.Now), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutputPath);

            string summary = string.Join("\n", main.Where(r => r.Bot == BotKind.Greedy)
                .Select(r => $"L{r.Level.Id}: Tham lam thắng {r.WinRate * 100:0}%"));
            Debug.Log($"[Suit Merge] Mô phỏng Level xong → {OutputPath}\n{summary}");
            EditorUtility.DisplayDialog("Mô phỏng Level", summary + $"\n\nBảng đầy đủ: {OutputPath}", "OK");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(OutputPath));
        }
    }
}
