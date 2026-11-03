namespace ElfAndSafety
{
    /// <summary>
    /// Anything the player can aim at and press Interact on.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Returns true when <paramref name="player"/> can use this right now, with a short action label
        /// for the prompt such as "Pick up Battery". When it returns false, the label may hold a short
        /// explanation ("Battery slot is already filled") or be null to say nothing.
        /// </summary>
        bool CanInteract(PlayerInteractor player, out string actionLabel);

        void Interact(PlayerInteractor player);
    }
}
