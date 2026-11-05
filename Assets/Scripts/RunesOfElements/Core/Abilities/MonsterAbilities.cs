using System.Linq;

namespace RunesOfElements.Core.Abilities
{
    internal static class MonsterUtil
    {
        public static void HealMonster(RulesEngine e, MonsterState m, int amount, string reason)
        {
            int before = m.Hp;
            m.Hp = System.Math.Min(m.MaxHp, m.Hp + amount);
            if (m.Hp > before)
                e.Log.Add(new GameEvent(GameEventType.Healed) { Target = m.Ref, Value = m.Hp - before, Text = $"{m.DisplayName} +{m.Hp - before} HP ({reason})" });
        }
    }

    // ============================ Рівень 1 ============================

    [AbilityId("mon_goblin_pair")]
    public sealed class GoblinPair : AbilityBase
    {
        public override int ModifyMonsterAttack(RulesEngine e, MonsterState monster, int attack)
        {
            if (monster != Owner) return attack;
            bool otherGoblin = e.State.LivingMonsters.Any(m => m != Owner && m.Abilities.Any(a => a is GoblinPair));
            return otherGoblin ? attack + 1 : attack;
        }
    }

    [AbilityId("mon_skeleton_immunity")]
    public sealed class SkeletonModifierImmunity : AbilityBase
    {
        private bool _used;
        public override void ModifyIncoming(RulesEngine e, DamageContext ctx)
        {
            if (ctx.Target != Owner || _used) return;
            int idx = ctx.OutgoingMods.FindIndex(m => m.Amount != 0);
            if (idx >= 0)
            {
                var mod = ctx.OutgoingMods[idx];
                ctx.OutgoingMods.RemoveAt(idx);
                _used = true;
                e.Log.Info($"{Owner.DisplayName} ігнорує модифікатор «{mod.Source}» ({mod.Amount:+#;-#})");
                return;
            }
            idx = ctx.IncomingMods.FindIndex(m => m.Amount > 0);
            if (idx >= 0)
            {
                var mod = ctx.IncomingMods[idx];
                ctx.IncomingMods.RemoveAt(idx);
                _used = true;
                e.Log.Info($"{Owner.DisplayName} ігнорує модифікатор «{mod.Source}»");
            }
        }
    }

    public abstract class DrainOnAttack : AbilityBase
    {
        protected abstract string SourceName { get; }
        public override void AfterMonsterAttack(RulesEngine e, MonsterState monster, MageState target, int dealt)
        {
            if (monster != Owner || target == null || !target.IsAlive || target.Eliminated) return;
            e.ChangeConcentration(target, -1, SourceName);
        }
    }

    [AbilityId("mon_spider_drain")]
    public sealed class SpiderDrain : DrainOnAttack { protected override string SourceName => "Печерний павук"; }

    [AbilityId("mon_shaman_drain")]
    public sealed class ShamanDrain : DrainOnAttack { protected override string SourceName => "Громовий шаман"; }

    [AbilityId("mon_flying_evasive")]
    public sealed class FlyingEvasive : AbilityBase
    {
        public override void ModifyIncoming(RulesEngine e, DamageContext ctx)
        {
            if (ctx.Target == Owner && ctx.IsSpell && ctx.Form == RuneType.Sphere)
                ctx.IncomingMods.Add(new DamageMod(-1, "Летюча (Сфера)"));
        }
    }

    [AbilityId("mon_wolf_regen")]
    public sealed class WolfRegen : AbilityBase
    {
        public override void AfterMonsterAction(RulesEngine e, MonsterState monster)
        {
            if (monster == Owner) MonsterUtil.HealMonster(e, OwnerMonster, 1, "регенерація");
        }
    }

