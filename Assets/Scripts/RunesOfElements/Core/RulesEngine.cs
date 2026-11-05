using System;
using System.Collections.Generic;
using System.Linq;

namespace RunesOfElements.Core
{
    /// <summary>
    /// Серце гри: автомат станів раунду (§5.9), валідація та виконання всіх дій.
    /// Повністю синхронний і детермінований для заданого IRandomSource — придатний для сервер-авторитетної мережі.
    /// </summary>
    public sealed class RulesEngine
    {
        public GameState State { get; }
        public EventLog Log { get; } = new EventLog();
        public DiceService Dice { get; }
        public GameContent Content => State.Content;

        public event Action StateChanged;

        public RulesEngine(GameContent content, GameSetup setup, IRandomSource rng)
        {
            State = new GameState { Content = content, Setup = setup };
            Dice = new DiceService(rng, Log);
        }

        // =====================================================================
        //  Командний інтерфейс
        // =====================================================================

        public CommandResult Submit(IGameCommand cmd)
        {
            string reason = cmd.Validate(this);
            if (reason != null) return CommandResult.Fail(reason);
            cmd.Execute(this);
            StateChanged?.Invoke();
            return CommandResult.Success;
        }

        // =====================================================================
        //  Кампанія
        // =====================================================================

        public void StartCampaign()
        {
            var s = State;
            var setup = s.Setup;

            // руни спільного запасу
            foreach (var spec in Content.RunePool)
                for (int i = 0; i < spec.Count; i++) s.RunePool.Add(spec.Rune);
            Dice.Shuffle(s.RunePool);

            // колоди монстрів
            foreach (MonsterLevel lvl in Enum.GetValues(typeof(MonsterLevel)))
            {
                var deck = Content.Monsters.Where(m => m.Level == lvl).ToList();
                Dice.Shuffle(deck);
                s.Decks[lvl] = deck;
            }
            var bosses = s.Decks[MonsterLevel.Boss];
            s.ChosenBoss = !string.IsNullOrEmpty(setup.BossId) ? Content.Monster(setup.BossId) : null;
            if (s.ChosenBoss == null && bosses.Count > 0) s.ChosenBoss = bosses[0];
            if (s.ChosenBoss != null) Log.Info($"Бос кампанії: {s.ChosenBoss.Name}");

            // маги
            var freeMages = Content.Mages.Where(m => !setup.Seats.Any(seat => seat != null && seat.MageId == m.Id)).ToList();
            Dice.Shuffle(freeMages);
            var blessingDeck = Content.Blessings.ToList();
            Dice.Shuffle(blessingDeck);

            for (int seat = 0; seat < Rules.MageSeats && seat < setup.Seats.Count; seat++)
            {
                var seatSetup = setup.Seats[seat];
                if (seatSetup == null) continue;
                var def = !string.IsNullOrEmpty(seatSetup.MageId) ? Content.Mage(seatSetup.MageId) : null;
                if (def == null && freeMages.Count > 0) { def = freeMages[0]; freeMages.RemoveAt(0); }
                if (def == null) continue;

                var mage = new MageState { Seat = seat, Def = def, Player = seatSetup };
                AddAbility(mage, def.PassiveAbilityId);

                if (setup.BlessingsEnabled && !seatSetup.IsBot && blessingDeck.Count > 0)
                {
                    mage.Blessing = blessingDeck[0];
                    blessingDeck.RemoveAt(0);
                    AddAbility(mage, mage.Blessing.Id);
                    if (mage.Blessing.Id == "blessing_elemental_bond")
                        mage.BlessingChosenElement = Dice.Pick(Rules.Elements);
                    Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Target = mage.Ref, Text = $"{mage.DisplayName} отримує карту «{mage.Blessing.Name}»" });
                }

                int maxHp = Rules.MaxHp;
                foreach (var a in mage.Abilities) maxHp = a.ModifyMaxHp(mage, maxHp);
                mage.MaxHp = maxHp;
                mage.Hp = maxHp;
                s.Mages[seat] = mage;

                // стартові руни: гарантуємо хоча б 1 Стихію і 1 Форму, третя — випадкова
                DrawRune(mage, r => Rules.IsElement(r));
                DrawRune(mage, r => !Rules.IsElement(r));
                for (int i = 2; i < Rules.StartingRunes; i++) DrawRune(mage, null);
            }

