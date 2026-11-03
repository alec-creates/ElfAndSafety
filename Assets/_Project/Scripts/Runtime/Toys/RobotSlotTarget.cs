using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Stations that let the player work on one specific slot of a robot sitting in them.
    /// </summary>
    public interface IRobotSlotStation
    {
        bool CanUseSlot(PlayerInteractor player, RobotBody robot, RobotPartKind slot, out string actionLabel);
        void UseSlot(PlayerInteractor player, RobotBody robot, RobotPartKind slot);
    }

    /// <summary>
    /// Aim target on a robot's battery or key mount (needs a trigger collider). Lets the player remove or
    /// install that specific part when the robot is in the work area of a station that supports it;
    /// does nothing elsewhere.
    /// </summary>
    public class RobotSlotTarget : MonoBehaviour, IInteractable
    {
        [SerializeField] RobotPartKind slot;

        RobotBody robot;

        public RobotPartKind Slot => slot;

        void Awake()
        {
            robot = GetComponentInParent<RobotBody>();
        }

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            if (StationForRobot() is IRobotSlotStation station)
            {
                return station.CanUseSlot(player, robot, slot, out actionLabel);
            }

            actionLabel = null;
            return false;
        }

        public void Interact(PlayerInteractor player)
        {
            if (StationForRobot() is IRobotSlotStation station)
            {
                station.UseSlot(player, robot, slot);
            }
        }

        WorkStation StationForRobot() => robot != null ? WorkStation.Containing(robot.transform.position) : null;
    }
}
