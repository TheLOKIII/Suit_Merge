using TMPro;
using UnityEngine;

namespace SuitMerge.View.Localization
{
    /// <summary>
    /// Gắn vào chữ cố định trong scene (MENU, UP NEXT, nút popup…): hiện chữ của <see cref="key"/>
    /// theo ngôn ngữ đang chọn, tự đổi khi người chơi đổi ngôn ngữ.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedLabel : MonoBehaviour
    {
        [Tooltip("Key trong Data/Localization/Resources/strings.json")]
        [SerializeField] private string key;

        private TMP_Text _text;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Loc.Changed -= Apply;

        public void Apply()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_text == null) _text = GetComponent<TMP_Text>();
            _text.text = Loc.Get(key);
        }
    }
}
