namespace ElfAndSafety
{
    /// <summary>
    /// Something that can own a <see cref="Carryable"/>: a player's hands, a station slot, or a robot's part slot.
    /// A carryable has at most one owner, and tells it when it leaves so the owner can clear its reference.
    /// </summary>
    public interface ICarryableOwner
    {
        void OnCarryableReleased(Carryable item);
    }
}
