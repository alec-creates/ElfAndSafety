using System;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A fixed placement point on a station. Holds one carryable at its attach point with no physics joints.
    /// Needs a trigger collider covering the slot so the player can aim at it.
    /// </summary>
    public class ItemSocket : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Bench";
        [SerializeField] Transform attachPoint;

        public string DisplayName => displayName;
        public Carryable Occupant { get; private set; }
        public bool IsEmpty => Occupant == null;

        /// <summary>Raised whenever an item is placed in or removed from this socket.</summary>
        public event Action<ItemSocket> OccupantChanged;

        void Reset()
        {
            attachPoint = transform;
        }

        /// <summary>Override in station-specific sockets to only accept certain items.</summary>
        public virtual bool Accepts(Carryable item) => true;

        public bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            Carryable held = player.Held;
            if (held == null)
            {
                // Aiming anywhere at an occupied slot is enough to take its item.
                actionLabel = Occupant != null ? $"Take {Occupant.DisplayName}" : null;
                return Occupant != null;
            }

            actionLabel = $"Place {held.DisplayName} on {displayName}";
            return IsEmpty && Accepts(held);
        }

        public void Interact(PlayerInteractor player)
        {
            if (player.Held == null)
            {
                if (Occupant != null) player.RequestPickUp(Occupant);
            }
            else
            {
                player.RequestPlace(this);
            }
        }

        internal bool TryPlace(Carryable item)
        {
            if (!IsEmpty || !Accepts(item))
            {
                return false;
            }

            Occupant = item;
            item.AttachToSocket(this, attachPoint != null ? attachPoint : transform);
            OccupantChanged?.Invoke(this);
            return true;
        }

        internal void ReleaseOccupant(Carryable item)
        {
            if (Occupant != item)
            {
                return;
            }

            Occupant = null;
            OccupantChanged?.Invoke(this);
        }
    }
}
