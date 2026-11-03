using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Works on the toy resting on the bench. Aim at an installed part with empty hands and press Interact
    /// to remove it: the part pops out loose at the first clear output point, or into the player's hands if
    /// every output point is blocked. The body stays on the bench.
    /// </summary>
    public class SalvageBench : WorkStation, IToySlotStation
    {
        [SerializeField] Transform[] outputPoints;
        [SerializeField] float outputClearance = 0.09f;
        [SerializeField] TextMesh statusLabel;

        public Transform[] OutputPoints => outputPoints;

        protected override bool AcceptsPlacement(Carryable item, out string reason)
        {
            bool isToy = item.GetComponent<ToyBody>() != null;
            reason = isToy ? null : "Only toys go on the salvage bench";
            return isToy;
        }

        public bool CanUseSlot(PlayerInteractor player, ToyBody toy, int slotIndex, out string actionLabel)
        {
            ToyPart part = toy.GetSlotPart(slotIndex);
            if (part == null || toy != FindToy())
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

        public void UseSlot(PlayerInteractor player, ToyBody toy, int slotIndex) => TryRemoveSlot(player, slotIndex);

        /// <summary>Takes the first installed part of this kind out of the toy on the bench.</summary>
        public bool TryRemovePart(PlayerInteractor player, PartKind kind)
        {
            ToyBody toy = FindToy();
            if (toy == null) return false;

            for (int i = 0; i < toy.SlotCount; i++)
            {
                ToyPart part = toy.GetSlotPart(i);
                if (part != null && part.Kind == kind) return TryRemoveSlot(player, i);
            }
            return false;
        }

        /// <summary>Takes one slot's part out of the toy on the bench. Each installed part can be removed once.</summary>
        public bool TryRemoveSlot(PlayerInteractor player, int slotIndex)
        {
            ToyBody toy = FindToy();
            ToyPart part = toy != null ? toy.GetSlotPart(slotIndex) : null;
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

            ToyBody toy = FindToy();
            bool hasParts = false;
            if (toy != null) foreach (ToyPart _ in toy.InstalledParts) { hasParts = true; break; }

            statusLabel.text = toy == null ? "Put a toy here" : hasParts ? "Aim at a part to remove it" : "Nothing left to remove";
        }
    }
}
