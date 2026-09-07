using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace CardWars.Tests
{
    // Characterization tests: expected values describe the existing game rules.
    // Scene-dependent entry points (animation, audio, tutorial UI) need Play Mode coverage.
    [TestFixture, Category("GameplayRegression")]
    public class GameplayRegressionTests
    {
        private GameState game;
        private object previousGame;
        private object previousQuestManager;
        private object previousRefCount;
        private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            previousGame = typeof(GameState).GetField("instance", PrivateStatic).GetValue(null);
            previousRefCount = typeof(GameState).GetField("refCount", PrivateStatic).GetValue(null);
            previousQuestManager = typeof(QuestConditionManager).GetField("instance", PrivateStatic).GetValue(null);
            game = new GameState();
            typeof(GameState).GetField("instance", PrivateStatic).SetValue(null, game);
            typeof(QuestConditionManager).GetField("instance", PrivateStatic).SetValue(null,
                new QuestConditionManager { currentStats = new QuestStats() });
        }

        [TearDown]
        public void TearDown()
        {
            typeof(GameState).GetField("instance", PrivateStatic).SetValue(null, previousGame);
            typeof(GameState).GetField("refCount", PrivateStatic).SetValue(null, previousRefCount);
            typeof(QuestConditionManager).GetField("instance", PrivateStatic).SetValue(null, previousQuestManager);
        }

        private static CreatureCard Form()
        {
            return new CreatureCard { ID = "test-creature", Name = "Test creature", ScriptName = "CreatureScript",
                Faction = Faction.Corn, BaseATK = 5, BaseDEF = 20, BaseVal1 = 3, BaseVal2 = 2,
                Cost = 3, FloopCost = 2, Rarity = 2 };
        }

        private T Put<T>(int player, int lane) where T : CreatureScript, new()
        {
            T creature = new T { Data = new CardItem(Form()), Owner = player,
                CurrentLane = game.GetLane(player, lane), GameInstance = game };
            creature.CurrentLane.Scripts[0] = creature;
            return creature;
        }

        [Test]
        public void NewMatchHasTwoPlayersFourMirroredLanesAndTwentyFiveHealth()
        {
            Assert.AreEqual(2, GameState.MAX_PLAYERS);
            Assert.AreEqual(4, GameState.LANE_COUNT);
            Assert.AreEqual(5, GameState.CARDS_TO_DEAL);
            Assert.AreEqual(7, GameState.MAX_HAND);
            for (int player = 0; player < 2; player++)
            {
                Assert.AreEqual(25, game.GetHealth(player));
                Assert.AreEqual(25, game.GetMaxHealth(player));
                Assert.AreEqual(0, game.GetMagicPoints(player));
                Assert.AreEqual(0, game.GetCardsInHand(player));
                Assert.AreEqual(4, game.EmptyLaneCount(player));
                for (int lane = 0; lane < 4; lane++)
                {
                    Lane current = game.GetLane(player, lane);
                    Assert.AreEqual(lane, current.Index);
                    Assert.AreSame(game.GetLane(1 - player, 3 - lane), current.OpponentLane);
                    Assert.AreSame(current, current.OpponentLane.OpponentLane);
                    Assert.AreEqual(lane == 0 || lane == 3, current.IsOuterLane);
                    Assert.AreEqual(LandscapeType.None, current.Type);
                    Assert.IsFalse(current.Disabled);
                }
            }
        }

        [TestCase(0), TestCase(1)]
        public void AdjacencyDoesNotWrapAroundBoard(int player)
        {
            for (int lane = 0; lane < 4; lane++)
                for (int other = 0; other < 4; other++)
                    Assert.AreEqual(Math.Abs(lane - other) == 1,
                        game.GetLane(player, lane).IsAdjacentTo(game.GetLane(player, other)));
            Assert.AreSame(game.GetLane(player, 1), game.GetLane(player, 0).GetInnerLane());
            Assert.AreSame(game.GetLane(player, 2), game.GetLane(player, 3).GetInnerLane());
        }

        [Test]
        public void BuildingAloneLeavesLaneOpenToCreatures()
        {
            Lane lane = game.GetLane(0, 0);
            lane.Scripts[1] = new BuildingScript();
            Assert.IsTrue(lane.HasBuilding());
            Assert.IsTrue(lane.IsEmpty());
            Assert.AreEqual(4, game.EmptyLaneCount(0));
            Put<CreatureScript>(0, 0);
            Assert.IsFalse(lane.IsEmpty());
            Assert.AreEqual(3, game.EmptyLaneCount(0));
            Assert.AreEqual(1, game.CreatureCount(0));
            Assert.AreEqual(1, game.BuildingCount(0));
            Assert.AreEqual(0, game.CreatureCount(1));
        }

        [TestCase(1, 5, 20, 3, 2), TestCase(3, 15, 60, 9, 6), TestCase(10, 50, 200, 30, 20)]
        public void CardLevelScalesCombatStatsAndAbilityValues(int level, int atk, int def, int val1, int val2)
        {
            CardItem card = new CardItem(Form(), level);
            Assert.AreEqual(atk, card.ATK);
            Assert.AreEqual(def, card.DEF);
            Assert.AreEqual(val1, card.Val1);
            Assert.AreEqual(val2, card.Val2);
        }

        [TestCase(0), TestCase(1)]
        public void CardNeedsMatchingLandscapeAndEnoughMagic(int player)
        {
            CreatureCard card = Form();
            game.SetLandscape(player, 0, LandscapeType.Corn);
            game.SetMagicPoints(player, 2);
            Assert.IsFalse(card.CanPlay(player, 0));
            game.SetMagicPoints(player, 3);
            Assert.IsTrue(card.CanPlay(player, 0));
            Assert.IsFalse(card.CanPlay(player, 1));
            Assert.AreEqual(3, game.GetMagicPoints(player), "Checking legality must not spend points.");
            card.Faction = Faction.Universal;
            Assert.IsTrue(card.CanPlay(player, 1));
        }

        [TestCase(CardType.Creature), TestCase(CardType.Building)]
        public void DisabledLaneAndRarityGateBlockPermanentCards(CardType type)
        {
            CreatureCard card = Form();
            card.Type = type;
            card.Faction = Faction.Universal;
            game.SetMagicPoints(0, 10);
            Lane lane = game.GetLane(0, 0);
            lane.RarityGate = 2;
            Assert.IsTrue(card.CanPlay(0, 0));
            lane.RarityGate = 1;
            Assert.IsFalse(card.CanPlay(0, 0));
            lane.RarityGate = 2;
            lane.Disabled = true;
            Assert.IsFalse(card.CanPlay(0, 0));
        }

        [TestCase(CardType.Creature), TestCase(CardType.Building), TestCase(CardType.Spell)]
        public void CastingBlockIsScopedToPlayerAndCardType(CardType type)
        {
            game.EnableCasting(0, type, false);
            foreach (CardType candidate in new[] { CardType.Creature, CardType.Building, CardType.Spell })
            {
                CreatureCard card = Form();
                card.Type = candidate;
                card.Faction = Faction.Universal;
                game.SetMagicPoints(0, 10);
                game.SetMagicPoints(1, 10);
                Assert.AreEqual(candidate != type, card.CanPlay(0, 0));
                Assert.IsTrue(card.CanPlay(1, 0));
            }
        }

        [Test]
        public void MatchingDiscountsStackAndDoNotAffectOtherPlayer()
        {
            CreatureCard card = Form();
            card.Cost = 10;
            game.SetDiscount(0, CardType.None, 1);
            game.SetDiscount(0, CardType.Creature, 2);
            game.SetDiscount(0, CardType.Creature, 3, true, Faction.Corn);
            game.SetDiscount(0, CardType.Spell, 50);
            game.SetDiscount(0, CardType.Creature, 50, true, Faction.Swamp);
            Assert.AreEqual(4, card.DetermineCost(0));
            Assert.AreEqual(10, card.DetermineCost(1));
            game.SetDiscount(0, CardType.None, 20);
            Assert.AreEqual(0, card.DetermineCost(0));
        }

        [Test]
        public void FloopFlatCostResetsPlayerModifierButKeepsLaneModifier()
        {
            CreatureScript creature = Put<ATKBonus>(0, 0);
            creature.CurrentLane.FloopMod = 1;
            game.AddFloopCostMod(0, 3);
            Assert.AreEqual(6, creature.DetermineFloopCost());
            game.SetFlatFloopCost(0, 2);
            Assert.AreEqual(0, game.GetFloopCostMod(0));
            Assert.AreEqual(3, creature.DetermineFloopCost());
            game.AddFloopCostMod(0, -10);
            Assert.AreEqual(0, creature.DetermineFloopCost());
            Assert.AreEqual(-1, game.GetFlatFloopCost(1));
        }

        [Test]
        public void FloopRequiresAbilityAffordabilityAndUnusedEnabledCard()
        {
            CreatureScript creature = Put<ATKBonus>(0, 0);
            game.SetMagicPoints(0, 1);
            Assert.IsFalse(game.CanFloopCard(0, creature));
            game.SetMagicPoints(0, 2);
            Assert.IsTrue(game.CanFloopCard(0, creature));
            creature.Flooped = true;
            Assert.IsFalse(game.CanFloopCard(0, creature));
            game.UnfloopCard(0, 0, CardType.Creature);
            Assert.IsTrue(game.CanFloopCard(0, creature));
            creature.FloopDisabled = true;
            Assert.IsFalse(game.CanFloopCard(0, creature));
            Assert.IsFalse(game.CanFloopCard(0, Put<CreatureScript>(0, 1)));
        }

        [Test]
        public void DamageOpponentAbilityRequiresCreatureAcrossMirroredLane()
        {
            DamageOpponent source = Put<DamageOpponent>(0, 0);
            Assert.IsFalse(source.CanFloop());
            Put<CreatureScript>(1, 0);
            Assert.IsFalse(source.CanFloop());
            CreatureScript target = Put<CreatureScript>(1, 3);
            Assert.IsTrue(source.CanFloop());
            Assert.IsTrue(source.DoResult(target));
            Assert.AreEqual(3, target.Damage);
            Assert.AreEqual(0, game.GetCreature(1, 0).Damage);
        }

        [TestCase(0, 0, 1f, 0), TestCase(7, 2, 1.5f, 7), TestCase(3, 8, 1f, 0),
         TestCase(5, 0, 0.5f, 2), TestCase(5, 0, 0f, 0), TestCase(30, 0, 1f, 30)]
        public void DamageSubtractsReductionThenMultipliesAndTruncates(int amount, int reduction, float factor, int expected)
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            creature.DamageReduction = reduction;
            creature.DamageFactor = factor;
            creature.TakeDamage(null, amount);
            Assert.AreEqual(expected, creature.Damage);
            Assert.AreEqual(expected, creature.DamageLastTurn);
            Assert.AreEqual(Math.Max(0, 20 - expected), creature.Health);
        }

        [Test]
        public void DamageAccumulatesButDamageLastTurnTracksLatestHit()
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            creature.TakeDamage(null, 4);
            creature.TakeDamage(null, 3);
            Assert.AreEqual(7, creature.Damage);
            Assert.AreEqual(3, creature.DamageLastTurn);
            Assert.AreEqual(13, creature.Health);
        }

        [TestCase(3, 0.5f, 8), TestCase(5, 0.5f, 8), TestCase(7, 0.5f, 6),
         TestCase(4, 1.5f, 4), TestCase(100, 1f, 0), TestCase(5, 0f, 10)]
        public void HealingRoundsMidpointsToEvenAndCannotOverheal(int amount, float factor, int remainingDamage)
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            creature.Damage = 10;
            creature.HealingFactor = factor;
            creature.Heal(amount);
            Assert.AreEqual(remainingDamage, creature.Damage);
            Assert.AreEqual(20 - remainingDamage, creature.Health);
        }

        [Test]
        public void AttackModifiersApplyBeforeFactorAndNegativeAttackClampsToZero()
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            creature.ATKMod = 2;
            creature.ATKFactor = 1.5f;
            Assert.AreEqual(10, creature.ATK);
            creature.ATKMod = -100;
            Assert.AreEqual(0, creature.ATK);
            Assert.AreEqual(-5, creature.ATKMod);
        }

        [Test]
        public void DefenseModifiersChangeMaxHealthWithoutRemovingDamage()
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            creature.Damage = 3;
            creature.DEFMod = 5;
            creature.DEFFactor = 0.5f;
            Assert.AreEqual(12, creature.DEF);
            Assert.AreEqual(9, creature.Health);
            Assert.AreEqual(0.75f, creature.GetHealthPct());
            Assert.AreEqual(3, creature.Damage);
        }

        private class VictoryBuilding : BuildingScript
        {
            public int Victories;
            public override void OnCreatureWon() { Victories++; }
        }

        [Test]
        public void LethalCreatureDamageTriggersAttackingLanesBuilding()
        {
            CreatureScript attacker = Put<CreatureScript>(0, 0);
            CreatureScript defender = Put<CreatureScript>(1, 3);
            VictoryBuilding building = new VictoryBuilding();
            attacker.CurrentLane.Scripts[1] = building;
            defender.TakeDamage(attacker, 19);
            Assert.AreEqual(0, building.Victories);
            defender.TakeDamage(attacker, 1);
            Assert.AreEqual(0, defender.Health);
            Assert.AreEqual(1, building.Victories);
        }

        [Test]
        public void DirectWinRequiresEmptyOpposingLaneAndLethalAttack()
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            game.SetHealth(PlayerType.Opponent, 6);
            Assert.IsFalse(creature.CanWin);
            game.SetHealth(PlayerType.Opponent, 5);
            Assert.IsTrue(creature.CanWin);
            Put<CreatureScript>(1, 3);
            Assert.IsFalse(creature.CanWin);
        }

        [Test]
        public void DangerUsesStrictLethalityAndExcludesWinningTrades()
        {
            CreatureScript creature = Put<CreatureScript>(0, 0);
            CreatureScript enemy = Put<CreatureScript>(1, 3);
            creature.Damage = 15;
            Assert.IsFalse(creature.InDanger, "Equal enemy ATK and health is currently not marked as danger.");
            enemy.ATKMod = 1;
            Assert.IsTrue(creature.InDanger);
            creature.ATKMod = 15;
            Assert.IsFalse(creature.InDanger);
        }

        [Test]
        public void BuffDamageDebuffAndHealingScenarioPreservesPermanentStats()
        {
            ATKBonus creature = Put<ATKBonus>(0, 0);
            creature.DoResult(creature);
            creature.DoResult(creature);
            Assert.AreEqual(11, creature.ATK);
            creature.TakeDamage(null, 9);
            ReduceATK debuff = new ReduceATK();
            debuff.DoResult(creature);
            Assert.AreEqual(5, creature.ATK);
            debuff.DoResult(creature);
            Assert.AreEqual(2, creature.ATK);
            new HealCreature().DoResult(creature);
            Assert.AreEqual(20, creature.Health);
            Assert.AreEqual(2, creature.ATK);
            Assert.AreEqual(6, creature.ATKMod);
        }

        [Test]
        public void HealSpellNeedsAnInjuredFriendlyCreature()
        {
            SpellCard spell = new SpellCard { ScriptName = "HealCreature", Faction = Faction.Universal, Cost = 1 };
            game.SetMagicPoints(0, 1);
            Assert.IsFalse(spell.CanPlay(0, -1));
            CreatureScript enemy = Put<CreatureScript>(1, 0);
            enemy.Damage = 5;
            Assert.IsFalse(spell.CanPlay(0, -1));
            CreatureScript friendly = Put<CreatureScript>(0, 0);
            Assert.IsFalse(spell.CanPlay(0, -1));
            friendly.TakeDamage(null, 1);
            Assert.IsTrue(spell.CanPlay(0, -1));
            new HealCreature().DoResult(friendly);
            Assert.IsFalse(spell.CanPlay(0, -1));
            Assert.AreEqual(5, enemy.Damage);
        }

        [Test]
        public void ResetSelfClearsDamageAndAdditiveBonusesButPreservesFactors()
        {
            ResetSelf creature = Put<ResetSelf>(0, 0);
            Assert.IsFalse(creature.CanFloop());
            creature.Damage = 7;
            creature.ATKMod = 6;
            creature.DEFMod = 4;
            creature.ATKFactor = 0.5f;
            Assert.IsTrue(creature.CanFloop());
            creature.DoResult(creature);
            Assert.AreEqual(0, creature.Damage);
            Assert.AreEqual(0, creature.ATKMod);
            Assert.AreEqual(0, creature.DEFMod);
            Assert.AreEqual(0.5f, creature.ATKFactor);
            Assert.IsFalse(creature.CanFloop());
        }

        [Test]
        public void FlippedAndFakeLandscapesAffectCountsAndOverrideAddsMatchingType()
        {
            game.SetLandscape(0, 0, LandscapeType.Corn);
            game.SetLandscape(0, 1, LandscapeType.Corn);
            game.SetLandscape(0, 2, LandscapeType.Swamp);
            game.SetLandscape(0, 3, LandscapeType.Swamp);
            game.GetLane(0, 1).Flipped = true; // Avoid the presentation callback in FlipLandscape.
            Assert.AreEqual(1, game.LandscapeCount(0, LandscapeType.Corn));
            game.AddFakeLandscape(0, LandscapeType.Corn);
            Assert.AreEqual(2, game.LandscapeCount(0, LandscapeType.Corn));
            game.RemoveFakeLandscape(0, LandscapeType.Corn);
            Assert.AreEqual(1, game.LandscapeCount(0, LandscapeType.Corn));
            game.SetOverrideLandscape(0, LandscapeType.Sand);
            Assert.AreEqual(3, game.LandscapeCount(0, LandscapeType.Sand));
            Assert.IsTrue(game.IsLaneOfLandscapeType(0, 0, LandscapeType.Corn));
            Assert.IsTrue(game.IsLaneOfLandscapeType(0, 0, LandscapeType.Sand));
            Assert.IsFalse(game.IsLaneOfLandscapeType(1, 0, LandscapeType.Sand));
        }

        [Test]
        public void DeckDealsFromFrontAndReturnedCardIsDrawnNext()
        {
            Deck deck = new Deck();
            CardItem first = new CardItem(Form());
            CardItem second = new CardItem(Form());
            deck.AddCard(first);
            deck.AddCard(second);
            Assert.AreSame(first, deck.DealCard());
            deck.PlaceCard(first);
            Assert.AreSame(first, deck.DealCard());
            Assert.AreSame(second, deck.DealCard());
            Assert.IsNull(deck.DealCard());
            Assert.AreEqual(0, deck.CardCount());
        }

        [Test]
        public void DrawDiscardAndRedrawScenarioHonorsSevenCardHandLimit()
        {
            Deck deck = new Deck();
            for (int i = 0; i < 9; i++) deck.AddCard(new CardItem(Form(), i + 1));
            game.SetDecks(new Deck(), deck);
            for (int i = 0; i < 5; i++) game.DrawCard(PlayerType.Opponent);
            Assert.AreEqual(5, game.GetCardsInHand(1));
            Assert.AreEqual(4, deck.CardCount());
            Assert.AreEqual(1, game.GetCardInHand(1, 0).Level);
            for (int i = 0; i < 4; i++) game.DrawCard(PlayerType.Opponent);
            Assert.AreEqual(7, game.GetCardsInHand(1));
            Assert.AreEqual(2, deck.CardCount());
            CardItem discarded = game.GetCardInHand(1, 0);
            game.RemoveCardFromHand(1, discarded);
            game.DiscardCard(1, discarded);
            game.DrawCard(PlayerType.Opponent);
            Assert.AreEqual(7, game.GetCardsInHand(1));
            Assert.AreEqual(1, deck.CardCount());
            Assert.AreEqual(8, game.GetCardInHand(1, 6).Level);
            Assert.AreSame(discarded, game.GetDiscardPile(1)[0]);
            Assert.AreEqual(1, game.DiscardCount(1, CardType.Creature));
            Assert.AreEqual(0, game.GetCardsInHand(0));
            Assert.AreEqual(0, game.DiscardCount(0));
        }

        [Test]
        public void DrawingFromEmptyDeckLeavesHandUnchanged()
        {
            game.SetDecks(new Deck(), new Deck());
            game.DrawCard(PlayerType.Opponent);
            Assert.AreEqual(0, game.GetCardsInHand(1));
            Assert.IsNull(game.GetCardInHand(1, 0));
        }

        [Test]
        public void DeckCloneKeepsCardIdentityButOwnsIndependentCardAndLandscapeLists()
        {
            Deck original = new Deck();
            CardItem card = new CardItem(Form(), 3);
            original.AddCard(card);
            original.AddLandscape(LandscapeType.Corn);
            Deck copy = original.Clone();
            Assert.AreSame(card, copy.GetCard(0));
            copy.RemoveCard(card);
            copy.SetLandscape(0, LandscapeType.Swamp);
            Assert.AreEqual(1, original.CardCount());
            Assert.AreEqual(LandscapeType.Corn, original.GetLandscape(0));
        }

        [Test]
        public void DuplicateLimitAllowsExactlyTheLimitRegardlessOfLevel()
        {
            Deck deck = new Deck();
            deck.AddCard(new CardItem(Form(), 1));
            deck.AddCard(new CardItem(Form(), 2));
            Assert.IsNull(deck.ExceedsMaxDuplicates(2));
            deck.AddCard(new CardItem(Form(), 3));
            Assert.AreEqual("Test creature", deck.ExceedsMaxDuplicates(2));
        }

        [Test]
        public void ResetBattleRingModifiersClearsAllFourAreasAndExtraMagic()
        {
            game.HitAreaModifier = 2f;
            game.DefenseAreaModifier = 3f;
            game.DefenseAreaCritModifier = 4f;
            game.CritAreaModifier = 5f;
            game.ExtraMagicPoints = 6;
            game.ResetAreaModifiers();
            game.ResetExtraMagicPoints();
            Assert.AreEqual(0f, game.HitAreaModifier);
            Assert.AreEqual(0f, game.DefenseAreaModifier);
            Assert.AreEqual(0f, game.DefenseAreaCritModifier);
            Assert.AreEqual(0f, game.CritAreaModifier);
            Assert.AreEqual(0, game.ExtraMagicPoints);
        }
    }
}
