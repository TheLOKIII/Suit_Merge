# Suit Merge

Game ghép lá bài theo chất (♥ ♦ ♣ ♠) trên bàn ô vuông: kéo lá từ tay thả xuống bàn, các lá cùng chất nằm cạnh nhau gộp lại thành lá cao hơn, tạo chuỗi combo và hoàn thành mục tiêu của từng level.

## Yêu cầu
- **Unity 6000.3.15f1** (Unity 6.3), Universal Render Pipeline 2D.
- Các package được Unity tự tải theo `Packages/manifest.json` khi mở dự án.

## Mở dự án
1. Tải về: `git clone https://github.com/TheLOKIII/Suit_Merge.git`
2. Mở Unity Hub → **Add** → chọn thư mục vừa tải → mở bằng Unity 6000.3.15f1.
   Lần mở đầu Unity cần vài phút để tạo lại thư mục `Library`.
3. Mở scene `Assets/_Game/Scenes/Game.unity` và bấm **Play**.

## Cấu trúc
| Thư mục | Nội dung |
|---|---|
| `Assets/_Game/Scripts/Core` | Luật chơi viết bằng C# thuần (không phụ thuộc Unity), có unit test |
| `Assets/_Game/Scripts/View` | Hiển thị: bàn, tay bài, HUD, popup, animation, âm thanh |
| `Assets/_Game/Scripts/App` | Khởi động game, nối Core với View |
| `Assets/_Game/Scripts/Editor` | Menu **Suit Merge** trong Unity: dựng / cập nhật scene, mô phỏng level |
| `Assets/_Game/Data` | Cấu hình game, dữ liệu level, chữ đa ngôn ngữ (Việt / Anh), theme |
| `Assets/_Game/Tests` | Unit test (chạy trong Unity Test Runner) |
| `Assets/_Game/Images`, `Audio`, `Fonts` | Hình, âm thanh tạm, font |
| `Tools/gen_audio.py` | Script Python tạo lại toàn bộ âm thanh tạm |

## Bản quyền
- Font **Baloo 2** — SIL Open Font License (xem `Assets/_Game/Fonts/OFL.txt`).
