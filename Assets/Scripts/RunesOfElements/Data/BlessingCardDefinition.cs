using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "Blessing", menuName = "Runes of Elements/Blessing Card")]
    public class BlessingCardDefinition : ScriptableObject
    {
        [SerializeField] private BlessingDef _def = new BlessingDef();
        [SerializeField] private Sprite _art;

        public BlessingDef Def => _def;
        public Sprite Art => _art;
        public void Configure(BlessingDef def, Sprite art) { _def = def; _art = art; }
    }
}
