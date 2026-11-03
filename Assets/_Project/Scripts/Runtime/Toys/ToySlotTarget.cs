using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Stations that let the player work on one specific slot of a toy sitting in them.
    /// </summary>
    public interface IToySlotStation
    {
        bool CanUseSlot(PlayerInteractor player, ToyBody toy, int slotIndex, out string actionLabel);
        void UseSlot(PlayerInteractor player, ToyBody toy, int slotIndex);
    }

    /// <summary>
    /// Aim target on one of a toy's part mounts (needs a trigger collider). Lets the player remove or
    /// install that slot's part when the toy is in the work area of a station that supports it;
    /// does nothing elsewhere.
    /// </summary>
    public class ToySlotTarget : MonoBehaviour, IInteractable
    {
        [SerializeField] int slotIndex;

        ToyBody toy;

        public int SlotIndex => slotIndex;

        /// <summary>The kind of part this slot takes.</summary>
        public PartKind Slot => Toy != null && slotIndex < Toy.SlotCount ? Toy.Slots[slotIndex].accepts : PartKind.Battery;

        ToyBody Toy => toy != null ? toy : toy = GetComponentInParent<ToyBody>();

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            if (StationForToy() is IToySlotStation station)
            {
                return station.CanUseSlot(player, Toy, slotIndex, out actionLabel);
            }

            actionLabel = null;
            return false;
        }

        public void Interact(PlayerInteractor player)
        {
            if (StationForToy() is IToySlotStation station)
            {
                station.UseSlot(player, Toy, slotIndex);
            }
        }

        WorkStation StationForToy() => Toy != null ? WorkStation.Containing(Toy.transform.position) : null;
    }
}
