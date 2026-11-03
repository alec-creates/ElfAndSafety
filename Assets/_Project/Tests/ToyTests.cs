using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// Bears, cars and cyborg bears on the real Workshop scene: their slots, the recipes, and how shipped
    /// toys are matched to the day's wish orders.
    /// </summary>
    public class ToyTests
    {
        WorkdayManager workday;
        PlayerInteractor player;
        DeliveryChute chute;
        ShippingChute shipping;
        AssemblyBench assembly;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Workshop");
            workday = Object.FindAnyObjectByType<WorkdayManager>();
            player = Object.FindAnyObjectByType<PlayerInteractor>();
            chute = Object.FindAnyObjectByType<DeliveryChute>();
            shipping = Object.FindAnyObjectByType<ShippingChute>();
            assembly = Object.FindAnyObjectByType<AssemblyBench>();
        }

        [Test]
        public void Toys_HaveTheirSlots()
        {
            // Day 4's waves are bears then cars.
            workday.SetDayForTest(3);
            Assert.IsTrue(workday.StartShift());
            DeliverNextWave();

            ToyBody bear = Toys(ToyType.TeddyBear).First();
            CollectionAssert.AreEqual(new[] { PartKind.Stuffing, PartKind.ButtonEyes, PartKind.Battery, PartKind.WindingKey }, bear.Slots.Select(s => s.accepts));

            ToyBody car = Toys(ToyType.ToyCar).First();
            CollectionAssert.AreEqual(new[] { PartKind.Battery, PartKind.WheelSet, PartKind.WheelSet }, car.Slots.Select(s => s.accepts));
            Assert.IsTrue(car.Slots.All(s => s.mount != null), "Every slot has a mount");
        }

        [Test]
        public void Inspection_KnowsBearsCarsAndCyborgBears()
        {
            workday.SetDayForTest(5);
            Assert.IsTrue(workday.StartShift());
            DeliverNextWave();
            DeliverNextWave();

            // A plain bear: clean, stuffing and eyes.
            ToyBody bear = Toys(ToyType.TeddyBear).First(b => b.GetPart(PartKind.Stuffing) != null && b.GetPart(PartKind.ButtonEyes) != null);
            bear.SetDirty(false);
            Assert.IsTrue(ToyInspection.Passes(bear, ToyRecipe.TeddyBear, out string plain), plain);
            Assert.IsFalse(ToyInspection.Passes(bear, ToyRecipe.CyborgBear, out string notCyborg));
            StringAssert.Contains(ToyInspection.MissingBattery, notCyborg);
            StringAssert.Contains(ToyInspection.MissingKey, notCyborg);
            Assert.AreSame(ToyRecipe.TeddyBear, ToyInspection.CompleteRecipe(bear));

            // Fit robot parts: now it's a cyborg bear, and no longer a plain one.
            Assert.IsTrue(bear.TryInstall(Loose(PartKind.Battery, working: true)));
            Assert.IsTrue(bear.TryInstall(Loose(PartKind.WindingKey, working: true)));
            Assert.IsTrue(ToyInspection.Passes(bear, ToyRecipe.CyborgBear, out string cyborg), cyborg);
            Assert.IsFalse(ToyInspection.Passes(bear, ToyRecipe.TeddyBear, out string tooMuch));
            StringAssert.Contains("battery wasn't on the wish list", tooMuch);
            Assert.AreSame(ToyRecipe.CyborgBear, ToyInspection.CompleteRecipe(bear));

            // A car with one wheel set.
            ToyBody car = Toys(ToyType.ToyCar).First(c => c.InstalledParts.Count(p => p.Kind == PartKind.WheelSet) == 1);
            car.SetDirty(false);
            Assert.IsFalse(ToyInspection.Passes(car, ToyRecipe.ToyCar, out string oneWheel));
            StringAssert.Contains("1 of 2 wheel sets missing", oneWheel);
            StringAssert.Contains(ToyInspection.DeadBattery, oneWheel);

            Assert.IsFalse(ToyInspection.Passes(car, ToyRecipe.WindUpRobot, out string wrongToy));
            StringAssert.Contains("this is a toy car, not a wind-up robot", wrongToy);
        }

        [Test]
        public void Slots_RefuseWrongPartsAndFullSlots()
        {
            workday.SetDayForTest(3);
            Assert.IsTrue(workday.StartShift());
            DeliverNextWave();

            ToyBody car = Toys(ToyType.ToyCar).First(c => c.InstalledParts.Count(p => p.Kind == PartKind.WheelSet) == 2);
            Assert.IsFalse(car.CanInstall(Loose(PartKind.WheelSet, true), out string full));
            StringAssert.Contains("All wheel set slots are filled", full);

            ToyBody bear = Toys(ToyType.TeddyBear).First();
            Assert.IsFalse(bear.CanInstall(Loose(PartKind.WheelSet, true), out string noSlot));
            StringAssert.Contains("A teddy bear has no wheel set slot", noSlot);
        }

        [Test]
        public void AssemblyBench_FitsAWheelSetIntoTheEmptyWheelSlot()
        {
            workday.SetDayForTest(3);
            Assert.IsTrue(workday.StartShift());
            DeliverNextWave();

            ToyBody car = Toys(ToyType.ToyCar).First(c => c.InstalledParts.Count(p => p.Kind == PartKind.WheelSet) == 1);
            Assert.IsTrue(player.RequestPickUp(car.Carryable));
            assembly.Interact(player);
            Assert.AreSame(car, assembly.FindToy());

            ToyPart wheels = Loose(PartKind.WheelSet, true);
            Assert.IsTrue(player.RequestPickUp(wheels.Carryable));
            Assert.IsTrue(assembly.CanInteract(player, out string label), label);
            StringAssert.Contains("Install Wheel Set", label);
            Assert.IsTrue(assembly.TryInstallHeld(player));
            Assert.AreEqual(2, car.InstalledParts.Count(p => p.Kind == PartKind.WheelSet));
            Assert.AreSame(car, wheels.InstalledIn);
            Assert.IsTrue(wheels.transform.IsChildOf(car.transform));
        }

        [Test]
        public void Shipping_FillsTheMatchingWishOrder()
        {
            // Day 5: two robots and two cyborg bears.
            workday.SetDayForTest(4);
            Assert.IsTrue(workday.StartShift());
            DeliverNextWave();
            CollectionAssert.AreEqual(
                new[] { OrderKind.WindUpRobot, OrderKind.WindUpRobot, OrderKind.CyborgBear, OrderKind.CyborgBear },
                workday.Orders.Select(o => o.kind));

            // A plain bear isn't on today's list: rejected, with what the closest order still needs.
            ToyBody[] bears = Toys(ToyType.TeddyBear).ToArray();
            bears[0].SetDirty(false);
            Ship(bears[0], expectAccepted: false);
            StringAssert.Contains(ToyInspection.MissingBattery, shipping.LastResult);
            StringAssert.Contains("cyborg bear", shipping.LastResult);
            Assert.AreEqual(0, workday.Shipped);

            // Make it a cyborg bear: accepted, and the first cyborg order is filled.
            Assert.IsTrue(bears[0].TryInstall(Loose(PartKind.Battery, true)));
            Assert.IsTrue(bears[0].TryInstall(Loose(PartKind.WindingKey, true)));
            Ship(bears[0], expectAccepted: true);
            Assert.IsTrue(workday.IsOrderFilled(2));
            Assert.IsFalse(workday.IsOrderFilled(3));
            StringAssert.Contains($"{workday.Orders[2].kidName}'s cyborg bear", shipping.LastResult);
            StringAssert.Contains($"[x] {workday.Orders[2].Describe()}", OrderBoard.BuildText(workday));
            StringAssert.Contains($"[  ] {workday.Orders[3].Describe()}", OrderBoard.BuildText(workday));
            Assert.AreEqual(1, workday.Shipped);
            Assert.AreEqual(ShiftState.Running, workday.State);
        }

        [Test]
        public void Shipping_RefusesAToyOnceItsOrdersAreAllFilled()
        {
            // Day 2: two robots and a teddy bear.
            workday.SetDayForTest(1);
            Assert.IsTrue(workday.StartShift());
            ToyBody[] robots = Toys(ToyType.WindUpRobot).ToArray();

            ToyBody first = robots[2];
            first.SetDirty(false);
            Assert.IsTrue(first.TryInstall(Loose(PartKind.WindingKey, true)));
            Ship(first, expectAccepted: true);

            ToyBody second = robots[0];
            second.SetDirty(false);
            second.Battery.Carryable.SetLoose(second.transform.position + Vector3.right, Quaternion.identity, Vector3.zero);
            Assert.IsTrue(second.TryInstall(Loose(PartKind.Battery, true)));
            Ship(second, expectAccepted: true);

            Assert.AreEqual(2, workday.Shipped);
            Assert.AreEqual(ShiftState.Running, workday.State, "The bear is still wanted");
            Assert.IsFalse(workday.TryMatchOrder(robots[1], out int none, out string reason));
            Assert.AreEqual(-1, none);
            StringAssert.Contains("every wind-up robot on today's wish list is already filled", reason);
        }

        // ---------------------------------------------------------------- Helpers

        System.Collections.Generic.IEnumerable<ToyBody> Toys(ToyType type) => chute.SpawnedItems
            .Select(i => i.GetComponent<ToyBody>())
            .Where(t => t != null && t.Type == type);

        ToyPart Loose(PartKind kind, bool working) => chute.SpawnedItems
            .Select(i => i.GetComponent<ToyPart>())
            .First(p => p != null && p.Kind == kind && p.IsWorking == working && p.Carryable.State == CarryState.Loose);

        /// <summary>Moves everything off the delivery table, then lets the next wave arrive.</summary>
        void DeliverNextWave()
        {
            int i = 0;
            foreach (Carryable item in chute.SpawnedItems.Where(c => c.State == CarryState.Loose))
            {
                item.SetLoose(new Vector3(-3f + (i % 8) * 0.7f, 0.3f, 0.5f + (i / 8) * 0.7f), Quaternion.identity, Vector3.zero);
                i++;
            }

            int before = workday.WavesDelivered;
            float due = before * 100f + 1f - (workday.ShiftLengthSeconds - workday.RemainingSeconds);
            workday.Tick(Mathf.Max(1.5f, due));
            Assert.AreEqual(before + 1, workday.WavesDelivered, "The next wave arrived");
        }

        void Ship(ToyBody toy, bool expectAccepted)
        {
            player.RequestDrop();
            Assert.IsTrue(player.RequestPickUp(toy.Carryable));
            Assert.AreEqual(expectAccepted, shipping.TrySubmit(player), shipping.LastResult);
        }
    }
}
