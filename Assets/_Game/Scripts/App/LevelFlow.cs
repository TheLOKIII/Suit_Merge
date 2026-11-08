using System.Collections.Generic;
using System.Linq;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using UnityEngine;

namespace SuitMerge.App
{
    /// <summary>
    /// Danh sách level chơi được (bước 4.5): đọc các file Data/Levels/*.json gán sẵn cho GameBootstrap.
    /// File lỗi bị bỏ qua (ghi Console — chạy menu Kiểm tra file level để xem chi tiết).
    /// </summary>
    public sealed class LevelFlow
    {
        private readonly List<LevelData> _levels = new List<LevelData>();

        public LevelFlow(IEnumerable<TextAsset> files)
        {
            if (files == null) return;
            foreach (TextAsset file in files)
            {
                if (file == null) continue;
                LevelLoadResult r = LevelLoader.Load(file.text, file.name);
                if (r.Ok) _levels.Add(r.Level);
                else Debug.LogError($"[Suit Merge] {file.name} lỗi — bỏ qua:\n  • " + string.Join("\n  • ", r.Errors));
            }
            _levels.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        public int Count => _levels.Count;
        public LevelData this[int index] => _levels[index];
        public IReadOnlyList<int> Ids => _levels.Select(l => l.Id).ToList();
        public bool HasNext(int index) => index + 1 < _levels.Count;

        /// <summary>Chất mở hiệu ứng ở level này mà level liền trước chưa mở (→ popup MỞ KHÓA!).</summary>
        public IReadOnlyList<Suit> NewSuits(int index)
        {
            IReadOnlyList<Suit> now = _levels[index].EnabledSuitEffects;
            IReadOnlyList<Suit> before = index > 0 ? _levels[index - 1].EnabledSuitEffects : new List<Suit>();
            return now.Where(s => !before.Contains(s)).ToList();
        }
    }
}
