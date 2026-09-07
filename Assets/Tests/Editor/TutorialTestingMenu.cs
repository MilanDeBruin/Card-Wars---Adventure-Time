using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CardWars.Tests
{
    /// <summary>
    /// Play Mode helpers for retesting tutorial flows without resetting an entire account.
    /// </summary>
    public static class TutorialTestingMenu
    {
        private const string MenuRoot = "Tools/Card Wars/Tutorial Testing/";
        private static readonly string[] ChestTutorialEntries = { "GATCHA", "G1", "G2", "G3", "G4", "G4.5" };
        private static readonly string[] AllGachaTutorialEntries = { "GATCHA", "GATCHA2", "G1", "G2", "G3", "G4", "G4.5", "G5", "G6", "G7" };

        [MenuItem(MenuRoot + "Show Gacha Tutorial Status")]
        private static void ShowGachaTutorialStatus()
        {
            PlayerInfoScript player = GetActivePlayer();
            if (player == null) return;

            Debug.Log("Gacha tutorial status: " + string.Join(", ", player.tutorialsCompleted.ToArray()));
        }

        [MenuItem(MenuRoot + "Open Tutorial Tester")]
        private static void OpenTutorialTester()
        {
            TutorialTestingWindow.ShowWindow();
        }

        [MenuItem(MenuRoot + "Check Premium Chest Animation Clips")]
        private static void CheckPremiumChestAnimationClips()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before checking the runtime chest animation.");
                return;
            }

            CWGachaController controller = Object.FindObjectOfType<CWGachaController>();
            if (controller == null || controller.premiumChestAnim == null)
            {
                Debug.LogWarning("CWGachaController or its premium chest Animation was not found in the active scene.");
                return;
            }

            Animation animation = controller.premiumChestAnim;
            bool hasOpenClip = animation.GetClip("PChest_OpenLvl5_0") != null;
            bool hasEndClip = animation.GetClip("PChest_End") != null;
            Debug.Log("Premium chest animation check — Open: " + hasOpenClip + ", End: " + hasEndClip);
        }

        [MenuItem(MenuRoot + "Reset Chest Opening Tutorial (GATCHA)")]
        private static void ResetChestOpeningTutorial()
        {
            ResetTutorialEntries(
                ChestTutorialEntries,
                "Reset chest-opening tutorial?",
                "This clears only the GATCHA tutorial flow from the active player profile and saves it. It does not reset the account.");
        }

        [MenuItem(MenuRoot + "Reset Complete Gacha Tutorial")]
        private static void ResetCompleteGachaTutorial()
        {
            ResetTutorialEntries(
                AllGachaTutorialEntries,
                "Reset complete Gacha tutorial?",
                "This clears both GATCHA tutorial flows from the active player profile and saves it. It does not reset the account.");
        }

        private static void ResetTutorialEntries(string[] entries, string title, string message)
        {
            PlayerInfoScript player = GetActivePlayer();
            if (player == null) return;
            if (!EditorUtility.DisplayDialog(title, message, "Reset tutorial", "Cancel")) return;

            HashSet<string> entriesToRemove = new HashSet<string>(entries);
            player.tutorialsCompleted.RemoveAll(entriesToRemove.Contains);
            player.Save();
            Debug.Log("Tutorial test reset complete. Restart the current flow or reload the game to begin it again.");
        }

        private static PlayerInfoScript GetActivePlayer()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before changing tutorial progress.");
                return null;
            }

            PlayerInfoScript player = PlayerInfoScript.GetInstance();
            if (player == null)
            {
                Debug.LogWarning("An active player profile has not been loaded yet.");
            }
            return player;
        }
    }

    /// <summary>
    /// Lets QA set the completion state immediately before any tutorial step and launch that step.
    /// It deliberately uses the runtime tutorial list, so it always follows db_Tutorials.json.
    /// </summary>
    public class TutorialTestingWindow : EditorWindow
    {
        private string selectedFlow = "FIRSTQUEST";
        private int selectedStep;
        private Vector2 scrollPosition;

        public static void ShowWindow()
        {
            TutorialTestingWindow window = GetWindow<TutorialTestingWindow>("Tutorial Tester");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tutorial testing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose a flow and phase. Set state clears that flow and marks only the earlier phases complete. For a phase with a Trigger, use Set state, return to the correct game screen and perform that action. Start phase is only for when that screen is already open.", MessageType.Info);

            if (!Application.isPlaying || PlayerInfoScript.GetInstance() == null || TutorialManager.Instance.tutorialOrder.Count == 0)
            {
                EditorGUILayout.HelpBox("Enter Play Mode and wait for the player profile and tutorial data to load.", MessageType.Warning);
                return;
            }

            List<string> flows = GetFlows();
            int flowIndex = Mathf.Max(0, flows.IndexOf(selectedFlow));
            int newFlowIndex = EditorGUILayout.Popup("Flow", flowIndex, flows.ToArray());
            if (newFlowIndex != flowIndex)
            {
                selectedFlow = flows[newFlowIndex];
                selectedStep = 0;
            }

            List<TutorialInfo> steps = GetSteps(selectedFlow);
            if (steps.Count == 0)
            {
                EditorGUILayout.HelpBox("No tutorial phases were found for this flow.", MessageType.Error);
                return;
            }

            selectedStep = Mathf.Clamp(selectedStep, 0, steps.Count - 1);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = selectedStep > 0;
            if (GUILayout.Button("< Previous phase")) selectedStep--;
            GUI.enabled = selectedStep < steps.Count - 1;
            if (GUILayout.Button("Next phase >")) selectedStep++;
            GUI.enabled = true;
            selectedStep = EditorGUILayout.Popup("Phase", selectedStep, GetStepLabels(steps));

            TutorialInfo selected = steps[selectedStep];
            EditorGUILayout.LabelField("Tutorial ID", selected.TutorialID);
            EditorGUILayout.LabelField("Trigger", selected.Trigger.ToString());
            EditorGUILayout.LabelField("Progress", (selectedStep + 1) + " / " + steps.Count);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set state before phase")) SetStateBefore(steps, selectedStep);
            if (GUILayout.Button("Start selected phase"))
            {
                SetStateBefore(steps, selectedStep);
                TutorialMonitor monitor = TutorialMonitor.Instance;
                if (monitor == null)
                {
                    Debug.LogWarning("TutorialMonitor was not found in the active scene. The test state was saved, but the phase was not opened.");
                }
                else
                {
                    monitor.StartTutorial(selected.TutorialID);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset selected flow")) ResetFlow(steps);
            if (GUILayout.Button("Reset ALL tutorial progress")) ResetAllTutorials();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Completed tutorial markers", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(85f));
            EditorGUILayout.SelectableLabel(string.Join(", ", PlayerInfoScript.GetInstance().tutorialsCompleted.ToArray()), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private static List<string> GetFlows()
        {
            List<string> flows = new List<string>();
            foreach (string id in TutorialManager.Instance.tutorialOrder)
            {
                TutorialInfo info = TutorialManager.Instance.Find(id);
                if (info == null) continue;
                string flow = string.IsNullOrEmpty(info.Flow) ? "(No flow)" : info.Flow;
                if (!flows.Contains(flow)) flows.Add(flow);
            }
            return flows;
        }

        private static List<TutorialInfo> GetSteps(string flow)
        {
            string flowValue = flow == "(No flow)" ? string.Empty : flow;
            List<TutorialInfo> result = new List<TutorialInfo>();
            foreach (string id in TutorialManager.Instance.tutorialOrder)
            {
                TutorialInfo info = TutorialManager.Instance.Find(id);
                if (info != null && info.Flow == flowValue) result.Add(info);
            }
            return result;
        }

        private static string[] GetStepLabels(List<TutorialInfo> steps)
        {
            string[] labels = new string[steps.Count];
            for (int i = 0; i < steps.Count; i++) labels[i] = (i + 1) + ". " + steps[i].TutorialID;
            return labels;
        }

        private static void SetStateBefore(List<TutorialInfo> steps, int stepIndex)
        {
            PlayerInfoScript player = PlayerInfoScript.GetInstance();
            HashSet<string> markers = new HashSet<string>();
            foreach (TutorialInfo info in steps) markers.Add(info.TutorialID);
            if (!string.IsNullOrEmpty(steps[0].Flow)) markers.Add(steps[0].Flow);
            player.tutorialsCompleted.RemoveAll(markers.Contains);
            for (int i = 0; i < stepIndex; i++) player.tutorialsCompleted.Add(steps[i].TutorialID);
            player.Save();
            Debug.Log("Tutorial test state set before " + steps[stepIndex].TutorialID + ".");
        }

        private static void ResetFlow(List<TutorialInfo> steps)
        {
            if (!EditorUtility.DisplayDialog("Reset tutorial flow?", "This clears only the selected tutorial flow from the active player profile.", "Reset flow", "Cancel")) return;
            SetStateBefore(steps, 0);
            Debug.Log("Tutorial flow reset: " + (string.IsNullOrEmpty(steps[0].Flow) ? "(No flow)" : steps[0].Flow));
        }

        private static void ResetAllTutorials()
        {
            if (!EditorUtility.DisplayDialog("Reset all tutorial progress?", "This clears every known tutorial marker from the active player profile. It does not reset the account.", "Reset all", "Cancel")) return;
            PlayerInfoScript player = PlayerInfoScript.GetInstance();
            HashSet<string> markers = new HashSet<string>(TutorialManager.Instance.tutorialOrder);
            foreach (TutorialInfo info in TutorialManager.Instance.tutorials.Values)
            {
                if (!string.IsNullOrEmpty(info.Flow)) markers.Add(info.Flow);
            }
            player.tutorialsCompleted.RemoveAll(markers.Contains);
            player.Save();
            Debug.Log("All tutorial progress has been reset.");
        }
    }
}
