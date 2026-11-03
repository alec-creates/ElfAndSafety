using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Carry a robot here and press Interact to submit it. Valid robots leave play and count once;
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

        public string LastResult { get; private set; } = "Ship finished robots here";
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
                actionLabel = "Bring a finished robot to ship";
                return false;
            }

            if (player.Held.GetComponent<RobotBody>() == null)
            {
                actionLabel = "Only whole robots can be shipped";
                return false;
            }

            actionLabel = $"Ship {player.Held.DisplayName}";
            return true;
        }

        public void Interact(PlayerInteractor player) => TrySubmit(player);

        /// <summary>Inspects the robot the player is carrying. Returns true only if it was accepted.</summary>
        public bool TrySubmit(PlayerInteractor player)
        {
            RobotBody robot = player.Held != null ? player.Held.GetComponent<RobotBody>() : null;
            if (robot == null || robot.Carryable.State != CarryState.Held || !ShiftAllowsShipping)
            {
                return false;
            }

            bool accepted = RobotInspection.IsShippable(robot, out string reason);
            if (accepted)
            {
                // Consume first so the same robot can never be counted twice.
                robot.Carryable.Consume();
                counter.RecordShipment();
                LastResult = "ACCEPTED!";
                WorkshopFeedback.Info($"Shipped! Santa is pleased. ({counter.Shipped} shipped)");
            }
            else
            {
                Reject(robot, reason);
            }

            RefreshLabel();
            return accepted;
        }

        void Reject(RobotBody robot, string reason)
        {
            LastResult = $"REJECTED: {reason}";

            foreach (Transform point in rejectDropPoints)
            {
                Physics.SyncTransforms();
                if (point != null && !Physics.CheckSphere(point.position, rejectClearance, ~(1 << GameLayers.Player), QueryTriggerInteraction.Ignore))
                {
                    // Dropped loose into the bin: it tumbles in like anything else.
                    robot.Carryable.SetLoose(point.position, point.rotation, Vector3.zero);
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
