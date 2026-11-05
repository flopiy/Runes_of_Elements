using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "Monster", menuName = "Runes of Elements/Monster")]
    public class MonsterDefinition : ScriptableObject
    {
        [SerializeField] private MonsterDef _def = new MonsterDef();
        [SerializeField] private Sprite _portrait;

        public MonsterDef Def => _def;
        public Sprite Portrait => _portrait;
        public void Configure(MonsterDef def, Sprite portrait) { _def = def; _portrait = portrait; }
    }
}
