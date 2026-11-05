using System.Collections.Generic;
using System.Linq;
using RunesOfElements.Core.Abilities;

namespace RunesOfElements.Core
{
    /// <summary>
    /// Евристика бота (§11): якщо доступне заклинання вкладається в концентрацію — атакує з найбільшою очікуваною
    /// користю; інакше медитує. Кожен виклик робить рівно одну дію, щоб презентація могла показати її.
    /// </summary>
    public static class BotPolicy
    {
        public struct SpellOption
        {
            public int ElementSlot, FormSlot, Target;
            public float Score;
        }

        /// <summary>Повертає наступну команду для бота в поточному стані (або null, якщо від бота нічого не чекають).</summary>
        public static IGameCommand NextCommand(RulesEngine e, int seat)
        {
            var s = e.State;
            var mage = s.Mages[seat];
            if (mage == null) return null;

            if (s.Phase == BattlePhase.Setup)
                return NextSetupSwap(e, mage);

            if (s.Phase == BattlePhase.BattleWon || s.Phase == BattlePhase.WaveWon)
            {
                if (mage.Hand.Count> e.GetHandLimit(mage)) return new DiscardRuneCommand(seat, WorstHandRune(mage));
                return null;
            }

            if (s.Phase != BattlePhase.MagesTurn || s.ActiveSeat != seat) return null;

            if (s.Step == TurnStep.RollDecision && s.Pending != null)
            {
                var p = s.Pending;
                if (p.CanMirror && p.Roll <= 2) return new ResolveRollCommand(seat, RollChoice.Mirror);
                if (p.CanReroll && p.Roll <= 3) return new ResolveRollCommand(seat, RollChoice.Reroll);
                return new ResolveRollCommand(seat, RollChoice.Accept);
            }

            if (s.Step != TurnStep.SwapRune) return null;

            // 1. одна корисна заміна руни
            if (mage.SwapsThisTurn == 0)
            {
                var swap = ChooseSwap(e, mage);
                if (swap != null) return swap;
            }

            // 2. «Крижаний полон» на найнебезпечнішого монстра
            var cryo = mage.Ability<CryomancerIcePrison>();
            if (cryo != null && !cryo.Used)
            {
                var danger = s.LivingMonsters.OrderByDescending(m => m.Attack).FirstOrDefault();
                if (danger != null && (danger.Attack >= 3 || danger.Def.Level == MonsterLevel.Boss) && !danger.Has(StatusType.Frozen))
                {
                    var cmd = new UseActiveAbilityCommand(seat, cryo.Id, danger.Position);
                    if (cmd.Validate(e) == null) return cmd;
                }
            }

            // 3. найкраще доступне заклинання
            var best = BestSpell(e, mage);
            if (best.HasValue) return new CastSpellCommand(seat, best.Value.ElementSlot, best.Value.FormSlot, best.Value.Target);

            // 4. медитація
            return new MeditateCommand(seat);
        }

        public static SpellOption? BestSpell(RulesEngine e, MageState mage)
        {
            SpellOption? best = null;
            for (int es = 0; es < 2; es++)
            for (int fs = 0; fs < 2; fs++)
            {
                var el = mage.ElementSlots[es];
                var form = mage.FormSlots[fs];
                if (!el.HasValue || !form.HasValue) continue;
                foreach (var mon in e.State.LivingMonsters)
                {
                    if (e.ValidateCast(mage.Seat, es, fs, mon.Position) != null) continue;
                    float score = ScoreSpell(e, mage, el.Value, form.Value, mon.Position);
                    if (!best.HasValue || score > best.Value.Score)
                        best = new SpellOption { ElementSlot = es, FormSlot = fs, Target = mon.Position, Score = score };
                }
            }
            return best;
        }

        public static float ScoreSpell(RulesEngine e, MageState mage, RuneType element, RuneType form, int target)
        {
            float expected = 3.5f + Rules.FlatDamageBonus(element);
            float score = 0f;
            bool magesLow = e.State.LivingMages.Any(m => m.Hp <= 2);
            foreach (int pos in e.GetSpellTargets(form, target))
            {
                var m = e.State.MonsterAt(pos);
                if (m == null) continue;
                float dmg = expected - m.Stacks(StatusType.StoneSkin);
                score += System.Math.Min(dmg, m.Hp);
                if (dmg >= m.Hp)
                {
                    bool assassin = m.Abilities.Any(a => a is AssassinShadowRift);
                    score += assassin && magesLow ? -6f : 2f + m.Attack;
                }
            }
            score -= e.GetSpellCost(mage, element, form) * 0.15f; // легка економія концентрації
            return score;
        }

