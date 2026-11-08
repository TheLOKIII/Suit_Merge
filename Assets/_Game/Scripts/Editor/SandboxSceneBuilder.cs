using System;
using System.IO;
using System.Text;
using SuitMerge.App;
using SuitMerge.View.Animation;
using SuitMerge.View.Board;
using SuitMerge.View.Cards;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using SuitMerge.View.Sandbox;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SuitMerge.EditorTools.GameSceneBuilder;
using Object = UnityEngine.Object;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Tạo scene Sandbox (test): scene công cụ test merge / hiệu ứng chất / mechanic sau này.
    /// Dùng lại bàn, tay, HUD, lớp Fx, EventPlayer, FxTheme như scene Game; thay GameBootstrap bằng SandboxController
    /// + thanh công cụ. KHÔNG ghi đè scene đã có, KHÔNG thêm vào Build Settings (công cụ dev).
    /// Cần chạy "Tạo scene Game" trước (để có font, CardTheme, prefab lá / ô).
    /// </summary>
    public static class SandboxSceneBuilder
    {
        internal const string SandboxScenePath = Root + "Scenes/Sandbox.unity";
        private const string PresetsPath = Root + "Data/Sandbox/sandbox_presets.json";
        private const string ConfigPath = Root + "Data/Config/game_config.json";
        private const string Title = "Tạo scene Sandbox";

        [MenuItem("Suit Merge/Tạo scene Sandbox (test)", priority = 2)]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(Title, "Hãy thoát Play mode trước.", "OK");
                return;
            }

            if (File.Exists(SandboxScenePath))
            {
                bool open = EditorUtility.DisplayDialog(Title,
                    $"Scene đã tồn tại:\n{SandboxScenePath}\n\nMenu này KHÔNG ghi đè scene. Muốn dựng lại thì tự xoá/đổi tên scene cũ trước.\n\nMở scene hiện có?",
                    "Mở scene", "Đóng");
                if (open && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(SandboxScenePath);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(MiniCardPrefabPath) == null
                || AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath) == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "Chưa có font / prefab lá bài. Hãy chạy Suit Merge → Tạo scene Game trước.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder("[Suit Merge] Tạo scene Sandbox:\n");
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Nạp asset SAU NewScene (NewScene giải phóng asset không ai dùng khỏi bộ nhớ)
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                var theme = AssetDatabase.LoadAssetAtPath<CardTheme>(ThemePath);
                var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath).GetComponent<CardView>();
                var miniPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MiniCardPrefabPath).GetComponent<MiniCardView>();
                var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath).GetComponent<CellView>();

                BuildScene(font, cardPrefab, miniPrefab, cellPrefab, withMockupPreview: false);
                log.AppendLine("  + Camera, Canvas, bàn, tay, HUD (giống scene Game)");

                var boardView = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
                var hudView = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
                var handView = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
                var frame = (RectTransform)boardView.transform.parent;

                GameSceneUpdater.EnsureBestText(hudView, font, log);

                // Thanh công cụ (trên bàn / tay, dưới lớp Fx)
                RectTransform toolbarRt = NewUI("SandboxToolbar", frame);
                Stretch(toolbarRt);
                var toolbar = toolbarRt.gameObject.AddComponent<SandboxToolbar>();
                SetRef(toolbar, "font", font);
                SetRef(toolbar, "cardTheme", theme);
                SetRef(toolbar, "buttonSprite", LoadSprite("Effects/fx-flash.png"));
                SetRef(toolbar, "pillSprite", LoadSprite("UI/btn-pill.png"));
                SetRef(toolbar, "panelSprite", LoadSprite("Board/board-panel.png"));
                SetRef(toolbar, "boardRect", boardView.transform);
                log.AppendLine("  + Thanh công cụ Sandbox");

                // Lớp Fx + EventPlayer (giống bước 2.5 của scene Game)
                RectTransform fx = NewUI("Fx", frame);
                Stretch(fx);
                var group = fx.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                fx.SetAsLastSibling();
                var player = new GameObject("EventPlayer").AddComponent<EventPlayer>();
                SetRef(player, "fxLayer", fx);
                SetRef(player, "boardView", boardView);
                SetRef(player, "handView", handView);
                SetRef(player, "hudView", hudView);
                SetRef(player, "flyingCardPrefab", cardPrefab);
                SetRef(player, "font", font);
                log.AppendLine("  + Lớp Fx + EventPlayer");
                GameSceneUpdater.EnsureStep14Fx(log); // FxTheme (hiệu ứng chất)

                var controller = new GameObject("SandboxController").AddComponent<SandboxController>();
                var config = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
                var presets = AssetDatabase.LoadAssetAtPath<TextAsset>(PresetsPath);
                if (presets == null) log.AppendLine($"    (cảnh báo: không thấy {PresetsPath})");
                SetRef(controller, "configJson", config);
                SetRef(controller, "presetsJson", presets);
                SetRef(controller, "boardView", boardView);
                SetRef(controller, "hudView", hudView);
                SetRef(controller, "handView", handView);
                SetRef(controller, "eventPlayer", player);
                SetRef(controller, "toolbar", toolbar);
                log.AppendLine("  + SandboxController");

                EditorSceneManager.SaveScene(scene, SandboxScenePath);
                log.AppendLine($"  + Scene: {SandboxScenePath} (không thêm vào Build Settings)");
                Debug.Log(log.ToString());
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(SandboxScenePath));
                EditorUtility.DisplayDialog(Title,
                    "Xong! Scene Sandbox đã được tạo và đang mở.\nBấm Play để test.\n\nChi tiết xem Console.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(log.ToString());
                Debug.LogException(e);
                EditorUtility.DisplayDialog(Title, "Có lỗi — xem Console.\n\n" + e.Message, "OK");
            }
        }
    }
}
