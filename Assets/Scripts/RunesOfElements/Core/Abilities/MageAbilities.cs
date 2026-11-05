using System.Linq;

namespace RunesOfElements.Core.Abilities
{
    /// <summary>Піромант — «Палаюча сила»: вогняні заклинання коштують на 1 концентрацію менше.</summary>
    [AbilityId("mage_pyro")]
    public sealed class PyromancerBurningPower : AbilityBase
    {
        public override int ModifySpellCost(RulesEngine e, MageState caster, RuneType element, RuneType form, int cost) =>
            caster == Owner && element == RuneType.Fire ? cost - 1 : cost;
    }

    /// <summary>Гідромант — «Водний щит»: після водного заклинання отримує щит, що поглинає наступну атаку монстра.</summary>
    [AbilityId("mage_hydro")]
    public sealed class HydromancerWaterShield : AbilityBase
    {
        public override void AfterSpellCast(RulesEngine e, MageState caster, SpellResult result)
        {
            if (caster != Owner || result.Element != RuneType.Water) return;
            e.ApplyStatus(Owner, StatusType.Shield, 1, -1, "Водний щит", 1);
        }
    }

    /// <summary>Електромант — «Ланцюг блискавки»: на «6» 3 шкоди перекидаються на іншого монстра.</summary>
    [AbilityId("mage_electro")]
    public sealed class ElectromancerChainLightning : AbilityBase
    {
        public const int ChainDamage = 3;

        public override void AfterSpellCast(RulesEngine e, MageState caster, SpellResult result)
        {
            if (caster != Owner || result.Element != RuneType.Lightning || result.Roll != 6) return;
            var candidates = e.State.LivingMonsters.Where(m => !result.Targets.Contains(m.Position)).ToList();
            if (candidates.Count == 0) candidates = e.State.LivingMonsters.Where(m => m.Position != result.PrimaryPos).ToList();
            if (candidates.Count == 0) return;
            var target = e.Dice.Pick(candidates);
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Source = Owner.Ref, Target = target.Ref, Element = RuneType.Lightning, Text = $"Ланцюг блискавки перескакує на {target.DisplayName}!" });
            e.DealDamage(new DamageContext { Attacker = Owner, Target = target, Base = ChainDamage, Direct = true, IsSpell = true, Element = RuneType.Lightning, SourceName = "Ланцюг блискавки" });
        }
    }

    /// <summary>Кріомант — «Крижаний полон»: раз на битву заморожує монстра (пропускає атаку цього раунду).</summary>
    [AbilityId("mage_cryo")]
    public sealed class CryomancerIcePrison : AbilityBase, IActiveAbility
    {
        private bool _used;
        public bool Used => _used;
        public string ActiveName => "Крижаний полон";
        public bool NeedsMonsterTarget => true;

        public override void OnBattleStart(RulesEngine e) => _used = false;

        public bool CanUse(RulesEngine e, MageState user, out string reason)
        {
            reason = _used ? "«Крижаний полон» уже використано в цій битві." : null;
            return !_used;
        }

        public void Use(RulesEngine e, MageState user, int targetPos)
        {
            _used = true;
            var target = e.State.MonsterAt(targetPos);
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Source = user.Ref, Target = target.Ref, Element = RuneType.Ice, Text = $"{user.DisplayName}: «Крижаний полон» на {target.DisplayName}" });
            e.ApplyStatus(target, StatusType.Frozen, 1, 1, "Крижаний полон");
        }
    }
}
