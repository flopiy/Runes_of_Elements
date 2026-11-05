using System;
using System.Collections.Generic;

namespace RunesOfElements.Core
{
    [Serializable]
    public sealed class MageDef
    {
        public string Id;
        public string Name;
        public RuneType Element;
        public string PassiveAbilityId;
        public string PassiveName;
        public string PassiveText;
    }

    [Serializable]
    public sealed class MonsterDef
    {
        public string Id;
        public string Name;
        public MonsterLevel Level;
        public int Hp;
        public int Attack;
        public List<string> AbilityIds = new List<string>();
        public string AbilityName;
        public string AbilityText;
    }

    [Serializable]
    public sealed class BlessingDef
    {
        public string Id;
        public int Number;
        public string Name;
        public BlessingKind Kind;
        public BlessingTrigger Trigger;
        public string Text;
        /// <summary>0 = необмежено.</summary>
        public int UsesPerBattle;
        public int UsesPerCampaign;
    }

    [Serializable]
    public sealed class BattleSpec
    {
        public int Level1;
        public int Level2;
        public int Level3;
        public bool Boss;
    }

    /// <summary>Поверх Башти: послідовність хвиль (кожна хвиля — окрема битва).</summary>
    [Serializable]
    public sealed class FloorSpec
    {
        public string Name;
        public List<BattleSpec> Waves = new List<BattleSpec>();
        /// <summary>Додаткове здоров'я всім монстрам поверху (балансний важіль).</summary>
        public int MonsterHpBonus;
    }

    [Serializable]
    public sealed class RunePoolSpec
    {
        public RuneType Rune;
        public int Count;
    }

    /// <summary>Повний набір контенту для однієї гри. Будується з ScriptableObject-ів або з DefaultContent.</summary>
    [Serializable]
    public sealed class GameContent
    {
        public List<MageDef> Mages = new List<MageDef>();
        public List<MonsterDef> Monsters = new List<MonsterDef>();
        public List<BlessingDef> Blessings = new List<BlessingDef>();
        /// <summary>Плоский список битв (генерується з Tower, якщо Tower не порожня).</summary>
        public List<BattleSpec> Campaign = new List<BattleSpec>();
        /// <summary>Башта: поверхи × хвилі. Якщо порожня — кожна битва Campaign вважається окремим поверхом з 1 хвилею.</summary>
        public List<FloorSpec> Tower = new List<FloorSpec>();
        public List<RunePoolSpec> RunePool = new List<RunePoolSpec>();

        /// <summary>Перебудовує Campaign з Tower і повертає таблицю (поверх, хвиля) для кожної битви.</summary>
        public void RebuildCampaignFromTower()
        {
            if (Tower.Count == 0) return;
            Campaign.Clear();
            foreach (var f in Tower) Campaign.AddRange(f.Waves);
        }

        public int FloorCount => Tower.Count > 0 ? Tower.Count : Campaign.Count;

        /// <summary>Повертає (індекс поверху, індекс хвилі, кількість хвиль) для плоского індексу битви.</summary>
        public void Locate(int battleIndex, out int floor, out int wave, out int wavesInFloor)
        {
            if (Tower.Count == 0) { floor = battleIndex; wave = 0; wavesInFloor = 1; return; }
            int idx = battleIndex;
            for (int f = 0; f < Tower.Count; f++)
            {
                int n = Tower[f].Waves.Count;
                if (idx < n) { floor = f; wave = idx; wavesInFloor = n; return; }
                idx -= n;
            }
            floor = Tower.Count - 1; wavesInFloor = Tower[floor].Waves.Count; wave = wavesInFloor - 1;
        }

        public int MonsterHpBonusFor(int battleIndex)
        {
            if (Tower.Count == 0) return 0;
            Locate(battleIndex, out int f, out _, out _);
            return Tower[f].MonsterHpBonus;
        }

        public MageDef Mage(string id) => Mages.Find(m => m.Id == id);
        public MonsterDef Monster(string id) => Monsters.Find(m => m.Id == id);
        public BlessingDef Blessing(string id) => Blessings.Find(b => b.Id == id);
    }

    [Serializable]
    public sealed class SeatSetup
    {
        public bool IsBot;
        /// <summary>Порожньо = випадковий маг.</summary>
        public string MageId;
        public string PlayerName;
    }

    [Serializable]
    public sealed class GameSetup
    {
        public List<SeatSetup> Seats = new List<SeatSetup>();
        /// <summary>Порожньо = випадковий бос.</summary>
        public string BossId;
        public bool ElementalOverheat = true;
        public bool FreeStartingPlacement = true;
        public bool BlessingsEnabled = true;
        public int Seed;

        public static GameSetup Default(int humans = 1, int seed = 0)
        {
            var s = new GameSetup { Seed = seed == 0 ? Environment.TickCount : seed };
            for (int i = 0; i < Rules.MageSeats; i++)
                s.Seats.Add(new SeatSetup { IsBot = i >= humans, PlayerName = i < humans ? $"Гравець {i + 1}" : $"Бот {i + 1}" });
            return s;
        }
    }
}
