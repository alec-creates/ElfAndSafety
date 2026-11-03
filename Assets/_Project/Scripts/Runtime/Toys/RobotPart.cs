using UnityEngine;

namespace ElfAndSafety
{
    public enum RobotPartKind
    {
        Battery,
        WindingKey
    }

    /// <summary>
    /// A removable robot component. Its condition is fixed per object: a dead battery stays dead and is
    /// replaced, not repaired, so dead and working batteries are separate prefabs with their own look.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public class RobotPart : MonoBehaviour, IDescribable
    {
        [SerializeField] RobotPartKind kind;
        [SerializeField] bool isWorking = true;

        Carryable carryable;

        public RobotPartKind Kind => kind;
        public bool IsWorking => isWorking;
        public Carryable Carryable => carryable != null ? carryable : carryable = GetComponent<Carryable>();
        public string DisplayName => Carryable.DisplayName;

        /// <summary>The robot this part is installed in, or null.</summary>
        public RobotBody InstalledIn => Carryable.State == CarryState.Installed ? Carryable.Owner as RobotBody : null;

        public string Describe()
        {
            if (kind == RobotPartKind.Battery)
            {
                return isWorking ? "Battery: working" : "Battery: DEAD (won't pass inspection)";
            }
            return "Winding key";
        }

        public static string SlotName(RobotPartKind kind) => kind == RobotPartKind.Battery ? "battery" : "winding key";
    }
}
