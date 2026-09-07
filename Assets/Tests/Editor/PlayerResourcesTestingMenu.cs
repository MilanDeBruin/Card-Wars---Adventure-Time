using UnityEditor;
using UnityEngine;

namespace CardWars.Tests
{
    /// <summary>
    /// Play Mode utility for supplying local test profiles without using the store or payment flow.
    /// </summary>
    public class PlayerResourcesTestingWindow : EditorWindow
    {
        private int heartAmount = 5;
        private int coinAmount = 1000;
        private int gemAmount = 100;

        [MenuItem("Tools/Card Wars/Player Resources/Open Resource Tester")]
        private static void Open()
        {
            PlayerResourcesTestingWindow window = GetWindow<PlayerResourcesTestingWindow>("Player Resources");
            window.minSize = new Vector2(330f, 260f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Test resources", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These buttons change only the active local test profile. No store or payment flow is used.", MessageType.Info);

            PlayerInfoScript player = GetActivePlayer();
            if (player == null) return;

            EditorGUILayout.LabelField("Current balance", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Hearts", player.Stamina + " / " + player.Stamina_Max);
            EditorGUILayout.LabelField("Coins", player.Coins.ToString());
            EditorGUILayout.LabelField("Gems", player.Gems.ToString());
            EditorGUILayout.Space();

            heartAmount = Mathf.Max(1, EditorGUILayout.IntField("Hearts to add", heartAmount));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add hearts")) AddHearts(player, heartAmount);
            if (GUILayout.Button("Fill hearts"))
            {
                player.Stamina = player.Stamina_Max;
                Save(player, "Hearts filled.");
            }
            EditorGUILayout.EndHorizontal();

            coinAmount = Mathf.Max(1, EditorGUILayout.IntField("Coins to add", coinAmount));
            if (GUILayout.Button("Add coins"))
            {
                player.Coins += coinAmount;
                Save(player, coinAmount + " coins added.");
            }

            gemAmount = Mathf.Max(1, EditorGUILayout.IntField("Gems to add", gemAmount));
            if (GUILayout.Button("Add gems"))
            {
                player.Gems += gemAmount;
                Save(player, gemAmount + " gems added.");
            }
        }

        private static PlayerInfoScript GetActivePlayer()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode before changing test resources.", MessageType.Warning);
                return null;
            }

            PlayerInfoScript player = PlayerInfoScript.GetInstance();
            if (player == null)
            {
                EditorGUILayout.HelpBox("Wait until the player profile has loaded.", MessageType.Warning);
            }
            return player;
        }

        private static void AddHearts(PlayerInfoScript player, int amount)
        {
            int maximum = player.Stamina_Max;
            player.Stamina = (maximum > 0) ? Mathf.Min(maximum, player.Stamina + amount) : player.Stamina + amount;
            Save(player, amount + " hearts added.");
        }

        private static void Save(PlayerInfoScript player, string message)
        {
            player.Save();
            Debug.Log("Test resources: " + message);
        }
    }
}
