using System.Text;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// Works on the toy resting on the bench. While carrying a part, aim at the toy or the bench to install
    /// that actual part in the first empty slot that takes it, or aim at a specific slot. The label lists
    /// every slot, whether the body is dirty, and which finished toy (if any) it now is.
    /// </summary>
    public class AssemblyBench : WorkStation, IToySlotStation
    {
        [SerializeField] TextMesh statusLabel;

        protected override bool AcceptsPlacement(Carryable item, out string reason)
        {
            bool isToy = item.GetComponent<ToyBody>() != null;
            reason = isToy ? null : "Put a toy here first, then install parts";
            return isToy;
        }

        public override bool CanInteract(PlayerInteractor player, out string actionLabel)
        {
            ToyPart part = player.Held != null ? player.Held.GetComponent<ToyPart>() : null;
            ToyBody toy = FindToy();
            if (part != null && toy != null)
            {
                return Describe(toy.CanInstall(part, out string reason), part, reason, out actionLabel);
            }

            return base.CanInteract(player, out actionLabel);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (player.Held != null && player.Held.GetComponent<ToyPart>() != null && FindToy() != null)
            {
                TryInstallHeld(player);
            }
            else
            {
                base.Interact(player);
            }
        }

        public bool CanUseSlot(PlayerInteractor player, ToyBody toy, int slotIndex, out string actionLabel)
        {
            ToyPart part = player.Held != null ? player.Held.GetComponent<ToyPart>() : null;
            if (part == null || toy != FindToy())
            {
                // Empty-handed: let the ray carry on to the toy body so it can be picked up.
                actionLabel = null;
                return false;
            }

            PartKind slot = toy.Slots[slotIndex].accepts;
            if (part.Kind != slot)
            {
                actionLabel = $"That's the {ToyPart.SlotName(slot)} slot";
                return false;
            }

            return Describe(toy.CanInstallAt(slotIndex, part, out string reason), part, reason, out actionLabel);
        }

        public void UseSlot(PlayerInteractor player, ToyBody toy, int slotIndex) => TryInstallHeld(player, slotIndex);

        /// <summary>Installs the carried part into the toy on this bench (into a given slot, or the first that fits).</summary>
        public bool TryInstallHeld(PlayerInteractor player, int slotIndex = -1)
        {
            ToyBody toy = FindToy();
            ToyPart part = player.Held != null ? player.Held.GetComponent<ToyPart>() : null;
            if (toy == null || part == null)
            {
                return false;
            }

            bool installed = slotIndex >= 0 ? toy.TryInstallAt(slotIndex, part) : toy.TryInstall(part);
            if (!installed)
            {
                return false;
            }

            if (!part.IsWorking)
            {
                WorkshopFeedback.Problem($"Installed {part.DisplayName}. It's {ToyRecipe.BrokenWord(part.Kind)}, so inspection will reject it");
            }
            return true;
        }

        static bool Describe(bool canInstall, ToyPart part, string reason, out string actionLabel)
        {
            if (!canInstall)
            {
                actionLabel = reason;
                return false;
            }

            // Dead parts are obvious; faulty ones aren't, so the prompt doesn't give them away.
            actionLabel = part.IsWorking ? $"Install {part.DisplayName}" : $"Install {part.DisplayName} (it's {ToyRecipe.BrokenWord(part.Kind)}!)";
            return true;
        }

        void Update()
        {
            if (statusLabel == null) return;

            ToyBody toy = FindToy();
            if (toy == null)
            {
                statusLabel.text = "Put a toy here";
                return;
            }

            var text = new StringBuilder($"Body: {(toy.IsDirty ? "DIRTY" : "clean")}\n");
            for (int i = 0; i < toy.SlotCount; i++)
            {
                ToyPart part = toy.GetSlotPart(i);
                string state = part == null ? "empty" : part.ConditionWord;
                text.Append($"{toy.Slots[i].name}: {state}\n");
            }

            ToyRecipe complete = ToyInspection.CompleteRecipe(toy);
            text.Append(complete != null ? $"READY: {complete.Name}" : "Not ready");
            statusLabel.text = text.ToString();
        }
    }
}
