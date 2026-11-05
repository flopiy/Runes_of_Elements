using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "Rune", menuName = "Runes of Elements/Rune")]
    public class RuneDefinition : ScriptableObject
    {
        [SerializeField] private RuneType _rune;
        [SerializeField] private string _displayName;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private Sprite _icon;
        [TextArea(2, 4)] [SerializeField] private string _description;

        public RuneType Rune => _rune;
        public string DisplayName => _displayName;
        public Color Color => _color;
        public Sprite Icon => _icon;
        public string Description => _description;

        public void Configure(RuneType rune, string name, Color color, Sprite icon, string description)
        { _rune = rune; _displayName = name; _color = color; _icon = icon; _description = description; }
    }
}
