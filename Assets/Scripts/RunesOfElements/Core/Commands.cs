using System;

namespace RunesOfElements.Core
{
    public readonly struct CommandResult
    {
        public bool Ok { get; }
        public string Reason { get; }
        private CommandResult(bool ok, string reason) { Ok = ok; Reason = reason; }
        public static CommandResult Success => new CommandResult(true, null);
        public static CommandResult Fail(string reason) => new CommandResult(false, reason);
    }

    /// <summary>
    /// Усі зміни стану йдуть через команди. У мережевій версії клієнт надсилає команду, сервер виконує Validate/Execute.
    /// </summary>
    public interface IGameCommand
    {
        /// <summary>null — команда валідна, інакше причина відмови українською.</summary>
        string Validate(RulesEngine e);
        void Execute(RulesEngine e);
    }

    [Serializable]
    public sealed class SwapRuneCommand : IGameCommand
    {
        public int Seat, HandIndex, SlotIndex;
        public RuneCategory Category;
        public SwapRuneCommand(int seat, int handIndex, RuneCategory category, int slotIndex) { Seat = seat; HandIndex = handIndex; Category = category; SlotIndex = slotIndex; }
        public string Validate(RulesEngine e) => e.ValidateSwap(Seat, HandIndex, Category, SlotIndex);
        public void Execute(RulesEngine e) => e.ExecuteSwap(Seat, HandIndex, Category, SlotIndex);
    }

    [Serializable]
    public sealed class CastSpellCommand : IGameCommand
    {
        public int Seat, ElementSlot, FormSlot, TargetPos;
        public CastSpellCommand(int seat, int elementSlot, int formSlot, int targetPos) { Seat = seat; ElementSlot = elementSlot; FormSlot = formSlot; TargetPos = targetPos; }
        public string Validate(RulesEngine e) => e.ValidateCast(Seat, ElementSlot, FormSlot, TargetPos);
        public void Execute(RulesEngine e) => e.ExecuteCast(Seat, ElementSlot, FormSlot, TargetPos);
    }

    [Serializable]
    public sealed class ResolveRollCommand : IGameCommand
    {
        public int Seat;
        public RollChoice Choice;
        public ResolveRollCommand(int seat, RollChoice choice) { Seat = seat; Choice = choice; }
        public string Validate(RulesEngine e) => e.ValidateRollChoice(Seat, Choice);
        public void Execute(RulesEngine e) => e.ExecuteRollChoice(Seat, Choice);
    }

    [Serializable]
    public sealed class MeditateCommand : IGameCommand
    {
        public int Seat;
        public MeditateCommand(int seat) { Seat = seat; }
        public string Validate(RulesEngine e) => e.ValidateMeditate(Seat);
        public void Execute(RulesEngine e) => e.ExecuteMeditate(Seat);
    }

    [Serializable]
    public sealed class EndTurnCommand : IGameCommand
    {
        public int Seat;
        public EndTurnCommand(int seat) { Seat = seat; }
        public string Validate(RulesEngine e) => e.ValidatePass(Seat);
        public void Execute(RulesEngine e) => e.ExecutePass(Seat);
    }

    [Serializable]
    public sealed class UseActiveAbilityCommand : IGameCommand
    {
        public int Seat, TargetPos;
        public string AbilityId;
        public UseActiveAbilityCommand(int seat, string abilityId, int targetPos) { Seat = seat; AbilityId = abilityId; TargetPos = targetPos; }
        public string Validate(RulesEngine e) => e.ValidateActive(Seat, AbilityId, TargetPos);
        public void Execute(RulesEngine e) => e.ExecuteActive(Seat, AbilityId, TargetPos);
    }

    [Serializable]
    public sealed class ConfirmSetupCommand : IGameCommand
    {
        public string Validate(RulesEngine e) => e.ValidateConfirmSetup();
        public void Execute(RulesEngine e) => e.ExecuteConfirmSetup();
    }

    [Serializable]
    public sealed class ContinueCampaignCommand : IGameCommand
    {
        public string Validate(RulesEngine e) => e.ValidateContinue();
        public void Execute(RulesEngine e) => e.ExecuteContinue();
    }

    [Serializable]
    public sealed class DiscardRuneCommand : IGameCommand
    {
        public int Seat, HandIndex;
        public DiscardRuneCommand(int seat, int handIndex) { Seat = seat; HandIndex = handIndex; }
        public string Validate(RulesEngine e) => e.ValidateDiscardRune(Seat, HandIndex);
        public void Execute(RulesEngine e) => e.ExecuteDiscardRune(Seat, HandIndex);
    }

    [Serializable]
    public sealed class DiscardBlessingCommand : IGameCommand
    {
        public int Seat;
        public DiscardBlessingCommand(int seat) { Seat = seat; }
        public string Validate(RulesEngine e) => e.ValidateDiscardBlessing(Seat);
        public void Execute(RulesEngine e) => e.ExecuteDiscardBlessing(Seat);
    }
}
