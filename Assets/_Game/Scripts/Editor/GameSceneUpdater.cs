using System;
using System.Text;
using SuitMerge.App;
using SuitMerge.View.Animation;
using SuitMerge.View.Board;
using SuitMerge.View.Cards;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using SuitMerge.View.Popups;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SuitMerge.EditorTools.GameSceneBuilder;
using Object = UnityEngine.Object;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Cập nhật scene Game: BỔ SUNG phần còn thiếu vào scene Game có sẵn.
    /// Chạy lại nhiều lần không tạo trùng; không xoá / không sửa những gì bạn đã chỉnh tay.
    /// Bước 2.4:
    /// 1. Viền 9-slice cho board-panel + btn-pill (để khung/nút co giãn không méo góc)
    /// 2. Dòng BEST phía trên điểm số
    /// 3. Popup Game Over (ẩn sẵn)
    /// 4. GameBootstrap (nối sẵn Board, Hud, Hand, Popup, game_config.json)
    /// 5. Tắt (KHÔNG xoá) component MockupPreview
    /// Bước 2.5:
    /// 6. Lớp Fx (lá bay + chữ nổi) trên cùng của khung thiết kế
    /// 7. EventPlayer (animation) nối sẵn + gán vào GameBootstrap
    /// Bản sửa 1 bước 2.5:
    /// 8. Nâng cỡ chữ điểm HUD (36 → 44), BEST (14 → 17), chữ nổi (+điểm 24 → 32, Combo 26 → 34, Ace 34 → 42)
    ///    — chỉ đổi ô vẫn đúng giá trị cũ, ô bạn đã chỉnh thì giữ nguyên
    /// Bước 2.6:
    /// 9. Popup tạm dừng (ẩn sẵn) + nối nút MENU và popup vào GameBootstrap
    /// Bước 1.4b (FX chất):
    /// 10. Viền 9-slice cho fx-flash + tạo Data/Theme/FxTheme.asset (ảnh FX) + gán vào EventPlayer
    /// Bước 4.5 (giao diện Level, song ngữ, FX Figma): xem Step45SceneBuilder
    /// </summary>
    public static class GameSceneUpdater
    {
        private const string ConfigPath = Root + "Data/Config/game_config.json";
        private const string BoardPanelPath = Root + "Images/Board/board-panel.png";
        private const string PillPath = Root + "Images/UI/btn-pill.png";
        private const string CardPrefabPath = Root + "Prefabs/Cards/Card.prefab";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color ScoreShadowColor = new Color32(0x3D, 0x12, 0x56, 0xFF);
        private static readonly Color NewBestColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);

        [MenuItem("Suit Merge/Cập nhật scene Game", priority = 1)]
        private static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Cập nhật scene Game", "Hãy thoát Play mode trước.", "OK");
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!System.IO.File.Exists(ScenePath))
                {
                    EditorUtility.DisplayDialog("Cập nhật scene Game",
                        "Chưa có scene Game. Hãy chạy Suit Merge → Tạo scene Game trước.", "OK");
                    return;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }

            var log = new StringBuilder("[Suit Merge] Cập nhật scene Game:\n");
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                if (font == null) throw new InvalidOperationException("Không tìm thấy " + FontAssetPath);

                EnsureStep24(font, log);
                EnsureStep25(font, log);
                EnsureStep25Fix1(log);
                EnsureStep26(font, log);
                EnsureStep14Fx(log);
                Step45SceneBuilder.Ensure(font, log);
                PairSceneBuilder.Ensure(font, log);
                AnimationSceneBuilder.Ensure(font, log);
                Scene scene = SceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("  Đã lưu scene.");
                Debug.Log(log.ToString());
                EditorUtility.DisplayDialog("Cập nhật scene Game",
                    "Xong! Bấm Play → bảng chọn level (L1–L10 hoặc ENDLESS).\n\nChi tiết xem Console.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(log.ToString());
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Cập nhật scene Game", "Có lỗi — xem Console.\n\n" + e.Message, "OK");
            }
        }

        /// <summary>
        /// Bổ sung phần bước 2.5 vào scene đang mở: lớp Fx + EventPlayer, gán EventPlayer vào GameBootstrap.
        /// Cần chạy SAU EnsureStep24 (cần GameBootstrap).
        /// </summary>
        internal static void EnsureStep25(TMP_FontAsset font, StringBuilder log)
        {
            var boardView = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            var hudView = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            var handView = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (boardView == null || hudView == null || handView == null || bootstrap == null)
                throw new InvalidOperationException("Scene thiếu Board / Hud / Hand / GameBootstrap.");

            // Lớp Fx: con cuối của DesignFrame (trên bàn, tay, HUD; dưới popup vì popup là con của Canvas)
            var frame = (RectTransform)boardView.transform.parent;
            var fx = frame.Find("Fx") as RectTransform;
            if (fx != null) log.AppendLine("  = Lớp Fx đã có, bỏ qua");
            else
            {
                fx = NewUI("Fx", frame);
                Stretch(fx);
                var group = fx.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false; // lá bay / chữ nổi không chặn kéo-thả
                group.interactable = false;
                log.AppendLine("  + Lớp Fx (lá bay + chữ nổi)");
            }
            fx.SetAsLastSibling();

            var player = Object.FindFirstObjectByType<EventPlayer>(FindObjectsInactive.Include);
            if (player != null) log.AppendLine("  = EventPlayer đã có, bỏ qua");
            else
            {
                var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
                if (cardPrefab == null) throw new InvalidOperationException("Không tìm thấy " + CardPrefabPath);
                player = new GameObject("EventPlayer").AddComponent<EventPlayer>();
                SetRef(player, "fxLayer", fx);
                SetRef(player, "boardView", boardView);
                SetRef(player, "handView", handView);
                SetRef(player, "hudView", hudView);
                SetRef(player, "flyingCardPrefab", cardPrefab.GetComponent<CardView>());
                SetRef(player, "font", font);
                log.AppendLine("  + EventPlayer (animation) — chỉnh Tốc độ / thời lượng trong Inspector");
            }

            var so = new SerializedObject(bootstrap);
            SerializedProperty prop = so.FindProperty("eventPlayer");
            if (prop.objectReferenceValue != null) log.AppendLine("  = GameBootstrap đã nối EventPlayer, bỏ qua");
            else
            {
                prop.objectReferenceValue = player;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("  + Nối EventPlayer vào GameBootstrap");
            }
        }

        /// <summary>Bổ sung phần bước 2.4 vào scene đang mở (dùng chung cho menu Tạo scene).</summary>
        internal static void EnsureStep24(TMP_FontAsset font, StringBuilder log)
        {
            EnsureSpriteBorder(BoardPanelPath, new Vector4(44f, 44f, 44f, 44f), log);
            EnsureSpriteBorder(PillPath, new Vector4(44f, 0f, 44f, 0f), log);

            var boardView = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            var hudView = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            var handView = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (boardView == null || hudView == null || handView == null || canvas == null)
                throw new InvalidOperationException("Scene thiếu Canvas / Board / Hud / Hand — scene không phải do menu Tạo scene Game dựng?");

            EnsureBestText(hudView, font, log);

            GameOverPopup popup = Object.FindFirstObjectByType<GameOverPopup>(FindObjectsInactive.Include);
            if (popup != null) log.AppendLine("  = Popup Game Over đã có, bỏ qua");
            else
            {
                popup = BuildGameOverPopup((RectTransform)canvas.transform, font);
                log.AppendLine("  + Popup Game Over (ẩn sẵn)");
            }

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (bootstrap != null) log.AppendLine("  = GameBootstrap đã có, bỏ qua");
            else
            {
                bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
                var config = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
                if (config == null) log.AppendLine($"    (cảnh báo: không thấy {ConfigPath} — sẽ dùng giá trị mặc định)");
                SetRef(bootstrap, "configJson", config);
                SetRef(bootstrap, "boardView", boardView);
                SetRef(bootstrap, "hudView", hudView);
                SetRef(bootstrap, "handView", handView);
                SetRef(bootstrap, "gameOverPopup", popup);
                log.AppendLine("  + GameBootstrap (đã nối Board, Hud, Hand, Popup, game_config.json)");
            }

            var preview = Object.FindFirstObjectByType<MockupPreview>(FindObjectsInactive.Include);
            if (preview != null && preview.enabled)
            {
                var so = new SerializedObject(preview);
                so.FindProperty("m_Enabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("  + Tắt component MockupPreview (không xoá — tick lại để xem dữ liệu mẫu)");
            }
            else log.AppendLine("  = MockupPreview đã tắt / không có");
        }

        // Dòng BEST (bản sửa 1 bước 2.5): 17px, hộp (184, 27.6) cao 27.2 → chân chữ cách đỉnh số điểm 44px (≈ 50.9) khoảng 5px.
        // (Bản 2.4 cũ: 14px, hộp (184, 33.1) cao 22.4)
        private const float BestFontSize = 17f;
        private static readonly Rect BestRect = new Rect(184f, 27.6f, 132f, 27.2f);

        /// <summary>Dòng "BEST …" phía trên điểm số (Bản sửa 1 bước 2.4; cỡ theo bản sửa 1 bước 2.5).</summary>
        internal static void EnsureBestText(HudView hudView, TMP_FontAsset font, StringBuilder log)
        {
            var so = new SerializedObject(hudView);
            if (so.FindProperty("bestText").objectReferenceValue != null)
            {
                log.AppendLine("  = Dòng BEST đã có, bỏ qua");
                return;
            }

            RectTransform best = NewUI("Best", hudView.transform);
            Place(best, BestRect.x, BestRect.y, BestRect.width, BestRect.height);
            TextMeshProUGUI text = AddText(best, font, "BEST 0", BestFontSize, LabelColor, TextAlignmentOptions.Top);
            SetRef(hudView, "bestText", text);
            log.AppendLine("  + Dòng BEST phía trên điểm số (Hud/Best)");
        }

        /// <summary>
        /// Bản sửa 1 bước 2.5 — nâng cỡ chữ cho scene CÓ SẴN. Chỉ đổi ô nào VẪN ĐÚNG giá trị cũ;
        /// ô bạn đã tự chỉnh khác thì giữ nguyên và ghi ra Console. Chạy lại không làm gì thêm.
        /// </summary>
        internal static void EnsureStep25Fix1(StringBuilder log)
        {
            var hudView = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            if (hudView != null)
            {
                var so = new SerializedObject(hudView);
                var value = so.FindProperty("scoreText").objectReferenceValue as TextMeshProUGUI;
                var shadow = so.FindProperty("scoreShadowText").objectReferenceValue as TextMeshProUGUI;
                var best = so.FindProperty("bestText").objectReferenceValue as TextMeshProUGUI;

                UpgradeText(value, "Điểm", 36f, ScoreFontSize,
                    new Rect(0f, -1.2f, 132f, 57.6f), ScoreValueRect, log);
                UpgradeText(shadow, "Bóng điểm", 36f, ScoreFontSize,
                    new Rect(0f, 0.6f, 132f, 57.6f), ScoreShadowRect, log);
                UpgradeText(best, "BEST", 14f, BestFontSize,
                    new Rect(184f, 33.1f, 132f, 22.4f), BestRect, log);
            }

            var player = Object.FindFirstObjectByType<EventPlayer>(FindObjectsInactive.Include);
            if (player != null)
            {
                var so = new SerializedObject(player);
                UpgradeFloat(so, "scoreTextSize", "Chữ +điểm", 24f, 32f, log);
                UpgradeFloat(so, "comboTextSize", "Chữ Combo", 26f, 34f, log);
                UpgradeFloat(so, "aceTextSize", "Chữ Ace", 34f, 42f, log);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void UpgradeText(TextMeshProUGUI text, string label, float oldSize, float newSize,
            Rect oldRect, Rect newRect, StringBuilder log)
        {
            if (text == null) return;

            if (Mathf.Abs(text.fontSize - newSize) < 0.01f) log.AppendLine($"  = {label}: cỡ đã là {newSize}");
            else if (Mathf.Abs(text.fontSize - oldSize) < 0.01f)
            {
                text.fontSize = newSize;
                EditorUtility.SetDirty(text);
                log.AppendLine($"  + {label}: cỡ {oldSize} → {newSize}");
            }
            else log.AppendLine($"  ! {label}: cỡ {text.fontSize} do bạn chỉnh — giữ nguyên");

            RectTransform rt = text.rectTransform;
            if (Matches(rt, newRect)) log.AppendLine($"  = {label}: vị trí đã đúng");
            else if (Matches(rt, oldRect))
            {
                Place(rt, newRect.x, newRect.y, newRect.width, newRect.height);
                EditorUtility.SetDirty(rt);
                log.AppendLine($"  + {label}: dời vị trí cho cỡ mới");
            }
            else log.AppendLine($"  ! {label}: vị trí do bạn chỉnh — giữ nguyên");
        }

        /// <summary>RectTransform đang đặt kiểu Place(x, y, w, h) (neo góc trên-trái) đúng bằng <paramref name="r"/>?</summary>
        private static bool Matches(RectTransform rt, Rect r) =>
            rt.anchorMin == new Vector2(0f, 1f) && rt.anchorMax == new Vector2(0f, 1f)
            && Vector2.Distance(rt.anchoredPosition, new Vector2(r.x, -r.y)) < 0.05f
            && Vector2.Distance(rt.sizeDelta, new Vector2(r.width, r.height)) < 0.05f;

        private static void UpgradeFloat(SerializedObject so, string field, string label, float oldValue, float newValue,
            StringBuilder log)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) return;
            if (Mathf.Abs(prop.floatValue - newValue) < 0.01f) log.AppendLine($"  = {label}: cỡ đã là {newValue}");
            else if (Mathf.Abs(prop.floatValue - oldValue) < 0.01f)
            {
                prop.floatValue = newValue;
                log.AppendLine($"  + {label}: cỡ {oldValue} → {newValue}");
            }
            else log.AppendLine($"  ! {label}: cỡ {prop.floatValue} do bạn chỉnh — giữ nguyên");
        }

        // ------------------------------------------------------------------ Bước 1.4b — FX chất

        private const string FxThemePath = Root + "Data/Theme/FxTheme.asset";
        private const string FxFolder = Root + "Images/Effects/";

        /// <summary>
        /// Bước 1.4b: viền 9-slice cho fx-flash, tạo FxTheme.asset (ô ảnh còn trống mới được gán),
        /// gán FxTheme vào EventPlayer nếu ô đó còn trống. Cần chạy SAU EnsureStep25 (cần EventPlayer).
        /// </summary>
        internal static void EnsureStep14Fx(StringBuilder log)
        {
            EnsureSpriteBorder(FxFolder + "fx-flash.png", new Vector4(48f, 48f, 48f, 48f), log);

            var theme = AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath);
            if (theme != null) log.AppendLine("  = FxTheme.asset đã có");
            else
            {
                theme = ScriptableObject.CreateInstance<FxTheme>();
                AssetDatabase.CreateAsset(theme, FxThemePath);
                log.AppendLine("  + " + FxThemePath + " (màu chất, ảnh FX, ô âm thanh)");
            }

            var so = new SerializedObject(theme);
            AssignIfEmpty(so, "ring", FxFolder + "fx-ring.png", log);
            AssignIfEmpty(so, "glow", FxFolder + "fx-glow.png", log);
            AssignIfEmpty(so, "spark", FxFolder + "fx-spark.png", log);
            AssignIfEmpty(so, "flash", FxFolder + "fx-flash.png", log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            var player = Object.FindFirstObjectByType<EventPlayer>(FindObjectsInactive.Include);
            if (player == null) throw new InvalidOperationException("Scene thiếu EventPlayer.");
            var playerSo = new SerializedObject(player);
            SerializedProperty prop = playerSo.FindProperty("fxTheme");
            if (prop.objectReferenceValue != null) log.AppendLine("  = EventPlayer đã có FxTheme");
            else
            {
                prop.objectReferenceValue = theme;
                playerSo.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("  + Gán FxTheme vào EventPlayer");
            }
        }

        private static void AssignIfEmpty(SerializedObject so, string field, string spritePath, StringBuilder log)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop.objectReferenceValue != null) return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                log.AppendLine($"    (cảnh báo: không thấy {spritePath})");
                return;
            }
            prop.objectReferenceValue = sprite;
            log.AppendLine($"  + FxTheme.{field} = {spritePath}");
        }

        private static void EnsureSpriteBorder(string path, Vector4 border, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                log.AppendLine($"    (cảnh báo: không thấy {path})");
                return;
            }
            if (importer.spriteBorder != Vector4.zero)
            {
                log.AppendLine($"  = Viền 9-slice đã có: {path}");
                return;
            }
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            log.AppendLine($"  + Viền 9-slice {border}: {path}");
        }

        // ------------------------------------------------------------------ Popup

        /// <summary>
        /// Popup Game Over: nền tối phủ toàn màn hình + khung 400×380 ở giữa.
        /// Toạ độ trong khung tính từ góc trên-trái của khung.
        /// </summary>
        /// <summary>
        /// Bước 2.6: popup tạm dừng (ẩn sẵn) + nối nút MENU và popup vào GameBootstrap.
        /// Cần chạy SAU EnsureStep24 (cần GameBootstrap).
        /// </summary>
        internal static void EnsureStep26(TMP_FontAsset font, StringBuilder log)
        {
            var hudView = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (hudView == null || bootstrap == null || canvas == null)
                throw new InvalidOperationException("Scene thiếu Hud / GameBootstrap / Canvas.");

            PausePopup popup = Object.FindFirstObjectByType<PausePopup>(FindObjectsInactive.Include);
            if (popup != null) log.AppendLine("  = Popup tạm dừng đã có, bỏ qua");
            else
            {
                popup = BuildPausePopup((RectTransform)canvas.transform, font);
                log.AppendLine("  + Popup tạm dừng (ẩn sẵn)");
            }

            var so = new SerializedObject(bootstrap);
            SerializedProperty popupProp = so.FindProperty("pausePopup");
            SerializedProperty menuProp = so.FindProperty("menuButton");

            if (popupProp.objectReferenceValue != null) log.AppendLine("  = GameBootstrap đã nối popup tạm dừng, bỏ qua");
            else
            {
                popupProp.objectReferenceValue = popup;
                log.AppendLine("  + Nối popup tạm dừng vào GameBootstrap");
            }

            if (menuProp.objectReferenceValue != null) log.AppendLine("  = GameBootstrap đã nối nút MENU, bỏ qua");
            else
            {
                Transform menu = hudView.transform.Find("MenuButton");
                Button button = menu != null ? menu.GetComponent<Button>() : null;
                if (button == null) log.AppendLine("    (cảnh báo: không thấy Hud/MenuButton có Button — hãy tự kéo nút vào ô Menu Button của GameBootstrap)");
                else
                {
                    menuProp.objectReferenceValue = button;
                    log.AppendLine("  + Nối nút MENU vào GameBootstrap");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Popup tạm dừng: nền tối phủ toàn màn hình + khung 400×332 ở giữa.
        /// PAUSED · SCORE (có bóng) · BEST · [RESUME] [RESTART]
        /// </summary>
        private static PausePopup BuildPausePopup(RectTransform canvas, TMP_FontAsset font)
        {
            RectTransform root = NewUI("PausePopup", canvas);
            Stretch(root);
            AddImage(root, null, raycast: true).color = DimColor;

            RectTransform panel = NewUI("Panel", root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(400f, 332f);
            panel.anchoredPosition = new Vector2(0f, 40f);
            AddImage(panel, AssetDatabase.LoadAssetAtPath<Sprite>(BoardPanelPath), raycast: true).type = Image.Type.Sliced;

            RectTransform title = NewUI("Title", panel);
            Place(title, 0f, 28f, 400f, 54f);
            AddText(title, font, "PAUSED", 34f, Color.white, TextAlignmentOptions.Top);

            RectTransform scoreLabel = NewUI("ScoreLabel", panel);
            Place(scoreLabel, 0f, 92f, 400f, 29f);
            AddText(scoreLabel, font, "SCORE", 18f, LabelColor, TextAlignmentOptions.Top);

            RectTransform scoreShadow = NewUI("ScoreShadow", panel);
            Place(scoreShadow, 0f, 116f, 400f, 70f);
            TextMeshProUGUI scoreShadowText = AddText(scoreShadow, font, "0", 44f, ScoreShadowColor, TextAlignmentOptions.Top);
            RectTransform scoreValue = NewUI("ScoreValue", panel);
            Place(scoreValue, 0f, 113.8f, 400f, 70f);
            TextMeshProUGUI scoreText = AddText(scoreValue, font, "0", 44f, Color.white, TextAlignmentOptions.Top);

            RectTransform bestLabel = NewUI("BestLabel", panel);
            Place(bestLabel, 0f, 184f, 400f, 29f);
            AddText(bestLabel, font, "BEST", 18f, LabelColor, TextAlignmentOptions.Top);

            RectTransform bestValue = NewUI("BestValue", panel);
            Place(bestValue, 0f, 206f, 400f, 48f);
            TextMeshProUGUI bestText = AddText(bestValue, font, "0", 30f, Color.white, TextAlignmentOptions.Top);

            Button resume = BuildPillButton(panel, font, "ResumeButton", "RESUME", 24f, 262f);
            Button restart = BuildPillButton(panel, font, "RestartButton", "RESTART", 208f, 262f);

            var popup = root.gameObject.AddComponent<PausePopup>();
            SetRef(popup, "scoreText", scoreText);
            SetRef(popup, "scoreShadowText", scoreShadowText);
            SetRef(popup, "bestText", bestText);
            SetRef(popup, "resumeButton", resume);
            SetRef(popup, "restartButton", restart);

            root.gameObject.SetActive(false);
            return popup;
        }

        /// <summary>Nút hồng bo tròn 168×46.8 (btn-pill 9-slice) + chữ trắng 20px.</summary>
        private static Button BuildPillButton(RectTransform parent, TMP_FontAsset font, string name, string label, float x, float y)
        {
            RectTransform button = NewUI(name, parent);
            Place(button, x, y, 168f, 46.8f);
            AddImage(button, AssetDatabase.LoadAssetAtPath<Sprite>(PillPath), raycast: true).type = Image.Type.Sliced;
            Button b = button.gameObject.AddComponent<Button>();
            RectTransform text = NewUI("Label", button);
            Place(text, 0f, 9f, 168f, 32f);
            AddText(text, font, label, 20f, Color.white, TextAlignmentOptions.Top);
            return b;
        }

        private static GameOverPopup BuildGameOverPopup(RectTransform canvas, TMP_FontAsset font)
        {
            RectTransform root = NewUI("GameOverPopup", canvas);
            Stretch(root);
            AddImage(root, null, raycast: true).color = DimColor; // chặn chạm xuống màn chơi

            RectTransform panel = NewUI("Panel", root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(400f, 380f);
            panel.anchoredPosition = new Vector2(0f, 40f);
            Image panelImg = AddImage(panel, AssetDatabase.LoadAssetAtPath<Sprite>(BoardPanelPath), raycast: true);
            panelImg.type = Image.Type.Sliced;

            RectTransform title = NewUI("Title", panel);
            Place(title, 0f, 30f, 400f, 54f);
            AddText(title, font, "NO MORE SPACE!", 34f, Color.white, TextAlignmentOptions.Top);

            RectTransform scoreLabel = NewUI("ScoreLabel", panel);
            Place(scoreLabel, 0f, 100f, 400f, 29f);
            AddText(scoreLabel, font, "SCORE", 18f, LabelColor, TextAlignmentOptions.Top);

            RectTransform scoreShadow = NewUI("ScoreShadow", panel);
            Place(scoreShadow, 0f, 124f, 400f, 70f);
            TextMeshProUGUI scoreShadowText = AddText(scoreShadow, font, "0", 44f, ScoreShadowColor, TextAlignmentOptions.Top);
            RectTransform scoreValue = NewUI("ScoreValue", panel);
            Place(scoreValue, 0f, 122.2f, 400f, 70f); // cao hơn bóng 1.8px như điểm trên HUD
            TextMeshProUGUI scoreText = AddText(scoreValue, font, "0", 44f, Color.white, TextAlignmentOptions.Top);

            RectTransform bestLabel = NewUI("BestLabel", panel);
            Place(bestLabel, 0f, 192f, 400f, 29f);
            AddText(bestLabel, font, "BEST", 18f, LabelColor, TextAlignmentOptions.Top);

            RectTransform bestValue = NewUI("BestValue", panel);
            Place(bestValue, 0f, 214f, 400f, 48f);
            TextMeshProUGUI bestText = AddText(bestValue, font, "0", 30f, Color.white, TextAlignmentOptions.Top);

            RectTransform newBest = NewUI("NewBest", panel);
            Place(newBest, 0f, 258f, 400f, 29f);
            AddText(newBest, font, "New best!", 18f, NewBestColor, TextAlignmentOptions.Top);

            RectTransform button = NewUI("PlayAgainButton", panel);
            Place(button, 90f, 300f, 220f, 46.8f);
            Image buttonImg = AddImage(button, AssetDatabase.LoadAssetAtPath<Sprite>(PillPath), raycast: true);
            buttonImg.type = Image.Type.Sliced;
            Button playAgain = button.gameObject.AddComponent<Button>();
            RectTransform buttonLabel = NewUI("Label", button);
            Place(buttonLabel, 0f, 9f, 220f, 32f);
            AddText(buttonLabel, font, "PLAY AGAIN", 20f, Color.white, TextAlignmentOptions.Top);

            var popup = root.gameObject.AddComponent<GameOverPopup>();
            SetRef(popup, "scoreText", scoreText);
            SetRef(popup, "scoreShadowText", scoreShadowText);
            SetRef(popup, "bestText", bestText);
            SetRef(popup, "newBestLabel", newBest.gameObject);
            SetRef(popup, "playAgainButton", playAgain);

            root.gameObject.SetActive(false);
            return popup;
        }
    }
}