    [AbilityId("mon_undying")]
    public sealed class Undying : AbilityBase
    {
        private bool _used;
        public override bool OnLethal(RulesEngine e, UnitState unit)
        {
            if (unit != Owner || _used) return false;
            _used = true;
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Target = Owner.Ref, Text = $"{Owner.DisplayName}: «Невмирущий» — лишається з 1 здоров'я!" });
            return true;
        }
    }

    [AbilityId("mon_fire_burn")]
    public sealed class FireElementalBurn : AbilityBase
    {
        public override void AfterMonsterAttack(RulesEngine e, MonsterState monster, MageState target, int dealt)
        {
            if (monster == Owner && target != null && target.IsAlive) e.ApplyStatus(target, StatusType.Burn, 1, 2, "Палаючий удар");
        }
    }

    [AbilityId("mon_necro_summon")]
    public sealed class NecromancerSummon : AbilityBase
    {
        private bool _used;
        public override void OnDamaged(RulesEngine e, DamageContext ctx, int dealt)
        {
            if (ctx.Target != Owner || _used) return;
            _used = true;
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Source = Owner.Ref, Text = $"{Owner.DisplayName}: «Поклик мертвих»!" });
            e.SummonMonster(MonsterLevel.Level1, "Поклик мертвих");
        }
    }

    // ============================ Рівень 2 ============================

    [AbilityId("mon_assassin_death")]
    public sealed class AssassinShadowRift : AbilityBase
    {
        public override void OnDeath(RulesEngine e, UnitState unit)
        {
            if (unit != Owner) return;
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Source = Owner.Ref, Text = $"{Owner.DisplayName}: «Розрив тіні» — 2 шкоди всім магам!" });
            foreach (var m in e.State.LivingMages.ToList())
                e.DealDamage(new DamageContext { Attacker = Owner, Target = m, Base = 2, Direct = true, SourceName = "Розрив тіні" });
        }
    }

    [AbilityId("mon_golem_firsthit")]
    public sealed class GolemFirstHit : AbilityBase
    {
        public override void ModifyIncoming(RulesEngine e, DamageContext ctx)
        {
            if (ctx.Target == Owner && OwnerMonster.HitsThisRound == 0)
                ctx.IncomingMods.Add(new DamageMod(-2, "Крижаний голем"));
        }
    }

    [AbilityId("mon_demon_rage")]
    public sealed class DemonRage : AbilityBase
    {
        public override int ModifyMonsterAttack(RulesEngine e, MonsterState monster, int attack) =>
            monster == Owner && Owner.Hp <= 5 ? attack + 1 : attack;
    }

    // ============================ Рівень 3 ============================

    [AbilityId("mon_stone_armor")]
    public sealed class StoneArmor : AbilityBase
    {
        public override void OnBattleStart(RulesEngine e) => e.ApplyStatus(Owner, StatusType.StoneSkin, 1, -2, "Кам'яна броня");
    }

    [AbilityId("mon_thunderlord_drain")]
    public sealed class ThunderLordDrain : AbilityBase
    {
        public override void AfterMonsterAttack(RulesEngine e, MonsterState monster, MageState target, int dealt)
        {
            if (monster != Owner) return;
            var mages = e.State.LivingMages.ToList();
            if (mages.Count == 0) return;
            e.ChangeConcentration(e.Dice.Pick(mages), -1, "Громовий Володар");
        }
    }

    [AbilityId("mon_riftlord_summon")]
    public sealed class RiftLordFury : AbilityBase
    {
        private bool _used;
        public override void OnDamaged(RulesEngine e, DamageContext ctx, int dealt)
        {
            if (ctx.Target != Owner || _used || Owner.Hp > 10 || Owner.Hp <= 0) return;
            _used = true;
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Source = Owner.Ref, Text = $"{Owner.DisplayName}: «Лють Розлому» кличе підкріплення!" });
            e.SummonMonster(MonsterLevel.Level1, "Лють Розлому");
            e.SummonMonster(MonsterLevel.Level1, "Лють Розлому");
        }
    }

    [AbilityId("mon_dragon_breath")]
    public sealed class VoidDragonBreath : AbilityBase
    {
        public override void OnBattleStart(RulesEngine e)
        {
            foreach (var m in e.State.LivingMages.ToList())
                e.ApplyStatus(m, StatusType.Poison, 1, -1, "Подих Пустоти (Розлом)");
        }
    }

    // ============================ Боси ============================

    [AbilityId("boss_chaos_warp")]
    public sealed class ChaosWarpedMagic : AbilityBase
    {
        public override void OnRoundStart(RulesEngine e)
        {
            for (int i = 0; i < e.State.ElementSurcharge.Length; i++) e.State.ElementSurcharge[i] = 0;
            var el = e.Dice.Pick(Rules.Elements);
            e.State.ElementSurcharge[e.State.ElementIndex(el)] = 1;
            e.Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Source = Owner.Ref, Element = el, Text = $"«Викривлена магія»: {Rules.RuneName(el)} цього раунду дорожча на 1" });
        }

        public override void OnDeath(RulesEngine e, UnitState unit)
        {
            if (unit == Owner) for (int i = 0; i < e.State.ElementSurcharge.Length; i++) e.State.ElementSurcharge[i] = 0;
        }
    }

    [AbilityId("boss_chaos_reflect")]
    public sealed class ChaosReflect : AbilityBase
    {
        public override void OnDamaged(RulesEngine e, DamageContext ctx, int dealt)
        {
            if (ctx.Target != Owner || !ctx.IsSpell || ctx.Direct || !(ctx.Attacker is MageState mage) || !mage.IsAlive) return;
            e.DealDamage(new DamageContext { Attacker = Owner, Target = mage, Base = 1, Direct = true, SourceName = "Відбиття" });
        }
    }

    [AbilityId("boss_chaos_surge")]
    public sealed class ChaosSurge : AbilityBase
    {
        public override void OnRoundStart(RulesEngine e)
        {
            if (e.State.Round % 3 != 0) return;
            var mages = e.State.LivingMages.ToList();
            if (mages.Count == 0) return;
            var target = e.Dice.Pick(mages);
            e.DealDamage(new DamageContext { Attacker = Owner, Target = target, Base = 2, Direct = true, SourceName = "Хаотичний сплеск" });
        }
    }

    [AbilityId("boss_queen_cold")]
    public sealed class QueenEternalCold : AbilityBase
    {
        public override void AfterConcentrationGain(RulesEngine e, MageState mage)
        {
            if (mage.Concentration > 1) e.ChangeConcentration(mage, -1, "Вічний холод");
        }
    }

    [AbilityId("boss_queen_armor")]
    public sealed class QueenIceArmor : AbilityBase
    {
        public override void ModifyIncoming(RulesEngine e, DamageContext ctx)
        {
            if (ctx.Target == Owner && OwnerMonster.HitsThisRound == 0)
                ctx.IncomingMods.Add(new DamageMod(-2, "Крижана броня"));
        }
    }

    [AbilityId("boss_queen_freeze")]
    public sealed class QueenFreeze : AbilityBase
    {
        public override void OnRoundStart(RulesEngine e)
        {
            var mages = e.State.LivingMages.ToList();
            if (mages.Count == 0) return;
            var target = e.Dice.Pick(mages);
            var el = e.Dice.Pick(Rules.Elements);
            e.ApplyStatus(target, StatusType.Silence, 1, 1, "Замороження", (int)el);
        }
    }

    [AbilityId("boss_guardian_steadfast")]
    public sealed class GuardianSteadfast : AbilityBase
    {
        public override bool BlocksStatus(RulesEngine e, UnitState target, StatusType type) =>
            target == Owner && (type == StatusType.Frozen || type == StatusType.Stunned);
    }

    [AbilityId("boss_guardian_wrath")]
    public sealed class GuardianWrath : AbilityBase
    {
        public override int ModifyMonsterAttack(RulesEngine e, MonsterState monster, int attack) =>
            monster == Owner && Owner.Hp <= 10 ? 6 : attack;
    }
}
