using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Works on the robot resting on the bench. While carrying a battery or winding key, aim at the robot,
    /// its slot, or the bench to install that actual part. The label lists what is missing and whether the
    /// body is dirty.
    /// </summary>
    public class AssemblyBench : WorkStation, IRobotSlotStation
    {
        [SerializeField] TextMesh statusLabel;

        protected override bool AcceptsPlacement(Carryable item, out string reason)
        {
            bool isRobot = item.GetComponent<RobotBody>() != null;
            reason = isRobot ? null : "Put a robot body here first, then install parts";
            return isRobot;
        }

        public override bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            RobotPart part = player.Held != null ? player.Held.GetComponent<RobotPart>() : null;
            RobotBody robot = FindRobot();
            if (part != null && robot != null)
            {
                return CanInstall(robot, part, out actionLabel);
            }

            return base.CanInteract(player, out actionLabel);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (player.Held != null && player.Held.GetComponent<RobotPart>() != null && FindRobot() != null)
            {
                TryInstallHeld(player);
            }
            else
            {
                base.Interact(player);
            }
        }

        public bool CanUseSlot(PlayerInteractor player, RobotBody robot, RobotPartKind slot, out string actionLabel)
        {
            RobotPart part = player.Held != null ? player.Held.GetComponent<RobotPart>() : null;
            if (part == null || robot != FindRobot())
            {
                // Empty-handed: let the ray carry on to the robot body so it can be picked up.
                actionLabel = null;
                return false;
            }

            if (part.Kind != slot)
            {
                actionLabel = $"That's the {RobotPart.SlotName(slot)} slot";
                return false;
            }

            return CanInstall(robot, part, out actionLabel);
        }

        public void UseSlot(PlayerInteractor player, RobotBody robot, RobotPartKind slot) => TryInstallHeld(player);

        /// <summary>Installs the part the player is carrying into the robot on this bench.</summary>
        public bool TryInstallHeld(PlayerInteractor player)
        {
            RobotBody robot = FindRobot();
            RobotPart part = player.Held != null ? player.Held.GetComponent<RobotPart>() : null;
            if (robot == null || part == null || !robot.TryInstall(part))
            {
                return false;
            }

            if (!part.IsWorking)
            {
                WorkshopFeedback.Problem($"Installed {part.DisplayName}. It's dead, so inspection will reject it");
            }
            return true;
        }

        static bool CanInstall(RobotBody robot, RobotPart part, out string actionLabel)
        {
            if (!robot.CanInstall(part, out string reason))
            {
                actionLabel = reason;
                return false;
            }

            actionLabel = part.IsWorking ? $"Install {part.DisplayName}" : $"Install {part.DisplayName} (it's dead!)";
            return true;
        }

        void Update()
        {
            if (statusLabel == null) return;

            RobotBody robot = FindRobot();
            if (robot == null)
            {
                statusLabel.text = "Put a robot body here";
                return;
            }

            string battery = robot.Battery == null ? "MISSING" : robot.Battery.IsWorking ? "ok" : "DEAD";
            string key = robot.WindingKey == null ? "MISSING" : "ok";
            string body = robot.IsDirty ? "DIRTY" : "clean";
            string verdict = RobotInspection.IsShippable(robot, out _) ? "READY TO SHIP" : "Not ready";
            statusLabel.text = $"Body: {body}\nBattery: {battery}\nKey: {key}\n{verdict}";
        }
    }
}
