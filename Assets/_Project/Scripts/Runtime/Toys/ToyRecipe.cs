using System;
using System.Collections.Generic;

namespace ElfAndSafety
{
    public enum ToyType
    {
        WindUpRobot,
        TeddyBear,
        ToyCar
    }

    /// <summary>Removable parts. Values are stored in prefabs and assets: only ever append.</summary>
    public enum PartKind
    {
        Battery = 0,
        WindingKey = 1,
        Stuffing = 2,
        ButtonEyes = 3,
        WheelSet = 4
    }

    /// <summary>What a kid can wish for. Values are stored in assets: only ever append.</summary>
    public enum OrderKind
    {
        WindUpRobot,
        TeddyBear,
        ToyCar,
        CyborgBear
    }

    /// <summary>A kid's wish: which toy, for whom.</summary>
    [Serializable]
    public class WishOrder
    {
        public OrderKind kind;
        public string kidName = "a kid";

        public ToyRecipe Recipe => ToyRecipe.For(kind);
        public string Describe() => $"{kidName}'s {Recipe.Name}";
    }

    /// <summary>
    /// Exactly what a finished toy must be: which body, and which parts (repeats mean more than one, e.g.
    /// two wheel sets). Anything installed beyond the recipe isn't wanted.
    /// </summary>
    public sealed class ToyRecipe
    {
        public ToyType Toy { get; }
        public IReadOnlyList<PartKind> Parts { get; }
        public string Name { get; }

        ToyRecipe(ToyType toy, string name, params PartKind[] parts)
        {
            Toy = toy;
            Name = name;
            Parts = parts;
        }

        public static readonly ToyRecipe WindUpRobot = new ToyRecipe(ToyType.WindUpRobot, "wind-up robot", PartKind.Battery, PartKind.WindingKey);
        public static readonly ToyRecipe TeddyBear = new ToyRecipe(ToyType.TeddyBear, "teddy bear", PartKind.Stuffing, PartKind.ButtonEyes);
        public static readonly ToyRecipe ToyCar = new ToyRecipe(ToyType.ToyCar, "toy car", PartKind.Battery, PartKind.WheelSet, PartKind.WheelSet);
        public static readonly ToyRecipe CyborgBear = new ToyRecipe(ToyType.TeddyBear, "cyborg bear", PartKind.Stuffing, PartKind.ButtonEyes, PartKind.Battery, PartKind.WindingKey);

        public static readonly ToyRecipe[] All = { WindUpRobot, TeddyBear, ToyCar, CyborgBear };

        public static ToyRecipe For(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.TeddyBear: return TeddyBear;
                case OrderKind.ToyCar: return ToyCar;
                case OrderKind.CyborgBear: return CyborgBear;
                default: return WindUpRobot;
            }
        }

        /// <summary>The ordinary version of a toy, used outside a shift (practice).</summary>
        public static ToyRecipe Standard(ToyType toy)
        {
            switch (toy)
            {
                case ToyType.TeddyBear: return TeddyBear;
                case ToyType.ToyCar: return ToyCar;
                default: return WindUpRobot;
            }
        }

        public int Count(PartKind kind)
        {
            int count = 0;
            foreach (PartKind part in Parts) if (part == kind) count++;
            return count;
        }

        public static string ToyName(ToyType toy)
        {
            switch (toy)
            {
                case ToyType.TeddyBear: return "teddy bear";
                case ToyType.ToyCar: return "toy car";
                default: return "wind-up robot";
            }
        }

        public static string PartName(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.WindingKey: return "winding key";
                case PartKind.Stuffing: return "stuffing";
                case PartKind.ButtonEyes: return "button eyes";
                case PartKind.WheelSet: return "wheel set";
                default: return "battery";
            }
        }

        /// <summary>How a part that doesn't work is described: batteries are dead, everything else broken.</summary>
        public static string BrokenWord(PartKind kind) => kind == PartKind.Battery ? "dead" : "broken";
    }
}
