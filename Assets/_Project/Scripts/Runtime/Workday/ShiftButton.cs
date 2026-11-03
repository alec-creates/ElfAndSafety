using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// The CLOCK IN button: starts the scored shift, so players can look around and practise first.
    /// Needs a collider so it can be aimed at.
    /// </summary>
    public class ShiftButton : MonoBehaviour, IInteractable
    {
        [SerializeField] WorkdayManager workday;

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            if (workday.State == ShiftState.Running)
            {
                actionLabel = "Shift in progress. Get back to work!";
                return false;
            }

            if (!workday.CanStartShift(out string reason))
            {
                actionLabel = reason;
                return false;
            }

            actionLabel = $"Clock in: start shift ({WorkdayManager.FormatTime(workday.ShiftLengthSeconds)}, quota {workday.Quota})";
            return true;
        }

        public void Interact(PlayerInteractor player) => workday.StartShift();
    }
}
