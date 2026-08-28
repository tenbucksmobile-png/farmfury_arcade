using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>Audit findings F3.5/F4.4: no purchase surface in this project (Shop's direct Remove
    /// Ads purchase, Coin Purchase's 4 packs + Restore Purchases, and the Hat/Trail/World Purchase
    /// screens sharing CosmeticPurchaseScreen) had any age-gate in front of a real-money charge,
    /// despite AdManager treating every player as child-directed (see the class doc comment on
    /// AdManager) — a real tension Apple flags under Guideline 3.1.1 for an app that signals a
    /// young audience elsewhere. This is a simple arithmetic parental gate, the same accepted
    /// pattern most COPPA-conscious apps use: a randomised addition problem with 3 answer choices,
    /// one correct. A wrong tap re-rolls a new question rather than locking the player out or
    /// dismissing the gate — friction, not a hard failure.
    ///
    /// Singleton (Utilities.Singleton&lt;T&gt;) rather than a per-screen serialized reference, so
    /// every purchase call site can gate itself with one line (ParentalGateController.Instance.
    /// Show(...)) without Phase5ProjectBuilder needing to wire a new cross-reference field onto
    /// each of the 3 different screens that call it.
    ///
    /// Built as a plain overlay on Canvas (Phase5ProjectBuilder.BuildParentalGate) — shown/hidden
    /// directly via SetActive, same convention as Pause/Settings/Legal, not a SceneTransitionManager
    /// screen. transform.SetAsLastSibling() on Show() guards against the exact "built earlier so it
    /// draws underneath a later sibling" bug already found and fixed for MenuHubScreen.</summary>
    public class ParentalGateController : Singleton<ParentalGateController>
    {
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private Button[] answerButtons = new Button[3];
        [SerializeField] private TextMeshProUGUI[] answerLabels = new TextMeshProUGUI[3];
        [SerializeField] private Button cancelButton;

        private int _correctIndex;
        private Action _onPassed;

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < answerButtons.Length; i++)
            {
                int captured = i; // avoid the classic C# loop-variable-capture bug
                if (answerButtons[captured] != null)
                {
                    answerButtons[captured].onClick.AddListener(() => HandleAnswerTapped(captured));
                }
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancel);
            }

            gameObject.SetActive(false);
        }

        /// <summary>Shows the gate; onPassed fires exactly once, only on a correct answer. Never
        /// fires on Cancel or on a closed/destroyed gate — callers should treat "no callback" as
        /// "purchase not authorised," the same not-just-assume-success discipline AdManager.
        /// ShowRewardedAd's own doc comment already asks of its callers.</summary>
        public void Show(Action onPassed)
        {
            _onPassed = onPassed;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RollNewQuestion();
        }

        private void RollNewQuestion()
        {
            int a = UnityEngine.Random.Range(2, 10);
            int b = UnityEngine.Random.Range(2, 10);
            int correct = a + b;

            if (questionText != null)
            {
                questionText.text = $"What is {a} + {b}?";
            }

            int wrong1 = RollDistinctWrongAnswer(correct, correct);
            int wrong2 = RollDistinctWrongAnswer(correct, wrong1);
            int[] wrongValues = { wrong1, wrong2 };

            _correctIndex = UnityEngine.Random.Range(0, answerLabels.Length);
            int wrongCursor = 0;
            for (int i = 0; i < answerLabels.Length; i++)
            {
                int value = i == _correctIndex ? correct : wrongValues[wrongCursor++];
                if (answerLabels[i] != null)
                {
                    answerLabels[i].text = value.ToString();
                }
            }
        }

        /// <summary>Picks a wrong answer within a few points of the correct one (so all 3 choices
        /// look like plausible arithmetic, not "click the only 2-digit number") that doesn't
        /// collide with the correct answer or the other wrong answer already rolled.</summary>
        private static int RollDistinctWrongAnswer(int correct, int avoid)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int candidate = correct + UnityEngine.Random.Range(-4, 5);
                if (candidate > 0 && candidate != correct && candidate != avoid)
                {
                    return candidate;
                }
            }
            return correct + 1; // exhausted retries (shouldn't happen at this range) — still distinct from correct
        }

        private void HandleAnswerTapped(int index)
        {
            if (index == _correctIndex)
            {
                gameObject.SetActive(false);
                var callback = _onPassed;
                _onPassed = null;
                callback?.Invoke();
            }
            else
            {
                RollNewQuestion();
            }
        }

        private void HandleCancel()
        {
            gameObject.SetActive(false);
            _onPassed = null;
        }
    }
}
