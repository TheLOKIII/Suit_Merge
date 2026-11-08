using System;
using System.Collections;
using System.Collections.Generic;
using SuitMerge.View.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Bảng chọn level TẠM (bước 4.5, tới M7 có màn Home thật): lưới L1…Ln + nút ENDLESS. Mọi level đều mở.
    /// Nút level nhân bản từ <see cref="levelButtonTemplate"/> (ẩn sẵn trong scene).
    /// </summary>
    public class LevelSelectPopup : PopupView
    {
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private Button levelButtonTemplate;
        [SerializeField] private Button endlessButton;

        /// <summary>Animation phần 4: nút sau bật trễ hơn nút trước bao nhiêu giây, và mỗi nút bật trong bao lâu.</summary>
        private const float StaggerStep = 0.03f;
        private const float StaggerPop = 0.2f;

        /// <summary>Chọn level (chỉ số 0-based trong danh sách level).</summary>
        public event Action<int> LevelChosen;
        public event Action EndlessChosen;

        private readonly List<Button> _buttons = new List<Button>();

        private void Awake()
        {
            endlessButton.onClick.AddListener(() => EndlessChosen?.Invoke());
            levelButtonTemplate.gameObject.SetActive(false);
        }

        /// <summary>Hiện lưới với các level có số <paramref name="levelIds"/> (theo thứ tự).</summary>
        public void Show(IReadOnlyList<int> levelIds)
        {
            if (_buttons.Count != levelIds.Count)
            {
                foreach (Button b in _buttons)
                    if (b != null) Destroy(b.gameObject);
                _buttons.Clear();
                for (int i = 0; i < levelIds.Count; i++)
                {
                    Button b = Instantiate(levelButtonTemplate, gridRoot);
                    b.gameObject.SetActive(true);
                    b.name = "Level " + levelIds[i];
                    int index = i;
                    b.onClick.AddListener(() => LevelChosen?.Invoke(index));
                    _buttons.Add(b);
                }
            }
            for (int i = 0; i < _buttons.Count; i++)
            {
                var label = _buttons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = levelIds[i].ToString();
            }
            // Animation phần 4: các nút bật lần lượt (cách nhau 0,03s) — ẩn trước khi popup hiện
            foreach (Button b in _buttons) b.transform.localScale = Vector3.zero;
            endlessButton.transform.localScale = Vector3.zero;
            Open();
        }

        protected override void OnShown() => StartCoroutine(Stagger());

        private IEnumerator Stagger()
        {
            var all = new List<Transform>();
            foreach (Button b in _buttons) all.Add(b.transform);
            all.Add(endlessButton.transform);
            float time = 0f;
            float total = StaggerStep * all.Count + StaggerPop;
            while (time < total)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    float k = Mathf.Clamp01((time - StaggerStep * i) / StaggerPop);
                    all[i].localScale = Vector3.one * (k <= 0f ? 0f : Ease.OutBack(k));
                }
                time += Time.unscaledDeltaTime;
                yield return null;
            }
            foreach (Transform t in all) t.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            foreach (Button b in _buttons)
                if (b != null) b.transform.localScale = Vector3.one;
            if (endlessButton != null) endlessButton.transform.localScale = Vector3.one;
        }
    }
}
