using System;

namespace RunesOfElements.Core
{
    public enum CommandKind : byte
    {
        None, SwapRune, CastSpell, ResolveRoll, Meditate, EndTurn, UseActive, ConfirmSetup, Continue, DiscardRune, DiscardBlessing
    }

    /// <summary>Плоске представлення команди для передачі мережею (без залежності від Netcode).</summary>
    [Serializable]
    public struct NetCommand
    {
        public CommandKind Kind;
        public int Seat;
        public int A, B, C, D;
        public string Str;

        /// <summary>Чи прив'язана команда до конкретного місця (тоді хост перевіряє власника).</summary>
        public bool IsSeatBound => Kind != CommandKind.ConfirmSetup && Kind != CommandKind.Continue && Kind != CommandKind.None;
    }

    public static class CommandCodec
    {
        public static NetCommand Encode(IGameCommand cmd)
        {
            switch (cmd)
            {
                case SwapRuneCommand c: return new NetCommand { Kind = CommandKind.SwapRune, Seat = c.Seat, A = c.HandIndex, B = (int)c.Category, C = c.SlotIndex };
                case CastSpellCommand c: return new NetCommand { Kind = CommandKind.CastSpell, Seat = c.Seat, A = c.ElementSlot, B = c.FormSlot, C = c.TargetPos };
                case ResolveRollCommand c: return new NetCommand { Kind = CommandKind.ResolveRoll, Seat = c.Seat, A = (int)c.Choice };
                case MeditateCommand c: return new NetCommand { Kind = CommandKind.Meditate, Seat = c.Seat };
                case EndTurnCommand c: return new NetCommand { Kind = CommandKind.EndTurn, Seat = c.Seat };
                case UseActiveAbilityCommand c: return new NetCommand { Kind = CommandKind.UseActive, Seat = c.Seat, A = c.TargetPos, Str = c.AbilityId };
                case ConfirmSetupCommand _: return new NetCommand { Kind = CommandKind.ConfirmSetup, Seat = -1 };
                case ContinueCampaignCommand _: return new NetCommand { Kind = CommandKind.Continue, Seat = -1 };
                case DiscardRuneCommand c: return new NetCommand { Kind = CommandKind.DiscardRune, Seat = c.Seat, A = c.HandIndex };
                case DiscardBlessingCommand c: return new NetCommand { Kind = CommandKind.DiscardBlessing, Seat = c.Seat };
            }
            return new NetCommand { Kind = CommandKind.None, Seat = -1 };
        }

        public static IGameCommand Decode(NetCommand n)
        {
            switch (n.Kind)
            {
                case CommandKind.SwapRune: return new SwapRuneCommand(n.Seat, n.A, (RuneCategory)n.B, n.C);
                case CommandKind.CastSpell: return new CastSpellCommand(n.Seat, n.A, n.B, n.C);
                case CommandKind.ResolveRoll: return new ResolveRollCommand(n.Seat, (RollChoice)n.A);
                case CommandKind.Meditate: return new MeditateCommand(n.Seat);
                case CommandKind.EndTurn: return new EndTurnCommand(n.Seat);
                case CommandKind.UseActive: return new UseActiveAbilityCommand(n.Seat, n.Str ?? "", n.A);
                case CommandKind.ConfirmSetup: return new ConfirmSetupCommand();
                case CommandKind.Continue: return new ContinueCampaignCommand();
                case CommandKind.DiscardRune: return new DiscardRuneCommand(n.Seat, n.A);
                case CommandKind.DiscardBlessing: return new DiscardBlessingCommand(n.Seat);
            }
            return null;
        }
    }
}
