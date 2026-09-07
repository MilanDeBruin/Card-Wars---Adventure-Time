using System.Collections.Generic;
using System.IO;
using MiniJSON;
using NUnit.Framework;

namespace CardWars.Tests
{
    [TestFixture, Category("GameplayRegression")]
    public class GameplayBalanceTests
    {
        [TestCase("db_Creatures.json"), TestCase("db_Buildings.json"), TestCase("db_Spells.json"),
         TestCase("db_Leaders.json"), TestCase("db_Parameters.json")]
        public void ShippedGameplayValuesMatchReviewedBaseline(string file)
        {
            string baselinePath = Path.Combine("Assets/Tests/Editor/Baselines", file);
            string actualPath = Path.Combine("Assets/StreamingAssets/Blueprints", file);
            List<object> baseline = Json.Deserialize(File.ReadAllText(baselinePath)) as List<object>;
            List<object> actual = Json.Deserialize(File.ReadAllText(actualPath)) as List<object>;
            Assert.IsNotNull(baseline, "Invalid baseline JSON: " + file);
            Assert.IsNotNull(actual, "Invalid gameplay JSON: " + file);
            Assert.AreEqual(baseline.Count, actual.Count, file + ": records added or removed; review gameplay impact.");
            Dictionary<string, Dictionary<string, object>> byId = new Dictionary<string, Dictionary<string, object>>();
            foreach (object item in actual)
            {
                Dictionary<string, object> row = (Dictionary<string, object>)item;
                string id = (string)row["ID"];
                Assert.IsFalse(byId.ContainsKey(id), file + ": duplicate ID " + id);
                byId.Add(id, row);
            }
            foreach (object item in baseline)
            {
                Dictionary<string, object> expected = (Dictionary<string, object>)item;
                string id = (string)expected["ID"];
                Assert.IsTrue(byId.ContainsKey(id), file + ": missing ID " + id);
                foreach (KeyValuePair<string, object> field in expected)
                {
                    string context = file + " / " + id + " / " + field.Key;
                    Assert.IsTrue(byId[id].ContainsKey(field.Key), context + ": missing gameplay field");
                    Assert.AreEqual(field.Value, byId[id][field.Key], context + ": gameplay balance changed");
                }
            }
        }
    }
}
