using System;
using System.Collections.Generic;

namespace RunesOfElements.Core
{
    public enum RuneType { Fire, Water, Ice, Lightning, Beam, Sphere }
    public enum RuneCategory { Element, Form }
    public enum MonsterLevel { Level1 = 1, Level2 = 2, Level3 = 3, Boss = 4 }
    public enum BlessingKind { Blessing, Curse, Risk }
    public enum BlessingTrigger { Passive, Active, Auto }

    public enum BattlePhase
    {
        NotStarted,
        Setup,          // вільна стартова викладка рун перед першою битвою
        RoundStart,
        MagesTurn,
        MonstersTurn,
        Recovery,
        WaveWon,        // хвилю виграно, поверх ще триває
        BattleWon,      // поверх пройдено (повна нагорода)
        Defeat,
        CampaignWon
    }

    public enum TurnStep { GainConcentration, SwapRune, RollDecision, Done }

    public enum StatusType
    {
        Frozen, Stunned, Burn, Poison, Weakened, Shattered, Shield, Inspired, Silence, StoneSkin
    }

    public enum DieType { D4 = 4, D6 = 6, D8 = 8 }

    public enum RollChoice { Accept, Reroll, Mirror }

    public static class Rules
    {
        public const int MaxHp = 10;
        public const int StartConcentration = 4;
        public const int MaxConcentration = 5;
        public const int HandLimit = 6;
        public const int StartingRunes = 3;
        public const int RoundRecovery = 5;
        public const int BattleRewardHp = 3;
        public const int ReviveHp = 3;
        public const int MageSeats = 4;
        public const int BoardPositions = 8;
        public const int RowSize = 4;
        public const int CampaignBattles = 5;
        public const int WavesPerFloor = 3;
        public const int WaveRewardHp = 3;
        public const int BlessingDiscardAfterFloor = 3;

        public static RuneCategory CategoryOf(RuneType r) => r == RuneType.Beam || r == RuneType.Sphere ? RuneCategory.Form : RuneCategory.Element;
        public static bool IsElement(RuneType r) => CategoryOf(r) == RuneCategory.Element;

        public static int BaseCost(RuneType r)
        {
            switch (r)
            {
                case RuneType.Fire: return 2;
                case RuneType.Beam: return 1;
                case RuneType.Sphere: return 3;
                default: return 1;
            }
        }

        public static int FlatDamageBonus(RuneType element) => element == RuneType.Fire ? 2 : 0;

        public static int ClampConcentration(int value) => Math.Max(0, Math.Min(MaxConcentration, value));

        public static string RuneName(RuneType r)
        {
            switch (r)
            {
                case RuneType.Fire: return "Вогонь";
                case RuneType.Water: return "Вода";
                case RuneType.Ice: return "Лід";
                case RuneType.Lightning: return "Блискавка";
                case RuneType.Beam: return "Промінь";
                case RuneType.Sphere: return "Сфера";
            }
            return r.ToString();
        }

        public static string StatusName(StatusType s)
        {
            switch (s)
            {
                case StatusType.Frozen: return "Заморожений";
                case StatusType.Stunned: return "Приголомшений";
                case StatusType.Burn: return "Опік";
                case StatusType.Poison: return "Отрута";
                case StatusType.Weakened: return "Ослаблений";
                case StatusType.Shattered: return "Розколотий";
                case StatusType.Shield: return "Щит";
                case StatusType.Inspired: return "Натхнений";
                case StatusType.Silence: return "Мовчання";
                case StatusType.StoneSkin: return "Кам'яна шкіра";
            }
            return s.ToString();
        }

        public static bool IsDebuff(StatusType s) =>
            s == StatusType.Frozen || s == StatusType.Stunned || s == StatusType.Burn || s == StatusType.Poison ||
            s == StatusType.Weakened || s == StatusType.Shattered || s == StatusType.Silence;

        public static readonly RuneType[] Elements = { RuneType.Fire, RuneType.Water, RuneType.Ice, RuneType.Lightning };
    }

    /// <summary>Єдине джерело випадковості. У мережевій версії живе лише на сервері.</summary>
    public interface IRandomSource
    {
        /// <summary>Повертає число в [minInclusive, maxExclusive).</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random _random;
        public int Seed { get; }
        public SeededRandom(int seed) { Seed = seed; _random = new Random(seed); }
        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>Детерміноване джерело для тестів: віддає числа з черги, далі — з fallback.</summary>
    public sealed class ScriptedRandom : IRandomSource
    {
        private readonly Queue<int> _values = new Queue<int>();
        private readonly IRandomSource _fallback;
        public ScriptedRandom(IRandomSource fallback = null) { _fallback = fallback ?? new SeededRandom(1); }
        public void Push(params int[] values) { foreach (var v in values) _values.Enqueue(v); }
        public int Next(int minInclusive, int maxExclusive)
        {
            if (_values.Count > 0)
            {
                int v = _values.Dequeue();
                if (v >= minInclusive && v < maxExclusive) return v;
            }
            return _fallback.Next(minInclusive, maxExclusive);
        }
    }

    [Serializable]
    public struct DiceRoll
    {
        public DieType Die;
        public int Value;
        public string Reason;
        public override string ToString() => $"d{(int)Die}={Value}";
    }

    public sealed class DiceService
    {
        private readonly IRandomSource _rng;
        private readonly EventLog _log;
        public DiceService(IRandomSource rng, EventLog log) { _rng = rng; _log = log; }

        public DiceRoll Roll(DieType die, string reason, bool logEvent = true)
        {
            var roll = new DiceRoll { Die = die, Value = _rng.Next(1, (int)die + 1), Reason = reason };
            if (logEvent) _log.Add(new GameEvent(GameEventType.DiceRolled) { Value = roll.Value, Die = die, Text = $"{reason}: d{(int)die} = {roll.Value}" });
            return roll;
        }

        public int Range(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        public T Pick<T>(IList<T> list) => list.Count == 0 ? default : list[_rng.Next(0, list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
