using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A hidden defect in an otherwise usable-looking part. Each one makes the toy it's installed in
    /// misbehave (see <see cref="PartMalfunction"/>), and none passes inspection. Values are stored in
    /// prefabs and assets: only ever append.
    /// </summary>
    public enum PartFault
    {
        None = 0,
        /// <summary>Winding key: the toy runs away whenever it's left loose.</summary>
        Overwound = 1,
        /// <summary>Battery: the toy zaps whoever picks it up.</summary>
        Leaky = 2,
        /// <summary>Wheel set: the car drives off on its own.</summary>
        Wobbly = 3,
        /// <summary>Button eyes: they pop off while the toy is carried.</summary>
        LooseThreads = 4
    }

    /// <summary>
    /// A removable toy component. Its condition is fixed per object: a dead battery stays dead and a faulty
    /// part stays faulty; they are replaced, not repaired, so each condition is its own prefab with its own look.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public class ToyPart : MonoBehaviour, IDescribable
    {
        [SerializeField] PartKind kind;
        [SerializeField] bool isWorking = true;
        [Tooltip("A hidden defect. Shows as a small visual tell, and makes the toy misbehave once installed.")]
        [SerializeField] PartFault fault = PartFault.None;

        Carryable carryable;

        public PartKind Kind => kind;
        public bool IsWorking => isWorking;
        public PartFault Fault => fault;
        public bool IsFaulty => fault != PartFault.None;

        /// <summary>Usable for a finished toy: working and without a defect.</summary>
        public bool IsGood => isWorking && !IsFaulty;

        public Carryable Carryable => carryable != null ? carryable : carryable = GetComponent<Carryable>();
        public string DisplayName => Carryable.DisplayName;

        /// <summary>The toy this part is installed in, or null.</summary>
        public ToyBody InstalledIn => Carryable.State == CarryState.Installed ? Carryable.Owner as ToyBody : null;

        /// <summary>The word for this part's condition on labels: ok, DEAD, or its defect's tell (e.g. rusty).</summary>
        public string ConditionWord => !isWorking ? ToyRecipe.BrokenWord(kind).ToUpper() : IsFaulty ? TellWord(fault) : "ok";

        public string Describe()
        {
            string name = ToyRecipe.PartName(kind);
            string title = char.ToUpper(name[0]) + name.Substring(1);
            if (!isWorking) return $"{title}: {ToyRecipe.BrokenWord(kind).ToUpper()} (won't pass inspection)";
            if (IsFaulty) return $"{title}: looks {TellWord(fault)}...";
            return kind == PartKind.Battery ? "Battery: working" : title;
        }

        /// <summary>Lower-case slot name for prompts, e.g. "battery".</summary>
        public static string SlotName(PartKind kind) => ToyRecipe.PartName(kind);

        /// <summary>What a careful elf notices about a faulty part.</summary>
        public static string TellWord(PartFault fault)
        {
            switch (fault)
            {
                case PartFault.Overwound: return "rusty";
                case PartFault.Leaky: return "leaking";
                case PartFault.Wobbly: return "wobbly";
                case PartFault.LooseThreads: return "loose";
                default: return "fine";
            }
        }
    }
}
