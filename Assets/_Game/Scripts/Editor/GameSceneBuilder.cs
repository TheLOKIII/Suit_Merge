using System;
using System.IO;
using System.Linq;
using System.Text;
using SuitMerge.App;
using SuitMerge.View.Board;
using SuitMerge.View.Cards;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Menu Suit Merge → Tạo scene Game: dựng 1 lần scene màn chơi Endless theo mockup Figma (khung 500×1080).
    /// Tạo lần lượt: TMP Font Asset, CardTheme, prefab Card / MiniCard / Cell, scene Game, thêm vào Build Settings.
    /// Thứ gì ĐÃ TỒN TẠI thì bỏ qua — KHÔNG ghi đè (ghi rõ ra Console).
    /// Toạ độ các thành phần lấy từ frame "Main Play – Endless" (105:18), gốc góc trên-trái.
    /// </summary>
    public static class GameSceneBuilder
    {
        internal const string Root = "Assets/_Game/";
        private const string Img = Root + "Images/";
        private const string FontTtfPath = Root + "Fonts/Baloo2-Bold.ttf";
        internal const string FontAssetPath = Root + "Fonts/Baloo2-Bold SDF.asset";
        private const string ThemeFolder = Root + "Data/Theme";
        internal const string ThemePath = ThemeFolder + "/CardTheme.asset";
        internal const string CardPrefabPath = Root + "Prefabs/Cards/Card.prefab";
        internal const string MiniCardPrefabPath = Root + "Prefabs/Cards/MiniCard.prefab";
        internal const string CellPrefabPath = Root + "Prefabs/Board/Cell.prefab";
        internal const string ScenePath = Root + "Scenes/Game.unity";

        private const float FrameWidth = 500f;
        private const float FrameHeight = 1080f;

        // Điểm HUD (bản sửa 1 bước 2.5) — toạ độ trong ô Score (184, 38): x, y (tính từ trên), rộng, cao
        internal const float ScoreFontSize = 44f;
        internal static readonly Rect ScoreValueRect = new Rect(0f, -7.2f, 132f, 70.4f);
        internal static readonly Rect ScoreShadowRect = new Rect(0f, -5.0f, 132f, 70.4f);

        private static readonly Color ScoreShadowColor = new Color32(0x3D, 0x12, 0x56, 0xFF);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color CameraColor = new Color32(0x4F, 0x0F, 0x73, 0xFF);

        // Ký tự nạp sẵn vào font (phần còn lại TMP tự thêm khi cần vì font ở chế độ Dynamic)
        private const string PreloadCharacters =
            "0123456789JQKA+-×,.!?:/ ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            "Kéo thả vào ô trống ĐIỂMỤCTÊBỘÀIơạỷụđểộàáảãạâấầẩẫậăắằẳẵặêếềểễệôốồổỗộơớờởỡợưứừửữựíìỉĩịúùủũụýỳỷỹỵđ";

        [MenuItem("Suit Merge/Tạo scene Game", priority = 0)]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Tạo scene Game", "Hãy thoát Play mode trước.", "OK");
                return;
            }

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("Tạo scene Game",
                    "Chưa import TMP Essential Resources.\n\n" +
                    "Vào Window → TextMeshPro → Import TMP Essential Resources → bấm Import, rồi chạy lại menu này.",
                    "OK");
                return;
            }

            if (File.Exists(ScenePath))
            {
                bool open = EditorUtility.DisplayDialog("Tạo scene Game",
                    $"Scene đã tồn tại:\n{ScenePath}\n\nMenu này KHÔNG ghi đè scene. Muốn dựng lại thì tự xoá/đổi tên scene cũ trước.\n\nMở scene hiện có?",
                    "Mở scene", "Đóng");
                if (open && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder("[Suit Merge] Tạo scene Game:\n");
            try
            {
                TMP_FontAsset font = GetOrCreateFontAsset(log);
                CardTheme theme = GetOrCreateTheme(font, log);

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // Tạo scene mới (Single) khiến Unity giải phóng asset không còn ai dùng khỏi bộ nhớ
                // → biến font/theme ở trên có thể đã thành "null giả". Nạp lại từ ổ đĩa trước khi gán.
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                theme = AssetDatabase.LoadAssetAtPath<CardTheme>(ThemePath);
                if (font == null || theme == null)
                    throw new InvalidOperationException("Không nạp lại được font asset / CardTheme sau khi tạo scene.");

                CardView cardPrefab = GetOrCreateCardPrefab(theme, font, log);
                MiniCardView miniCardPrefab = GetOrCreateMiniCardPrefab(theme, font, log);
                CellView cellPrefab = GetOrCreateCellPrefab(cardPrefab, log);

                BuildScene(font, cardPrefab, miniCardPrefab, cellPrefab);
                GameSceneUpdater.EnsureStep24(font, log); // phần bước 2.4: GameBootstrap, popup Game Over…
                GameSceneUpdater.EnsureStep25(font, log); // phần bước 2.5: lớp Fx, EventPlayer
                GameSceneUpdater.EnsureStep26(font, log); // phần bước 2.6: popup tạm dừng, nút MENU
                GameSceneUpdater.EnsureStep14Fx(log);     // phần bước 1.4b: FxTheme (hiệu ứng chất)
                Step45SceneBuilder.Ensure(font, log);     // phần bước 4.5: HUD Level, popup Figma, song ngữ
                PairSceneBuilder.Ensure(font, log);       // phần M4.5: khối đôi (ảnh, chữ dưới khung tay, popup HẾT CHỖ!)
                AnimationSceneBuilder.Ensure(font, log);  // Animation phần 1: nút nhún, tua nhanh, Cài đặt HIỆU ỨNG
                EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine($"  + Scene: {ScenePath}");

                AddToBuildSettings(log);
                AssetDatabase.SaveAssets();
                Debug.Log(log.ToString());
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
                EditorUtility.DisplayDialog("Tạo scene Game",
                    "Xong! Scene Game đã được tạo và đang mở.\nBấm Play để chơi Endless.\n(Muốn xem dữ liệu mẫu giống mockup: tick MockupPreview, bỏ tick GameBootstrap.)\n\nChi tiết xem Console.",
                    "OK");
            }
            catch (Exception e)
            {
                Debug.LogError(log.ToString());
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Tạo scene Game", "Có lỗi — xem Console.\n\n" + e.Message, "OK");
            }
        }

        // ------------------------------------------------------------------ Font

        private static TMP_FontAsset GetOrCreateFontAsset(StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                log.AppendLine($"  = Font asset đã có, bỏ qua: {FontAssetPath}");
                return existing;
            }

            var ttf = AssetDatabase.LoadAssetAtPath<Font>(FontTtfPath);
            if (ttf == null) throw new FileNotFoundException("Không tìm thấy font " + FontTtfPath);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                ttf, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            fontAsset.name = "Baloo2-Bold SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            fontAsset.atlasTextures[0].name = "Baloo2-Bold SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = "Baloo2-Bold SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

            fontAsset.TryAddCharacters(PreloadCharacters, out string missing);
            if (!string.IsNullOrEmpty(missing)) log.AppendLine($"    (font không có ký tự: {missing})");
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            log.AppendLine($"  + Font asset: {FontAssetPath}");
            return fontAsset;
        }

        // ------------------------------------------------------------------ Theme

        private static CardTheme GetOrCreateTheme(TMP_FontAsset font, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CardTheme>(ThemePath);
            if (existing != null)
            {
                log.AppendLine($"  = CardTheme đã có, bỏ qua: {ThemePath}");
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ThemeFolder))
                AssetDatabase.CreateFolder(Root + "Data", "Theme");

            var theme = ScriptableObject.CreateInstance<CardTheme>();
            theme.cardFace = LoadSprite("Cards/card-blank.png");
            theme.cardShadow = LoadSprite("Cards/card-shadow.png");
            theme.heart = LoadSprite("Suits/suit-heart.png");
            theme.diamond = LoadSprite("Suits/suit-diamond.png");
            theme.club = LoadSprite("Suits/suit-club.png");
            theme.spade = LoadSprite("Suits/suit-spade.png");
            theme.font = font;
            AssetDatabase.CreateAsset(theme, ThemePath);
            log.AppendLine($"  + CardTheme: {ThemePath}");
            return theme;
        }

        // ------------------------------------------------------------------ Prefabs

        private static CardView GetOrCreateCardPrefab(CardTheme theme, TMP_FontAsset font, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (existing != null)
            {
                log.AppendLine($"  = Prefab đã có, bỏ qua: {CardPrefabPath}");
                return existing.GetComponent<CardView>();
            }

            RectTransform root = NewUI("Card", null);
            root.sizeDelta = new Vector2(200f, 200f);

            RectTransform shadow = NewUI("Shadow", root);
            AddImage(shadow, theme.cardShadow);
            RectTransform face = NewUI("Face", root);
            Stretch(face);
            AddImage(face, theme.cardFace);
            RectTransform suit = NewUI("Suit", root);
            AddImage(suit, theme.spade).preserveAspect = true;
            RectTransform rank = NewUI("Rank", root);
            AddText(rank, font, "9", 128f, theme.blackRankColor, TextAlignmentOptions.TopRight);

            var view = root.gameObject.AddComponent<CardView>();
            SetRef(view, "theme", theme);
            SetRef(view, "shadow", shadow.GetComponent<Image>());
            SetRef(view, "face", face.GetComponent<Image>());
            SetRef(view, "suitIcon", suit.GetComponent<Image>());
            SetRef(view, "rankText", rank.GetComponent<TextMeshProUGUI>());
            view.ApplyLayout();

            return SavePrefab(root, CardPrefabPath, log).GetComponent<CardView>();
        }

        private static MiniCardView GetOrCreateMiniCardPrefab(CardTheme theme, TMP_FontAsset font, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MiniCardPrefabPath);
            if (existing != null)
            {
                log.AppendLine($"  = Prefab đã có, bỏ qua: {MiniCardPrefabPath}");
                return existing.GetComponent<MiniCardView>();
            }

            RectTransform root = NewUI("MiniCard", null);
            root.sizeDelta = new Vector2(40.8f, 49.3f);

            RectTransform bg = NewUI("Background", root);
            Stretch(bg);
            AddImage(bg, LoadSprite("UI/up-next-card.png"));
            RectTransform suit = NewUI("Suit", root);
            AddImage(suit, theme.spade).preserveAspect = true;
            RectTransform rank = NewUI("Rank", root);
            AddText(rank, font, "9", 28.9f, theme.blackRankColor, TextAlignmentOptions.Top);

            var view = root.gameObject.AddComponent<MiniCardView>();
            SetRef(view, "theme", theme);
            SetRef(view, "background", bg.GetComponent<Image>());
            SetRef(view, "suitIcon", suit.GetComponent<Image>());
            SetRef(view, "rankText", rank.GetComponent<TextMeshProUGUI>());
            view.ApplyLayout();

            return SavePrefab(root, MiniCardPrefabPath, log).GetComponent<MiniCardView>();
        }

        private static CellView GetOrCreateCellPrefab(CardView cardPrefab, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
            if (existing != null)
            {
                log.AppendLine($"  = Prefab đã có, bỏ qua: {CellPrefabPath}");
                return existing.GetComponent<CellView>();
            }

            RectTransform root = NewUI("Cell", null);
            root.sizeDelta = new Vector2(84f, 84f);
            Sprite empty = LoadSprite("Board/cell-empty.png");
            Image bg = AddImage(root, empty, raycast: true); // nhận thả lá ở bước 2.4

            var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, root);
            Stretch((RectTransform)card.transform);
            card.SetActive(false); // ô trống mặc định

            var view = root.gameObject.AddComponent<CellView>();
            SetRef(view, "background", bg);
            SetRef(view, "emptySprite", empty);
            SetRef(view, "lockedSprite", LoadSprite("Board/cell-locked.png"));
            SetRef(view, "cardView", card.GetComponent<CardView>());

            return SavePrefab(root, CellPrefabPath, log).GetComponent<CellView>();
        }

        private static GameObject SavePrefab(RectTransform root, string path, StringBuilder log)
        {
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            log.AppendLine($"  + Prefab: {path}");
            return asset;
        }

        // ------------------------------------------------------------------ Scene

        /// <summary>Dựng camera, Canvas, khung thiết kế, bàn, tay, HUD. Scene Sandbox gọi với <paramref name="withMockupPreview"/> = false.</summary>
        internal static void BuildScene(TMP_FontAsset font, CardView cardPrefab, MiniCardView miniCardPrefab, CellView cellPrefab,
            bool withMockupPreview = true)
        {
            // Camera (Canvas dùng Overlay nên camera chỉ tô màu nền)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = CameraColor;
            camGo.AddComponent<AudioListener>(); // "tai nghe" của Unity — thiếu thì mọi âm thanh đều câm (sửa ở Animation phần 5)

            // EventSystem cho Input System mới (Project Settings: Active Input Handling = Input System)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Canvas: tham chiếu 500×1080, Expand → khung thiết kế luôn hiện trọn
            var canvasGo = new GameObject("Canvas", typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(FrameWidth, FrameHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRt = (RectTransform)canvasGo.transform;

            // Nền gradient phủ toàn màn hình (cả phần thừa ngoài khung)
            RectTransform background = NewUI("Background", canvasRt);
            Stretch(background);
            AddImage(background, LoadSprite("Backgrounds/bg-play.png"));

            // Khung thiết kế 500×1080 ở giữa màn hình
            RectTransform frame = NewUI("DesignFrame", canvasRt);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
            frame.anchoredPosition = Vector2.zero;

            BoardView boardView = BuildBoard(frame, cellPrefab);
            HandView handView = BuildHand(frame, font, cardPrefab);
            HudView hudView = BuildHud(frame, font, miniCardPrefab);

            // Công cụ dev: dữ liệu mẫu giống mockup
            if (!withMockupPreview) return;
            var preview = new GameObject("MockupPreview").AddComponent<MockupPreview>();
            SetRef(preview, "boardView", boardView);
            SetRef(preview, "hudView", hudView);
            SetRef(preview, "handView", handView);
        }

        private static BoardView BuildBoard(RectTransform frame, CellView cellPrefab)
        {
            RectTransform board = NewUI("Board", frame);
            Place(board, 16f, 168f, 468f, 468f);
            AddImage(board, LoadSprite("Board/board-panel.png"));

            var view = board.gameObject.AddComponent<BoardView>();
            SetRef(view, "cellPrefab", cellPrefab);
            SetRef(view, "cellRoot", board);

            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 5; c++)
                {
                    var cell = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab.gameObject, board);
                    cell.name = $"Cell {r},{c}";
                    view.PlaceCell((RectTransform)cell.transform, r, c);
                    RecordInstance(cell);
                }
            return view;
        }

        private static HandView BuildHand(RectTransform frame, TMP_FontAsset font, CardView cardPrefab)
        {
            RectTransform hand = NewUI("Hand", frame);
            Place(hand, 184f, 676f, 132f, 132f);

            RectTransform frameImg = NewUI("Frame", hand);
            Stretch(frameImg);
            AddImage(frameImg, LoadSprite("UI/hand-frame.png"));

            var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab.gameObject, hand);
            card.name = "CurrentCard";
            Place((RectTransform)card.transform, 10f, 10f, 112f, 112f);
            var cardView = card.GetComponent<CardView>();
            SetBool(cardView, "showShadow", true);
            cardView.ApplyLayout();
            RecordInstance(card);

            var view = hand.gameObject.AddComponent<HandView>();
            SetRef(view, "cardView", cardView);

            // "Kéo thả vào ô trống" — Figma 106:61 (189, 818), căn giữa quanh x = 250
            RectTransform hint = NewUI("Hint", frame);
            Place(hint, 150f, 818f, 200f, 22.4f);
            AddText(hint, font, "Kéo thả vào ô trống", 14f, HintColor, TextAlignmentOptions.Top);
            return view;
        }

        private static HudView BuildHud(RectTransform frame, TMP_FontAsset font, MiniCardView miniCardPrefab)
        {
            RectTransform hud = NewUI("Hud", frame);
            Stretch(hud);

            // Nút MENU (105:22) — chưa gắn chức năng (bước 2.6)
            RectTransform menu = NewUI("MenuButton", hud);
            Place(menu, 14f, 44f, 118.8f, 46.8f);
            AddImage(menu, LoadSprite("UI/btn-menu.png"), raycast: true);
            menu.gameObject.AddComponent<Button>();
            RectTransform menuLabel = NewUI("Label", menu);
            Place(menuLabel, 51.75f, 12.15f, 41f, 24.5f);
            AddText(menuLabel, font, "MENU", 15.3f, Color.white, TextAlignmentOptions.Top);

            // Điểm (105:29): chữ trắng + bóng chữ #3D1256 thấp hơn.
            // Bản sửa 1 (2.5): 36 → 44px; hộp chữ dời lên để tâm các số vẫn ngang tâm nút MENU,
            // bóng lệch 1.8 × 44/36 = 2.2px. (Figma gốc: 36px, hộp y −1.2 / bóng +0.6)
            RectTransform score = NewUI("Score", hud);
            Place(score, 184f, 38f, 132f, 48f);
            RectTransform scoreShadow = NewUI("Shadow", score);
            Place(scoreShadow, ScoreShadowRect.x, ScoreShadowRect.y, ScoreShadowRect.width, ScoreShadowRect.height);
            var shadowText = AddText(scoreShadow, font, "0", ScoreFontSize, ScoreShadowColor, TextAlignmentOptions.Top);
            RectTransform scoreValue = NewUI("Value", score);
            Place(scoreValue, ScoreValueRect.x, ScoreValueRect.y, ScoreValueRect.width, ScoreValueRect.height);
            var scoreText = AddText(scoreValue, font, "0", ScoreFontSize, Color.white, TextAlignmentOptions.Top);

            // Coin (105:32): số căn phải, kết thúc trước icon
            RectTransform coins = NewUI("Coins", hud);
            Place(coins, 401f, 42f, 85.8f, 46.2f);
            RectTransform coinValue = NewUI("Value", coins);
            Place(coinValue, -40f, 6.6f, 78.2f, 37f);
            var coinText = AddText(coinValue, font, "0", 23.1f, Color.white, TextAlignmentOptions.TopRight);
            RectTransform coinIcon = NewUI("Icon", coins);
            Place(coinIcon, 44.825f, 7.7f, 32.725f, 32.45f);
            AddImage(coinIcon, LoadSprite("UI/coin-icon.png"));

            // UP NEXT (105:76)
            RectTransform upNext = NewUI("UpNext", hud);
            Place(upNext, 374f, 690f, 105.4f, 79.9f);
            RectTransform upLabel = NewUI("Label", upNext);
            Place(upLabel, 0f, 0f, 105.4f, 22f);
            AddText(upLabel, font, "UP NEXT", 13.6f, Color.white, TextAlignmentOptions.Top);
            var mini = (GameObject)PrefabUtility.InstantiatePrefab(miniCardPrefab.gameObject, upNext);
            mini.name = "NextCard";
            Place((RectTransform)mini.transform, 32.3f, 23.8f, 40.8f, 49.3f);
            mini.GetComponent<MiniCardView>().ApplyLayout();
            RecordInstance(mini);

            var view = hud.gameObject.AddComponent<HudView>();
            SetRef(view, "scoreText", scoreText);
            SetRef(view, "scoreShadowText", shadowText);
            SetRef(view, "coinText", coinText);
            SetRef(view, "nextCard", mini.GetComponent<MiniCardView>());
            return view;
        }

        private static void AddToBuildSettings(StringBuilder log)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == ScenePath))
            {
                log.AppendLine("  = Scene đã có trong Build Settings");
                return;
            }
            EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            log.AppendLine("  + Thêm scene vào cuối Build Settings (các scene cũ giữ nguyên)");
        }

        // ------------------------------------------------------------------ Helpers

        internal static Sprite LoadSprite(string pathInImages)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Img + pathInImages);
            if (sprite == null) Debug.LogWarning($"[Suit Merge] Không tìm thấy sprite {Img + pathInImages}");
            return sprite;
        }

        internal static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Đặt theo toạ độ Figma: (x, y) là góc trên-trái trong cha, đơn vị px khung 500×1080.</summary>
        internal static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        internal static Image AddImage(RectTransform rt, Sprite sprite, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = raycast;
            return img;
        }

        internal static TextMeshProUGUI AddText(RectTransform rt, TMP_FontAsset font, string text, float size,
            Color color, TextAlignmentOptions alignment)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        internal static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new MissingFieldException(target.GetType().Name, field);
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) throw new MissingFieldException(target.GetType().Name, field);
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Ghi nhận thay đổi bằng code trên prefab instance (vị trí, cỡ chữ…) để Unity lưu vào scene.</summary>
        internal static void RecordInstance(GameObject instance)
        {
            foreach (Component c in instance.GetComponentsInChildren<Component>(true))
                if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }
    }
}
