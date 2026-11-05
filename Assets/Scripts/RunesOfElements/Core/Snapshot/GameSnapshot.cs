using System;
using System.Collections.Generic;
using System.Linq;
using RunesOfElements.Core.Abilities;
using UnityEngine;

namespace RunesOfElements.Core
{
    [Serializable]
    public sealed class StatusSnap
    {
        public StatusType Type;
        public int Stacks;
        public int Rounds;
        public string Label;
        public string Source;
    }

    [Serializable]
    public sealed class MageSnap
    {
        public bool Exists;
        public int Seat;
        public string MageId;
        public string Name;
        public RuneType Element;
        public string PassiveName;
        public string PassiveText;
        public string PlayerName;
        public bool IsBot;
        public int Hp, MaxHp, Concentration;
        public bool Eliminated;
        public bool MeditatedLastTurn;
        /// <summary>-1 = порожньо, інакше (int)RuneType.</summary>
        public int[] ElementSlots = { -1, -1 };
        public int[] FormSlots = { -1, -1 };
        public List<int> Hand = new List<int>();
        public int HandLimit, SwapsThisTurn, SwapLimit, SwapCost;
        public string BlessingId, BlessingName, BlessingText;
        public BlessingKind BlessingKind;
        public int BlessingUsesBattle, BlessingUsesCampaign, BlessingUsesPerBattle, BlessingUsesPerCampaign;
        public int BondElement = -1;
        public bool HasCryo;
        public bool CryoAvailable;
        /// <summary>Вартість комбінацій [es*2+fs]; -1 = немає рун.</summary>
        public int[] SpellCosts = { -1, -1, -1, -1 };
        public List<StatusSnap> Statuses = new List<StatusSnap>();

        public RuneType? Slot(RuneCategory cat, int i)
        {
            int v = cat == RuneCategory.Element ? ElementSlots[i] : FormSlots[i];
            return v < 0 ? (RuneType?)null : (RuneType)v;
        }
        public bool IsAlive => Exists && !Eliminated && Hp > 0;
    }

    [Serializable]
    public sealed class MonsterSnap
    {
        public bool Exists;
        public int Position;
        public string MonsterId;
        public string Name;
        public MonsterLevel Level;
        public int Hp, MaxHp, Attack;
        public string AbilityName, AbilityText;
        public List<StatusSnap> Statuses = new List<StatusSnap>();
    }

    [Serializable]
    public sealed class EventSnap
    {
        public int Seq;
        public GameEventType Type;
        public string Text;
        public int Value;
        public DieType Die;
        public RuneType Element;
        public bool TargetValid, TargetIsMage;
        public int TargetIndex;
        public bool SourceValid, SourceIsMage;
        public int SourceIndex;
    }

    /// <summary>
    /// Повний видимий стан гри. Його будує той, хто тримає RulesEngine (локально або хост),
    /// а UI читає лише знімок, тож однаково працює і в соло, і в онлайні.
    /// </summary>
    [Serializable]
    public sealed class GameSnapshot
    {
        public const int EventWindow = 60;

        public int Version;
        public int BattleIndex, Floor, FloorCount, Wave, WavesInFloor, Round, HighestFloor;
        public string FloorName;
        public BattlePhase Phase;
        public TurnStep Step;
        public int ActiveSeat;
        public string BossName;
        public int RunePoolCount, DeckL1, DeckL2, DeckL3;
        /// <summary>Кількість рун у спільному запасі за типом, індекс = (int)RuneType.</summary>
        public int[] RunePoolByType = new int[6];
        public int[] ElementSurcharge= new int[4];
        public bool BlessingDiscardOffered;
        public bool Overheat;
        public MageSnap[] Mages = new MageSnap[Rules.MageSeats];
        public MonsterSnap[] Monsters = new MonsterSnap[Rules.BoardPositions];

        public bool HasPending;
        public int PendingSeat, PendingRoll;
        public bool PendingCanReroll, PendingCanMirror;
        public RuneType PendingElement, PendingForm;

        public List<EventSnap> Events = new List<EventSnap>();
        public int TotalEvents;

