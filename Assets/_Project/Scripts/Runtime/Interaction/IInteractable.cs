namespace ElfAndSafety
{
    /// <summary>
    /// Anything the player can aim at and press Interact on.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Returns true when <paramref name="player"/> can use this right now, with a short action label
        /// for the prompt such as "Pick up Battery".
        /// </summary>
        bool CanInteract(PlayerInteractor player, out string actionLabel);

        void Interact(PlayerInteractor player);
    }
}
