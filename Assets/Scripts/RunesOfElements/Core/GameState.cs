using System;
using System.Collections.Generic;
using System.Linq;

namespace RunesOfElements.Core
{
    [Serializable]
    public sealed class StatusInstance
    {
        public StatusType Type;
        public int Stacks = 1;
        /// <summary>-1 = поки не знято, -2 = до кінця битви.</summary>
        public int RemainingRounds = 1;
        public string Source;
        /// <summary>Додатковий параметр: стихія для «Мовчання», 1 = «поглинає всю атаку» для щита Гідроманта.</summary>
        public int Param;

        public string Label => Stacks > 1 || Type == StatusType.Burn || Type == StatusType.Poison || Type == StatusType.StoneSkin || (Type == StatusType.Shield && Param == 0)
            ? $"{Rules.StatusName(Type)} {Stacks}"
            : Type == StatusType.Silence ? $"{Rules.StatusName(Type)} ({Rules.RuneName((RuneType)Param)})" : Rules.StatusName(Type);
    }

    public abstract class UnitState
    {
        public int Hp;
        public int MaxHp;
        public readonly List<StatusInstance> Statuses = new List<StatusInstance>();
        public readonly List<AbilityBase> Abilities = new List<AbilityBase>();
        public abstract UnitRef Ref { get; }
        public abstract string DisplayName { get; }
        public bool IsAlive => Hp > 0;

        public StatusInstance GetStatus(StatusType t) => Statuses.FirstOrDefault(s => s.Type == t);
        public bool Has(StatusType t) => GetStatus(t) != null;
        public int Stacks(StatusType t) { var s = GetStatus(t); return s?.Stacks ?? 0; }
        public T Ability<T>() where T : AbilityBase => Abilities.OfType<T>().FirstOrDefault();
    }

    public sealed class MageState : UnitState
    {
        public int Seat;
        public MageDef Def;
        public SeatSetup Player;
        public int Concentration;
        public bool MeditatedLastTurn;
        public bool Eliminated;            // вибув у поточній битві
        public RuneType?[] ElementSlots = new RuneType?[2];
        public RuneType?[] FormSlots = new RuneType?[2];
        public readonly List<RuneType> Hand = new List<RuneType>();
        public BlessingDef Blessing;
        public int BlessingUsesBattle;
        public int BlessingUsesCampaign;
        public RuneType? BlessingChosenElement;  // для «Стихійного зв'язку»

        // поточний хід
        public int SwapsThisTurn;
        public int MeditationsThisBattle;
        public int SpellsThisRound;
        public bool ActedThisTurn;

        public override UnitRef Ref => UnitRef.Mage(Seat);
        public override string DisplayName => Def != null ? Def.Name : $"Маг {Seat + 1}";
        public bool IsBot => Player != null && Player.IsBot;

        public RuneType? GetSlot(RuneCategory cat, int index) => cat == RuneCategory.Element ? ElementSlots[index] : FormSlots[index];
        public void SetSlot(RuneCategory cat, int index, RuneType? rune)
        {
            if (cat == RuneCategory.Element) ElementSlots[index] = rune; else FormSlots[index] = rune;
        }

        public IEnumerable<RuneType> AllRunes()
        {
            foreach (var r in Hand) yield return r;
            foreach (var r in ElementSlots) if (r.HasValue) yield return r.Value;
            foreach (var r in FormSlots) if (r.HasValue) yield return r.Value;
        }
    }

    public sealed class MonsterState : UnitState
    {
        public MonsterDef Def;
        public int Position;   // 0..7
        public int Attack;
        public int DamageEventsTaken;
        public int HitsThisRound;

        public override UnitRef Ref => UnitRef.Monster(Position);
        public override string DisplayName => Def != null ? Def.Name : "Монстр";
        public int Row => Position / Rules.RowSize;
        public int Column => Position % Rules.RowSize;
    }

    /// <summary>Очікує рішення гравця щодо кидка шкоди («Щастя сортувальника» / «Дзеркало фортуни»).</summary>
    public sealed class PendingSpell
    {
        public int Seat;
        public RuneType Element;
        public RuneType Form;
        public int TargetPos;
        public List<int> Targets = new List<int>();
        public int Cost;
        public int Roll;
        public bool CanReroll;
        public bool CanMirror;
    }

    public sealed class GameState
    {
        public GameSetup Setup;
        public GameContent Content;
        public BattlePhase Phase = BattlePhase.NotStarted;
        public TurnStep Step = TurnStep.GainConcentration;
        public int BattleIndex;        // 0..4
        public int Round;
        public int ActiveSeat = -1;
        public readonly MageState[] Mages = new MageState[Rules.MageSeats];
        public readonly MonsterState[] Board = new MonsterState[Rules.BoardPositions];
        public readonly List<RuneType> RunePool = new List<RuneType>();
        public readonly Dictionary<MonsterLevel, List<MonsterDef>> Decks = new Dictionary<MonsterLevel, List<MonsterDef>>();
        public MonsterDef ChosenBoss;
        public readonly int[] ElementSurcharge = new int[4]; // Архімаг Хаосу
        public PendingSpell Pending;
        public bool BlessingDiscardOffered;
        public readonly List<AbilityBase> GlobalAbilities = new List<AbilityBase>();

        public IEnumerable<MageState> LivingMages => Mages.Where(m => m != null && m.IsAlive && !m.Eliminated);
        public IEnumerable<MageState> AllMages => Mages.Where(m => m != null);
        public IEnumerable<MonsterState> LivingMonsters => Board.Where(m => m != null && m.IsAlive);
        public MageState ActiveMage => ActiveSeat >= 0 && ActiveSeat < Mages.Length ? Mages[ActiveSeat] : null;
        public BattleSpec CurrentBattleSpec => Content.Campaign.Count > BattleIndex ? Content.Campaign[BattleIndex] : null;

        /// <summary>Поверх (0-based) поточної битви.</summary>
        public int Floor { get { Content.Locate(BattleIndex, out int f, out _, out _); return f; } }
        /// <summary>Хвиля (0-based) на поточному поверсі.</summary>
        public int Wave { get { Content.Locate(BattleIndex, out _, out int w, out _); return w; } }
        public int WavesInFloor { get { Content.Locate(BattleIndex, out _, out _, out int n); return n; } }
        public int FloorCount => Content.FloorCount;
        public bool IsLastWaveOfFloor => Wave >= WavesInFloor - 1;
        public bool IsLastBattle => BattleIndex >= Content.Campaign.Count - 1;
        /// <summary>Найвищий досягнутий поверх (1-based), для рекорду.</summary>
        public int HighestFloorReached;

        public MonsterState MonsterAt(int pos) => pos >= 0 && pos < Board.Length && Board[pos] != null && Board[pos].IsAlive ? Board[pos] : null;

        public UnitState Get(UnitRef r)
        {
            if (!r.IsValid) return null;
            return r.IsMage ? (UnitState)(r.Index >= 0 && r.Index < Mages.Length ? Mages[r.Index] : null)
                            : (r.Index >= 0 && r.Index < Board.Length ? Board[r.Index] : null);
        }

        public IEnumerable<UnitState> AllUnits()
        {
            foreach (var m in AllMages) yield return m;
            foreach (var m in Board) if (m != null) yield return m;
        }

        public int ElementIndex(RuneType element) => Array.IndexOf(Rules.Elements, element);
    }
}
