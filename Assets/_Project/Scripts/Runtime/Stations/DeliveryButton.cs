using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A physical button on the delivery chute: request a test batch, or (development) reset it.
    /// Needs a collider so it can be aimed at.
    /// </summary>
    public class DeliveryButton : MonoBehaviour, IInteractable
    {
        public enum ButtonAction { RequestDelivery, DevResetBatch }

        [SerializeField] DeliveryChute chute;
        [SerializeField] ButtonAction action;

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            if (action == ButtonAction.DevResetBatch)
            {
                if (!chute.CanDevReset(out string resetBlocked))
                {
                    actionLabel = $"DEV reset: {resetBlocked}";
                    return false;
                }

                actionLabel = "DEV: Reset test batch (removes all delivered toys)";
                return true;
            }

            if (!chute.CanRequestDelivery(out string reason))
            {
                actionLabel = reason;
                return false;
            }

            actionLabel = "Request test delivery";
            return true;
        }

        public void Interact(PlayerInteractor player)
        {
            if (action == ButtonAction.DevResetBatch) chute.ResetTestBatch();
            else chute.TryRequestDelivery();
        }
    }
}
