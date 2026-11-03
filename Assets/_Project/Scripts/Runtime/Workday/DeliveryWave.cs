using System;
using System.Collections.Generic;

namespace ElfAndSafety
{
    /// <summary>One part in a delivery, installed in a toy or loose.</summary>
    [Serializable]
    public struct PartSpec
    {
        public PartKind kind;
        public bool working;

        public PartSpec(PartKind kind, bool working = true)
        {
            this.kind = kind;
            this.working = working;
        }
    }

    /// <summary>A broken toy as it arrives: dirty or not, with whatever parts are still in it.</summary>
    [Serializable]
    public class DeliveredToy
    {
        public ToyType toy;
        public bool dirty = true;
        public List<PartSpec> installed = new List<PartSpec>();
    }

    /// <summary>One drop onto the delivery table: some toys and some loose parts (at most one item per table spot).</summary>
    [Serializable]
    public class DeliveryWave
    {
        public string label = "Delivery";
        public List<DeliveredToy> toys = new List<DeliveredToy>();
        public List<PartSpec> looseParts = new List<PartSpec>();

        public int ItemCount => toys.Count + looseParts.Count;

        // --- The standard waves the default week is built from. Each one is solvable on its own. ---

        /// <summary>The milestone 2 batch: parts for exactly two robots.</summary>
        public static DeliveryWave Robots() => new DeliveryWave
        {
            label = "Robots",
            toys = new List<DeliveredToy>
            {
                Toy(ToyType.WindUpRobot, true, new PartSpec(PartKind.Battery, false), new PartSpec(PartKind.WindingKey)),
                Toy(ToyType.WindUpRobot, true),
                Toy(ToyType.WindUpRobot, false, new PartSpec(PartKind.Battery)),
            },
            looseParts = new List<PartSpec> { new PartSpec(PartKind.Battery), new PartSpec(PartKind.WindingKey), new PartSpec(PartKind.Battery, false) },
        };

        /// <summary>Two bears, each missing something: enough for two teddy bears.</summary>
        public static DeliveryWave Bears() => new DeliveryWave
        {
            label = "Bears",
            toys = new List<DeliveredToy>
            {
                Toy(ToyType.TeddyBear, true, new PartSpec(PartKind.Stuffing)),
                Toy(ToyType.TeddyBear, false, new PartSpec(PartKind.ButtonEyes)),
            },
            looseParts = new List<PartSpec> { new PartSpec(PartKind.ButtonEyes), new PartSpec(PartKind.Stuffing), new PartSpec(PartKind.Battery, false) },
        };

        /// <summary>Two cars and spare wheels and batteries: enough for two toy cars.</summary>
        public static DeliveryWave Cars() => new DeliveryWave
        {
            label = "Cars",
            toys = new List<DeliveredToy>
            {
                Toy(ToyType.ToyCar, true, new PartSpec(PartKind.Battery, false), new PartSpec(PartKind.WheelSet)),
                Toy(ToyType.ToyCar, false, new PartSpec(PartKind.WheelSet), new PartSpec(PartKind.WheelSet)),
            },
            looseParts = new List<PartSpec> { new PartSpec(PartKind.Battery), new PartSpec(PartKind.Battery), new PartSpec(PartKind.WheelSet) },
        };

        /// <summary>Two dirty bears plus robot parts: enough for two cyborg bears.</summary>
        public static DeliveryWave CyborgKit() => new DeliveryWave
        {
            label = "Cyborg kit",
            toys = new List<DeliveredToy>
            {
                Toy(ToyType.TeddyBear, true, new PartSpec(PartKind.Stuffing), new PartSpec(PartKind.ButtonEyes)),
                Toy(ToyType.TeddyBear, true, new PartSpec(PartKind.Stuffing), new PartSpec(PartKind.ButtonEyes)),
            },
            looseParts = new List<PartSpec> { new PartSpec(PartKind.Battery), new PartSpec(PartKind.WindingKey), new PartSpec(PartKind.Battery), new PartSpec(PartKind.WindingKey) },
        };

        static DeliveredToy Toy(ToyType toy, bool dirty, params PartSpec[] installed) =>
            new DeliveredToy { toy = toy, dirty = dirty, installed = new List<PartSpec>(installed) };
    }
}
