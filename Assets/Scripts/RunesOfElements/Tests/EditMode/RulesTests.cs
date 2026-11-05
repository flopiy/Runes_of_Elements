using System.Linq;
using NUnit.Framework;
using RunesOfElements.Core;

namespace RunesOfElements.Tests
{
    internal sealed class Harness
    {
        public readonly RulesEngine E;
        public readonly ScriptedRandom Rng;
        public readonly GameContent C;
        public GameState S => E.State;

        public Harness(bool overheat = false, string blessingSeat0 = null)
        {
            C = DefaultContent.Create();
            var setup = GameSetup.Default(0, 123);
            setup.ElementalOverheat = overheat;
            setup.FreeStartingPlacement = true;
            string[] ids = { "pyromancer", "hydromancer", "electromancer", "cryomancer" };
            for (int i = 0; i < 4; i++) setup.Seats[i].MageId = ids[i];
            Rng = new ScriptedRandom(new SeededRandom(5));
            E = new RulesEngine(C, setup, Rng);
            E.StartCampaign();
            E.ClearBoard();
            foreach (var m in S.AllMages)
            {
                m.Hand.Clear();
                m.ElementSlots[0] = m.ElementSlots[1] = null;
                m.FormSlots[0] = m.FormSlots[1] = null;
            }
            if (blessingSeat0 != null) GiveBlessing(0, blessingSeat0);
        }

        public void GiveBlessing(int seat, string id)
        {
            var m = S.Mages[seat];
            m.Blessing = C.Blessing(id);
            m.Abilities.Add(AbilityRegistry.Create(id, m));
            int max = Rules.MaxHp;
            foreach (var a in m.Abilities) max = a.ModifyMaxHp(m, max);
            m.MaxHp = max;
            m.Hp = max;
        }

        public MonsterState Mon(string id, int pos) => E.PlaceMonsterAt(C.Monster(id), pos);
        public MageState Mage(int seat) => S.Mages[seat];

        public void Equip(int seat, RuneType el, RuneType form)
        {
            Mage(seat).ElementSlots[0] = el;
            Mage(seat).FormSlots[0] = form;
        }

        public void Start() => Ok(new ConfirmSetupCommand());

        public void Ok(IGameCommand cmd)
        {
            var r = E.Submit(cmd);
            Assert.IsTrue(r.Ok, $"{cmd.GetType().Name} відхилено: {r.Reason}");
            Assert.IsNull(E.CheckInvariants());
        }

        public void Fail(IGameCommand cmd)
        {
            var r = E.Submit(cmd);
            Assert.IsFalse(r.Ok, $"{cmd.GetType().Name} мала бути відхилена");
        }

        /// <summary>Пропускає ходи всіх магів, що залишились у фазі.</summary>
        public void PassRemainingMages()
        {
            int guard = 0;
            int round = S.Round;
            while (S.Phase == BattlePhase.MagesTurn && S.ActiveSeat >= 0 && S.Round == round && guard++ < 10)
                Ok(new EndTurnCommand(S.ActiveSeat));
        }
    }

    public class ConcentrationTests
    {
        [Test]
        public void StartsAt4_GainsOnTurn_CapsAt5()
        {
            var h = new Harness();
            h.Mon("wolf", 0);
            h.Start();
            Assert.AreEqual(0, h.S.ActiveSeat);
            Assert.AreEqual(5, h.Mage(0).Concentration);
            Assert.AreEqual(4, h.Mage(1).Concentration);
        }

        [Test]
        public void Meditation_GivesPlus2_NextTurn_WithinCap()
        {
            var h = new Harness();
            h.Mon("goblin", 0);
            h.Start();
            h.Mage(0).Concentration = 1;
            h.Ok(new MeditateCommand(0));
            h.Rng.Push(4); // d4 → атакує мага 4
            h.PassRemainingMages();
            Assert.AreEqual(0, h.S.ActiveSeat);
            Assert.AreEqual(3, h.Mage(0).Concentration);

            h.Mage(0).Concentration = 4;
            h.Ok(new MeditateCommand(0));
            h.Rng.Push(4);
            h.PassRemainingMages();
            Assert.AreEqual(5, h.Mage(0).Concentration, "надлишок згоряє");
        }

