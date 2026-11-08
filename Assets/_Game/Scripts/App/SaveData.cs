using UnityEngine;

namespace SuitMerge.App
{
    /// <summary>
    /// Dữ liệu lưu giữa các lần chơi (PlayerPrefs).
    /// Xoá để thử lại: Edit → Clear All PlayerPrefs (trong Unity Editor).
    /// </summary>
    public static class SaveData
    {
        private const string EndlessBestKey = "suitmerge.endless.best";
        private const string CoinsKey = "suitmerge.coins";

        public static int EndlessBest
        {
            get => PlayerPrefs.GetInt(EndlessBestKey, 0);
            private set
            {
                PlayerPrefs.SetInt(EndlessBestKey, value);
                PlayerPrefs.Save();
            }
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CoinsKey, 0);
            set
            {
                PlayerPrefs.SetInt(CoinsKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Ghi điểm ván Endless vừa kết thúc. Trả true nếu là kỷ lục mới.</summary>
        public static bool SubmitEndlessScore(int score)
        {
            if (score <= EndlessBest) return false;
            EndlessBest = score;
            return true;
        }
    }
}
