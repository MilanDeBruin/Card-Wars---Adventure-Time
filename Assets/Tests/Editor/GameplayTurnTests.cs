using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CardWars.Tests
{
    [TestFixture, Category("GameplayRegression")]
    public class GameplayTurnTests
    {
        private GameplayRegressionTests isolation;
        private GameState game;
        private GameObject host;
        private readonly Dictionary<FieldInfo, object> savedStatics = new Dictionary<FieldInfo, object>();

        private void ReplaceStatic(Type type, string name, object replacement)
        {
            FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            savedStatics.Add(field, field.GetValue(null));
            field.SetValue(null, replacement);
        }

        [SetUp]
        public void SetUp()
        {
            try
            {
                isolation = new GameplayRegressionTests();
                isolation.SetUp();
                game = GameState.Instance;
                // Inactive, hidden components provide the real managers without running
                // scene startup, loading a save, starting audio or changing the open scene.
                host = new GameObject("Gameplay regression fixture");
                host.hideFlags = HideFlags.HideAndDontSave;
                host.SetActive(false);
                CWFloopActionManager floop = host.AddComponent<CWFloopActionManager>();
                typeof(CWFloopActionManager).GetField("persistantContext", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(floop, new Dictionary<CardScript, string>());
                ReplaceStatic(typeof(CWFloopActionManager), "g_floopManager", floop);
                ReplaceStatic(typeof(QuestEarningManager), "g_earningManager", host.AddComponent<QuestEarningManager>());
                ReplaceStatic(typeof(BattlePhaseManager), "g_battlePhaseManager", host.AddComponent<BattlePhaseManager>());
                ReplaceStatic(typeof(ParametersManager), "instance", new ParametersManager { Max_Magic_Points = 9 });
                game.GameData = host.AddComponent<GameDataScript>();
                game.SetDecks(new Deck(), new Deck());
            }
            catch
            {
                TearDown();
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (KeyValuePair<FieldInfo, object> saved in savedStatics) saved.Key.SetValue(null, saved.Value);
            savedStatics.Clear();
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (isolation != null) isolation.TearDown();
        }

        private class TurnCreature : CreatureScript
        {
            public int Starts;
            public override void StartTurn() { Starts++; }
        }

        private TurnCreature Put(int player, int lane)
        {
            TurnCreature creature = new TurnCreature { Owner = player, GameInstance = game,
                CurrentLane = game.GetLane(player, lane), Data = new CardItem(new CreatureCard { BaseATK = 5, BaseDEF = 20 }) };
            creature.CurrentLane.Scripts[0] = creature;
            return creature;
        }

        private class ExpiringSpell : CardScript
        {
            public int Expirations;
            public override void OnOpponentStartTurn()
            {
                Expirations++;
                GameInstance.EndPersistentSpellEffect(Owner, this);
            }
        }

        [TestCase(0), TestCase(1)]
        public void StartTurnRefreshesOnlyActivePlayerAndExpiresOpposingPersistentSpells(int player)
        {
            TurnCreature active = Put(player, 0);
            TurnCreature waiting = Put(1 - player, 0);
            active.Moved = active.Flooped = active.Fresh = active.Used = true;
            active.Helpless = true;
            active.Protected = true;
            active.Damage = 7;
            waiting.Flooped = true;
            game.SpellsCast[player] = 2;
            game.CreaturesSummoned[player] = 3;
            game.CreaturesRemoved[player] = 4;
            game.SpellsCast[1 - player] = 5;
            game.SetOverrideLandscape(player, LandscapeType.Sand);
            ExpiringSpell expired = new ExpiringSpell { Owner = 1 - player, GameInstance = game };
            ExpiringSpell retained = new ExpiringSpell { Owner = player, GameInstance = game };
            game.AddPersistentSpellEffect(1 - player, expired);
            game.AddPersistentSpellEffect(player, retained);
            QuestEarningManager.GetInstance().dropedThisBattle = true;

            game.StartTurn(player);

            Assert.IsFalse(active.Moved);
            Assert.IsFalse(active.Flooped);
            Assert.IsFalse(active.Fresh);
            Assert.IsFalse(active.Used);
            Assert.IsFalse(active.Helpless);
            Assert.IsFalse(active.Protected);
            Assert.AreEqual(7, active.Damage, "Starting a turn must not heal existing damage.");
            Assert.AreEqual(1, active.Starts);
            Assert.IsTrue(waiting.Flooped);
            Assert.AreEqual(0, waiting.Starts);
            Assert.AreEqual(0, game.SpellsCast[player]);
            Assert.AreEqual(0, game.CreaturesSummoned[player]);
            Assert.AreEqual(0, game.CreaturesRemoved[player]);
            Assert.AreEqual(5, game.SpellsCast[1 - player]);
            Assert.AreEqual(LandscapeType.None, game.GetOverrideLandscape(player, 0));
            Assert.AreEqual(1, expired.Expirations);
            Assert.AreEqual(0, retained.Expirations);
            Assert.IsFalse(QuestEarningManager.GetInstance().dropedThisBattle);
            if (player == 0) Assert.AreEqual(BattlePhase.OutOfCards, BattlePhaseManager.GetInstance().Phase);
        }

        [TestCase(1, 2, 2), TestCase(2, 2, 3), TestCase(2, 8, 9), TestCase(2, 9, 9)]
        public void EndTurnAdvancesMagicForStartingPlayerCapsAtNineAndConsumesBonusOnce(int firstPlayer, int current, int next)
        {
            game.GameData.FirstPlayer = firstPlayer;
            game.CurrentMagicPoints = current;
            game.AddBonusPoints(1, 2);
            game.SetMagicPoints(0, 17);
            game.EndTurn(PlayerType.Opponent);
            Assert.AreEqual(next, game.CurrentMagicPoints);
            Assert.AreEqual(next + 2, game.GetMagicPoints(1));
            Assert.AreEqual(17, game.GetMagicPoints(0));
            game.GameData.FirstPlayer = 1;
            game.EndTurn(PlayerType.Opponent);
            Assert.AreEqual(next, game.GetMagicPoints(1), "The temporary bonus must be consumed once.");
        }

        [Test]
        public void EndTurnRemovesTemporaryRestrictionsAndDiscountsOnlyForEndingPlayer()
        {
            game.GameData.FirstPlayer = 1;
            CreatureCard card = new CreatureCard { Cost = 5 };
            game.SetDiscount(1, CardType.Creature, 3);
            game.SetDiscount(0, CardType.Creature, 2);
            game.SetFlatFloopCost(1, 4);
            game.AddFloopCostMod(1, 2);
            game.SetATKPenalty(1, 3);
            game.EnableFlooping(1, false);
            foreach (CardType type in new[] { CardType.Creature, CardType.Building, CardType.Spell })
                game.EnableCasting(1, type, false);
            game.GetLane(1, 0).Disabled = true;
            TurnCreature creature = Put(1, 0);
            creature.CantAttack = true;

            game.EndTurn(PlayerType.Opponent);

            Assert.AreEqual(0, game.GetDiscount(1, card));
            Assert.AreEqual(2, game.GetDiscount(0, card));
            Assert.AreEqual(-1, game.GetFlatFloopCost(1));
            Assert.AreEqual(0, game.GetFloopCostMod(1));
            Assert.AreEqual(0, game.GetATKPenalty(1));
            Assert.IsTrue(game.IsFloopingEnabled(1));
            foreach (CardType type in new[] { CardType.Creature, CardType.Building, CardType.Spell })
                Assert.IsTrue(game.IsCastingEnabled(1, type));
            Assert.IsFalse(game.GetLane(1, 0).Disabled);
            Assert.IsFalse(creature.CantAttack);
        }
    }
}
