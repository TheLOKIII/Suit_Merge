using System.IO;
using UnityEditor;
using UnityEngine;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Chụp Game view: lưu ảnh Game view (khi đang Play) vào Docs/Ref/capture-game.png
    /// để so với mockup Figma (Docs/Ref/mockup-endless.png).
    /// </summary>
    public static class CaptureGameView
    {
        private const string OutputPath = "Assets/_Game/Docs/Ref/capture-game.png";

        [MenuItem("Suit Merge/Chụp Game view", priority = 20)]
        private static void Capture()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Chụp Game view",
                    "Hãy bấm Play trước, đợi màn hình hiện xong rồi chọn lại menu này.", "OK");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            System.DateTime requestedAt = System.DateTime.Now.AddSeconds(-1);
            ScreenCapture.CaptureScreenshot(OutputPath);

            // Unity ghi file ở cuối frame → đợi file mới xuất hiện rồi mới import (tối đa 5 giây)
            double deadline = EditorApplication.timeSinceStartup + 5.0;
            void WaitForFile()
            {
                bool written = File.Exists(OutputPath) && File.GetLastWriteTime(OutputPath) >= requestedAt;
                if (!written && EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= WaitForFile;
                if (written)
                {
                    AssetDatabase.ImportAsset(OutputPath);
                    Debug.Log($"[Suit Merge] Đã chụp Game view → {OutputPath} (kích thước = độ phân giải Game view hiện tại)");
                }
                else
                {
                    Debug.LogWarning("[Suit Merge] Chưa chụp được Game view — hãy mở tab Game rồi thử lại.");
                }
            }
            EditorApplication.update += WaitForFile;
        }
    }
}