            BeginBattle(0);
        }

        private void AddAbility(UnitState unit, string id)
        {
            var a = AbilityRegistry.Create(id, unit);
            if (a != null) unit.Abilities.Add(a);
        }

        public bool DrawRune(MageState mage, Func<RuneType, bool> filter)
        {
            int idx = filter == null ? (State.RunePool.Count > 0 ? 0 : -1) : State.RunePool.FindIndex(r => filter(r));
            if (idx < 0) return false;
            var rune = State.RunePool[idx];
            State.RunePool.RemoveAt(idx);
            mage.Hand.Add(rune);
            return true;
        }

        private void BeginBattle(int index)
        {
            var s = State;
            s.BattleIndex = index;
            s.Round = 0;
            s.Pending = null;
            s.ActiveSeat = -1;
            for (int i = 0; i < s.Board.Length; i++) s.Board[i] = null;
            for (int i = 0; i < s.ElementSurcharge.Length; i++) s.ElementSurcharge[i] = 0;

            bool firstWaveOfFloor = s.Wave == 0;
            foreach (var m in s.AllMages)
            {
                // Вибулі на попередніх хвилях лишаються поза грою до кінця поверху.
                if (!firstWaveOfFloor && (m.Eliminated || !m.IsAlive))
                {
                    m.Eliminated = true;
                    m.Hp = 0;
                    m.Statuses.Clear();
                    continue;
                }
                m.Eliminated = false;
                m.Statuses.Clear();
                m.MeditatedLastTurn = false;
                m.MeditationsThisBattle = 0;
                m.BlessingUsesBattle = 0;
                int conc = Rules.StartConcentration;
                foreach (var a in m.Abilities) conc = a.ModifyStartingConcentration(m, conc);
                m.Concentration = Rules.ClampConcentration(conc);
                int hp = index == 0 ? m.MaxHp : m.Hp;
                foreach (var a in m.Abilities) hp = a.ModifyBattleStartHp(m, hp);
                m.Hp = Math.Max(1, Math.Min(m.MaxHp, hp));
            }

            s.HighestFloorReached = Math.Max(s.HighestFloorReached, s.Floor + 1);
            string floorName = Content.Tower.Count > s.Floor ? Content.Tower[s.Floor].Name : $"Поверх {s.Floor + 1}";
            Log.Add(new GameEvent(GameEventType.BattleStarted)
            {
                Value = index + 1,
                Text = $"— {floorName} ({s.Floor + 1}/{s.FloorCount}) · Хвиля {s.Wave + 1}/{s.WavesInFloor} —"
            });

            var spec = s.CurrentBattleSpec ?? new BattleSpec { Level1 = 3 };
            var toSpawn = new List<MonsterDef>();
            if (spec.Boss && s.ChosenBoss != null) toSpawn.Add(s.ChosenBoss);
            toSpawn.AddRange(DrawMonsters(MonsterLevel.Level3, spec.Level3));
            toSpawn.AddRange(DrawMonsters(MonsterLevel.Level2, spec.Level2));
            toSpawn.AddRange(DrawMonsters(MonsterLevel.Level1, spec.Level1));
            foreach (var def in toSpawn) SpawnMonster(def, "розстановка", false);

            foreach (var a in ActiveAbilities()) a.OnBattleSetup(this);
            foreach (var a in ActiveAbilities()) a.OnBattleStart(this);

            if (index == 0 && s.Setup.FreeStartingPlacement)
            {
                SetPhase(BattlePhase.Setup);
                Log.Info("Підготовка: розкладіть стартові руни на картки магів, потім натисніть «До бою».");
                return;
            }
            BeginRound();
        }

        public List<MonsterDef> DrawMonsters(MonsterLevel level, int count)
        {
            var result = new List<MonsterDef>();
            for (int i = 0; i < count; i++)
            {
                var def = DrawFromDeck(level);
                if (def != null) result.Add(def);
            }
            return result;
        }

        private MonsterDef DrawFromDeck(MonsterLevel level)
        {
            if (level != MonsterLevel.Boss && State.Decks.TryGetValue(level, out var own) && own.Count == 0)
                ReshuffleDeck(level);
            for (var lvl = level; lvl >= MonsterLevel.Level1; lvl--)
            {
                if (State.Decks.TryGetValue(lvl, out var deck) && deck.Count > 0)
                {
                    var def = deck[0];
                    deck.RemoveAt(0);
                    if (lvl != level) Log.Info($"Колода рівня {(int)level} вичерпана — береться монстр рівня {(int)lvl}.");
                    return def;
                }
            }
            Log.Info("Колоди монстрів вичерпано.");
            return null;
        }

        /// <summary>Повертає в колоду всі карти рівня, крім тих, що зараз живі на полі, і перемішує.</summary>
        public void ReshuffleDeck(MonsterLevel level)
        {
            var onBoard = State.LivingMonsters.Select(m => m.Def).ToList();
            var deck = Content.Monsters.Where(m => m.Level == level && !onBoard.Contains(m)).ToList();
            if (deck.Count == 0) return;
            Dice.Shuffle(deck);
            State.Decks[level] = deck;
            Log.Info($"Колоду монстрів рівня {(int)level} перемішано наново ({deck.Count} карт).");
        }

        private MonsterState CreateMonster(MonsterDef def, int pos)
        {
            int hp = def.Hp + Content.MonsterHpBonusFor(State.BattleIndex);
            var m = new MonsterState { Def = def, Position = pos, Hp = hp, MaxHp = hp, Attack = def.Attack };
            foreach (var id in def.AbilityIds) AddAbility(m, id);
            State.Board[pos] = m;
            return m;
        }

        /// <summary>Розміщує монстра кидком d8 (1–4 верхній ряд, 5–8 нижній; зайняту позицію перекидають).</summary>
        public MonsterState SpawnMonster(MonsterDef def, string reason, bool battleRunning = true)
        {
            if (def == null) return null;
            if (!State.Board.Any(IsFree))
            {
                Log.Info($"Немає вільних позицій для «{def.Name}».");
                return null;
            }
            int pos = -1;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var roll = Dice.Roll(DieType.D8, $"Позиція «{def.Name}»");
                if (IsFree(State.Board[roll.Value - 1])) { pos = roll.Value - 1; break; }
                Log.Info($"Позиція {roll.Value} зайнята — перекидаємо.");
            }
            if (pos < 0) pos = Array.FindIndex(State.Board, IsFree);

            var m = CreateMonster(def, pos);
            Log.Add(new GameEvent(GameEventType.MonsterSpawned) { Target = m.Ref, Value = pos + 1, Text = $"{def.Name} займає позицію {pos + 1} ({reason})" });
            if (battleRunning) foreach (var a in m.Abilities) a.OnBattleStart(this);
            return m;
        }

        private static bool IsFree(MonsterState m) => m == null || !m.IsAlive;

        /// <summary>Ставить монстра на конкретну позицію без кидка (для тестів і налагодження).</summary>
        public MonsterState PlaceMonsterAt(MonsterDef def, int pos)
        {
            var m = CreateMonster(def, pos);
            foreach (var a in m.Abilities) a.OnBattleStart(this);
            return m;
        }

        public void ClearBoard()
        {
            for (int i = 0; i < State.Board.Length; i++) State.Board[i] = null;
        }

        public MonsterState SummonMonster(MonsterLevel level, string reason)
        {
            var def = DrawFromDeck(level);
            return def == null ? null : SpawnMonster(def, reason);
        }

        // =====================================================================
        //  Раунд
        // =====================================================================

        private void SetPhase(BattlePhase phase)
        {
            State.Phase = phase;
            Log.Add(new GameEvent(GameEventType.PhaseChanged) { Phase = phase, Text = PhaseText(phase) });
        }

        public static string PhaseText(BattlePhase p)
        {
            switch (p)
            {
                case BattlePhase.Setup: return "Підготовка до бою";
                case BattlePhase.RoundStart: return "Початок раунду";
                case BattlePhase.MagesTurn: return "Хід магів";
                case BattlePhase.MonstersTurn: return "Хід монстрів";
                case BattlePhase.Recovery: return "Відновлення";
                case BattlePhase.WaveWon: return "Хвилю відбито";
                case BattlePhase.BattleWon: return "Поверх пройдено";
                case BattlePhase.Defeat: return "Поразка";
                case BattlePhase.CampaignWon: return "Башту підкорено";
            }
            return p.ToString();
        }

        public bool IsOver => State.Phase == BattlePhase.Defeat || State.Phase == BattlePhase.CampaignWon;

        private void BeginRound()
        {
            var s = State;
            s.Round++;
            SetPhase(BattlePhase.RoundStart);
            Log.Info($"Раунд {s.Round}");

            foreach (var m in s.Board) if (m != null) m.HitsThisRound = 0;
            foreach (var m in s.AllMages) m.SpellsThisRound = 0;

            // Опік: шкода на початку раунду
            foreach (var u in s.AllUnits().Where(u => u.IsAlive).ToList())
            {
                var burn = u.GetStatus(StatusType.Burn);
                if (burn == null) continue;
                DealDamage(new DamageContext { Target = u, Base = burn.Stacks, Direct = true, SourceName = "Опік" });
                burn.RemainingRounds--;
                if (burn.RemainingRounds <= 0) RemoveStatus(u, burn);
            }
            if (CheckBattleEnd()) return;

            foreach (var a in ActiveAbilities()) a.OnRoundStart(this);
            if (CheckBattleEnd()) return;

            SetPhase(BattlePhase.MagesTurn);
            s.ActiveSeat = -1;
            AdvanceToNextMage();
        }

        private void AdvanceToNextMage()
        {
            var s = State;
            if (s.Phase != BattlePhase.MagesTurn) return;
            for (int seat = s.ActiveSeat + 1; seat < Rules.MageSeats; seat++)
            {
                var m = s.Mages[seat];
                if (m != null && m.IsAlive && !m.Eliminated)
                {
                    BeginMageTurn(m);
                    return;
                }
            }
            s.ActiveSeat = -1;
            RunMonstersPhase();
        }

        private void BeginMageTurn(MageState mage)
        {
            var s = State;
            s.ActiveSeat = mage.Seat;
            s.Step = TurnStep.GainConcentration;
            mage.SwapsThisTurn = 0;
            mage.ActedThisTurn = false;
            Log.Add(new GameEvent(GameEventType.TurnStarted) { Target = mage.Ref, Text = $"Хід: {mage.DisplayName} ({mage.Player?.PlayerName})" });

            // Крок 1 — концентрація
            int gain = 1;
            if (mage.MeditatedLastTurn)
            {
                int bonus = 2;
                foreach (var a in ActiveAbilities()) bonus = a.ModifyMeditationBonus(this, mage, bonus);
                gain = bonus;
            }
            if (mage.Has(StatusType.Inspired)) gain += 1;
            foreach (var a in ActiveAbilities()) gain = a.ModifyConcentrationGain(this, mage, gain);
            mage.MeditatedLastTurn = false;
            ChangeConcentration(mage, gain, "початок ходу");
            foreach (var a in ActiveAbilities()) a.AfterConcentrationGain(this, mage);

            var stun = mage.GetStatus(StatusType.Stunned);
            if (stun != null)
            {
                RemoveStatus(mage, stun);
                Log.Info($"{mage.DisplayName} приголомшений і пропускає дію.");
                EndMageTurn();
                return;
            }
            s.Step = TurnStep.SwapRune;
        }

        private void EndMageTurn()
        {
            State.Step = TurnStep.Done;
            State.Pending = null;
            if (CheckBattleEnd()) return;
            AdvanceToNextMage();
        }

        private void RunMonstersPhase()
        {
            var s = State;
            SetPhase(BattlePhase.MonstersTurn);
            for (int pos = 0; pos < Rules.BoardPositions; pos++)
            {
                if (s.Phase != BattlePhase.MonstersTurn) return;
                var m = s.MonsterAt(pos);
                if (m == null) continue;

                if (m.Has(StatusType.Frozen))
                {
                    Log.Add(new GameEvent(GameEventType.MonsterSkipped) { Source = m.Ref, Text = $"{m.DisplayName} заморожений і пропускає атаку." });
                    continue;
                }
                var stun = m.GetStatus(StatusType.Stunned);
                if (stun != null)
                {
                    RemoveStatus(m, stun);
                    Log.Add(new GameEvent(GameEventType.MonsterSkipped) { Source = m.Ref, Text = $"{m.DisplayName} приголомшений і пропускає дію." });
                    continue;
                }

                var target = RollMageTarget(m);
                if (target == null) break;

                int attack = m.Attack;
                foreach (var a in ActiveAbilities()) attack = a.ModifyMonsterAttack(this, m, attack);
                Log.Add(new GameEvent(GameEventType.MonsterAttack) { Source = m.Ref, Target = target.Ref, Value = attack, Text = $"{m.DisplayName} атакує {target.DisplayName} ({attack})" });

                int dealt = DealDamage(new DamageContext { Attacker = m, Target = target, Base = attack, IsMonsterAttack = true, SourceName = m.DisplayName });
                foreach (var a in ActiveAbilities()) a.AfterMonsterAttack(this, m, target, dealt);
                if (CheckBattleEnd()) return;
                if (m.IsAlive) foreach (var a in ActiveAbilities()) a.AfterMonsterAction(this, m);
                if (CheckBattleEnd()) return;
            }
            RunRecovery();
        }

        /// <summary>Ціль атаки монстра — кидок d4 по клітинах магового ряду; порожня/вибула клітина — перекидання.</summary>
        public MageState RollMageTarget(MonsterState attacker)
        {
            if (!State.LivingMages.Any()) return null;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var roll = Dice.Roll(DieType.D4, $"Ціль «{attacker.DisplayName}»");
                var m = State.Mages[roll.Value - 1];
                if (m != null && m.IsAlive && !m.Eliminated) return m;
                Log.Info($"Клітина {roll.Value} порожня — перекидаємо.");
            }
            return Dice.Pick(State.LivingMages.ToList());
        }

        private void RunRecovery()
        {
            var s = State;
            SetPhase(BattlePhase.Recovery);

            // Отрута — наприкінці раунду
            foreach (var u in s.AllUnits().Where(u => u.IsAlive).ToList())
            {
                var poison = u.GetStatus(StatusType.Poison);
                if (poison == null) continue;
                if (u is MageState mm && mm.Eliminated) continue;
                DealDamage(new DamageContext { Target = u, Base = poison.Stacks, Direct = true, SourceName = poison.Source ?? "Отрута" });
            }
            if (CheckBattleEnd()) return;

            foreach (var m in s.LivingMages.ToList())
            {
                int amount = Rules.RoundRecovery;
                foreach (var a in ActiveAbilities()) amount = a.ModifyHeal(this, m, amount);
                Heal(m, amount, "відновлення");
            }

            // тривалість станів
            foreach (var u in s.AllUnits().ToList())
            {
                foreach (var st in u.Statuses.ToList())
                {
                    if (st.Type == StatusType.Burn || st.RemainingRounds < 0) continue;
                    st.RemainingRounds--;
                    if (st.RemainingRounds <= 0) RemoveStatus(u, st);
                }
            }

            foreach (var a in ActiveAbilities()) a.OnRoundEnd(this);
            if (CheckBattleEnd()) return;
            BeginRound();
        }

        /// <summary>Перевіряє поразку (пріоритет) і перемогу в битві.</summary>
        public bool CheckBattleEnd()
        {
            var s = State;
            if (s.Phase == BattlePhase.Defeat || s.Phase == BattlePhase.BattleWon || s.Phase == BattlePhase.WaveWon || s.Phase == BattlePhase.CampaignWon) return true;
            if (!s.LivingMages.Any())
            {
                s.ActiveSeat = -1;
                s.Pending = null;
                SetPhase(BattlePhase.Defeat);
                Log.Add(new GameEvent(GameEventType.Defeat) { Text = "Усі маги вибули. Поразка." });
                return true;
            }
            if (!s.LivingMonsters.Any())
            {
                OnBattleWon();
                return true;
            }
            return false;
        }

        private void OnBattleWon()
        {
            var s = State;
            s.ActiveSeat = -1;
            s.Pending = null;
            foreach (var a in ActiveAbilities()) a.OnBattleEnd(this);

            // ---- Хвиля всередині поверху: +3 HP живим, вибулі лишаються поза грою, без руни ----
            if (!s.IsLastWaveOfFloor && !s.IsLastBattle)
            {
                Log.Add(new GameEvent(GameEventType.BattleWon) { Value = s.BattleIndex + 1, Text = $"Хвилю {s.Wave + 1}/{s.WavesInFloor} відбито!" });
                foreach (var m in s.AllMages)
                {
                    m.Statuses.Clear();
                    if (m.Eliminated || !m.IsAlive) continue;
                    int amount = Rules.WaveRewardHp;
                    foreach (var a in m.Abilities) amount = a.ModifyHeal(this, m, amount);
                    Heal(m, amount, "перепочинок між хвилями");
                }
                SetPhase(BattlePhase.WaveWon);
                return;
            }

            Log.Add(new GameEvent(GameEventType.BattleWon) { Value = s.Floor + 1, Text = $"Поверх {s.Floor + 1} пройдено!" });

            foreach (var m in s.AllMages)
            {
                m.Statuses.Clear();
                if (m.Eliminated || !m.IsAlive)
                {
                    m.Eliminated = false;
                    m.Hp = Math.Min(m.MaxHp, Rules.ReviveHp);
                    Log.Add(new GameEvent(GameEventType.MageRevived) { Target = m.Ref, Value = m.Hp, Text = $"{m.DisplayName} повертається з {m.Hp} здоров'я" });
                }
                else
                {
                    if (DrawRune(m, null))
                        Log.Add(new GameEvent(GameEventType.RewardGranted) { Target = m.Ref, Element = m.Hand[m.Hand.Count - 1], Text = $"{m.DisplayName} отримує руну «{Rules.RuneName(m.Hand[m.Hand.Count - 1])}»" });
                    int amount = Rules.BattleRewardHp;
                    foreach (var a in m.Abilities) amount = a.ModifyHeal(this, m, amount);
                    Heal(m, amount, "нагорода");
                }
            }

            if (s.IsLastBattle)
            {
                SetPhase(BattlePhase.CampaignWon);
                Log.Add(new GameEvent(GameEventType.CampaignWon) { Text = "Розлом запечатано. Башту підкорено!" });
                return;
            }
            s.BlessingDiscardOffered = s.Floor == Rules.BlessingDiscardAfterFloor - 1;
            SetPhase(BattlePhase.BattleWon);
        }

        // =====================================================================
        //  Шкода, лікування, стани
        // =====================================================================

        public List<AbilityBase> ActiveAbilities()
        {
            var list = new List<AbilityBase>(State.GlobalAbilities);
            foreach (var m in State.AllMages) if (m.IsAlive && !m.Eliminated) list.AddRange(m.Abilities);
            foreach (var m in State.Board) if (m != null && m.IsAlive) list.AddRange(m.Abilities);
            return list;
        }

        public int DealDamage(DamageContext ctx)
        {
            var target = ctx.Target;
            if (target == null || !target.IsAlive) return 0;
            if (target is MageState tm && tm.Eliminated) return 0;

            if (!ctx.Direct && ctx.Attacker != null)
            {
                if (ctx.Attacker.Has(StatusType.Weakened)) ctx.OutgoingMods.Add(new DamageMod(-1, "Ослаблений"));
                foreach (var a in ActiveAbilities()) a.ModifyOutgoing(this, ctx);
            }
            if (target.Has(StatusType.Shattered)) ctx.IncomingMods.Add(new DamageMod(1, "Розколотий"));
            int stone = target.Stacks(StatusType.StoneSkin);
            if (stone > 0) ctx.IncomingMods.Add(new DamageMod(-stone, "Кам'яна шкіра"));
            foreach (var a in ActiveAbilities()) a.ModifyIncoming(this, ctx);

            int total = ctx.Base + ctx.OutgoingMods.Sum(m => m.Amount) + ctx.IncomingMods.Sum(m => m.Amount);
            total = Math.Max(0, total);
            if (target is MonsterState tmon) tmon.HitsThisRound++;

            var shield = target.GetStatus(StatusType.Shield);
            if (shield != null && total > 0)
            {
                int absorbed;
                if (shield.Param == 1)
                {
                    if (ctx.IsMonsterAttack) { absorbed = total; RemoveStatus(target, shield); }
                    else absorbed = 0;
                }
                else
                {
                    absorbed = Math.Min(shield.Stacks, total);
                    shield.Stacks -= absorbed;
                    if (shield.Stacks <= 0) RemoveStatus(target, shield);
                }
                if (absorbed > 0)
                {
                    total -= absorbed;
                    ctx.Absorbed = absorbed;
                    Log.Add(new GameEvent(GameEventType.ShieldAbsorbed) { Target = target.Ref, Value = absorbed, Text = $"Щит {target.DisplayName} поглинає {absorbed}" });
                }
            }

            ctx.Final = total;
            if (total <= 0)
            {
                Log.Add(new GameEvent(GameEventType.DamageDealt) { Source = ctx.Attacker?.Ref ?? UnitRef.None, Target = target.Ref, Value = 0, Text = $"{target.DisplayName}: шкода повністю заблокована" });
                return 0;
            }

            target.Hp -= total;
            if (target is MonsterState mon) mon.DamageEventsTaken++;
            string src = ctx.SourceName != null ? $" ({ctx.SourceName})" : "";
            Log.Add(new GameEvent(GameEventType.DamageDealt) { Source = ctx.Attacker?.Ref ?? UnitRef.None, Target = target.Ref, Value = total, Element = ctx.Element, Form = ctx.Form, Text = $"{target.DisplayName} −{total}{src}" });

            foreach (var a in ActiveAbilities()) a.OnDamaged(this, ctx, total);

            if (target.Hp <= 0)
            {
                bool saved = false;
                foreach (var a in target.Abilities.ToList())
                {
                    if (a.OnLethal(this, target)) { saved = true; break; }
                }
                if (saved)
                {
                    target.Hp = 1;
                }
                else
                {
                    target.Hp = 0;
                    Kill(target);
                }
            }
            return total;
        }

        private void Kill(UnitState unit)
        {
            if (unit is MageState mage)
            {
                mage.Eliminated = true;
                mage.Statuses.Clear();
                Log.Add(new GameEvent(GameEventType.MageDied) { Target = mage.Ref, Text = $"{mage.DisplayName} вибуває з битви!" });
            }
            else if (unit is MonsterState mon)
            {
                Log.Add(new GameEvent(GameEventType.MonsterDied) { Target = mon.Ref, Text = $"{mon.DisplayName} переможений" });
                foreach (var a in mon.Abilities.ToList()) a.OnDeath(this, mon);
                foreach (var a in ActiveAbilities()) a.OnDeath(this, mon);
            }
        }

        public void Heal(MageState mage, int amount, string reason)
        {
            if (mage == null || !mage.IsAlive || mage.Eliminated || amount <= 0) return;
            int before = mage.Hp;
            mage.Hp = Math.Min(mage.MaxHp, mage.Hp + amount);
            int healed = mage.Hp - before;
            if (healed > 0) Log.Add(new GameEvent(GameEventType.Healed) { Target = mage.Ref, Value = healed, Text = $"{mage.DisplayName} +{healed} HP ({reason})" });
        }

        public void ChangeConcentration(MageState mage, int delta, string reason)
        {
            if (mage == null) return;
            int before = mage.Concentration;
            mage.Concentration = Rules.ClampConcentration(mage.Concentration + delta);
            int diff = mage.Concentration - before;
            Log.Add(new GameEvent(GameEventType.ConcentrationChanged) { Target = mage.Ref, Value = diff, Text = $"{mage.DisplayName}: концентрація {before} → {mage.Concentration} ({reason})" });
        }

        public bool ApplyStatus(UnitState target, StatusType type, int stacks, int rounds, string source, int param = 0)
        {
            if (target == null || !target.IsAlive) return false;
            if (target is MageState m && m.Eliminated) return false;
            foreach (var a in ActiveAbilities())
            {
                if (a.BlocksStatus(this, target, type))
                {
                    Log.Add(new GameEvent(GameEventType.Info) { Target = target.Ref, Text = $"{target.DisplayName}: імунітет до стану «{Rules.StatusName(type)}»" });
                    return false;
                }
            }
            var existing = target.GetStatus(type);
            if (existing != null)
            {
                existing.Stacks = Math.Max(existing.Stacks, stacks);
                existing.RemainingRounds = rounds < 0 || existing.RemainingRounds < 0 ? Math.Min(rounds, existing.RemainingRounds) : Math.Max(rounds, existing.RemainingRounds);
                existing.Param = param;
                existing.Source = source;
            }
            else
            {
                existing = new StatusInstance { Type = type, Stacks = stacks, RemainingRounds = rounds, Source = source, Param = param };
                target.Statuses.Add(existing);
            }
            Log.Add(new GameEvent(GameEventType.StatusApplied) { Target = target.Ref, Status = type, Value = stacks, Text = $"{target.DisplayName}: {existing.Label} ({source})" });
            return true;
        }

        public void RemoveStatus(UnitState target, StatusInstance st)
        {
            if (target.Statuses.Remove(st))
                Log.Add(new GameEvent(GameEventType.StatusRemoved) { Target = target.Ref, Status = st.Type, Text = $"{target.DisplayName}: знято «{Rules.StatusName(st.Type)}»" });
        }

        // =====================================================================
        //  Заклинання: ціна, цілі, превʼю
        // =====================================================================

        public int GetSpellCost(MageState caster, RuneType element, RuneType form)
        {
            int cost = Rules.BaseCost(element) + Rules.BaseCost(form);
            int ei = State.ElementIndex(element);
            if (ei >= 0) cost += State.ElementSurcharge[ei];
            foreach (var a in ActiveAbilities()) cost = a.ModifySpellCost(this, caster, element, form, cost);
            return Math.Max(0, cost);
        }

        /// <summary>Промінь: обраний + та сама колонка іншого ряду. Сфера: обраний + сусіди в межах ряду.</summary>
        public List<int> GetSpellTargets(RuneType form, int primaryPos)
        {
            var result = new List<int>();
            if (State.MonsterAt(primaryPos) == null) return result;
            result.Add(primaryPos);
            if (form == RuneType.Beam)
            {
                int other = (primaryPos + Rules.RowSize) % Rules.BoardPositions;
                if (State.MonsterAt(other) != null) result.Add(other);
            }
            else if (form == RuneType.Sphere)
            {
                int col = primaryPos % Rules.RowSize;
                if (col > 0 && State.MonsterAt(primaryPos - 1) != null) result.Add(primaryPos - 1);
                if (col < Rules.RowSize - 1 && State.MonsterAt(primaryPos + 1) != null) result.Add(primaryPos + 1);
            }
            return result;
        }

        public bool IsSilenced(MageState mage, RuneType element)
        {
            var s = mage.GetStatus(StatusType.Silence);
            if (s != null && s.Param == (int)element) return true;
            foreach (var a in ActiveAbilities()) if (a.Owner == mage && !a.CanCastElement(mage, element)) return true;
            return false;
        }

        public int GetSwapLimit(MageState m)
        {
            int limit = 1;
            foreach (var a in m.Abilities) limit = a.ModifySwapLimit(m, limit);
            return limit;
        }

        public int GetSwapCost(MageState m)
        {
            int cost = 0;
            foreach (var a in m.Abilities) cost = a.ModifySwapCost(m, cost);
            return cost;
        }

        public int GetHandLimit(MageState m)
        {
            int limit = Rules.HandLimit;
            foreach (var a in m.Abilities) limit = a.ModifyHandLimit(m, limit);
            return limit;
        }

        public bool IsActiveHumanDecision(int seat) => State.Phase == BattlePhase.MagesTurn && State.ActiveSeat == seat;

        // ---------------- Валідація дій ----------------

        public string ValidateSwap(int seat, int handIndex, RuneCategory cat, int slotIndex)
        {
            var s = State;
            var mage = seat >= 0 && seat < s.Mages.Length ? s.Mages[seat] : null;
            if (mage == null) return "Немає такого мага.";
            if (handIndex < 0 || handIndex >= mage.Hand.Count) return "Немає такої руни в руці.";
            if (slotIndex < 0 || slotIndex > 1) return "Немає такої комірки.";
            if (Rules.CategoryOf(mage.Hand[handIndex]) != cat)
                return cat == RuneCategory.Element ? "Сюди кладуться лише руни Стихій." : "Сюди кладуться лише руни Форм.";
            if (s.Phase == BattlePhase.Setup) return null;
            if (s.Phase != BattlePhase.MagesTurn || s.ActiveSeat != seat) return "Зараз не хід цього мага.";
            if (s.Step != TurnStep.SwapRune) return "Руну можна змінити лише до застосування заклинання.";
            if (mage.SwapsThisTurn >= GetSwapLimit(mage))
                return GetSwapLimit(mage) == 0 ? "«Пакт мовчання»: цей маг не може змінювати руни." : "Цього ходу руну вже змінено.";
            int cost = GetSwapCost(mage);
            if (cost > mage.Concentration) return $"Зміна руни коштує {cost} концентрації.";
            return null;
        }

        public void ExecuteSwap(int seat, int handIndex, RuneCategory cat, int slotIndex)
        {
            var mage = State.Mages[seat];
            var rune = mage.Hand[handIndex];
            mage.Hand.RemoveAt(handIndex);
            var old = mage.GetSlot(cat, slotIndex);
            if (old.HasValue) mage.Hand.Add(old.Value);
            mage.SetSlot(cat, slotIndex, rune);
            if (State.Phase == BattlePhase.MagesTurn)
            {
                mage.SwapsThisTurn++;
                int cost = GetSwapCost(mage);
                if (cost > 0) ChangeConcentration(mage, -cost, "«Важка рука»");
            }
            string txt = old.HasValue ? $"{mage.DisplayName}: {Rules.RuneName(old.Value)} → {Rules.RuneName(rune)}" : $"{mage.DisplayName} викладає руну «{Rules.RuneName(rune)}»";
            Log.Add(new GameEvent(GameEventType.RuneSwapped) { Target = mage.Ref, Element = rune, Text = txt });
        }

        public string ValidateCast(int seat, int elementSlot, int formSlot, int targetPos)
        {
            var s = State;
            if (s.Phase != BattlePhase.MagesTurn || s.ActiveSeat != seat) return "Зараз не хід цього мага.";
            if (s.Step != TurnStep.SwapRune) return "Цього ходу вже була дія.";
            var mage = s.Mages[seat];
            if (elementSlot < 0 || elementSlot > 1 || formSlot < 0 || formSlot > 1) return "Невірна комірка.";
            var el = mage.ElementSlots[elementSlot];
            var form = mage.FormSlots[formSlot];
            if (!el.HasValue) return "У комірці немає руни Стихії.";
            if (!form.HasValue) return "У комірці немає руни Форми.";
            if (IsSilenced(mage, el.Value)) return $"Стихія «{Rules.RuneName(el.Value)}» під забороною (Мовчання).";
            int cost = GetSpellCost(mage, el.Value, form.Value);
            if (cost > mage.Concentration) return $"Не вистачає концентрації: {cost} > {mage.Concentration}.";
            if (s.MonsterAt(targetPos) == null) return "Ціль недоступна (порожня позиція або переможений монстр).";
            return null;
        }

        public void ExecuteCast(int seat, int elementSlot, int formSlot, int targetPos)
        {
            var s = State;
            var mage = s.Mages[seat];
            var el = mage.ElementSlots[elementSlot].Value;
            var form = mage.FormSlots[formSlot].Value;
            int cost = GetSpellCost(mage, el, form);
            ChangeConcentration(mage, -cost, $"{Rules.RuneName(el)} + {Rules.RuneName(form)}");
            mage.SpellsThisRound++;
            mage.ActedThisTurn = true;

            var roll = Dice.Roll(DieType.D6, $"Шкода «{Rules.RuneName(el)} + {Rules.RuneName(form)}»");
            var pending = new PendingSpell
            {
                Seat = seat, Element = el, Form = form, TargetPos = targetPos, Cost = cost, Roll = roll.Value,
                Targets = GetSpellTargets(form, targetPos)
            };
            var luck = mage.Abilities.OfType<IRollModifier>().FirstOrDefault();
            if (luck != null)
            {
                pending.CanReroll = luck.CanReroll(this, mage);
                pending.CanMirror = luck.CanMirror(this, mage);
            }
            if (pending.CanReroll || pending.CanMirror)
            {
                s.Pending = pending;
                s.Step = TurnStep.RollDecision;
                return;
            }
            ResolveSpell(pending);
        }

        public string ValidateRollChoice(int seat, RollChoice choice)
        {
            var p = State.Pending;
            if (State.Step != TurnStep.RollDecision || p == null || p.Seat != seat) return "Немає кидка, що очікує рішення.";
            if (choice == RollChoice.Reroll && !p.CanReroll) return "Перекидання недоступне.";
            if (choice == RollChoice.Mirror && !p.CanMirror) return "«Дзеркало фортуни» недоступне.";
            return null;
        }

        public void ExecuteRollChoice(int seat, RollChoice choice)
        {
            var p = State.Pending;
            var mage = State.Mages[seat];
            var luck = mage.Abilities.OfType<IRollModifier>().FirstOrDefault();
            if (choice == RollChoice.Reroll)
            {
                var second = Dice.Roll(DieType.D6, "«Щастя сортувальника»: перекидання");
                p.Roll = Math.Max(p.Roll, second.Value);
                luck?.ConsumeReroll(this, mage);
                Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Target = mage.Ref, Value = p.Roll, Text = $"Обрано кращий результат: {p.Roll}" });
            }
            else if (choice == RollChoice.Mirror)
            {
                int before = p.Roll;
                p.Roll = 7 - p.Roll;
                luck?.ConsumeMirror(this, mage);
                Log.Add(new GameEvent(GameEventType.BlessingTriggered) { Target = mage.Ref, Value = p.Roll, Text = $"«Дзеркало фортуни»: {before} → {p.Roll}" });
            }
            ResolveSpell(p);
        }

        private void ResolveSpell(PendingSpell p)
        {
            var s = State;
            var mage = s.Mages[p.Seat];
            s.Pending = null;
            s.Step = TurnStep.Done;

            var result = new SpellResult { Element = p.Element, Form = p.Form, Roll = p.Roll, PrimaryPos = p.TargetPos };
            result.Targets.AddRange(p.Targets);
            int baseDamage = p.Roll + Rules.FlatDamageBonus(p.Element);
            Log.Add(new GameEvent(GameEventType.SpellCast) { Source = mage.Ref, Target = UnitRef.Monster(p.TargetPos), Element = p.Element, Form = p.Form, Value = baseDamage, Text = $"{mage.DisplayName}: {Rules.RuneName(p.Element)} + {Rules.RuneName(p.Form)} — базова шкода {baseDamage}" });

            foreach (int pos in p.Targets)
            {
                var target = s.MonsterAt(pos);
                if (target == null) continue;
                int dealt = DealDamage(new DamageContext
                {
                    Attacker = mage, Target = target, Base = baseDamage, IsSpell = true, Element = p.Element, Form = p.Form,
                    IsPrimaryTarget = pos == p.TargetPos, SourceName = Rules.RuneName(p.Element)
                });
                result.DamageByPos[pos] = dealt;
            }

            // Стихійний перегрів
            if (s.Setup.ElementalOverheat && p.Roll == 6)
            {
                var primary = s.MonsterAt(p.TargetPos);
                if (primary != null)
                {
                    switch (p.Element)
                    {
                        case RuneType.Fire: ApplyStatus(primary, StatusType.Burn, 2, 2, "Стихійний перегрів"); break;
                        case RuneType.Ice: ApplyStatus(primary, StatusType.Weakened, 1, 1, "Стихійний перегрів"); break;
                        case RuneType.Water: ApplyStatus(primary, StatusType.Shattered, 1, 1, "Стихійний перегрів"); break;
                        case RuneType.Lightning: ApplyStatus(primary, StatusType.Stunned, 1, -1, "Стихійний перегрів"); break;
                    }
                }
            }

            foreach (var a in ActiveAbilities()) a.AfterSpellCast(this, mage, result);
            EndMageTurn();
        }

        public string ValidateMeditate(int seat)
        {
            if (State.Phase != BattlePhase.MagesTurn || State.ActiveSeat != seat) return "Зараз не хід цього мага.";
            if (State.Step != TurnStep.SwapRune) return "Цього ходу вже була дія.";
            return null;
        }

        public void ExecuteMeditate(int seat)
        {
            var mage = State.Mages[seat];
            mage.MeditatedLastTurn = true;
            mage.MeditationsThisBattle++;
            mage.ActedThisTurn = true;
            Log.Add(new GameEvent(GameEventType.Meditated) { Target = mage.Ref, Text = $"{mage.DisplayName} медитує (наступного ходу більше концентрації)" });
            EndMageTurn();
        }

        public string ValidatePass(int seat) => ValidateMeditate(seat);

        public void ExecutePass(int seat)
        {
            Log.Info($"{State.Mages[seat].DisplayName} завершує хід без дії.");
            EndMageTurn();
        }

        public IActiveAbility FindActive(MageState mage, string abilityId) =>
            mage.Abilities.FirstOrDefault(a => a.Id == abilityId) as IActiveAbility;

        public string ValidateActive(int seat, string abilityId, int targetPos)
        {
            if (State.Phase != BattlePhase.MagesTurn || State.ActiveSeat != seat) return "Зараз не хід цього мага.";
            if (State.Step != TurnStep.SwapRune) return "Цього ходу вже була дія.";
            var mage = State.Mages[seat];
            var active = FindActive(mage, abilityId);
            if (active == null) return "Немає такої здібності.";
            if (!active.CanUse(this, mage, out var reason)) return reason;
            if (active.NeedsMonsterTarget && State.MonsterAt(targetPos) == null) return "Оберіть живого монстра.";
            return null;
        }

        public void ExecuteActive(int seat, string abilityId, int targetPos)
        {
            var mage = State.Mages[seat];
            FindActive(mage, abilityId).Use(this, mage, targetPos);
            CheckBattleEnd();
        }

        public string ValidateConfirmSetup() => State.Phase == BattlePhase.Setup ? null : "Підготовка вже завершена.";
        public void ExecuteConfirmSetup() => BeginRound();

        public string ValidateContinue()
        {
            if (State.Phase != BattlePhase.BattleWon && State.Phase != BattlePhase.WaveWon) return "Битва ще триває.";
            foreach (var m in State.AllMages)
                if (m.Hand.Count > GetHandLimit(m)) return $"{m.DisplayName}: у руці {m.Hand.Count} рун, ліміт {GetHandLimit(m)} — здайте зайві.";
            return null;
        }

        public void ExecuteContinue()
        {
            State.BlessingDiscardOffered = false;
            BeginBattle(State.BattleIndex + 1);
        }

        public string ValidateDiscardRune(int seat, int handIndex)
        {
            if (State.Phase != BattlePhase.BattleWon && State.Phase != BattlePhase.WaveWon) return "Руни здаються лише між битвами.";
            var m = State.Mages[seat];
            if (m == null || handIndex < 0 || handIndex >= m.Hand.Count) return "Немає такої руни.";
            return null;
        }

        public void ExecuteDiscardRune(int seat, int handIndex)
        {
            var m = State.Mages[seat];
            var r = m.Hand[handIndex];
            m.Hand.RemoveAt(handIndex);
            State.RunePool.Add(r);
            Log.Info($"{m.DisplayName} здає руну «{Rules.RuneName(r)}» у спільний запас");
        }

        public string ValidateDiscardBlessing(int seat)
        {
            if (State.Phase != BattlePhase.BattleWon || !State.BlessingDiscardOffered) return "Скинути карту можна лише після третьої битви.";
            var m = State.Mages[seat];
            if (m == null || m.Blessing == null) return "У мага немає карти.";
            return null;
        }

        public void ExecuteDiscardBlessing(int seat)
        {
            var m = State.Mages[seat];
            Log.Info($"{m.DisplayName} скидає карту «{m.Blessing.Name}»");
            m.Abilities.RemoveAll(a => a.Id == m.Blessing.Id);
            m.Blessing = null;
        }

        /// <summary>Інваріанти для тестів: концентрація 0..5, HP 0..max.</summary>
        public string CheckInvariants()
        {
            foreach (var m in State.AllMages)
            {
                if (m.Concentration < 0 || m.Concentration > Rules.MaxConcentration) return $"{m.DisplayName}: концентрація {m.Concentration}";
                if (m.Hp < 0 || m.Hp > m.MaxHp) return $"{m.DisplayName}: HP {m.Hp}/{m.MaxHp}";
            }
            foreach (var m in State.Board)
                if (m != null && (m.Hp < 0 || m.Hp > m.MaxHp)) return $"{m.DisplayName}: HP {m.Hp}/{m.MaxHp}";
            return null;
        }
    }

    /// <summary>Карти, що впливають на кидок шкоди після того, як гравець побачив результат.</summary>
    public interface IRollModifier
    {
        bool CanReroll(RulesEngine e, MageState mage);
        bool CanMirror(RulesEngine e, MageState mage);
        void ConsumeReroll(RulesEngine e, MageState mage);
        void ConsumeMirror(RulesEngine e, MageState mage);
    }
}