        public MageSnap ActiveMage => ActiveSeat >= 0 && ActiveSeat < Mages.Length ? Mages[ActiveSeat] : null;
        public bool IsBetweenBattles => Phase == BattlePhase.WaveWon || Phase == BattlePhase.BattleWon;
        public bool IsOver => Phase == BattlePhase.Defeat || Phase == BattlePhase.CampaignWon;

        public MonsterSnap MonsterAt(int pos) => pos >= 0 && pos < Monsters.Length && Monsters[pos] != null && Monsters[pos].Exists ? Monsters[pos] : null;

        /// <summary>Та сама геометрія, що й RulesEngine.GetSpellTargets, але для клієнта без рушія.</summary>
        public List<int> PreviewTargets(RuneType form, int primaryPos)
        {
            var result = new List<int>();
            if (MonsterAt(primaryPos) == null) return result;
            result.Add(primaryPos);
            if (form == RuneType.Beam)
            {
                int other = (primaryPos + Rules.RowSize) % Rules.BoardPositions;
                if (MonsterAt(other) != null) result.Add(other);
            }
            else if (form == RuneType.Sphere)
            {
                int col = primaryPos % Rules.RowSize;
                if (col > 0 && MonsterAt(primaryPos - 1) != null) result.Add(primaryPos - 1);
                if (col < Rules.RowSize - 1 && MonsterAt(primaryPos + 1) != null) result.Add(primaryPos + 1);
            }
            return result;
        }

        public string ToJson() => JsonUtility.ToJson(this);
        public static GameSnapshot FromJson(string json) => JsonUtility.FromJson<GameSnapshot>(json);

        public static GameSnapshot From(RulesEngine e, int version = 0)
        {
            var s = e.State;
            var snap = new GameSnapshot
            {
                Version = version,
                BattleIndex = s.BattleIndex,
                Floor = s.Floor,
                FloorCount = s.FloorCount,
                Wave = s.Wave,
                WavesInFloor = s.WavesInFloor,
                Round = s.Round,
                HighestFloor = s.HighestFloorReached,
                FloorName = s.Content.Tower.Count > s.Floor ? s.Content.Tower[s.Floor].Name : $"Поверх {s.Floor + 1}",
                Phase = s.Phase,
                Step = s.Step,
                ActiveSeat = s.ActiveSeat,
                BossName = s.ChosenBoss?.Name,
                RunePoolCount = s.RunePool.Count,
                DeckL1 = DeckCount(s, MonsterLevel.Level1),
                DeckL2 = DeckCount(s, MonsterLevel.Level2),
                DeckL3 = DeckCount(s, MonsterLevel.Level3),
                ElementSurcharge = (int[])s.ElementSurcharge.Clone(),
                BlessingDiscardOffered = s.BlessingDiscardOffered,
                Overheat = s.Setup.ElementalOverheat,
                TotalEvents = e.Log.Count
            };

            foreach (var r in s.RunePool)
            {
                int idx = (int)r;
                if (idx >= 0 && idx < snap.RunePoolByType.Length) snap.RunePoolByType[idx]++;
            }

            for (int i = 0; i < Rules.MageSeats; i++) snap.Mages[i] = SnapMage(e, s.Mages[i], i);
            for (int i = 0; i < Rules.BoardPositions; i++) snap.Monsters[i] = SnapMonster(s.Board[i], i);

            if (s.Pending != null)
            {
                snap.HasPending = true;
                snap.PendingSeat = s.Pending.Seat;
                snap.PendingRoll = s.Pending.Roll;
                snap.PendingCanReroll = s.Pending.CanReroll;
                snap.PendingCanMirror = s.Pending.CanMirror;
                snap.PendingElement = s.Pending.Element;
                snap.PendingForm = s.Pending.Form;
            }

            int start = Math.Max(0, e.Log.Count - EventWindow);
            for (int i = start; i < e.Log.Count; i++)
            {
                var ev = e.Log.Events[i];
                snap.Events.Add(new EventSnap
                {
                    Seq = i, Type = ev.Type, Text = ev.Text ?? ev.Type.ToString(), Value = ev.Value, Die = ev.Die, Element = ev.Element,
                    TargetValid = ev.Target.IsValid, TargetIsMage = ev.Target.IsMage, TargetIndex = ev.Target.Index,
                    SourceValid = ev.Source.IsValid, SourceIsMage = ev.Source.IsMage, SourceIndex = ev.Source.Index
                });
            }
            return snap;
        }

