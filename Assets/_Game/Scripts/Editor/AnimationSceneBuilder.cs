using System;
using System.Text;
using SuitMerge.App;
using SuitMerge.View.Animation;
using SuitMerge.View.Cards;
using SuitMerge.View.Board;
using SuitMerge.View.Popups;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SuitMerge.EditorTools.GameSceneBuilder;
using static SuitMerge.EditorTools.Step45SceneBuilder;
using Object = UnityEngine.Object;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Animation phần 1 — bổ sung vào scene Game (gọi sau <see cref="PairSceneBuilder.Ensure"/>):
    /// hàng HIỆU ỨNG (ĐẦY ĐỦ / GIẢM BỚT) trong popup Cài đặt · lớp bắt chạm tua nhanh nối vào EventPlayer ·
    /// <see cref="ButtonPress"/> (nút nhún) cho MỌI nút của scene, kể cả nút trong popup đang ẩn.
    /// Animation phần 3: ảnh tia sáng (fx-rays) + viền phát sáng (fx-edge, 9-slice) vào FxTheme (scene Sandbox dùng chung).
    /// Animation phần 4: màn đen chuyển màn (ScreenFader, nối vào GameBootstrap) · hạt nền (Ambient, dưới bàn) ·
    /// tia sáng cho popup MỞ KHÓA.
    /// Animation phần 5: tạo Data/Theme/SfxTheme.asset + gán file âm (Audio/SFX, Audio/Music) · âm 4 chất vào FxTheme ·
    /// nối SfxTheme vào EventPlayer · hàng ÂM THANH / NHẠC (BẬT / TẮT) trong Cài đặt.
    /// Chạy lại nhiều lần không tạo trùng, không xoá gì.
    /// </summary>
    internal static class AnimationSceneBuilder
    {
        private const string CatcherName = "FastForward";
        private const string FaderName = "ScreenFader";
        private const string AmbientName = "Ambient";
        private const string SfxThemePath = Root + "Data/Theme/SfxTheme.asset";
        private const string SfxFolder = Root + "Audio/SFX/";
        private const string MusicPath = Root + "Audio/Music/music-loop.wav";

        /// <summary>Ô của SfxTheme ↔ tên file trong Audio/SFX (âm tạm tạo bằng Tools/gen_audio.py).</summary>
        private static readonly (string field, string file)[] SfxFiles =
        {
            ("pick", "pick"), ("place", "place"), ("invalid", "invalid"), ("rotate", "rotate"), ("button", "button"),
            ("merge", "merge"), ("create", "create"), ("combo", "combo"), ("draw", "draw"),
            ("ace", "ace"), ("goalStep", "goal-step"), ("goalDone", "goal-done"),
            ("popupOpen", "popup"), ("win", "win"), ("lose", "lose"), ("unlock", "unlock")
        };

        /// <summary>Ô âm thanh 4 chất của FxTheme (bước 1.4b) ↔ tên file.</summary>
        private static readonly (string field, string file)[] FxFiles =
        {
            ("stamp", "fx-stamp"), ("diamond", "fx-diamond"), ("heartStep", "fx-heart-step"), ("club", "fx-club"), ("spade", "fx-spade")
        };
        private const string EdgePath = Root + "Images/Effects/fx-edge.png";
        /// <summary>fx-edge.png 256×256: dải sáng 56px + góc bo 40 → cắt 9-slice 64px mỗi cạnh.</summary>
        private static readonly Vector4 EdgeBorder = new Vector4(64f, 64f, 64f, 64f);

        internal static void Ensure(TMP_FontAsset font, StringBuilder log)
        {
            log.AppendLine("  — Animation phần 1 (nền tảng):");
            var boardView = Object.FindFirstObjectByType<BoardView>(FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<EventPlayer>(FindObjectsInactive.Include);
            if (boardView == null || player == null)
                throw new InvalidOperationException("Scene thiếu Board / EventPlayer.");

            EnsureFxTheme(log);
            EnsureSettingsRow(log);
            EnsureCatcher((RectTransform)boardView.transform.parent, player, log);
            log.AppendLine("  — Animation phần 4 (đầu / cuối level, chuyển màn):");
            EnsureFader(log);
            EnsureAmbient((RectTransform)boardView.transform.parent, log);
            EnsureUnlockRays(log);
            log.AppendLine("  — Animation phần 5 (âm thanh):");
            EnsureAudio(player, log);
            EnsureAudioListener(log);
            EnsureAudioRows(log);
            EnsureButtonPress(log); // sau cùng → gồm cả các nút vừa dựng
        }

        /// <summary>SfxTheme.asset (tạo nếu chưa có) + gán file âm vào ô còn trống; nhạc nền nhập dạng Streaming.</summary>
        private static void EnsureAudio(EventPlayer player, StringBuilder log)
        {
            var theme = AssetDatabase.LoadAssetAtPath<SfxTheme>(SfxThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<SfxTheme>();
                AssetDatabase.CreateAsset(theme, SfxThemePath);
                log.AppendLine("  + " + SfxThemePath);
            }
            var so = new SerializedObject(theme);
            int missing = 0;
            foreach ((string field, string file) in SfxFiles)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + file + ".wav");
                if (clip == null) missing++;
                Fill(so, field, clip, log);
            }
            Fill(so, "music", AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath), log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            if (missing > 0) log.AppendLine($"    (cảnh báo: thiếu {missing} file trong {SfxFolder} — ô đó im lặng)");

            if (AssetImporter.GetAtPath(MusicPath) is AudioImporter music)
            {
                AudioImporterSampleSettings settings = music.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.Streaming)
                {
                    settings.loadType = AudioClipLoadType.Streaming;
                    music.defaultSampleSettings = settings;
                    music.SaveAndReimport();
                    log.AppendLine("  + Nhạc nền nhập dạng Streaming");
                }
            }

            var fx = AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath);
            if (fx != null)
            {
                var fxSo = new SerializedObject(fx);
                foreach ((string field, string file) in FxFiles)
                    Fill(fxSo, field, AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + file + ".wav"), log);
                fxSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fx);
            }

            var playerSo = new SerializedObject(player);
            SetIfEmpty(playerSo, "sfxTheme", theme, log);
            playerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Unity chỉ phát âm thanh khi scene có Audio Listener. Camera do menu Tạo scene Game dựng trước đây thiếu nó
        /// → gắn vào Main Camera (không có camera thì gắn vào EventPlayer).
        /// </summary>
        private static void EnsureAudioListener(StringBuilder log)
        {
            if (Object.FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) != null)
            {
                log.AppendLine("  = Audio Listener đã có");
                return;
            }
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            var player = Object.FindFirstObjectByType<EventPlayer>(FindObjectsInactive.Include);
            GameObject host = cam != null ? cam.gameObject : player != null ? player.gameObject : null;
            if (host == null) return;
            host.AddComponent<AudioListener>();
            log.AppendLine($"  + Audio Listener trên {host.name} (thiếu nó thì không có tiếng)");
        }

        /// <summary>Cài đặt: hàng "Âm thanh" BẬT / TẮT và "Nhạc" BẬT / TẮT, dưới hàng HIỆU ỨNG.</summary>
        private static void EnsureAudioRows(StringBuilder log)
        {
            var popup = Object.FindFirstObjectByType<SettingsPopup>(FindObjectsInactive.Include);
            if (popup == null) return;
            var panel = popup.transform.Find("Panel") as RectTransform;
            if (panel == null) return;
            var so = new SerializedObject(popup);
            if (so.FindProperty("sfxOnButton").objectReferenceValue == null)
            {
                (Button on, Button off) = AddToggleRow(panel, "Effects", "Sound", "Âm thanh", "settings.sound", "btn.on", "btn.off");
                so.FindProperty("sfxOnButton").objectReferenceValue = on;
                so.FindProperty("sfxOffButton").objectReferenceValue = off;
                log.AppendLine("  + Cài đặt: hàng ÂM THANH — BẬT / TẮT");
            }
            if (so.FindProperty("musicOnButton").objectReferenceValue == null)
            {
                (Button on, Button off) = AddToggleRow(panel, "Sound", "Music", "Nhạc", "settings.music", "btn.on", "btn.off");
                so.FindProperty("musicOnButton").objectReferenceValue = on;
                so.FindProperty("musicOffButton").objectReferenceValue = off;
                log.AppendLine("  + Cài đặt: hàng NHẠC — BẬT / TẮT");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Chữ + hàng 2 nút (teal / tím, 170 × 70 như hàng ngôn ngữ) chèn ngay sau hàng <paramref name="after"/>
        /// (không thấy → cuối khung).
        /// </summary>
        private static (Button left, Button right) AddToggleRow(RectTransform panel, string after, string rowName, string labelText,
            string labelKey, string leftKey, string rightKey)
        {
            Transform prev = panel.Find(after);
            int index = prev != null ? prev.GetSiblingIndex() + 1 : panel.childCount;

            TextMeshProUGUI label = Localized(Step45SceneBuilder.Text(panel, labelText, 24f, UiTheme.TextDark, 380f), labelKey);
            label.name = rowName + "Label";
            label.transform.SetSiblingIndex(index);

            RectTransform row = NewUI(rowName, panel);
            row.sizeDelta = new Vector2(380f, 70f);
            HLayout(row, new RectOffset(0, 0, 0, 0), 16f, TextAnchor.MiddleCenter);
            row.SetSiblingIndex(index + 1);
            Button left = Step45SceneBuilder.Button(row, Theme.buttonTeal, leftKey, 170f, 24f);
            Button right = Step45SceneBuilder.Button(row, Theme.buttonPurple, rightKey, 170f, 24f);
            return (left, right);
        }

        /// <summary>Màn đen phủ cả Canvas (trên popup), ẩn sẵn — GameBootstrap bật khi đổi màn.</summary>
        private static void EnsureFader(StringBuilder log)
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (canvas == null || bootstrap == null) throw new InvalidOperationException("Scene thiếu Canvas / GameBootstrap.");
            var fader = Object.FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
            if (fader != null) log.AppendLine("  = Màn đen chuyển màn đã có");
            else
            {
                RectTransform rt = NewUI(FaderName, canvas.transform);
                Stretch(rt);
                AddImage(rt, null, raycast: true).color = new Color32(0x0A, 0x00, 0x14, 0xFF);
                var group = rt.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                fader = rt.gameObject.AddComponent<ScreenFader>();
                rt.SetAsLastSibling();
                rt.gameObject.SetActive(false);
                log.AppendLine("  + Màn đen chuyển màn (ScreenFader)");
            }
            var so = new SerializedObject(bootstrap);
            SetIfEmpty(so, "screenFader", fader, log);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Lớp hạt ♠♥♦♣ trôi nền: con ĐẦU của khung thiết kế → nằm dưới bàn / tay / HUD.</summary>
        private static void EnsureAmbient(RectTransform frame, StringBuilder log)
        {
            var existing = frame.Find(AmbientName);
            if (existing != null)
            {
                log.AppendLine("  = Hạt nền đã có");
                return;
            }
            RectTransform rt = NewUI(AmbientName, frame);
            Stretch(rt);
            rt.SetAsFirstSibling();
            var ambient = rt.gameObject.AddComponent<AmbientParticles>();
            SetRef(ambient, "cardTheme", AssetDatabase.LoadAssetAtPath<CardTheme>(ThemePath));
            log.AppendLine("  + Hạt nền ♠♥♦♣ (Ambient)");
        }

        private static void EnsureUnlockRays(StringBuilder log)
        {
            var popup = Object.FindFirstObjectByType<UnlockPopup>(FindObjectsInactive.Include);
            if (popup == null) return;
            var so = new SerializedObject(popup);
            SetIfEmpty(so, "fxTheme", AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath), log);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Animation phần 3: ảnh FX mới (ô còn trống mới gán — đã chỉnh tay thì giữ nguyên).</summary>
        private static void EnsureFxTheme(StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(EdgePath) as TextureImporter;
            if (importer == null) log.AppendLine("    (cảnh báo: không thấy " + EdgePath + ")");
            else if (importer.spriteBorder == Vector4.zero)
            {
                importer.spriteBorder = EdgeBorder;
                importer.SaveAndReimport();
                log.AppendLine("  + Viền 9-slice: " + EdgePath);
            }

            var theme = AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath);
            if (theme == null) throw new InvalidOperationException("Không thấy " + FxThemePath);
            var so = new SerializedObject(theme);
            Fill(so, "rays", Fx("fx-rays"), log);
            Fill(so, "edgeGlow", Fx("fx-edge"), log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        /// <summary>Dưới hàng ngôn ngữ: chữ "Hiệu ứng" + 2 nút ĐẦY ĐỦ (teal = đang chọn) / GIẢM BỚT (tím), cùng cỡ nút ngôn ngữ.</summary>
        private static void EnsureSettingsRow(StringBuilder log)
        {
            var popup = Object.FindFirstObjectByType<SettingsPopup>(FindObjectsInactive.Include);
            if (popup == null)
            {
                log.AppendLine("    (cảnh báo: không thấy SettingsPopup — bỏ qua hàng HIỆU ỨNG)");
                return;
            }
            var so = new SerializedObject(popup);
            if (so.FindProperty("fxFullButton").objectReferenceValue != null)
            {
                log.AppendLine("  = Hàng HIỆU ỨNG trong Cài đặt đã có");
                return;
            }

            var panel = popup.transform.Find("Panel") as RectTransform;
            if (panel == null) throw new InvalidOperationException("SettingsPopup thiếu Panel.");
            (Button full, Button reduced) = AddToggleRow(panel, "Languages", "Effects", "Hiệu ứng", "settings.effects", "btn.fx_full", "btn.fx_reduced");

            so.FindProperty("fxFullButton").objectReferenceValue = full;
            so.FindProperty("fxReducedButton").objectReferenceValue = reduced;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("  + Cài đặt: hàng HIỆU ỨNG — ĐẦY ĐỦ / GIẢM BỚT");
        }

        /// <summary>
        /// Lớp trong suốt phủ cả khung màn chơi, nằm TRÊN lớp Fx (chạm vào đâu cũng tua được), ẩn sẵn —
        /// EventPlayer chỉ bật khi đang phát lượt.
        /// </summary>
        private static void EnsureCatcher(RectTransform frame, EventPlayer player, StringBuilder log)
        {
            var rt = frame.Find(CatcherName) as RectTransform;
            FastForwardCatcher catcher;
            if (rt != null)
            {
                catcher = rt.GetComponent<FastForwardCatcher>();
                log.AppendLine("  = Lớp tua nhanh đã có");
            }
            else
            {
                rt = NewUI(CatcherName, frame);
                Stretch(rt);
                AddImage(rt, null, raycast: true).color = new Color(0f, 0f, 0f, 0f);
                catcher = rt.gameObject.AddComponent<FastForwardCatcher>();
                log.AppendLine("  + Lớp tua nhanh (chạm khi đang phát animation → ×3)");
            }
            rt.SetAsLastSibling(); // trên lớp Fx (lớp Fx được đưa xuống cuối mỗi lần cập nhật bước 2.5)
            rt.gameObject.SetActive(false);

            var so = new SerializedObject(player);
            SetIfEmpty(so, "fastForwardCatcher", catcher, log);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureButtonPress(StringBuilder log)
        {
            int added = 0, total = 0;
            foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                total++;
                if (b.GetComponent<ButtonPress>() != null) continue;
                b.gameObject.AddComponent<ButtonPress>();
                added++;
            }
            log.AppendLine(added > 0 ? $"  + Nút nhún (ButtonPress): thêm vào {added}/{total} nút"
                                     : $"  = Nút nhún đã có ở cả {total} nút");
        }
    }
}