        private static IGameCommand ChooseSwap(RulesEngine e, MageState mage)
        {
            bool hasElement = mage.ElementSlots.Any(r => r.HasValue);
            bool hasForm = mage.FormSlots.Any(r => r.HasValue);

            // спочатку заповнюємо порожні комірки
            if (!hasForm || !hasElement || mage.FormSlots.Any(r => !r.HasValue) || mage.ElementSlots.Any(r => !r.HasValue))
            {
                var order = !hasForm ? new[] { RuneCategory.Form, RuneCategory.Element } : new[] { RuneCategory.Element, RuneCategory.Form };
                foreach (var cat in order)
                {
                    int empty = cat == RuneCategory.Element ? System.Array.FindIndex(mage.ElementSlots, r => !r.HasValue) : System.Array.FindIndex(mage.FormSlots, r => !r.HasValue);
                    if (empty < 0) continue;
                    int handIdx = BestHandRune(mage, cat, null);
                    if (handIdx < 0) continue;
                    var cmd = new SwapRuneCommand(mage.Seat, handIdx, cat, empty);
                    if (cmd.Validate(e) == null) return cmd;
                }
            }
            return null;
        }

        private static int BestHandRune(MageState mage, RuneCategory cat, RuneType? exclude)
        {
            int best = -1;
            int bestScore = int.MinValue;
            for (int i = 0; i < mage.Hand.Count; i++)
            {
                var r = mage.Hand[i];
                if (Rules.CategoryOf(r) != cat) continue;
                if (mage.ElementSlots.Contains(r) || mage.FormSlots.Contains(r)) continue; // без дублікатів на картці
                int score = r == mage.Def.Element ? 10 : 0;
                if (r == RuneType.Beam) score += 3;  // дешевша форма
                if (r == RuneType.Sphere) score += 2;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best < 0)
                for (int i = 0; i < mage.Hand.Count; i++)
                    if (Rules.CategoryOf(mage.Hand[i]) == cat) return i;
            return best;
        }

        private static IGameCommand NextSetupSwap(RulesEngine e, MageState mage)
        {
            for (int i = 0; i < 2; i++)
            {
                if (!mage.ElementSlots[i].HasValue)
                {
                    int h = BestHandRune(mage, RuneCategory.Element, null);
                    if (h >= 0) return new SwapRuneCommand(mage.Seat, h, RuneCategory.Element, i);
                }
                if (!mage.FormSlots[i].HasValue)
                {
                    int h = BestHandRune(mage, RuneCategory.Form, null);
                    if (h >= 0) return new SwapRuneCommand(mage.Seat, h, RuneCategory.Form, i);
                }
            }
            return null;
        }

        private static int WorstHandRune(MageState mage)
        {
            // здаємо дублікат або руну, що не є стихією мага
            var counts = new Dictionary<RuneType, int>();
            foreach (var r in mage.AllRunes()) counts[r] = counts.TryGetValue(r, out var c) ? c + 1 : 1;
            int worst = 0;
            int worstScore = int.MaxValue;
            for (int i = 0; i < mage.Hand.Count; i++)
            {
                var r = mage.Hand[i];
                int score = (r == mage.Def.Element ? 10 : 0) - counts[r] * 2;
                if (score < worstScore) { worstScore = score; worst = i; }
            }
            return worst;
        }

        /// <summary>Повністю розігрує гру ботами (для тестів і балансу). Повертає кількість кроків.</summary>
        public static int AutoPlay(RulesEngine e, int maxSteps = 20000)
        {
            int steps = 0;
            while (!e.IsOver && steps < maxSteps)
            {
                steps++;
                var s = e.State;
                IGameCommand cmd = null;
                if (s.Phase == BattlePhase.Setup)
                {
                    foreach (var m in s.AllMages) { cmd = NextCommand(e, m.Seat); if (cmd != null) break; }
                    cmd = cmd ?? new ConfirmSetupCommand();
                }
                else if (s.Phase == BattlePhase.BattleWon || s.Phase == BattlePhase.WaveWon)
                {
                    foreach (var m in s.AllMages) { cmd = NextCommand(e, m.Seat); if (cmd != null) break; }
                    cmd = cmd ?? new ContinueCampaignCommand();
                }
                else if (s.Phase == BattlePhase.MagesTurn && s.ActiveSeat >= 0)
                {
                    cmd = NextCommand(e, s.ActiveSeat);
                }
                if (cmd == null) break;
                var res = e.Submit(cmd);
                if (!res.Ok) throw new System.InvalidOperationException($"Бот подав невалідну команду {cmd.GetType().Name}: {res.Reason}");
                var inv = e.CheckInvariants();
                if (inv != null) throw new System.InvalidOperationException("Порушено інваріант: " + inv);
            }
            return steps;
        }
    }
}
