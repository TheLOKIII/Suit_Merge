using System;
using System.IO;
using System.Linq;
using System.Text;
using SuitMerge.App;
using SuitMerge.View.Animation;
using SuitMerge.View.Board;
using SuitMerge.View.Cards;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using SuitMerge.View.Localization;
using SuitMerge.View.Popups;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SuitMerge.EditorTools.GameSceneBuilder;
using Object = UnityEngine.Object;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Bước 4.5 — bổ sung vào scene Game (gọi từ menu Cập nhật scene Game / Tạo scene Game):
    /// viền 9-slice ảnh mới · FxTheme (overlay + badge) · UiTheme.asset · HUD Level · bàn tay tutorial ·
    /// chữ 2 ngôn ngữ cho chữ cố định · 7 popup Figma (Tạm dừng, Hết chỗ, Hoàn thành, Hết lá, Mở khoá, Chọn level, Cài đặt)
    /// · nối vào GameBootstrap / EventPlayer.
    /// Chạy lại nhiều lần không tạo trùng. KHÔNG xoá gì: popup Tạm dừng / Game Over kiểu cũ được giữ (ẩn, đổi tên "cũ").
    /// </summary>
    internal static class Step45SceneBuilder
    {
        private const string UiFolder = Root + "Images/UI/";
        private const string FxFolder = Root + "Images/Effects/";
        internal const string FxThemePath = Root + "Data/Theme/FxTheme.asset";
        private const string UiThemePath = Root + "Data/Theme/UiTheme.asset";
        private const string LevelFolder = Root + "Data/Levels";
        private const string FigmaSuffix = " (Figma)";
        private const string OldSuffix = " (cũ – không dùng)";

        private static readonly Color Dim = new Color32(0x1A, 0x06, 0x26, 0xB8); // rgba(26,6,38,0.72)
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private static TMP_FontAsset _font;
        private static UiTheme _theme;

        /// <summary>UiTheme đã chuẩn bị trong <see cref="Ensure"/> (cho phần dựng khối đôi M4.5 dùng lại).</summary>
        internal static UiTheme Theme => _theme;

        internal static void Ensure(TMP_FontAsset font, StringBuilder log)
        {
            _font = font;
            log.AppendLine("  — Bước 4.5 (giao diện Level, song ngữ, FX Figma):");
            EnsureBorders(log);
            EnsureFxTheme(log);
            _theme = EnsureUiTheme(log);

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var hudView = Object.FindFirstObjectByType<HudView>(FindObjectsInactive.Include);
            var boardView = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<EventPlayer>(FindObjectsInactive.Include);
            if (canvas == null || hudView == null || boardView == null || bootstrap == null || player == null)
                throw new InvalidOperationException("Scene thiếu Canvas / Hud / Board / GameBootstrap / EventPlayer.");
            var canvasRt = (RectTransform)canvas.transform;
            var frame = (RectTransform)boardView.transform.parent;

            LocalizeStaticTexts(hudView, frame, log);
            LevelHudView levelHud = EnsureLevelHud(hudView, log);
            HintPointerView pointer = EnsureHintPointer(frame, log);

            PausePopup pause = EnsureFigmaPopup<PausePopup>("PausePopup", log, () => BuildPause(canvasRt));
            GameOverPopup gameOver = EnsureFigmaPopup<GameOverPopup>("GameOverPopup", log, () => BuildGameOver(canvasRt));
            LevelCompletePopup complete = EnsurePopup<LevelCompletePopup>("LevelCompletePopup", log, () => BuildComplete(canvasRt));
            LevelFailPopup fail = EnsurePopup<LevelFailPopup>("LevelFailPopup", log, () => BuildFail(canvasRt));
            UnlockPopup unlock = EnsurePopup<UnlockPopup>("UnlockPopup", log, () => BuildUnlock(canvasRt));
            LevelSelectPopup select = EnsurePopup<LevelSelectPopup>("LevelSelectPopup", log, () => BuildSelect(canvasRt));
            SettingsPopup settings = EnsurePopup<SettingsPopup>("SettingsPopup", log, () => BuildSettings(canvasRt));

            var so = new SerializedObject(bootstrap);
            so.FindProperty("pausePopup").objectReferenceValue = pause;
            so.FindProperty("gameOverPopup").objectReferenceValue = gameOver;
            SetIfEmpty(so, "levelHud", levelHud, log);
            SetIfEmpty(so, "hintPointer", pointer, log);
            SetIfEmpty(so, "levelCompletePopup", complete, log);
            SetIfEmpty(so, "levelFailPopup", fail, log);
            SetIfEmpty(so, "unlockPopup", unlock, log);
            SetIfEmpty(so, "levelSelectPopup", select, log);
            SetIfEmpty(so, "settingsPopup", settings, log);
            AssignLevelFiles(so, log);
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  + GameBootstrap dùng popup Tạm dừng / Hết chỗ kiểu Figma");

            var playerSo = new SerializedObject(player);
            SetIfEmpty(playerSo, "levelHud", levelHud, log);
            playerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Ảnh + theme

        private static void EnsureBorders(StringBuilder log)
        {
            // Vector4 = (trái, dưới, phải, trên) — đơn vị pixel ảnh @2x
            foreach (string b in new[] { "btn-teal", "btn-pink", "btn-gold", "btn-purple" })
                Border(UiFolder + b + ".png", new Vector4(44f, 56f, 44f, 44f), log);
            Border(UiFolder + "popup-panel.png", new Vector4(84f, 104f, 84f, 84f), log);
            Border(UiFolder + "popup-title.png", new Vector4(60f, 70f, 60f, 60f), log);
            Border(UiFolder + "hud-round.png", new Vector4(32f, 32f, 32f, 32f), log);
            foreach (string b in new[] { "badge-pink", "badge-blue", "badge-dark" })
                Border(FxFolder + b + ".png", new Vector4(56f, 0f, 56f, 0f), log);
            Border(FxFolder + "badge-red.png", new Vector4(68f, 0f, 68f, 0f), log);
        }

        private static void Border(string path, Vector4 border, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                log.AppendLine($"    (cảnh báo: không thấy {path})");
                return;
            }
            if (importer.spriteBorder != Vector4.zero) return;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            log.AppendLine($"  + Viền 9-slice: {path}");
        }

        internal static Sprite Ui(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + name + ".png");
        internal static Sprite Fx(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(FxFolder + name + ".png");

        private static void EnsureFxTheme(StringBuilder log)
        {
            var theme = AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath);
            if (theme == null) throw new InvalidOperationException("Không thấy " + FxThemePath);
            var so = new SerializedObject(theme);
            Fill(so, "overlayValid", Fx("overlay-valid"), log);
            Fill(so, "overlayMerge", Fx("overlay-merge"), log);
            Fill(so, "overlaySpade", Fx("overlay-spade"), log);
            Fill(so, "overlayClub", Fx("overlay-club"), log);
            Fill(so, "overlayHint", Fx("overlay-hint"), log);
            Fill(so, "overlayInvalid", Fx("overlay-invalid"), log);
            Fill(so, "badgeDiamond", Fx("badge-red"), log);
            Fill(so, "badgeHeart", Fx("badge-pink"), log);
            Fill(so, "badgeClub", Fx("badge-blue"), log);
            Fill(so, "badgeSpade", Fx("badge-dark"), log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        private static UiTheme EnsureUiTheme(StringBuilder log)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(UiThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, UiThemePath);
                log.AppendLine("  + " + UiThemePath + " (font, icon mục tiêu, nút)");
            }
            var so = new SerializedObject(theme);
            Fill(so, "font", _font, log);
            var card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Fill(so, "cardPrefab", card != null ? card.GetComponent<CardView>() : null, log);
            Fill(so, "cardBlank", AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Images/Cards/card-blank.png"), log);
            Fill(so, "goalScore", Ui("goal-score"), log);
            Fill(so, "goalCheck", Ui("goal-check"), log);
            Fill(so, "goalDiamond", Ui("goal-suit-diamond"), log);
            Fill(so, "goalClub", Ui("goal-suit-club"), log);
            Fill(so, "goalSpade", Ui("goal-suit-spade"), log);
            Fill(so, "goalHeart", Ui("goal-suit-heart"), log);
            Fill(so, "buttonTeal", Ui("btn-teal"), log);
            Fill(so, "buttonPink", Ui("btn-pink"), log);
            Fill(so, "buttonGold", Ui("btn-gold"), log);
            Fill(so, "buttonPurple", Ui("btn-purple"), log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        internal static void Fill(SerializedObject so, string field, Object value, StringBuilder log)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null || prop.objectReferenceValue != null || value == null) return;
            prop.objectReferenceValue = value;
            log.AppendLine($"  + {so.targetObject.name}.{field}");
        }

        internal static void SetIfEmpty(SerializedObject so, string field, Object value, StringBuilder log)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null || prop.objectReferenceValue != null) return;
            prop.objectReferenceValue = value;
            log.AppendLine($"  + Nối {field} vào {so.targetObject.name}");
        }

        private static void AssignLevelFiles(SerializedObject so, StringBuilder log)
        {
            SerializedProperty prop = so.FindProperty("levelFiles");
            if (prop.arraySize > 0)
            {
                log.AppendLine($"  = GameBootstrap đã có {prop.arraySize} file level (thêm level mới: kéo file vào ô Level Files)");
                return;
            }
            string[] files = Directory.Exists(LevelFolder)
                ? Directory.GetFiles(LevelFolder, "*.json").Select(f => f.Replace('\\', '/')).OrderBy(f => f).ToArray()
                : new string[0];
            prop.arraySize = files.Length;
            for (int i = 0; i < files.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(files[i]);
            log.AppendLine($"  + GameBootstrap.levelFiles = {files.Length} file trong {LevelFolder}");
        }

        // ------------------------------------------------------------------ Chữ cố định → 2 ngôn ngữ

        private static void LocalizeStaticTexts(HudView hud, RectTransform frame, StringBuilder log)
        {
            Localize(hud.transform.Find("MenuButton/Label"), "hud.menu", log);
            Localize(hud.transform.Find("UpNext/Label"), "hud.up_next", log);
            Localize(frame.Find("Hint"), "hud.drag_hint", log);
        }

        private static void Localize(Transform t, string key, StringBuilder log)
        {
            if (t == null || t.GetComponent<TMP_Text>() == null)
            {
                log.AppendLine($"    (cảnh báo: không thấy chữ cho key \"{key}\")");
                return;
            }
            if (t.GetComponent<LocalizedLabel>() != null) return;
            SetString(t.gameObject.AddComponent<LocalizedLabel>(), "key", key);
            log.AppendLine($"  + Chữ 2 ngôn ngữ \"{key}\": {t.name}");
        }

        private static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ HUD Level (Figma Level 13 178:539)

        private static LevelHudView EnsureLevelHud(HudView hud, StringBuilder log)
        {
            var existing = hud.GetComponentInChildren<LevelHudView>(true);
            if (existing != null) return existing;

            RectTransform root = NewUI("LevelHud", hud.transform);
            Stretch(root);
            Sprite round = Ui("hud-round");

            // LEVEL N (level-title 178:661): pill hồng, căn giữa x = 250, y = 44, cao 43
            RectTransform title = NewUI("LevelTitle", root);
            title.anchorMin = title.anchorMax = title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -44f);
            Image titleBg = AddImage(title, round);
            titleBg.type = Image.Type.Sliced;
            titleBg.color = UiTheme.LevelPill;
            HLayout(title, new RectOffset(18, 18, 4, 4), 0f, TextAnchor.MiddleCenter);
            Fit(title, true, true);
            TextMeshProUGUI levelText = Text(title, "LEVEL 1", 22f, Color.white);

            // MỤC TIÊU (objective-panel 178:663): (14, 106), lề 16/8, khe 14
            RectTransform objective = NewUI("Objective", root);
            Place(objective, 14f, 106f, 213f, 45f);
            Image objBg = AddImage(objective, round);
            objBg.type = Image.Type.Sliced;
            objBg.color = UiTheme.HudPanel;
            HLayout(objective, new RectOffset(16, 16, 8, 8), 14f, TextAnchor.MiddleLeft);
            Fit(objective, true, true);
            Localized(Text(objective, "MỤC TIÊU", 14f, UiTheme.TextWhite70), "hud.goals");
            RectTransform goals = NewUI("Goals", objective);
            HLayout(goals, new RectOffset(0, 0, 0, 0), 14f, TextAnchor.MiddleLeft);

            // BỘ BÀI (deck-counter 178:674): (420, 102) 65×62
            RectTransform deck = NewUI("Deck", root);
            Place(deck, 420f, 102f, 65f, 62f);
            Image deckBg = AddImage(deck, round);
            deckBg.type = Image.Type.Sliced;
            deckBg.color = UiTheme.HudPanel;
            RectTransform deckLabel = NewUI("Label", deck);
            Place(deckLabel, 0f, 6f, 65f, 19f);
            Localized(AddText(deckLabel, _font, "BỘ BÀI", 12f, UiTheme.TextWhite70, TextAlignmentOptions.Top), "hud.deck");
            RectTransform deckValue = NewUI("Value", deck);
            Place(deckValue, 0f, 21f, 65f, 35f);
            TextMeshProUGUI deckText = AddText(deckValue, _font, "0", 22f, UiTheme.TextGold, TextAlignmentOptions.Top);

            var view = root.gameObject.AddComponent<LevelHudView>();
            SetRef(view, "theme", _theme);
            SetRef(view, "levelText", levelText);
            SetRef(view, "goalsRoot", goals);
            SetRef(view, "deckText", deckText);
            root.gameObject.SetActive(false);
            log.AppendLine("  + HUD Level (LEVEL N · MỤC TIÊU · BỘ BÀI) — ẩn sẵn, hiện khi chơi Level");
            return view;
        }

        private static HintPointerView EnsureHintPointer(RectTransform frame, StringBuilder log)
        {
            var existing = frame.GetComponentInChildren<HintPointerView>(true);
            if (existing != null) return existing;

            RectTransform root = NewUI("Tutorial", frame);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Transform fx = frame.Find("Fx");
            if (fx != null) root.SetSiblingIndex(fx.GetSiblingIndex()); // dưới lớp Fx, trên bàn + tay

            RectTransform hand = NewUI("Hand", root);
            hand.anchorMin = hand.anchorMax = new Vector2(0.5f, 0.5f);
            hand.sizeDelta = new Vector2(120f, 149f); // Figma hand-pointer 177:411
            AddImage(hand, Ui("hand-pointer"));

            var view = root.gameObject.AddComponent<HintPointerView>();
            SetRef(view, "hand", hand);
            Transform caption = frame.Find("Hint");
            if (caption != null) SetRef(view, "caption", caption.GetComponent<TextMeshProUGUI>());
            log.AppendLine("  + Bàn tay tutorial (DesignFrame/Tutorial)");
            return view;
        }

        // ------------------------------------------------------------------ Popup: tìm / dựng

        /// <summary>Popup đã có bản Figma → dùng lại; chưa có → dựng mới, bản cũ đổi tên "(cũ – không dùng)" và giữ ẩn.</summary>
        private static T EnsureFigmaPopup<T>(string name, StringBuilder log, Func<T> build) where T : Component
        {
            T[] all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            T figma = all.FirstOrDefault(p => p.name.EndsWith(FigmaSuffix));
            if (figma != null)
            {
                log.AppendLine($"  = {name}{FigmaSuffix} đã có");
                return figma;
            }
            foreach (T old in all)
            {
                old.gameObject.SetActive(false);
                if (!old.name.EndsWith(OldSuffix)) old.name += OldSuffix;
            }
            T built = build();
            built.name = name + FigmaSuffix;
            log.AppendLine($"  + {name}{FigmaSuffix} (bản cũ giữ lại, ẩn, đổi tên \"…{OldSuffix}\")");
            return built;
        }

        internal static T EnsurePopup<T>(string name, StringBuilder log, Func<T> build) where T : Component
        {
            T existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (existing != null)
            {
                log.AppendLine($"  = {name} đã có");
                return existing;
            }
            T built = build();
            built.name = name;
            log.AppendLine($"  + {name}");
            return built;
        }

        // ------------------------------------------------------------------ Popup: từng loại

        private static PausePopup BuildPause(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, _) = Shell(canvas, "PausePopup", "pause.title", 80);
            Button resume = Button(panel, _theme.buttonTeal, "btn.resume", 300f);
            Button restart = Button(panel, _theme.buttonPink, "btn.restart", 300f);
            Button settings = Button(panel, _theme.buttonPurple, "btn.settings", 300f);
            Button home = Ghost(panel, "btn.home");
            Button close = CloseButton(panel);
            var popup = root.gameObject.AddComponent<PausePopup>();
            SetRef(popup, "resumeButton", resume);
            SetRef(popup, "restartButton", restart);
            SetRef(popup, "settingsButton", settings);
            SetRef(popup, "homeButton", home);
            SetRef(popup, "closeButton", close);
            root.gameObject.SetActive(false);
            return popup;
        }

        private static GameOverPopup BuildGameOver(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, _) = Shell(canvas, "GameOverPopup", "gameover.title", 76);
            Localized(Text(panel, "ĐIỂM", 18f, UiTheme.TextMuted, 380f), "label.score");
            TextMeshProUGUI score = Text(panel, "0", 56f, UiTheme.TextDark, 380f);
            Localized(Text(panel, "KỶ LỤC", 18f, UiTheme.TextMuted, 380f), "label.best");
            TextMeshProUGUI best = Text(panel, "0", 34f, UiTheme.TextDark, 380f);
            TextMeshProUGUI newBest = Localized(Text(panel, "Kỷ lục mới!", 24f, UiTheme.TextPink, 380f), "label.new_best");
            Button again = Button(panel, _theme.buttonPink, "btn.play_again", 280f);
            Button home = Ghost(panel, "btn.home");
            var popup = root.gameObject.AddComponent<GameOverPopup>();
            SetRef(popup, "scoreText", score);
            SetRef(popup, "bestText", best);
            SetRef(popup, "newBestLabel", newBest.gameObject);
            SetRef(popup, "playAgainButton", again);
            SetRef(popup, "homeButton", home);
            root.gameObject.SetActive(false);
            return popup;
        }

        private static LevelCompletePopup BuildComplete(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, TextMeshProUGUI title) = Shell(canvas, "LevelCompletePopup", null, 76);
            Localized(Text(panel, "HOÀN THÀNH!", 48f, UiTheme.TextTeal, 380f), "complete.done");
            Localized(Text(panel, "ĐIỂM", 18f, UiTheme.TextMuted, 380f), "label.score");
            TextMeshProUGUI score = Text(panel, "0", 56f, UiTheme.TextDark, 380f);

            RectTransform reward = NewUI("Reward", panel);
            reward.sizeDelta = new Vector2(380f, 70f);
            HLayout(reward, new RectOffset(0, 0, 0, 0), 10f, TextAnchor.MiddleCenter);
            RectTransform coin = NewUI("Coin", reward);
            AddImage(coin, Ui("coin-icon")).preserveAspect = true;
            var coinLe = coin.gameObject.AddComponent<LayoutElement>();
            coinLe.preferredWidth = 40.5f;
            coinLe.preferredHeight = 46.8f;
            TextMeshProUGUI rewardText = Text(reward, "+20", 44f, UiTheme.TextCoin);

            Button next = Button(panel, _theme.buttonTeal, "btn.continue", 260f);
            var popup = root.gameObject.AddComponent<LevelCompletePopup>();
            SetRef(popup, "titleText", title);
            SetRef(popup, "scoreText", score);
            SetRef(popup, "rewardText", rewardText);
            SetRef(popup, "continueButton", next);
            root.gameObject.SetActive(false);
            return popup;
        }

        private static LevelFailPopup BuildFail(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, TextMeshProUGUI title) = Shell(canvas, "LevelFailPopup", null, 76);
            Localized(Text(panel, "Còn thiếu một chút!", 30f, UiTheme.TextPink, 380f), "fail.subtitle");
            RectTransform goals = NewUI("Goals", panel);
            goals.sizeDelta = new Vector2(380f, 60f);
            HLayout(goals, new RectOffset(0, 0, 0, 0), 28f, TextAnchor.MiddleCenter);
            Button retry = Button(panel, _theme.buttonTeal, "btn.restart", 280f);
            Button giveUp = Ghost(panel, "btn.give_up");
            var popup = root.gameObject.AddComponent<LevelFailPopup>();
            SetRef(popup, "theme", _theme);
            SetRef(popup, "titleText", title);
            SetRef(popup, "goalsRoot", goals);
            SetRef(popup, "retryButton", retry);
            SetRef(popup, "giveUpButton", giveUp);
            root.gameObject.SetActive(false);
            return popup;
        }

        private static UnlockPopup BuildUnlock(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, _) = Shell(canvas, "UnlockPopup", "unlock.title", 90);
            RectTransform icon = NewUI("Icon", panel);
            Image iconImg = AddImage(icon, _theme.goalDiamond);
            iconImg.preserveAspect = true;
            var le = icon.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 140.8f;
            icon.sizeDelta = new Vector2(140.8f, 140.8f);
            TextMeshProUGUI name = Text(panel, "Hiệu ứng Rô", 36f, UiTheme.TextRed, 380f);
            TextMeshProUGUI desc = Text(panel, "Ghép 3 lá CÙNG CHẤT RÔ\n→ điểm merge ×3", 24f, UiTheme.TextDark, 380f);
            desc.textWrappingMode = TextWrappingModes.Normal;
            Button ok = Button(panel, _theme.buttonTeal, "btn.got_it", 240f);
            var popup = root.gameObject.AddComponent<UnlockPopup>();
            SetRef(popup, "theme", _theme);
            SetRef(popup, "icon", iconImg);
            SetRef(popup, "nameText", name);
            SetRef(popup, "descText", desc);
            SetRef(popup, "okButton", ok);
            root.gameObject.SetActive(false);
            return popup;
        }

        private static LevelSelectPopup BuildSelect(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, _) = Shell(canvas, "LevelSelectPopup", "select.title", 86);
            RectTransform grid = NewUI("Grid", panel);
            grid.sizeDelta = new Vector2(380f, 160f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(64f, 70f);
            layout.spacing = new Vector2(12f, 14f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;

            Button template = Button(grid, _theme.buttonPink, null, 64f);
            template.name = "LevelButtonTemplate";
            template.GetComponentInChildren<TextMeshProUGUI>().text = "1";
            template.gameObject.SetActive(false);

            Button endless = Button(panel, _theme.buttonPurple, "btn.endless", 260f);
            var popup = root.gameObject.AddComponent<LevelSelectPopup>();
            SetRef(popup, "gridRoot", grid);
            SetRef(popup, "levelButtonTemplate", template);
            SetRef(popup, "endlessButton", endless);
            root.gameObject.SetActive(false);
            return popup;
        }

        private static SettingsPopup BuildSettings(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, _) = Shell(canvas, "SettingsPopup", "settings.title", 86);
            Localized(Text(panel, "Ngôn ngữ", 24f, UiTheme.TextDark, 380f), "settings.language");
            RectTransform row = NewUI("Languages", panel);
            row.sizeDelta = new Vector2(380f, 70f);
            HLayout(row, new RectOffset(0, 0, 0, 0), 16f, TextAnchor.MiddleCenter);
            Button vi = Button(row, _theme.buttonTeal, "lang.vi", 170f, 24f);
            Button en = Button(row, _theme.buttonPurple, "lang.en", 170f, 24f);
            Button close = CloseButton(panel);
            var popup = root.gameObject.AddComponent<SettingsPopup>();
            SetRef(popup, "theme", _theme);
            SetRef(popup, "vietnameseButton", vi);
            SetRef(popup, "englishButton", en);
            SetRef(popup, "closeButton", close);
            root.gameObject.SetActive(false);
            return popup;
        }

        // ------------------------------------------------------------------ Bộ dựng popup (Figma popup-panel 178:386)

        /// <summary>
        /// Nền tối + khung 440 (thân kem viền vàng, cao theo nội dung) + ruy băng tiêu đề hồng.
        /// Nội dung xếp dọc giữa, cách đỉnh khung <paramref name="topPadding"/>, khe 18 (Figma).
        /// <paramref name="titleKey"/> = null → tiêu đề do code đặt (vd "LEVEL 3").
        /// </summary>
        internal static (RectTransform root, RectTransform panel, TextMeshProUGUI title) Shell(RectTransform canvas, string name,
            string titleKey, int topPadding)
        {
            RectTransform root = NewUI(name, canvas);
            Stretch(root);
            AddImage(root, null, raycast: true).color = Dim; // chặn chạm xuống màn chơi

            RectTransform panel = NewUI("Panel", root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(440f, 400f);
            panel.anchoredPosition = new Vector2(0f, 20f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, topPadding, 50);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Fit(panel, false, true);

            // Thân: inset trên 20 (chừa ruy băng), ảnh có bóng 10 phía dưới
            RectTransform body = NewUI("Body", panel);
            Ignore(body);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(0f, -10f);
            body.offsetMax = new Vector2(0f, -20f);
            Image bodyImg = AddImage(body, Ui("popup-panel"), raycast: true);
            bodyImg.type = Image.Type.Sliced;

            // Ruy băng tiêu đề: căn giữa trên đỉnh khung, rộng theo chữ (lề 36, chữ 34 trắng)
            RectTransform tab = NewUI("Title", panel);
            Ignore(tab);
            tab.anchorMin = tab.anchorMax = tab.pivot = new Vector2(0.5f, 1f);
            tab.anchoredPosition = Vector2.zero;
            Image tabImg = AddImage(tab, Ui("popup-title"));
            tabImg.type = Image.Type.Sliced;
            HLayout(tab, new RectOffset(36, 36, 6, 13), 0f, TextAnchor.MiddleCenter);
            Fit(tab, true, true);
            TextMeshProUGUI title = Text(tab, "TIÊU ĐỀ", 34f, Color.white);
            if (titleKey != null) Localized(title, titleKey);
            return (root, panel, title);
        }

        /// <summary>Nút Figma (btn 178:385): nền 9-slice cao 70 (gồm bóng 6) + chữ 28 trắng (nút vàng: chữ #290f38).</summary>
        internal static Button Button(RectTransform parent, Sprite sprite, string key, float width, float textSize = 28f)
        {
            RectTransform rt = NewUI(key != null ? "Button " + key : "Button", parent);
            rt.sizeDelta = new Vector2(width, 70f);
            Image img = AddImage(rt, sprite, raycast: true);
            img.type = Image.Type.Sliced;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 70f;
            Button b = rt.gameObject.AddComponent<Button>();

            RectTransform label = NewUI("Label", rt);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(8f, 8f);   // bóng 6 + viền
            label.offsetMax = new Vector2(-8f, -2f);
            Color color = sprite == _theme.buttonGold ? new Color32(0x29, 0x0F, 0x38, 0xFF) : Color.white;
            TextMeshProUGUI t = AddText(label, _font, key ?? "", textSize, color, TextAlignmentOptions.Center);
            if (key != null) Localized(t, key);
            return b;
        }

        /// <summary>Nút chữ không nền (Ghost): chữ 22 tím xám.</summary>
        internal static Button Ghost(RectTransform parent, string key)
        {
            RectTransform rt = NewUI("Button " + key, parent);
            rt.sizeDelta = new Vector2(300f, 44f);
            AddImage(rt, null, raycast: true).color = Clear;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 300f;
            le.preferredHeight = 44f;
            Button b = rt.gameObject.AddComponent<Button>();
            RectTransform label = NewUI("Label", rt);
            Stretch(label);
            Localized(AddText(label, _font, key, 22f, UiTheme.TextMuted, TextAlignmentOptions.Center), key);
            return b;
        }

        /// <summary>Nút X tròn hồng 52 ở góc trên-phải khung (Figma popup-pause: x 430, y +10).</summary>
        private static Button CloseButton(RectTransform panel)
        {
            RectTransform rt = NewUI("CloseButton", panel);
            Ignore(rt);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(52f, 52f);
            rt.anchoredPosition = new Vector2(-14f, -36f);
            AddImage(rt, Ui("btn-close"), raycast: true);
            return rt.gameObject.AddComponent<Button>();
        }

        /// <summary>Chữ Baloo 2 căn giữa; <paramref name="width"/> > 0 = rộng cố định (trong khung popup).</summary>
        internal static TextMeshProUGUI Text(RectTransform parent, string text, float size, Color color, float width = 0f)
        {
            RectTransform rt = NewUI("Text", parent);
            if (width > 0f) rt.sizeDelta = new Vector2(width, size * 1.6f);
            TextMeshProUGUI t = AddText(rt, _font, text, size, color, TextAlignmentOptions.Center);
            return t;
        }

        internal static TextMeshProUGUI Localized(TextMeshProUGUI t, string key)
        {
            SetString(t.gameObject.AddComponent<LocalizedLabel>(), "key", key);
            return t;
        }

        internal static void HLayout(RectTransform rt, RectOffset padding, float spacing, TextAnchor align)
        {
            var l = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            l.padding = padding;
            l.spacing = spacing;
            l.childAlignment = align;
            l.childControlWidth = l.childControlHeight = true;
            l.childForceExpandWidth = l.childForceExpandHeight = false;
        }

        private static void Fit(RectTransform rt, bool horizontal, bool vertical)
        {
            var f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        }

        private static void Ignore(RectTransform rt) => rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
    }
}
