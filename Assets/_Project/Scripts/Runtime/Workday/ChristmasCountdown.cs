using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ElfAndSafety
{
    /// <summary>
    /// The week before Christmas: one entry per day, from 7 days to go down to Christmas Eve. Each day has a
    /// wish list of orders, a shift length, and the delivery waves that bring its toys and parts. A day is
    /// only winnable if its waves bring enough toy bodies and working parts for every order.
    /// </summary>
    [CreateAssetMenu(menuName = "Elf & Safety/Christmas Countdown", fileName = "Christmas Countdown")]
    public class ChristmasCountdown : ScriptableObject
    {
        [Serializable]
        public class Day
        {
            [Tooltip("The day's wish list. The quota is how many orders there are.")]
            public List<WishOrder> orders = new List<WishOrder>();
            [Min(30f)] public float shiftLengthSeconds = 300f;
            [Tooltip("The first wave arrives at clock-in; the rest follow at the interval below.")]
            public List<DeliveryWave> waves = new List<DeliveryWave>();
            [Min(10f)] public float secondsBetweenWaves = 90f;
            [Tooltip("Santa's note for the day, shown before clocking in.")]
            [TextArea] public string santaNote = "";

            public int Quota => orders.Count;
        }

        [Tooltip("Index 0 is the first day (7 days until Christmas); the last entry is Christmas Eve.")]
        public List<Day> days = DefaultWeek();

        public int DayCount => days.Count;

        public Day GetDay(int dayIndex) => days[Mathf.Clamp(dayIndex, 0, days.Count - 1)];

        /// <summary>
        /// True if the day's waves bring a body for every ordered toy and enough working parts for every
        /// recipe, every wave fits on the table, and every wave arrives at least
        /// <paramref name="minimumWorkSeconds"/> before the shift ends.
        /// </summary>
        public static bool IsWinnable(Day day, int tableSpots, out string reason, float minimumWorkSeconds = 60f)
        {
            if (day.orders.Count == 0 || day.waves.Count == 0)
            {
                reason = "it has no orders or no deliveries";
                return false;
            }

            DeliveryWave tooBig = day.waves.FirstOrDefault(w => w.ItemCount > tableSpots);
            if (tooBig != null)
            {
                reason = $"the {tooBig.label} delivery has {tooBig.ItemCount} items but the table has {tableSpots} spots";
                return false;
            }

            float lastWave = (day.waves.Count - 1) * day.secondsBetweenWaves;
            if (lastWave > day.shiftLengthSeconds - minimumWorkSeconds)
            {
                reason = "the last delivery wave arrives too close to the end of the shift";
                return false;
            }

            // Bodies are per toy type; parts are interchangeable between toys, so totals are enough.
            foreach (ToyType toy in Enum.GetValues(typeof(ToyType)))
            {
                int wanted = day.orders.Count(o => o.Recipe.Toy == toy);
                int delivered = day.waves.Sum(w => w.toys.Count(t => t.toy == toy));
                if (wanted > delivered)
                {
                    reason = $"{wanted} {ToyRecipe.ToyName(toy)} orders but only {delivered} delivered";
                    return false;
                }
            }

            foreach (PartKind kind in Enum.GetValues(typeof(PartKind)))
            {
                int wanted = day.orders.Sum(o => o.Recipe.Count(kind));
                // Only good parts count: dead and faulty ones are extras that never complete an order.
                int good = day.waves.Sum(w =>
                    w.looseParts.Count(p => p.kind == kind && p.IsGood) +
                    w.toys.Sum(t => t.installed.Count(p => p.kind == kind && p.IsGood)));
                if (wanted > good)
                {
                    reason = $"orders need {wanted} x good {ToyRecipe.PartName(kind)}, but only {good} are delivered";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        static WishOrder Order(OrderKind kind, string kid) => new WishOrder { kind = kind, kidName = kid };

        /// <summary>
        /// The default week: robots only on day 1 (the milestone 3 game), then bears, cars and cyborg bears,
        /// with more orders each day.
        /// </summary>
        public static List<Day> DefaultWeek() => new List<Day>
        {
            new Day
            {
                orders = { Order(OrderKind.WindUpRobot, "Timmy"), Order(OrderKind.WindUpRobot, "Ava") },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 90f,
                waves = { DeliveryWave.Robots() },
                santaNote = "A gentle start. Santa is watching. Santa is always watching.",
            },
            new Day
            {
                orders = { Order(OrderKind.WindUpRobot, "Noah"), Order(OrderKind.WindUpRobot, "Mia"), Order(OrderKind.TeddyBear, "Leo") },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 100f,
                waves = { DeliveryWave.Robots(), DeliveryWave.Bears() },
                santaNote = "Teddy bears today. Stuffing goes INSIDE the bear. Check those button eyes are sewn on.",
            },
            new Day
            {
                orders = { Order(OrderKind.WindUpRobot, "Zoe"), Order(OrderKind.TeddyBear, "Sam"), Order(OrderKind.ToyCar, "Ivy") },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 90f,
                waves = { DeliveryWave.Robots(), DeliveryWave.Bears(), DeliveryWave.Cars() },
                santaNote = "Toy cars now. Two wheel sets each. Santa counted. Wobbly wheels go in the bin, not the car.",
            },
            new Day
            {
                orders = { Order(OrderKind.TeddyBear, "Max"), Order(OrderKind.TeddyBear, "Lily"), Order(OrderKind.ToyCar, "Omar"), Order(OrderKind.ToyCar, "Ruby") },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 90f,
                waves = { DeliveryWave.Bears(), DeliveryWave.Cars() },
                santaNote = "Halfway there. The furnace is warming up too.",
            },
            new Day
            {
                orders = { Order(OrderKind.WindUpRobot, "Finn"), Order(OrderKind.WindUpRobot, "Nora"), Order(OrderKind.CyborgBear, "Jax"), Order(OrderKind.CyborgBear, "Elsie") },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 90f,
                waves = { DeliveryWave.RiskyRobots(), DeliveryWave.CyborgKit() },
                santaNote = "Some kids want CYBORG BEARS. Bear, stuffing, eyes, battery, key. Don't ask why.",
            },
            new Day
            {
                orders = { Order(OrderKind.ToyCar, "Theo"), Order(OrderKind.ToyCar, "Maya"), Order(OrderKind.TeddyBear, "Eli"), Order(OrderKind.CyborgBear, "Rosa"), Order(OrderKind.CyborgBear, "Kai") },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 80f,
                waves = { DeliveryWave.Cars(), DeliveryWave.Bears(), DeliveryWave.CyborgKit() },
                santaNote = "Two days left. Three deliveries today. Smile.",
            },
            new Day
            {
                orders =
                {
                    Order(OrderKind.WindUpRobot, "Hugo"), Order(OrderKind.WindUpRobot, "Pip"), Order(OrderKind.ToyCar, "June"),
                    Order(OrderKind.TeddyBear, "Bea"), Order(OrderKind.CyborgBear, "Rex"), Order(OrderKind.CyborgBear, "Luna"),
                },
                shiftLengthSeconds = 300f, secondsBetweenWaves = 70f,
                waves = { DeliveryWave.RiskyRobots(), DeliveryWave.Cars(), DeliveryWave.CyborgKit(), DeliveryWave.Bears() },
                santaNote = "Christmas Eve. Every child. Every present. No excuses.",
            },
        };
    }
}
