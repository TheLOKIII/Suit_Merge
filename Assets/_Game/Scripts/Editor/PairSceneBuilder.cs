using System;
using System.Text;
using SuitMerge.App;
using SuitMerge.View.Animation;
using SuitMerge.View.Hand;
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
    /// M4.5 phần B — bổ sung khối đôi vào scene Game (gọi sau <see cref="Step45SceneBuilder.Ensure"/>):
    /// ảnh khung tay khối đôi + mũi tên xoay vào FxTheme (scene Sandbox dùng chung) · nối chữ dưới khung tay ·
    /// popup Figma "HẾT CHỖ!" cho khối đôi (178:390; nút Swap / Hammer dựng sẵn nhưng ẩn tới M5).
    /// Chạy lại nhiều lần không tạo trùng, không xoá gì.
    /// </summary>
    internal static class PairSceneBuilder
    {
        internal static void Ensure(TMP_FontAsset font, StringBuilder log)
        {
            log.AppendLine("  — M4.5 (khối đôi):");
            EnsureFxTheme(log);

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (canvas == null || hand == null || bootstrap == null)
                throw new InvalidOperationException("Scene thiếu Canvas / HandView / GameBootstrap.");

            // Chữ "Kéo thả vào ô trống" → khối đôi đổi thành "Chạm để xoay · Kéo để đặt"
            Transform hint = hand.transform.parent != null ? hand.transform.parent.Find("Hint") : null;
            var handSo = new SerializedObject(hand);
            if (hint != null) SetIfEmpty(handSo, "caption", hint.GetComponent<TextMeshProUGUI>(), log);
            handSo.ApplyModifiedPropertiesWithoutUndo();

            PairNoSpacePopup popup = EnsurePopup<PairNoSpacePopup>("PairNoSpacePopup", log, () => BuildPopup((RectTransform)canvas.transform));
            var so = new SerializedObject(bootstrap);
            SetIfEmpty(so, "pairNoSpacePopup", popup, log);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFxTheme(StringBuilder log)
        {
            var theme = AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath);
            if (theme == null) throw new InvalidOperationException("Không thấy " + FxThemePath);
            var so = new SerializedObject(theme);
            Fill(so, "handPairH", Ui("hand-pair-h"), log);
            Fill(so, "handPairV", Ui("hand-pair-v"), log);
            Fill(so, "rotateArrow", Ui("rotate-arrow"), log);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        /// <summary>Figma popup-pair-no-space 178:390: khối đôi 0.8 · chữ 22 tối · 2 nút vàng (ẩn) · BỎ CUỘC.</summary>
        private static PairNoSpacePopup BuildPopup(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel, _) = Shell(canvas, "PairNoSpacePopup", "pair.title", 76);

            RectTransform piece = NewUI("Piece", panel);
            piece.sizeDelta = new Vector2(201.6f, 105.6f);
            var le = piece.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 201.6f;
            le.preferredHeight = 105.6f;

            TextMeshProUGUI text = Localized(Step45SceneBuilder.Text(panel, "Không còn 2 ô trống kề nhau\ncho khối đôi này.", 22f, new Color32(0x2A, 0x2A, 0x2A, 0xFF), 380f), "pair.no_space");
            text.rectTransform.sizeDelta = new Vector2(380f, 22f * 1.6f * 2f);

            Button swap = Step45SceneBuilder.Button(panel, Theme.buttonGold, "btn.swap_next", 380f);
            Button hammer = Step45SceneBuilder.Button(panel, Theme.buttonGold, "btn.hammer_one", 380f);
            swap.gameObject.SetActive(false);
            hammer.gameObject.SetActive(false);
            Button giveUp = Ghost(panel, "btn.give_up");

            var popup = root.gameObject.AddComponent<PairNoSpacePopup>();
            SetRef(popup, "theme", Theme);
            SetRef(popup, "fxTheme", AssetDatabase.LoadAssetAtPath<FxTheme>(FxThemePath));
            SetRef(popup, "pieceRoot", piece);
            SetRef(popup, "swapButton", swap);
            SetRef(popup, "hammerButton", hammer);
            SetRef(popup, "giveUpButton", giveUp);
            root.gameObject.SetActive(false);
            return popup;
        }
    }
}
