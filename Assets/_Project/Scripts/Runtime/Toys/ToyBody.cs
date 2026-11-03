using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// A toy body (robot, bear, car) with named part slots. Each slot refers to the actual
    /// <see cref="ToyPart"/> installed there; installed parts are parented to the slot's mount so they move
    /// with the toy. Which parts make a finished toy is up to the recipe (see <see cref="ToyInspection"/>):
    /// a bear's battery and key slots are only filled for a cyborg bear.
    /// </summary>
    [RequireComponent(typeof(Carryable))]
    public class ToyBody : MonoBehaviour, ICarryableOwner, IDescribable
    {
        public const int MaxSlots = 4;

        [Serializable]
        public class Slot
        {
            public string name = "Battery";
            public PartKind accepts;
            public Transform mount;
        }

        [SerializeField] ToyType type;
        [SerializeField] bool isDirty = true;
        [SerializeField] List<Slot> slots = new List<Slot>();
        [Tooltip("Shown while the body is dirty. Replace with any dirt art.")]
        [SerializeField] GameObject dirtVisual;

        static readonly List<ToyBody> all = new List<ToyBody>();

        ToyPart[] installed;
        Carryable carryable;

        /// <summary>Every toy currently in play, so stations can find the one in their work area.</summary>
        public static IReadOnlyList<ToyBody> All => all;

        public ToyType Type => type;
        public bool IsDirty => isDirty;
        public IReadOnlyList<Slot> Slots => slots;
        public int SlotCount => slots.Count;
        public Carryable Carryable => carryable != null ? carryable : carryable = GetComponent<Carryable>();
        public string DisplayName => Carryable.DisplayName;

        /// <summary>Shortcuts for the robot's two parts (the first installed part of that kind).</summary>
        public ToyPart Battery => GetPart(PartKind.Battery);
        public ToyPart WindingKey => GetPart(PartKind.WindingKey);

        /// <summary>Raised when a part is installed or removed, or the body is cleaned.</summary>
        public event Action<ToyBody> Changed;

        ToyPart[] Installed => installed ??= new ToyPart[slots.Count];

        void Awake()
        {
            RefreshDirtVisual();
        }

        void OnEnable() => all.Add(this);

        void OnDisable() => all.Remove(this);

        public ToyPart GetSlotPart(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount ? Installed[slotIndex] : null;

        /// <summary>The first installed part of this kind, or null.</summary>
        public ToyPart GetPart(PartKind kind)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (Installed[i] != null && Installed[i].Kind == kind) return Installed[i];
            }
            return null;
        }

        public IEnumerable<ToyPart> InstalledParts
        {
            get
            {
                foreach (ToyPart part in Installed) if (part != null) yield return part;
            }
        }

        public bool HasSlotFor(PartKind kind) => slots.Exists(s => s.accepts == kind);

        /// <summary>Can this part go into some empty slot? Gives the reason when not.</summary>
        public bool CanInstall(ToyPart part, out string reason) => CanInstallAt(FirstEmptySlot(part), part, out reason);

        public bool CanInstallAt(int slotIndex, ToyPart part, out string reason)
        {
            if (part == null)
            {
                reason = "Only toy parts can be installed";
                return false;
            }

            string partName = ToyRecipe.PartName(part.Kind);
            if (part.InstalledIn == this)
            {
                reason = $"{part.DisplayName} is already installed";
                return false;
            }

            if (!HasSlotFor(part.Kind))
            {
                reason = $"A {ToyRecipe.ToyName(type)} has no {partName} slot";
                return false;
            }

            if (slotIndex < 0 || slotIndex >= SlotCount || slots[slotIndex].accepts != part.Kind || Installed[slotIndex] != null)
            {
                int matching = slots.FindAll(s => s.accepts == part.Kind).Count;
                reason = matching > 1 ? $"All {partName} slots are filled" : $"The {partName} slot is already filled";
                return false;
            }

            reason = null;
            return true;
        }

        // --- State changes, called by stations and deliveries. ---

        /// <summary>Moves the actual part into the first empty slot that takes it, releasing it from wherever it was.</summary>
        internal bool TryInstall(ToyPart part) => part != null && TryInstallAt(FirstEmptySlot(part), part);

        internal bool TryInstallAt(int slotIndex, ToyPart part)
        {
            if (!CanInstallAt(slotIndex, part, out _) || part.Carryable.State == CarryState.Consumed)
            {
                return false;
            }

            part.Carryable.SetInstalled(this, slots[slotIndex].mount);
            Installed[slotIndex] = part;
            Changed?.Invoke(this);
            return true;
        }

        internal void SetDirty(bool dirty)
        {
            if (isDirty == dirty)
            {
                return;
            }

            isDirty = dirty;
            RefreshDirtVisual();
            Changed?.Invoke(this);
        }

        void ICarryableOwner.OnCarryableReleased(Carryable item)
        {
            // A part leaving its slot always comes through here, however it was moved.
            for (int i = 0; i < Installed.Length; i++)
            {
                if (Installed[i] != null && Installed[i].Carryable == item)
                {
                    Installed[i] = null;
                    if (this != null) Changed?.Invoke(this);
                    return;
                }
            }
        }

        public string Describe()
        {
            var parts = new List<string> { $"body {(isDirty ? "DIRTY" : "clean")}" };
            for (int i = 0; i < SlotCount; i++)
            {
                ToyPart part = Installed[i];
                string state = part == null ? "empty" : part.ConditionWord;
                parts.Add($"{slots[i].name.ToLower()} {state}");
            }

            ToyRecipe complete = ToyInspection.CompleteRecipe(this);
            string verdict = complete != null ? $"   (finished {complete.Name})" : string.Empty;
            return $"{DisplayName}: {string.Join(" | ", parts)}{verdict}";
        }

        int FirstEmptySlot(ToyPart part)
        {
            if (part == null) return -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i].accepts == part.Kind && Installed[i] == null) return i;
            }
            // No empty slot: report against the first matching one, so the reason says it's filled.
            return slots.FindIndex(s => s.accepts == part.Kind);
        }

        void RefreshDirtVisual()
        {
            if (dirtVisual != null)
            {
                dirtVisual.SetActive(isDirty);
            }
        }
    }
}
