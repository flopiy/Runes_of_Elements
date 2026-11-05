using System;

namespace RunesOfElements.Core.Abilities
{
    public abstract class BlessingAbility : AbilityBase
    {
        protected void Announce(RulesEngine e, string text) =>
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Target = Owner.Ref, Text = text });
        protected bool IsOwnSpell(DamageContext ctx) => ctx.Attacker == Owner && ctx.IsSpell && !ctx.Direct;
    }

    // ============================ Благословення ============================

    [AbilityId("blessing_power_flow")]
    public sealed class PowerFlow : BlessingAbility
    {
        public override int ModifyStartingConcentration(MageState mage, int value) => mage == Owner ? 5 : value;
    }

    [AbilityId("blessing_double_swap")]
    public sealed class DoubleSwap : BlessingAbility
    {
        public override int ModifySwapLimit(MageState mage, int limit) => mage == Owner ? Math.Max(limit, 2) : limit;
    }

    [AbilityId("blessing_tempered_will")]
    public sealed class TemperedWill : BlessingAbility
    {
        private bool _used;
        public override void OnBattleStart(RulesEngine e) => _used = false;
        public override bool BlocksStatus(RulesEngine e, UnitState target, StatusType type)
        {
            if (target != Owner || _used || !Rules.IsDebuff(type)) return false;
            _used = true;
            Announce(e, $"«Загартована воля» захищає {Owner.DisplayName} від стану «{Rules.StatusName(type)}»");
            return true;
        }
    }

    [AbilityId("blessing_thirst_knowledge")]
    public sealed class ThirstForKnowledge : BlessingAbility
    {
        public override int ModifyMeditationBonus(RulesEngine e, MageState mage, int bonus) => mage == Owner ? bonus + 1 : bonus;
    }

    [AbilityId("blessing_resonance")]
    public sealed class MagicResonance : BlessingAbility
    {
        private int _lastRound = -1;
        public override void AfterSpellCast(RulesEngine e, MageState caster, SpellResult result)
        {
            if (caster != Owner || result.Targets.Count < 2 || _lastRound == e.State.Round) return;
            _lastRound = e.State.Round;
            Announce(e, "«Магічний резонанс»: +1 концентрація");
            e.ChangeConcentration(OwnerMage, 1, "Магічний резонанс");
        }
    }

    [AbilityId("blessing_titan_blood")]
    public sealed class TitanBlood : BlessingAbility
    {
        public override int ModifyMaxHp(MageState mage, int value) => mage == Owner ? 12 : value;
        public override int ModifyBattleStartHp(MageState mage, int hp) => mage == Owner ? mage.MaxHp : hp;
    }

    [AbilityId("blessing_sorter_luck")]
    public sealed class SorterLuck : BlessingAbility, IRollModifier
    {
        public bool CanReroll(RulesEngine e, MageState mage) => mage == Owner && mage.BlessingUsesBattle == 0;
        public bool CanMirror(RulesEngine e, MageState mage) => false;
        public void ConsumeReroll(RulesEngine e, MageState mage) => mage.BlessingUsesBattle++;
        public void ConsumeMirror(RulesEngine e, MageState mage) { }
    }

    [AbilityId("blessing_mage_hand")]
    public sealed class MageHand : BlessingAbility
    {
        public override int ModifyHandLimit(MageState mage, int limit) => mage == Owner ? 7 : limit;
    }

    [AbilityId("blessing_elemental_bond")]
    public sealed class ElementalBond : BlessingAbility
    {
        public override int ModifySpellCost(RulesEngine e, MageState caster, RuneType element, RuneType form, int cost) =>
            caster == Owner && OwnerMage.BlessingChosenElement == element ? cost - 1 : cost;
    }

    [AbilityId("blessing_hope_radiance")]
    public sealed class HopeRadiance : BlessingAbility
    {
        public override bool OnLethal(RulesEngine e, UnitState unit)
        {
            if (unit != Owner || OwnerMage.BlessingUsesCampaign > 0) return false;
            OwnerMage.BlessingUsesCampaign++;
            Announce(e, $"«Сяйво надії» рятує {Owner.DisplayName}!");
            return true;
        }
    }

    // ============================ Прокляття ============================

    [AbilityId("curse_magic_fatigue")]
    public sealed class MagicFatigue : BlessingAbility
    {
        public override int ModifyMeditationBonus(RulesEngine e, MageState mage, int bonus) =>
            mage == Owner && mage.MeditationsThisBattle == 1 ? bonus - 1 : bonus;
    }

    [AbilityId("curse_resonance_crack")]
    public sealed class ResonanceCrack : BlessingAbility
    {
        public override void ModifyOutgoing(RulesEngine e, DamageContext ctx)
        {
            if (IsOwnSpell(ctx) && OwnerMage.SpellsThisRound == 1) ctx.OutgoingMods.Add(new DamageMod(-2, "Тріщина в резонансі"));
        }
    }

    [AbilityId("curse_vain_thirst")]
    public sealed class VainThirst : BlessingAbility
    {
        public override int ModifyHeal(RulesEngine e, MageState mage, int amount) => mage == Owner ? Math.Max(0, amount - 2) : amount;
    }

    [AbilityId("curse_heavy_hand")]
    public sealed class HeavyHand : BlessingAbility
    {
        public override int ModifySwapCost(MageState mage, int cost) => mage == Owner ? cost + 1 : cost;
    }

    [AbilityId("curse_rift_whisper")]
    public sealed class RiftWhisper : BlessingAbility
    {
        private bool _done;
        public override void OnBattleSetup(RulesEngine e)
        {
            if (_done || e.State.Floor != 2 || e.State.Wave != 0) return;
            _done = true;
            Announce(e, "«Шепіт Розлому» кличе додаткового монстра");
            e.SummonMonster(MonsterLevel.Level1, "Шепіт Розлому");
        }
    }

    // ============================ Ризикові ============================

    [AbilityId("risk_chaos_roulette")]
    public sealed class ChaosRoulette : BlessingAbility
    {
        public override void OnRoundStart(RulesEngine e)
        {
            var roll = e.Dice.Roll(DieType.D6, $"«Рулетка хаосу» ({Owner.DisplayName})");
            if (roll.Value <= 2) e.ChangeConcentration(OwnerMage, -1, "Рулетка хаосу");
            else if (roll.Value >= 5) e.ChangeConcentration(OwnerMage, 1, "Рулетка хаосу");
        }
    }

    [AbilityId("risk_double_price")]
    public sealed class DoublePrice : BlessingAbility
    {
        public override int ModifySpellCost(RulesEngine e, MageState caster, RuneType element, RuneType form, int cost) => caster == Owner ? cost + 1 : cost;
        public override void ModifyOutgoing(RulesEngine e, DamageContext ctx)
        {
            if (IsOwnSpell(ctx)) ctx.OutgoingMods.Add(new DamageMod(1, "Подвійна ціна"));
        }
    }

    [AbilityId("risk_silence_pact")]
    public sealed class SilencePact : BlessingAbility
    {
        public override int ModifySwapLimit(MageState mage, int limit) => mage == Owner ? 0 : limit;
        public override int ModifyConcentrationGain(RulesEngine e, MageState mage, int gain) => mage == Owner ? gain + 1 : gain;
    }

    [AbilityId("risk_fate_exchange")]
    public sealed class FateExchange : BlessingAbility
    {
        public override int ModifyBattleStartHp(MageState mage, int hp) => mage == Owner ? 7 : hp;
        public override void ModifyOutgoing(RulesEngine e, DamageContext ctx)
        {
            if (IsOwnSpell(ctx)) ctx.OutgoingMods.Add(new DamageMod(2, "Обмін долею"));
        }
    }

    [AbilityId("risk_fortune_mirror")]
    public sealed class FortuneMirror : BlessingAbility, IRollModifier
    {
        public bool CanReroll(RulesEngine e, MageState mage) => false;
        public bool CanMirror(RulesEngine e, MageState mage) => mage == Owner && mage.BlessingUsesCampaign == 0;
        public void ConsumeReroll(RulesEngine e, MageState mage) { }
        public void ConsumeMirror(RulesEngine e, MageState mage) => mage.BlessingUsesCampaign++;
    }
}