        private static int DeckCount(GameState s, MonsterLevel l) => s.Decks.TryGetValue(l, out var d) ? d.Count : 0;

        private static List<StatusSnap> SnapStatuses(UnitState u) =>
            u.Statuses.Select(st => new StatusSnap { Type = st.Type, Stacks = st.Stacks, Rounds = st.RemainingRounds, Label = st.Label, Source = st.Source }).ToList();

        private static MageSnap SnapMage(RulesEngine e, MageState m, int seat)
        {
            if (m == null) return new MageSnap { Exists = false, Seat = seat };
            var snap = new MageSnap
            {
                Exists = true,
                Seat = seat,
                MageId = m.Def.Id,
                Name = m.DisplayName,
                Element = m.Def.Element,
                PassiveName = m.Def.PassiveName,
                PassiveText = m.Def.PassiveText,
                PlayerName = m.Player?.PlayerName,
                IsBot = m.IsBot,
                Hp = m.Hp,
                MaxHp = m.MaxHp,
                Concentration = m.Concentration,
                Eliminated = m.Eliminated,
                MeditatedLastTurn = m.MeditatedLastTurn,
                ElementSlots = new[] { m.ElementSlots[0].HasValue ? (int)m.ElementSlots[0].Value : -1, m.ElementSlots[1].HasValue ? (int)m.ElementSlots[1].Value : -1 },
                FormSlots = new[] { m.FormSlots[0].HasValue ? (int)m.FormSlots[0].Value : -1, m.FormSlots[1].HasValue ? (int)m.FormSlots[1].Value : -1 },
                Hand = m.Hand.Select(r => (int)r).ToList(),
                HandLimit = e.GetHandLimit(m),
                SwapsThisTurn = m.SwapsThisTurn,
                SwapLimit = e.GetSwapLimit(m),
                SwapCost = e.GetSwapCost(m),
                BlessingUsesBattle = m.BlessingUsesBattle,
                BlessingUsesCampaign = m.BlessingUsesCampaign,
                BondElement = m.BlessingChosenElement.HasValue ? (int)m.BlessingChosenElement.Value : -1,
                Statuses = SnapStatuses(m)
            };
            if (m.Blessing != null)
            {
                snap.BlessingId = m.Blessing.Id;
                snap.BlessingName = m.Blessing.Name;
                snap.BlessingText = m.Blessing.Text;
                snap.BlessingKind = m.Blessing.Kind;
                snap.BlessingUsesPerBattle = m.Blessing.UsesPerBattle;
                snap.BlessingUsesPerCampaign = m.Blessing.UsesPerCampaign;
            }
            var cryo = m.Ability<CryomancerIcePrison>();
            snap.HasCryo = cryo != null;
            snap.CryoAvailable = cryo != null && !cryo.Used;
            for (int es = 0; es < 2; es++)
            for (int fs = 0; fs < 2; fs++)
            {
                var el = m.ElementSlots[es];
                var f = m.FormSlots[fs];
                snap.SpellCosts[es * 2 + fs] = el.HasValue && f.HasValue ? e.GetSpellCost(m, el.Value, f.Value) : -1;
            }
            return snap;
        }

        private static MonsterSnap SnapMonster(MonsterState m, int pos)
        {
            if (m == null || !m.IsAlive) return new MonsterSnap { Exists = false, Position = pos };
            return new MonsterSnap
            {
                Exists = true,
                Position = pos,
                MonsterId = m.Def.Id,
                Name = m.DisplayName,
                Level = m.Def.Level,
                Hp = m.Hp,
                MaxHp = m.MaxHp,
                Attack = m.Attack,
                AbilityName = m.Def.AbilityName,
                AbilityText = m.Def.AbilityText,
                Statuses = SnapStatuses(m)
            };
        }
    }
}
