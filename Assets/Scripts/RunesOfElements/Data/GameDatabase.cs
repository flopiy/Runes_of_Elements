using System.Collections.Generic;
using System.Linq;
using RunesOfElements.Core;
using UnityEngine;

namespace RunesOfElements.Data
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Runes of Elements/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField] private List<MageDefinition> _mages = new List<MageDefinition>();
        [SerializeField] private List<MonsterDefinition> _monsters = new List<MonsterDefinition>();
        [SerializeField] private List<BlessingCardDefinition> _blessings = new List<BlessingCardDefinition>();
        [SerializeField] private List<RuneDefinition> _runes = new List<RuneDefinition>();
        [SerializeField] private List<StatusDefinition> _statuses = new List<StatusDefinition>();
        [SerializeField] private CampaignDefinition _campaign;

        public IReadOnlyList<MageDefinition> Mages => _mages;
        public IReadOnlyList<MonsterDefinition> Monsters => _monsters;
        public IReadOnlyList<BlessingCardDefinition> Blessings => _blessings;
        public IReadOnlyList<RuneDefinition> Runes => _runes;
        public IReadOnlyList<StatusDefinition> Statuses => _statuses;
        public CampaignDefinition Campaign => _campaign;

        public void Configure(List<MageDefinition> mages, List<MonsterDefinition> monsters, List<BlessingCardDefinition> blessings,
            List<RuneDefinition> runes, List<StatusDefinition> statuses, CampaignDefinition campaign)
        {
            _mages = mages; _monsters = monsters; _blessings = blessings; _runes = runes; _statuses = statuses; _campaign = campaign;
        }

        public GameContent BuildContent()
        {
            var c = new GameContent();
            c.Mages.AddRange(_mages.Where(m => m != null).Select(m => m.Def));
            c.Monsters.AddRange(_monsters.Where(m => m != null).Select(m => m.Def));
            c.Blessings.AddRange(_blessings.Where(b => b != null).Select(b => b.Def));
            if (_campaign != null)
            {
                if (_campaign.Floors != null && _campaign.Floors.Count > 0)
                {
                    c.Tower.AddRange(_campaign.Floors);
                    c.RebuildCampaignFromTower();
                }
                else c.Campaign.AddRange(_campaign.Battles);
                c.RunePool.AddRange(_campaign.RunePool);
            }
            return c;
        }

        public MageDefinition Mage(string id) => _mages.FirstOrDefault(m => m != null && m.Def.Id == id);
        public MonsterDefinition Monster(string id) => _monsters.FirstOrDefault(m => m != null && m.Def.Id == id);
        public BlessingCardDefinition Blessing(string id) => _blessings.FirstOrDefault(b => b != null && b.Def.Id == id);
        public RuneDefinition Rune(RuneType r) => _runes.FirstOrDefault(x => x != null && x.Rune == r);
        public StatusDefinition Status(StatusType t) => _statuses.FirstOrDefault(x => x != null && x.Type == t);

        public Color RuneColor(RuneType r) { var d = Rune(r); return d != null ? d.Color : Color.white; }
        public Color StatusColor(StatusType t) { var d = Status(t); return d != null ? d.Color : Color.white; }
    }
}
