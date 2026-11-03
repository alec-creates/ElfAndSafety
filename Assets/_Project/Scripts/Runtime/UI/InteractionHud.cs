using UnityEngine;
using UnityEngine.UI;

namespace ElfAndSafety
{
    /// <summary>
    /// Crosshair plus prompts for the local player: what Interact will do, what is being carried,
    /// a status line for whatever is under the crosshair, station feedback, and the shipped count.
    /// </summary>
    public class InteractionHud : MonoBehaviour
    {
        [SerializeField] PlayerInteractor player;
        [SerializeField] Text promptLabel;
        [SerializeField] Text carryLabel;
        [SerializeField] Graphic crosshair;
        [SerializeField] Color idleCrosshairColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] Color activeCrosshairColor = new Color(1f, 0.82f, 0.2f, 1f);

        [Header("Milestone 2")]
        [SerializeField] Text infoLabel;
        [SerializeField] Text feedbackLabel;
        [SerializeField] Text shippedLabel;
        [SerializeField] ShipmentCounter counter;
        [SerializeField] float feedbackSeconds = 3.5f;
        [SerializeField] Color hintColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] Color infoFeedbackColor = new Color(0.55f, 1f, 0.55f);
        [SerializeField] Color problemFeedbackColor = new Color(1f, 0.45f, 0.35f);

        float feedbackHideTime;

        public void Bind(PlayerInteractor target) => player = target;

        void OnEnable() => WorkshopFeedback.Posted += ShowFeedback;

        void OnDisable() => WorkshopFeedback.Posted -= ShowFeedback;

        void Update()
        {
            bool hasTarget = player != null && player.Target != null;
            if (hasTarget)
            {
                promptLabel.text = $"[E] {player.TargetAction}";
                promptLabel.color = Color.white;
            }
            else
            {
                promptLabel.text = player != null && player.TargetHint != null ? player.TargetHint : string.Empty;
                promptLabel.color = hintColor;
            }

            Carryable held = player != null ? player.Held : null;
            carryLabel.text = held != null ? $"Carrying: {held.DisplayName}    [Q] Drop" : string.Empty;

            crosshair.color = hasTarget ? activeCrosshairColor : idleCrosshairColor;

            if (infoLabel != null)
            {
                infoLabel.text = player != null && player.LookInfo != null ? player.LookInfo : string.Empty;
            }

            if (shippedLabel != null)
            {
                shippedLabel.text = counter != null ? $"Toys shipped: {counter.Shipped}" : string.Empty;
            }

            if (feedbackLabel != null && Time.time > feedbackHideTime)
            {
                feedbackLabel.text = string.Empty;
            }
        }

        void ShowFeedback(string message, bool isProblem)
        {
            if (feedbackLabel == null) return;
            feedbackLabel.text = message;
            feedbackLabel.color = isProblem ? problemFeedbackColor : infoFeedbackColor;
            feedbackHideTime = Time.time + feedbackSeconds;
        }
    }
}
