using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Works on the robot resting on the bench. Aim at an installed part with empty hands and press Interact
    /// to remove it: the part pops out loose at the first clear output point, or into the player's hands if
    /// every output point is blocked. The body stays on the bench.
    /// </summary>
    public class SalvageBench : WorkStation, IRobotSlotStation
    {
        [SerializeField] Transform[] outputPoints;
        [SerializeField] float outputClearance = 0.09f;
        [SerializeField] TextMesh statusLabel;

        public Transform[] OutputPoints => outputPoints;

        protected override bool AcceptsPlacement(Carryable item, out string reason)
        {
            bool isRobot = item.GetComponent<RobotBody>() != null;
            reason = isRobot ? null : "Only robot bodies go on the salvage bench";
            return isRobot;
        }

        public bool CanUseSlot(PlayerInteractor player, RobotBody robot, RobotPartKind slot, out string actionLabel)
        {
            RobotPart part = robot.GetPart(slot);
            if (part == null || robot != FindRobot())
            {
                actionLabel = null;
                return false;
            }

            if (player.Held != null)
            {
                actionLabel = "Empty your hands to remove parts";
                return false;
            }

            actionLabel = $"Remove {part.DisplayName}";
            return true;
        }

        public void UseSlot(PlayerInteractor player, RobotBody robot, RobotPartKind slot) => TryRemovePart(player, slot);

        /// <summary>Takes the part out of the robot on this bench. Each installed part can be removed once.</summary>
        public bool TryRemovePart(PlayerInteractor player, RobotPartKind slot)
        {
            RobotBody robot = FindRobot();
            RobotPart part = robot != null ? robot.GetPart(slot) : null;
            if (part == null)
            {
                return false;
            }

            foreach (Transform point in outputPoints)
            {
                if (point != null && !IsBlocked(point.position, outputClearance))
                {
                    part.Carryable.SetLoose(point.position, point.rotation, Vector3.zero);
                    WorkshopFeedback.Info($"Recovered {part.DisplayName}");
                    return true;
                }
            }

            // Every output spot is blocked: hand it over instead of dropping it somewhere unreachable.
            if (player != null && player.ReceiveFromStation(part.Carryable))
            {
                WorkshopFeedback.Info($"Recovered {part.DisplayName} (output tray blocked)");
                return true;
            }

            WorkshopFeedback.Problem("Output tray is blocked. Clear it first");
            return false;
        }

        void Update()
        {
            if (statusLabel == null) return;

            RobotBody robot = FindRobot();
            statusLabel.text = robot == null
                ? "Put a robot here"
                : robot.Battery == null && robot.WindingKey == null ? "Nothing left to remove" : "Aim at a part to remove it";
        }
    }
}
