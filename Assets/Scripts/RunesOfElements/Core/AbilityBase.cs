using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RunesOfElements.Core
{
    public struct DamageMod
    {
        public int Amount;
        public string Source;
        public DamageMod(int amount, string source) { Amount = amount; Source = source; }
    }

    public sealed class DamageContext
    {
        public UnitState Attacker;
        public UnitState Target;
        public int Base;
        public bool IsSpell;
        public bool IsMonsterAttack;
        /// <summary>Пряма шкода (ланцюг блискавки, ефекти) — без модифікаторів атакувальника.</summary>
        public bool Direct;
        public RuneType Element;
        public RuneType Form;
        public bool IsPrimaryTarget;
        public readonly List<DamageMod> OutgoingMods = new List<DamageMod>();
        public readonly List<DamageMod> IncomingMods = new List<DamageMod>();
        /// <summary>Встановлене значення атаки замість базової (напр. «Гнів Стража»).</summary>
        public int Final;
        public int Absorbed;
        public string SourceName;
    }

    public sealed class SpellResult
    {
        public RuneType Element;
        public RuneType Form;
        public int Roll;
        public int PrimaryPos;
        public readonly List<int> Targets = new List<int>();
        public readonly Dictionary<int, int> DamageByPos = new Dictionary<int, int>();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AbilityIdAttribute : Attribute
    {
        public string Id { get; }
        public AbilityIdAttribute(string id) { Id = id; }
    }

    /// <summary>
    /// Базовий клас для пасивок магів, здібностей монстрів, карт Благословень і глобальних ефектів.
    /// Рушій розсилає хуки всім активним здібностям; кожна сама перевіряє, чи стосується її подія (Owner).
    /// </summary>
    public abstract class AbilityBase
    {
        public string Id;
        public UnitState Owner;
        public MageState OwnerMage => Owner as MageState;
        public MonsterState OwnerMonster => Owner as MonsterState;

        // життєвий цикл
        public virtual void OnBattleSetup(RulesEngine e) { }
        public virtual void OnBattleStart(RulesEngine e) { }
        public virtual void OnRoundStart(RulesEngine e) { }
        public virtual void OnRoundEnd(RulesEngine e) { }
        public virtual void OnBattleEnd(RulesEngine e) { }

        // концентрація / ходи
        public virtual int ModifyStartingConcentration(MageState mage, int value) => value;
        public virtual int ModifyMeditationBonus(RulesEngine e, MageState mage, int bonus) => bonus;
        public virtual int ModifyConcentrationGain(RulesEngine e, MageState mage, int gain) => gain;
        public virtual void AfterConcentrationGain(RulesEngine e, MageState mage) { }
        public virtual int ModifySwapLimit(MageState mage, int limit) => limit;
        public virtual int ModifySwapCost(MageState mage, int cost) => cost;
        public virtual int ModifyHandLimit(MageState mage, int limit) => limit;
        public virtual int ModifyMaxHp(MageState mage, int value) => value;
        public virtual int ModifyBattleStartHp(MageState mage, int hp) => hp;

        // заклинання
        public virtual int ModifySpellCost(RulesEngine e, MageState caster, RuneType element, RuneType form, int cost) => cost;
        public virtual void AfterSpellCast(RulesEngine e, MageState caster, SpellResult result) { }

        // шкода
        public virtual void ModifyOutgoing(RulesEngine e, DamageContext ctx) { }
        public virtual void ModifyIncoming(RulesEngine e, DamageContext ctx) { }
        public virtual void OnDamaged(RulesEngine e, DamageContext ctx, int dealt) { }
        /// <summary>Повертає true, якщо здібність рятує юніта від смерті (HP = 1).</summary>
        public virtual bool OnLethal(RulesEngine e, UnitState unit) => false;
        public virtual void OnDeath(RulesEngine e, UnitState unit) { }

        // монстри
        public virtual int ModifyMonsterAttack(RulesEngine e, MonsterState monster, int attack) => attack;
        public virtual void AfterMonsterAttack(RulesEngine e, MonsterState monster, MageState target, int dealt) { }
        public virtual void AfterMonsterAction(RulesEngine e, MonsterState monster) { }

        // інше
        public virtual int ModifyHeal(RulesEngine e, MageState mage, int amount) => amount;
        public virtual bool BlocksStatus(RulesEngine e, UnitState target, StatusType type) => false;
        public virtual bool CanCastElement(MageState caster, RuneType element) => true;
    }

    /// <summary>Здібність, яку гравець активує вручну (кнопкою).</summary>
    public interface IActiveAbility
    {
        string ActiveName { get; }
        bool NeedsMonsterTarget { get; }
        bool CanUse(RulesEngine e, MageState user, out string reason);
        void Use(RulesEngine e, MageState user, int targetPos);
    }

    public static class AbilityRegistry
    {
        private static Dictionary<string, Type> _types;

        private static void EnsureBuilt()
        {
            if (_types != null) return;
            _types = new Dictionary<string, Type>();
            foreach (var t in typeof(AbilityBase).Assembly.GetTypes())
            {
                if (t.IsAbstract || !typeof(AbilityBase).IsAssignableFrom(t)) continue;
                var attr = t.GetCustomAttribute<AbilityIdAttribute>();
                if (attr != null) _types[attr.Id] = t;
            }
        }

        public static bool Has(string id) { EnsureBuilt(); return !string.IsNullOrEmpty(id) && _types.ContainsKey(id); }

        public static IEnumerable<string> AllIds { get { EnsureBuilt(); return _types.Keys.ToList(); } }

        public static AbilityBase Create(string id, UnitState owner)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id) || !_types.TryGetValue(id, out var type)) return null;
            var a = (AbilityBase)Activator.CreateInstance(type);
            a.Id = id;
            a.Owner = owner;
            return a;
        }
    }
}
