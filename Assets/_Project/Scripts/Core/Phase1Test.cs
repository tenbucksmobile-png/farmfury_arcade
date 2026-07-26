using UnityEngine;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Manual/automated verification harness for Phase 1. Not gameplay or UI — a temporary
    /// diagnostic to prove the data pipeline (ScriptableObject load, level instantiation,
    /// PlayerPrefs persistence) works end to end. Safe to delete once Phase 2+ adds real
    /// player input and a real UI to trigger these flows.
    /// </summary>
    public class Phase1Test : MonoBehaviour
    {
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunVerification();
            }
        }

        [ContextMenu("Run Phase 1 Verification")]
        public void RunVerification()
        {
            Debug.Log("[Phase1Test] --- Starting Phase 1 verification ---");
            VerifyDataManagerLoaded();
            VerifyLoadLevelZero();
            VerifySaveManagerRoundTrip();
            Debug.Log("[Phase1Test] --- Phase 1 verification complete ---");
        }

        private void VerifyDataManagerLoaded()
        {
            var level = DataManager.Instance.GetLevelData(0);
            if (level != null)
            {
                Debug.Log("[Phase1Test] PASS: DataManager returned LevelData for index 0.");
            }
            else
            {
                Debug.LogError("[Phase1Test] FAIL: DataManager has no LevelData for index 0.");
            }
        }

        private void VerifyLoadLevelZero()
        {
            GameManager.Instance.LoadLevel(0);
            bool loaded = GameManager.Instance.CurrentLevel != null && GameManager.Instance.CurrentState == GameState.Playing;
            Debug.Log(loaded
                ? "[Phase1Test] PASS: GameManager.LoadLevel(0) set CurrentLevel and entered Playing state."
                : "[Phase1Test] FAIL: GameManager.LoadLevel(0) did not load correctly.");
        }

        private void VerifySaveManagerRoundTrip()
        {
            int before = SaveManager.Instance.CoinBalance;
            SaveManager.Instance.AddCoins(5);
            SaveManager.Instance.SaveProgress();
            SaveManager.Instance.LoadProgress();
            bool roundTripOk = SaveManager.Instance.CoinBalance == before + 5;

            Debug.Log(roundTripOk
                ? "[Phase1Test] PASS: SaveManager PlayerPrefs round-trip succeeded."
                : $"[Phase1Test] FAIL: expected coin balance {before + 5}, got {SaveManager.Instance.CoinBalance}.");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 220, 90));
            if (GUILayout.Button("Load Level 0"))
            {
                GameManager.Instance.LoadLevel(0);
            }
            if (GUILayout.Button("Run Full Verification"))
            {
                RunVerification();
            }
            GUILayout.EndArea();
        }
    }
}
