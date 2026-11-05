using System;
using System.Collections.Generic;

namespace RunesOfElements.Core
{
    public enum GameEventType
    {
        Info,
        PhaseChanged,
        TurnStarted,
        DiceRolled,
        ConcentrationChanged,
        RuneSwapped,
        SpellCast,
        DamageDealt,
        Healed,
        ShieldAbsorbed,
        StatusApplied,
        StatusRemoved,
        MonsterSpawned,
        MonsterDied,
        MageDied,
        MageRevived,
        MonsterAttack,
        MonsterSkipped,
        Meditated,
        RewardGranted,
        BlessingTriggered,
        BattleStarted,
        BattleWon,
        Defeat,
        CampaignWon
    }

    /// <summary>Посилання на учасника: маг (seat 0..3) або монстр (позиція 0..7).</summary>
    [Serializable]
    public struct UnitRef
    {
        public bool IsMage;
        public int Index;
        public bool IsValid;
        public static UnitRef None => new UnitRef { IsValid = false, Index = -1 };
        public static UnitRef Mage(int seat) => new UnitRef { IsMage = true, Index = seat, IsValid = true };
        public static UnitRef Monster(int pos) => new UnitRef { IsMage = false, Index = pos, IsValid = true };
    }

    [Serializable]
    public sealed class GameEvent
    {
        public GameEventType Type;
        public UnitRef Source = UnitRef.None;
        public UnitRef Target = UnitRef.None;
        public int Value;
        public DieType Die;
        public RuneType Element;
        public RuneType Form;
        public StatusType Status;
        public BattlePhase Phase;
        public string Text;

        public GameEvent(GameEventType type) { Type = type; }
        public override string ToString() => Text ?? Type.ToString();
    }

    public sealed class EventLog
    {
        private readonly List<GameEvent> _events = new List<GameEvent>();
        public IReadOnlyList<GameEvent> Events => _events;
        public int Count => _events.Count;
        public event Action<GameEvent> Added;

        public void Add(GameEvent e)
        {
            _events.Add(e);
            Added?.Invoke(e);
        }

        public void Info(string text) => Add(new GameEvent(GameEventType.Info) { Text = text });
    }
}
