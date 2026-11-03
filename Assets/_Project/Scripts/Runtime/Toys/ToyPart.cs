using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A removable toy component. Its condition is fixed per object: a dead battery stays dead and is
    /// replaced, not repaired, so working and dead parts are separate prefabs with their own look.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public class ToyPart : MonoBehaviour, IDescribable
    {
        [SerializeField] PartKind kind;
        [SerializeField] bool isWorking = true;

        Carryable carryable;

        public PartKind Kind => kind;
        public bool IsWorking => isWorking;
        public Carryable Carryable => carryable != null ? carryable : carryable = GetComponent<Carryable>();
        public string DisplayName => Carryable.DisplayName;

        /// <summary>The toy this part is installed in, or null.</summary>
        public ToyBody InstalledIn => Carryable.State == CarryState.Installed ? Carryable.Owner as ToyBody : null;

        public string Describe()
        {
            string name = ToyRecipe.PartName(kind);
            if (isWorking) return kind == PartKind.Battery ? "Battery: working" : char.ToUpper(name[0]) + name.Substring(1);
            return $"{char.ToUpper(name[0]) + name.Substring(1)}: {ToyRecipe.BrokenWord(kind).ToUpper()} (won't pass inspection)";
        }

        /// <summary>Lower-case slot name for prompts, e.g. "battery".</summary>
        public static string SlotName(PartKind kind) => ToyRecipe.PartName(kind);
    }
}