        [Test]
        public void InsufficientConcentration_Rejected_NeverNegative()
        {
            var h = new Harness();
            h.Mon("wolf", 0);
            h.Equip(1, RuneType.Fire, RuneType.Sphere);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            Assert.AreEqual(1, h.S.ActiveSeat);
            Assert.AreEqual(5, h.E.GetSpellCost(h.Mage(1), RuneType.Fire, RuneType.Sphere));
            h.Mage(1).Concentration = 3;
            h.Fail(new CastSpellCommand(1, 0, 0, 0));
            Assert.AreEqual(3, h.Mage(1).Concentration);
        }
    }

    public class TurnStructureTests
    {
        [Test]
        public void OnlyOneRuneSwapPerTurn()
        {
            var h = new Harness();
            h.Mon("wolf", 0);
            h.Mage(0).Hand.AddRange(new[] { RuneType.Fire, RuneType.Water });
            h.Start();
            h.Ok(new SwapRuneCommand(0, 0, RuneCategory.Element, 0));
            h.Fail(new SwapRuneCommand(0, 0, RuneCategory.Element, 1));
        }

        [Test]
        public void CannotSwapIntoWrongCategory()
        {
            var h = new Harness();
            h.Mon("wolf", 0);
            h.Mage(0).Hand.Add(RuneType.Beam);
            h.Start();
            h.Fail(new SwapRuneCommand(0, 0, RuneCategory.Element, 0));
        }

        [Test]
        public void CannotSwapOrCastAfterAction()
        {
            var h = new Harness();
            h.Mon("rotten_guard", 0);
            h.Equip(0, RuneType.Water, RuneType.Beam);
            h.Mage(0).Hand.Add(RuneType.Ice);
            h.Start();
            h.Rng.Push(2);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.IsNotNull(h.E.ValidateSwap(0, 0, RuneCategory.Element, 1));
            h.Fail(new CastSpellCommand(0, 0, 0, 0));
        }

        [Test]
        public void SpellCosts()
        {
            var h = new Harness();
            Assert.AreEqual(4, h.E.GetSpellCost(h.Mage(0), RuneType.Fire, RuneType.Sphere), "Піромант");
            Assert.AreEqual(5, h.E.GetSpellCost(h.Mage(1), RuneType.Fire, RuneType.Sphere));
            Assert.AreEqual(2, h.E.GetSpellCost(h.Mage(1), RuneType.Water, RuneType.Beam));
        }
    }

    public class GeometryTests
    {
        [Test]
        public void Beam_HitsSameColumnOtherRow()
        {
            var h = new Harness();
            h.Mon("wolf", 1); h.Mon("wolf", 5);
            CollectionAssert.AreEquivalent(new[] { 1, 5 }, h.E.GetSpellTargets(RuneType.Beam, 1));
            CollectionAssert.AreEquivalent(new[] { 5, 1 }, h.E.GetSpellTargets(RuneType.Beam, 5));
            h.S.Board[5].Hp = 0;
            CollectionAssert.AreEquivalent(new[] { 1 }, h.E.GetSpellTargets(RuneType.Beam, 1));
        }

