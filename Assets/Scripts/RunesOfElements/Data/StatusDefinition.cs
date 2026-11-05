using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "Status", menuName = "Runes of Elements/Status")]
    public class StatusDefinition : ScriptableObject
    {
        [SerializeField] private StatusType _type;
        [SerializeField] private string _displayName;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private Sprite _icon;
        [TextArea(2, 4)] [SerializeField] private string _tooltip;

        public StatusType Type => _type;
        public string DisplayName => _displayName;
        public Color Color => _color;
        public Sprite Icon => _icon;
        public string Tooltip => _tooltip;

        public void Configure(StatusType type, string name, Color color, Sprite icon, string tooltip)
        { _type = type; _displayName = name; _color = color; _icon = icon; _tooltip = tooltip; }
    }
}
