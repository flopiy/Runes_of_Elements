using System.Collections.Generic;
using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "Campaign", menuName = "Runes of Elements/Campaign")]
    public class CampaignDefinition : ScriptableObject
    {
        [Tooltip("Поверхи Башти (кожен — список хвиль). Якщо порожньо, використовується плоский список битв.")]
        [SerializeField] private List<FloorSpec> _floors = new List<FloorSpec>();
        [SerializeField] private List<BattleSpec> _battles = new List<BattleSpec>();
        [SerializeField] private List<RunePoolSpec> _runePool = new List<RunePoolSpec>();

        public List<FloorSpec> Floors => _floors;
        public List<BattleSpec> Battles => _battles;
        public List<RunePoolSpec> RunePool => _runePool;
        public void Configure(List<BattleSpec> battles, List<RunePoolSpec> pool) { _battles = battles; _runePool = pool; }
        public void Configure(List<FloorSpec> floors, List<BattleSpec> battles, List<RunePoolSpec> pool) { _floors = floors; _battles = battles; _runePool = pool; }
    }
}