        [Test]
        public void Sphere_HitsRowNeighbours_NotAcrossRows()
        {
            var h = new Harness();
            h.Mon("wolf", 2); h.Mon("wolf", 3); h.Mon("wolf", 4); h.Mon("wolf", 5); h.Mon("wolf", 6);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, h.E.GetSpellTargets(RuneType.Sphere, 3));
            CollectionAssert.AreEquivalent(new[] { 4, 5, 6 }, h.E.GetSpellTargets(RuneType.Sphere, 5));
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, h.E.GetSpellTargets(RuneType.Sphere, 4));
        }

        [Test]
        public void OneRoll_SameDamageForAllTargets_FirePlus2()
        {
            var h = new Harness();
            h.Mon("wolf", 4); h.Mon("wolf", 5); h.Mon("wolf", 6);
            h.Equip(1, RuneType.Fire, RuneType.Sphere);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            h.Rng.Push(3);
            h.Ok(new CastSpellCommand(1, 0, 0, 5));
            Assert.AreEqual(2, h.S.Board[4].Hp);
            Assert.AreEqual(2, h.S.Board[5].Hp);
            Assert.AreEqual(2, h.S.Board[6].Hp);
        }

        [Test]
        public void DefeatedMonster_LeavesHole_NoShift_NotTargetable()
        {
            var h = new Harness();
            h.Mon("spider", 1); h.Mon("wolf", 2);
            h.Equip(1, RuneType.Fire, RuneType.Beam);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(1, 0, 0, 1));
            Assert.IsNull(h.S.MonsterAt(1));
            Assert.IsNotNull(h.S.MonsterAt(2));
            Assert.AreEqual(2, h.S.Board[2].Position);
            Assert.IsNotNull(h.E.ValidateCast(2, 0, 0, 1));
        }
    }

    public class RoundFlowTests
    {
        [Test]
        public void MonsterTarget_RerollsDeadMage_ThenRecoveryCapped()
        {
            var h = new Harness();
            h.Mon("goblin", 0);
            h.Mage(1).Hp = 0; h.Mage(1).Eliminated = true;
            h.Start();
            h.Mage(0).Hp = 9;
            h.Rng.Push(2, 1); // d4=2 (вибулий) → перекид → 1
            h.PassRemainingMages();
            // гоблін вдарив мага 1 на 2 (9 → 7), потім +5 → 10 (кап)
            Assert.AreEqual(10, h.Mage(0).Hp);
            Assert.AreEqual(0, h.Mage(1).Hp, "вибулий не лікується");
            Assert.IsTrue(h.E.Log.Events.Any(e => e.Text.Contains("Клітина 2")));
        }

        [Test]
        public void Victory_WhenLastMonsterDies_MidSphere()
        {
            var h = new Harness();
            h.Mon("spider", 0); h.Mon("goblin", 1);
            h.Equip(1, RuneType.Fire, RuneType.Sphere);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(1, 0, 0, 0));
            Assert.AreEqual(BattlePhase.WaveWon, h.S.Phase, "1-ша хвиля поверху виграна");
        }

        [Test]
        public void Defeat_Immediately_WhenLastMageFalls()
        {
            var h = new Harness();
            h.Mon("shadow_assassin", 0);
            for (int i = 0; i < 4; i++) h.Mage(i).Hp = 2;
            h.S.Board[0].Hp = 1;
            h.Equip(0, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Rng.Push(1);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(BattlePhase.Defeat, h.S.Phase, "Розрив тіні вбив усіх — поразка пріоритетніша за перемогу");
        }

        [Test]
        public void BattleRewards_HpRuneAndRevive_HandLimit()
        {
            var h = new Harness();
            h.Mon("spider", 0);
            h.Mage(2).Hp = 0; h.Mage(2).Eliminated = true;
            h.Mage(1).Hp = 5;
            h.Equip(0, RuneType.Fire, RuneType.Beam);
            h.Mage(1).Hand.AddRange(new[] { RuneType.Fire, RuneType.Fire, RuneType.Fire, RuneType.Fire, RuneType.Fire, RuneType.Fire });
            h.S.BattleIndex = 2; // остання хвиля 1-го поверху → повна нагорода
            h.Start();
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(BattlePhase.BattleWon, h.S.Phase);
            Assert.AreEqual(8, h.Mage(1).Hp, "+3");
            Assert.AreEqual(3, h.Mage(2).Hp, "повернення з 3");
            Assert.AreEqual(7, h.Mage(1).Hand.Count);
            h.Fail(new ContinueCampaignCommand());
            h.Ok(new DiscardRuneCommand(1, 0));
            h.Ok(new ContinueCampaignCommand());
            Assert.AreEqual(3, h.S.BattleIndex);
            Assert.AreEqual(1, h.S.Floor);
            Assert.AreEqual(0, h.S.Wave);
        }
    }

    public class TowerTests
    {
        [Test]
        public void Tower_Has10Floors3Waves()
        {
            var c = DefaultContent.Create();
            Assert.AreEqual(10, c.Tower.Count);
            Assert.IsTrue(c.Tower.All(f => f.Waves.Count == Rules.WavesPerFloor));
            Assert.AreEqual(30, c.Campaign.Count);
            Assert.IsTrue(c.Tower[9].Waves[2].Boss, "бос на останній хвилі 10-го поверху");
        }

        [Test]
        public void WaveClear_NoRune_EliminatedStayOut()
        {
            var h = new Harness();
            h.Mon("spider", 0);
            h.Mage(2).Hp = 0; h.Mage(2).Eliminated = true;
            h.Mage(1).Hp = 5;
            h.Equip(0, RuneType.Fire, RuneType.Beam);
            h.Start();
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(BattlePhase.WaveWon, h.S.Phase);
            Assert.AreEqual(8, h.Mage(1).Hp, "+3 між хвилями");
            Assert.AreEqual(0, h.Mage(1).Hand.Count, "руну між хвилями не дають");
            h.Ok(new ContinueCampaignCommand());
            Assert.AreEqual(1, h.S.Wave);
            Assert.IsTrue(h.Mage(2).Eliminated, "вибулий лишається поза грою до кінця поверху");
            Assert.AreEqual(0, h.Mage(2).Hp);
        }

        [Test]
        public void FloorClear_RevivesEliminated_GivesRune()
        {
            var h = new Harness();
            h.Mon("spider", 0);
            h.S.BattleIndex = 2;
            h.Mage(3).Hp = 0; h.Mage(3).Eliminated = true;
            h.Equip(0, RuneType.Fire, RuneType.Beam);
            h.Start();
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(BattlePhase.BattleWon, h.S.Phase);
            Assert.AreEqual(3, h.Mage(3).Hp);
            Assert.IsFalse(h.Mage(3).Eliminated);
            Assert.AreEqual(1, h.Mage(1).Hand.Count);
        }

        [Test]
        public void DeckReshuffles_WhenEmpty()
        {
            var h = new Harness();
            h.S.Decks[MonsterLevel.Level2].Clear();
            var drawn = h.E.DrawMonsters(MonsterLevel.Level2, 2);
            Assert.AreEqual(2, drawn.Count);
            Assert.IsTrue(drawn.All(d => d.Level == MonsterLevel.Level2));
        }

        [Test]
        public void BlessingDiscard_OfferedOnlyAfterFloor3()
        {
            var h = new Harness(blessingSeat0: "curse_heavy_hand");
            h.Mon("spider", 0);
            h.S.BattleIndex = 8; // поверх 3, хвиля 3
            h.Equip(0, RuneType.Fire, RuneType.Beam);
            h.Start();
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(BattlePhase.BattleWon, h.S.Phase);
            Assert.IsTrue(h.S.BlessingDiscardOffered);
            h.Ok(new DiscardBlessingCommand(0));
            Assert.IsNull(h.Mage(0).Blessing);
        }

        [Test]
        public void RiftWhisper_TriggersOnFloor3Wave1()
        {
            var setup = GameSetup.Default(1, 7);
            var e = new RulesEngine(DefaultContent.Create(), setup, new SeededRandom(7));
            e.StartCampaign();
            var m = e.State.Mages[0];
            var ab = AbilityRegistry.Create("curse_rift_whisper", m);
            e.State.BattleIndex = 5;
            ab.OnBattleSetup(e);
            Assert.IsFalse(e.Log.Events.Any(ev => ev.Text != null && ev.Text.Contains("Шепіт Розлому")));
            e.State.BattleIndex = 6;
            ab.OnBattleSetup(e);
            Assert.IsTrue(e.Log.Events.Any(ev => ev.Text != null && ev.Text.Contains("Шепіт Розлому")));
        }
    }

    public class AbilityTests
    {
        [Test]
        public void Undying_SurvivesFirstLethal()
        {
            var h = new Harness();
            var g = h.Mon("rotten_guard", 0); h.Mon("wolf", 7);
            h.Equip(0, RuneType.Fire, RuneType.Beam);
            g.Hp = 3;
            h.Start();
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(1, g.Hp);
        }

        [Test]
        public void Necromancer_SummonsOnFirstDamage()
        {
            var h = new Harness();
            h.Mon("necromancer", 0);
            h.Equip(0, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Rng.Push(1, 8); // d6=1, d8=8 → позиція 8
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(2, h.S.LivingMonsters.Count());
        }

        [Test]
        public void IceGolem_FirstHitPerRoundReduced()
        {
            var h = new Harness();
            var g = h.Mon("ice_golem", 0);
            h.Equip(0, RuneType.Water, RuneType.Beam);
            h.Equip(1, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Rng.Push(4);
            h.Ok(new CastSpellCommand(0, 0, 0, 0)); // 4 − 2
            Assert.AreEqual(10, g.Hp);
            h.Rng.Push(4);
            h.Ok(new CastSpellCommand(1, 0, 0, 0)); // повні 4
            Assert.AreEqual(6, g.Hp);
        }

        [Test]
        public void HydromancerShield_AbsorbsNextMonsterAttack()
        {
            var h = new Harness();
            h.Mon("wolf", 0);
            h.Equip(1, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            h.Rng.Push(1);
            h.Ok(new CastSpellCommand(1, 0, 0, 0));
            Assert.IsTrue(h.Mage(1).Has(StatusType.Shield));
            h.Rng.Push(2); // вовк б'є Гідроманта
            h.PassRemainingMages();
            Assert.AreEqual(10, h.Mage(1).Hp);
            Assert.IsFalse(h.Mage(1).Has(StatusType.Shield));
        }

        [Test]
        public void ElectromancerChain_OnlyOnSix()
        {
            var h = new Harness();
            var a = h.Mon("wolf", 0); var b = h.Mon("wolf", 3);
            h.Equip(2, RuneType.Lightning, RuneType.Beam);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            h.Ok(new EndTurnCommand(1));
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(2, 0, 0, 0));
            Assert.AreEqual(1, a.Hp);
            Assert.AreEqual(4, b.Hp, "ланцюг 3 шкоди");
        }

        [Test]
        public void StoneSkin_ReducesEveryHit()
        {
            var h = new Harness();
            var s = h.Mon("ancient_sentinel", 0);
            h.Equip(0, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Rng.Push(5);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(13, s.Hp);
        }

        [Test]
        public void IceQueen_ColdNotBelowOne()
        {
            var h = new Harness();
            h.Mon("boss_ice_queen", 0);
            h.Start();
            Assert.AreEqual(4, h.Mage(0).Concentration, "4+1−1");
            h.Mage(1).Concentration = 0;
            h.Ok(new EndTurnCommand(0));
            Assert.AreEqual(1, h.Mage(1).Concentration, "0+1 = 1, не нижче 1");
        }

        [Test]
        public void AncientGuardian_ImmuneToFreeze()
        {
            var h = new Harness();
            var g = h.Mon("boss_ancient_guardian", 0);
            h.Start();
            h.Ok(new EndTurnCommand(0)); h.Ok(new EndTurnCommand(1)); h.Ok(new EndTurnCommand(2));
            h.Ok(new UseActiveAbilityCommand(3, "mage_cryo", 0));
            Assert.IsFalse(g.Has(StatusType.Frozen));
        }

        [Test]
        public void Overheat_AppliesStatusOnSix_OnlyToSurvivor()
        {
            var h = new Harness(overheat: true);
            var w = h.Mon("ancient_sentinel", 0);
            h.Equip(1, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Ok(new EndTurnCommand(0));
            h.Rng.Push(6);
            h.Ok(new CastSpellCommand(1, 0, 0, 0));
            Assert.IsTrue(w.Has(StatusType.Shattered));
        }
    }

    public class BlessingTests
    {
        [Test]
        public void TitanBlood_Max12()
        {
            var h = new Harness(blessingSeat0: "blessing_titan_blood");
            Assert.AreEqual(12, h.Mage(0).MaxHp);
        }

        [Test]
        public void ThirstForKnowledge_MeditationPlus3_Capped()
        {
            var h = new Harness(blessingSeat0: "blessing_thirst_knowledge");
            h.Mon("goblin", 0);
            h.Start();
            h.Mage(0).Concentration = 0;
            h.Ok(new MeditateCommand(0));
            h.Rng.Push(4);
            h.PassRemainingMages();
            Assert.AreEqual(3, h.Mage(0).Concentration);
        }

        [Test]
        public void VainThirst_HealReducedBy2()
        {
            var h = new Harness(blessingSeat0: "curse_vain_thirst");
            h.Mon("goblin", 0);
            h.Start();
            h.Mage(0).Hp = 2;
            h.Rng.Push(4);
            h.PassRemainingMages();
            Assert.AreEqual(5, h.Mage(0).Hp, "+5−2");
        }

        [Test]
        public void SilencePact_NoSwaps_PlusOneConcentration()
        {
            var h = new Harness(blessingSeat0: "risk_silence_pact");
            h.Mon("goblin", 0);
            h.Mage(0).Hand.Add(RuneType.Fire);
            h.Start();
            Assert.AreEqual(5, h.Mage(0).Concentration);
            h.Fail(new SwapRuneCommand(0, 0, RuneCategory.Element, 0));
        }

        [Test]
        public void HeavyHand_SwapCostsConcentration()
        {
            var h = new Harness(blessingSeat0: "curse_heavy_hand");
            h.Mon("goblin", 0);
            h.Mage(0).Hand.Add(RuneType.Fire);
            h.Start();
            h.Ok(new SwapRuneCommand(0, 0, RuneCategory.Element, 0));
            Assert.AreEqual(4, h.Mage(0).Concentration);
        }

        [Test]
        public void HopeRadiance_SavesOncePerCampaign()
        {
            var h = new Harness(blessingSeat0: "blessing_hope_radiance");
            h.Mon("wolf", 0);
            h.Start();
            h.Mage(0).Hp = 2;
            h.Rng.Push(1, 1, 1);
            h.PassRemainingMages();
            Assert.IsTrue(h.Mage(0).IsAlive);
            Assert.IsFalse(h.Mage(0).Eliminated);
        }

        [Test]
        public void FortuneMirror_FlipsRoll()
        {
            var h = new Harness(blessingSeat0: "risk_fortune_mirror");
            var w = h.Mon("ancient_sentinel", 0);
            h.Equip(0, RuneType.Water, RuneType.Beam);
            h.Start();
            h.Rng.Push(1);
            h.Ok(new CastSpellCommand(0, 0, 0, 0));
            Assert.AreEqual(TurnStep.RollDecision, h.S.Step);
            h.Ok(new ResolveRollCommand(0, RollChoice.Mirror));
            Assert.AreEqual(17 - (6 - 1), w.Hp, "1 → 6, мінус кам'яна шкіра");
        }
    }

    public class SnapshotTests
    {
        [Test]
        public void Snapshot_JsonRoundTrip()
        {
            var setup = GameSetup.Default(1, 11);
            var e = new RulesEngine(DefaultContent.Create(), setup, new SeededRandom(11));
            e.StartCampaign();
            var snap = GameSnapshot.From(e, 5);
            var back = GameSnapshot.FromJson(snap.ToJson());
            Assert.AreEqual(5, back.Version);
            Assert.AreEqual(snap.Phase, back.Phase);
            Assert.AreEqual(10, back.FloorCount);
            Assert.AreEqual(snap.Mages[0].Hand.Count, back.Mages[0].Hand.Count);
            Assert.AreEqual(snap.Monsters.Count(m => m.Exists), back.Monsters.Count(m => m.Exists));
            Assert.AreEqual(snap.Events.Count, back.Events.Count);
        }

        [Test]
        public void CommandCodec_RoundTrip()
        {
            IGameCommand[] cmds =
            {
                new SwapRuneCommand(1, 2, RuneCategory.Form, 1), new CastSpellCommand(2, 1, 0, 6), new ResolveRollCommand(0, RollChoice.Mirror),
                new MeditateCommand(3), new EndTurnCommand(1), new UseActiveAbilityCommand(3, "mage_cryo", 4), new ConfirmSetupCommand(),
                new ContinueCampaignCommand(), new DiscardRuneCommand(2, 3), new DiscardBlessingCommand(0)
            };
            foreach (var c in cmds)
            {
                var d = CommandCodec.Decode(CommandCodec.Encode(c));
                Assert.AreEqual(c.GetType(), d.GetType());
                Assert.AreEqual(UnityEngine.JsonUtility.ToJson(c), UnityEngine.JsonUtility.ToJson(d));
            }
        }
    }

    public class CampaignStressTests
    {
        [Test]
        public void BotsPlay200Campaigns_NoExceptions_InvariantsHold()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var setup = GameSetup.Default(seed % 2 == 0 ? 4 : 0, seed);
                var e = new RulesEngine(DefaultContent.Create(), setup, new SeededRandom(seed));
                e.StartCampaign();
                BotPolicy.AutoPlay(e);
                Assert.IsTrue(e.IsOver, $"seed {seed} завис у {e.State.Phase}");
                Assert.LessOrEqual(e.State.Round, 60, $"seed {seed}: забагато раундів в одній хвилі");
            }
        }
    }
}
