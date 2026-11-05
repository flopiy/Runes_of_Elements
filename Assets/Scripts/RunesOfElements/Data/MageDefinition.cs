using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "Mage", menuName = "Runes of Elements/Mage")]
    public class MageDefinition : ScriptableObject
    {
        [SerializeField] private MageDef _def = new MageDef();
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private Sprite _portrait;

        public MageDef Def => _def;
        public Color Color => _color;
        public Sprite Portrait => _portrait;
        public void Configure(MageDef def, Color color, Sprite portrait) { _def = def; _color = color; _portrait = portrait; }
    }
}
