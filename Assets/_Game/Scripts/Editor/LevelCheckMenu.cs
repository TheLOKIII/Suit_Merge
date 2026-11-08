using System.IO;
using System.Linq;
using System.Text;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Level;
using UnityEditor;
using UnityEngine;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Kiểm tra file level: đọc mọi Data/Levels/*.json bằng LevelLoader,
    /// in ra Console mỗi file 1 mục (OK + tóm tắt + dãy lá sẽ rút, hoặc danh sách lỗi / cảnh báo). Không sửa file nào.
    /// </summary>
    public static class LevelCheckMenu
    {
        private const string LevelFolder = GameSceneBuilder.Root + "Data/Levels";

        [MenuItem("Suit Merge/Kiểm tra file level", priority = 41)]
        private static void Run()
        {
            string[] files = Directory.Exists(LevelFolder)
                ? Directory.GetFiles(LevelFolder, "*.json").OrderBy(f => f).ToArray()
                : new string[0];
            if (files.Length == 0)
            {
                Debug.LogWarning($"[Suit Merge] Không có file level nào trong {LevelFolder}");
                EditorUtility.DisplayDialog("Kiểm tra file level", $"Không có file .json nào trong {LevelFolder}", "OK");
                return;
            }

            int ok = 0, failed = 0, warned = 0;
            foreach (string path in files)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                LevelLoadResult result = LevelLoader.Load(File.ReadAllText(path, Encoding.UTF8), name);
                if (!result.Ok)
                {
                    failed++;
                    Debug.LogError($"[Suit Merge] {name}: {result.Errors.Count} LỖI\n" + Lines(result.Errors) +
                                   (result.Warnings.Count > 0 ? "\nCảnh báo:\n" + Lines(result.Warnings) : ""),
                        AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                    continue;
                }

                ok++;
                string summary = $"[Suit Merge] {name}: OK — {LevelLoader.Summary(result.Level)}\n  {Sequence(result.Level)}";
                if (result.Warnings.Count == 0) Debug.Log(summary, AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                else
                {
                    warned++;
                    Debug.LogWarning($"{summary}\n{result.Warnings.Count} cảnh báo:\n" + Lines(result.Warnings),
                        AssetDatabase.LoadAssetAtPath<TextAsset>(path));
                }
            }

            string message = $"Đã kiểm tra {files.Length} file: {ok} OK ({warned} có cảnh báo), {failed} lỗi.\nChi tiết xem Console.";
            Debug.Log("[Suit Merge] " + message);
            EditorUtility.DisplayDialog("Kiểm tra file level", message, "OK");
        }

        /// <summary>Dãy lá sẽ rút (bước 4.2) — để chọn seed bằng mắt.</summary>
        private static string Sequence(LevelData level)
        {
            try
            {
                return LevelDeck.DescribeSequence(level.Deck);
            }
            catch (System.NotSupportedException e)
            {
                return "Dãy lá: chưa xem được — " + e.Message;
            }
        }

        private static string Lines(System.Collections.Generic.IEnumerable<string> items) =>
            string.Join("\n", items.Select(s => "  • " + s));
    }
}
