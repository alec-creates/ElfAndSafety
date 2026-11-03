using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Carry a toy here and press Interact to submit it. During a shift it must exactly match an open wish
    /// order, which it fills; in practice it only has to be its ordinary finished version. Accepted toys
    /// leave play and count once;
    /// invalid ones drop loose into the reject bin at the first clear drop point, with the reason, or stay
    /// in the player's hands if the bin is full. Needs a trigger collider over the chute mouth.
    /// </summary>
    public class ShippingChute : MonoBehaviour, IInteractable
    {
        [SerializeField] ShipmentCounter counter;
        [Tooltip("Optional. When set, shipping stops once the shift has ended.")]
        [SerializeField] WorkdayManager workday;
        [SerializeField] Transform[] rejectDropPoints;
        [SerializeField] float rejectClearance = 0.2f;
        [SerializeField] TextMesh statusLabel;

        const string IdleText = "Ship finished toys here";

        public string LastResult { get; private set; } = IdleText;
        public Transform[] RejectDropPoints => rejectDropPoints;

        void Start()
        {
            if (counter != null) counter.Changed += OnCountChanged;
            RefreshLabel();
        }

        void OnDestroy()
        {
            if (counter != null) counter.Changed -= OnCountChanged;
        }

        void OnCountChanged(int shipped) => RefreshLabel();

        /// <summary>A new day: forget yesterday's last result.</summary>
        internal void ResetForNewDay()
        {
            LastResult = IdleText;
            RefreshLabel();
        }

        /// <summary>Co-op client: show the host's last inspection result.</summary>
        internal void MirrorLastResult(string result)
        {
            if (LastResult == result) return;
            LastResult = result;
            RefreshLabel();
        }

        bool ShiftAllowsShipping => workday == null || workday.AcceptsShipments;

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            if (!ShiftAllowsShipping)
            {
                actionLabel = "The shift is over";
                return false;
            }

            if (player.Held == null)
            {
                actionLabel = "Bring a finished toy to ship";
                return false;
            }

            if (player.Held.GetComponent<ToyBody>() == null)
            {
                actionLabel = "Only whole toys can be shipped";
                return false;
            }

            actionLabel = $"Ship {player.Held.DisplayName}";
            return true;
        }

        public void Interact(PlayerInteractor player) => TrySubmit(player);

        /// <summary>Inspects the toy the player is carrying. Returns true only if it was accepted.</summary>
        public bool TrySubmit(PlayerInteractor player)
        {
            ToyBody toy = player.Held != null ? player.Held.GetComponent<ToyBody>() : null;
            if (toy == null || toy.Carryable.State != CarryState.Held || !ShiftAllowsShipping)
            {
                return false;
            }

            int orderIndex = -1;
            string reason;
            bool scored = workday != null && workday.State == ShiftState.Running;
            bool accepted = scored ? workday.TryMatchOrder(toy, out orderIndex, out reason) : ToyInspection.IsShippable(toy, out reason);
            if (accepted)
            {
                string what = orderIndex >= 0 ? workday.Orders[orderIndex].Describe() : toy.DisplayName;

                // Consume first so the same toy can never be counted twice; fill the order before counting,
                // so the shipment that completes the wish list ends the shift with every order filled.
                toy.Carryable.Consume();
                if (orderIndex >= 0) workday.FillOrder(orderIndex);
                counter.RecordShipment();
                LastResult = $"ACCEPTED: {what}";
                WorkshopFeedback.Info($"Shipped {what}! Santa is pleased. ({counter.Shipped} shipped)");
            }
            else
            {
                Reject(toy, reason);
            }

            RefreshLabel();
            return accepted;
        }

        void Reject(ToyBody toy, string reason)
        {
            LastResult = $"REJECTED: {reason}";

            foreach (Transform point in rejectDropPoints)
            {
                Physics.SyncTransforms();
                if (point != null && !Physics.CheckSphere(point.position, rejectClearance, ~(1 << GameLayers.Player), QueryTriggerInteraction.Ignore))
                {
                    // Dropped loose into the bin: it tumbles in like anything else.
                    toy.Carryable.SetLoose(point.position, point.rotation, Vector3.zero);
                    WorkshopFeedback.Problem($"Rejected: {reason}. Dumped in the reject bin");
                    return;
                }
            }

            WorkshopFeedback.Problem($"Rejected: {reason}. Reject bin is full, so you keep it");
        }

        void RefreshLabel()
        {
            if (statusLabel != null)
            {
                statusLabel.text = $"SHIPPED: {(counter != null ? counter.Shipped : 0)}\n{LastResult}";
            }
        }
    }
}
