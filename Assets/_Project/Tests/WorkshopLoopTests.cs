using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// Drives the real Workshop scene through the same methods the interaction prompts call.
    /// Aiming, input and feel are not covered: those need a manual play test.
    /// </summary>
    public class WorkshopLoopTests
    {
        PlayerInteractor player;
        DeliveryChute chute;
        SalvageBench salvage;
        CleaningStation cleaning;
        AssemblyBench assembly;
        ShippingChute shipping;
        ShipmentCounter counter;

        // Default batch, in spawn order: A = dirty + dead battery + key, B = dirty and empty, C = clean + working battery.
        ToyBody robotA, robotB, robotC;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Workshop");

            player = Object.FindAnyObjectByType<PlayerInteractor>();
            chute = Object.FindAnyObjectByType<DeliveryChute>();
            salvage = Object.FindAnyObjectByType<SalvageBench>();
            cleaning = Object.FindAnyObjectByType<CleaningStation>();
            assembly = Object.FindAnyObjectByType<AssemblyBench>();
            shipping = Object.FindAnyObjectByType<ShippingChute>();
            counter = Object.FindAnyObjectByType<ShipmentCounter>();

            Assert.IsTrue(chute.TryRequestDelivery(), "First delivery should spawn");
            ToyBody[] robots = Robots();
            Assert.AreEqual(3, robots.Length);
            (robotA, robotB, robotC) = (robots[0], robots[1], robots[2]);
            AssertOwnershipConsistent();

            // Let the delivery settle on the table.
            yield return new WaitForSeconds(0.5f);
        }

        [Test]
        public void Delivery_SpawnsSolvableBatchOnce_AsLooseItems()
        {
            Assert.AreEqual(9, chute.SpawnedItems.Count, "3 robots, 3 installed parts, 3 loose parts");
            Assert.IsFalse(chute.TryRequestDelivery(), "A second delivery must be refused while the batch is out");
            Assert.AreEqual(9, chute.SpawnedItems.Count);

            Assert.IsTrue(robotA.IsDirty && robotA.Battery != null && !robotA.Battery.IsWorking && robotA.WindingKey != null);
            Assert.IsTrue(robotB.IsDirty && robotB.Battery == null && robotB.WindingKey == null);
            Assert.IsTrue(!robotC.IsDirty && robotC.Battery != null && robotC.Battery.IsWorking && robotC.WindingKey == null);
            Assert.IsTrue(Robots().All(r => r.Carryable.State == CarryState.Loose), "Delivered robots are free physics objects");
            Assert.IsTrue(robotA.transform.position.y > 0.5f, "Settled on the delivery table, not fallen through it");
        }

        [Test]
        public void Salvage_RecoversEachInstalledPartExactlyOnce()
        {
            int partsBefore = Object.FindObjectsByType<ToyPart>().Length;
            ToyPart deadBattery = robotA.Battery;
            ToyPart key = robotA.WindingKey;

            Assert.IsFalse(player.RequestPickUp(key.Carryable), "Installed parts can't be grabbed directly");

            Carry(robotA);
            PlaceOn(salvage);
            Assert.AreSame(robotA, salvage.FindToy());

            Assert.IsTrue(salvage.TryRemovePart(player, PartKind.Battery));
            Assert.IsFalse(salvage.TryRemovePart(player, PartKind.Battery), "Removing twice must do nothing");
            Assert.IsTrue(salvage.TryRemovePart(player, PartKind.WindingKey));

            Assert.IsNull(robotA.Battery);
            Assert.IsNull(robotA.WindingKey);
            Assert.AreEqual(CarryState.Loose, deadBattery.Carryable.State, "Recovered parts are loose physics objects");
            Assert.AreEqual(CarryState.Loose, key.Carryable.State);
            Assert.IsNull(deadBattery.transform.parent, "No longer attached to the robot");
            Assert.Less(Vector3.Distance(deadBattery.transform.position, salvage.OutputPoints[0].position), 0.01f, "Popped out at the first output spot");
            Assert.Less(Vector3.Distance(key.transform.position, salvage.OutputPoints[1].position), 0.01f, "First spot was taken, so the next one");
            Assert.AreSame(robotA, salvage.FindToy(), "The body stays on the bench");
            Assert.AreEqual(partsBefore, Object.FindObjectsByType<ToyPart>().Length, "No parts created or lost");
            AssertOwnershipConsistent();
        }

        [Test]
        public void Salvage_WithBlockedOutputs_HandsPartToEmptyHandedPlayer()
        {
            ToyPart[] looseParts = LooseParts();
            for (int i = 0; i < salvage.OutputPoints.Length; i++)
            {
                looseParts[i].Carryable.SetLoose(salvage.OutputPoints[i].position, Quaternion.identity, Vector3.zero);
            }

            Carry(robotA);
            PlaceOn(salvage);
            ToyPart deadBattery = robotA.Battery;

            Assert.IsTrue(salvage.TryRemovePart(player, PartKind.Battery));
            Assert.AreSame(deadBattery.Carryable, player.Held);

            Assert.IsFalse(salvage.TryRemovePart(player, PartKind.WindingKey), "Nowhere to put it: the key must stay installed");
            Assert.IsNotNull(robotA.WindingKey);
            AssertOwnershipConsistent();
        }

        [UnityTest]
        public IEnumerator Cleaning_ReservesBodyThenReleasesItClean()
        {
            Carry(robotB);
            PlaceOn(cleaning);
            yield return null;

            Assert.IsTrue(cleaning.IsCleaning);
            Assert.AreEqual(CarryState.Reserved, robotB.Carryable.State);
            Assert.IsFalse(player.RequestPickUp(robotB.Carryable), "Can't remove a body mid-scrub");

            Carry(robotA);
            Assert.IsFalse(cleaning.CanInteract(player, out string busy), "Only one body at a time");
            StringAssert.Contains("already has a toy", busy);
            player.RequestDrop();

            yield return new WaitForSeconds(cleaning.CleanDuration + 0.25f);

            Assert.IsFalse(robotB.IsDirty);
            Assert.IsFalse(cleaning.IsCleaning);
            Assert.AreEqual(CarryState.Loose, robotB.Carryable.State, "Released as a normal physics object");
            Carry(robotB);
            Assert.IsFalse(cleaning.CanInteract(player, out string clean), "Clean bodies are not accepted again");
            StringAssert.Contains("Already clean", clean);
            AssertOwnershipConsistent();
        }

        [UnityTest]
        public IEnumerator Stations_WorkOnRobotsThatArriveByPhysics_AndLetThemLeave()
        {
            // Dropped from above instead of placed: it falls onto the bench and the bench picks it up.
            Vector3 above = assembly.PlacePoint.position + new Vector3(0.2f, 0.4f, 0f);
            robotC.Carryable.SetLoose(above, assembly.PlacePoint.rotation, Vector3.zero);
            yield return new WaitForSeconds(1f);
            Assert.AreSame(robotC, assembly.FindToy(), "A robot that lands on the bench counts");

            // Knocked off the bench (later: walks away by itself): the bench lets it go.
            robotC.Carryable.SetLoose(new Vector3(0f, 0.3f, 0f), Quaternion.identity, Vector3.zero);
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(assembly.FindToy());

            Carry(LooseParts().First(p => p.Kind == PartKind.WindingKey));
            Assert.IsFalse(assembly.TryInstallHeld(player), "Nothing on the bench to install into");
            AssertOwnershipConsistent();
        }

        [Test]
        public void Assembly_InstallsTheActualPart_AndRefusesFilledSlots()
        {
            Carry(robotC);
            PlaceOn(assembly);

            ToyPart key = LooseParts().First(p => p.Kind == PartKind.WindingKey);
            Carry(key);
            Assert.IsTrue(assembly.TryInstallHeld(player));
            Assert.AreSame(key, robotC.WindingKey);
            Assert.AreEqual(CarryState.Installed, key.Carryable.State);
            Assert.IsTrue(key.transform.IsChildOf(robotC.transform), "Installed parts move with the robot");
            Assert.IsNull(player.Held);
            Assert.IsFalse(player.RequestPickUp(key.Carryable));

            ToyPart spareBattery = LooseParts().First(p => p.Kind == PartKind.Battery && p.IsWorking);
            Carry(spareBattery);
            Assert.IsFalse(assembly.TryInstallHeld(player), "Battery slot is already filled");
            Assert.AreSame(spareBattery.Carryable, player.Held, "Refused part stays in hand");
            AssertOwnershipConsistent();
        }

        [Test]
        public void Shipping_RejectsInvalidRobotsWithReasons()
        {
            Carry(robotA);
            Assert.IsFalse(shipping.TrySubmit(player));
            StringAssert.Contains(ToyInspection.DirtyBody, shipping.LastResult);
            StringAssert.Contains(ToyInspection.DeadBattery, shipping.LastResult);
            Assert.AreEqual(CarryState.Loose, robotA.Carryable.State, "Dumped loose into the reject bin");
            Assert.Less(Vector3.Distance(robotA.transform.position, shipping.RejectDropPoints[0].position), 0.01f);

            Carry(robotB);
            Assert.IsFalse(shipping.TrySubmit(player));
            StringAssert.Contains(ToyInspection.MissingBattery, shipping.LastResult);
            StringAssert.Contains(ToyInspection.MissingKey, shipping.LastResult);

            Carry(robotC);
            Assert.IsFalse(shipping.TrySubmit(player));
            StringAssert.Contains(ToyInspection.MissingKey, shipping.LastResult);
            StringAssert.DoesNotContain(ToyInspection.DirtyBody, shipping.LastResult);

            Assert.AreEqual(0, counter.Shipped);
            Assert.IsNull(player.Held);
            AssertOwnershipConsistent();
        }

        [UnityTest]
        public IEnumerator FullLoop_RepairRejectedRobot_ThenShipTwo()
        {
            // Rejected first: dirty with a dead battery.
            Carry(robotA);
            Assert.IsFalse(shipping.TrySubmit(player));
            yield return new WaitForSeconds(0.5f);

            // Salvage the dead battery, clean the body.
            Carry(robotA);
            PlaceOn(salvage);
            Assert.IsTrue(salvage.TryRemovePart(player, PartKind.Battery));
            Carry(robotA);
            PlaceOn(cleaning);
            yield return new WaitForSeconds(cleaning.CleanDuration + 0.25f);
            Assert.IsFalse(robotA.IsDirty);

            // Fit the loose working battery (the key never left).
            Carry(robotA);
            PlaceOn(assembly);
            Carry(LooseParts().First(p => p.Kind == PartKind.Battery && p.IsWorking));
            Assert.IsTrue(assembly.TryInstallHeld(player));
            Carry(robotA);

            Assert.IsTrue(shipping.TrySubmit(player), shipping.LastResult);
            Assert.IsFalse(shipping.TrySubmit(player), "Nothing left in hand to ship");
            Assert.AreEqual(1, counter.Shipped);
            AssertOwnershipConsistent();
            yield return null;
            Assert.IsTrue(robotA == null, "Shipped robots leave play");

            // Second robot: robot C only needs the loose key.
            Carry(robotC);
            PlaceOn(assembly);
            Carry(LooseParts().First(p => p.Kind == PartKind.WindingKey));
            Assert.IsTrue(assembly.TryInstallHeld(player));
            Carry(robotC);
            Assert.IsTrue(shipping.TrySubmit(player), shipping.LastResult);
            Assert.AreEqual(2, counter.Shipped);
            AssertOwnershipConsistent();
        }

        [UnityTest]
        public IEnumerator Reset_ClearsEverythingAndKeepsCounter()
        {
            // Ship robot C so the counter is non-zero, then leave things scattered and mid-operation.
            Carry(robotC);
            PlaceOn(assembly);
            Carry(LooseParts().First(p => p.Kind == PartKind.WindingKey));
            assembly.TryInstallHeld(player);
            Carry(robotC);
            Assert.IsTrue(shipping.TrySubmit(player));

            Carry(robotB);
            PlaceOn(cleaning);
            Carry(robotA);
            PlaceOn(salvage);
            salvage.TryRemovePart(player, PartKind.Battery);
            Carry(LooseParts().First(p => p.Kind == PartKind.Battery && p.IsWorking));
            yield return null;
            Assert.IsTrue(cleaning.IsCleaning, "Reset should interrupt a scrub in progress");

            chute.ResetTestBatch();
            chute.ResetTestBatch();

            Assert.IsNull(player.Held, "Held item was removed by the reset");
            Assert.IsFalse(cleaning.IsCleaning);
            Assert.AreEqual(1, counter.Shipped, "Reset leaves the shipped count alone");

            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<ToyBody>().Length);
            Assert.AreEqual(0, Object.FindObjectsByType<ToyPart>().Length);
            Assert.IsNull(salvage.FindToy());

            Assert.IsTrue(chute.TryRequestDelivery(), "A fresh batch can be requested after a reset");
            Assert.AreEqual(3, Robots().Length);
            AssertOwnershipConsistent();
        }

        // ---------------------------------------------------------------- Helpers

        ToyBody[] Robots() => chute.SpawnedItems.Select(i => i.GetComponent<ToyBody>()).Where(r => r != null).ToArray();

        ToyPart[] LooseParts() => chute.SpawnedItems
            .Select(i => i.GetComponent<ToyPart>())
            .Where(p => p != null && p.Carryable.State == CarryState.Loose)
            .ToArray();

        void Carry(Component item)
        {
            var carryable = item.GetComponent<Carryable>();
            Assert.IsTrue(player.RequestPickUp(carryable), $"Should be able to pick up {item.name} ({carryable.State})");
            AssertOwnershipConsistent();
        }

        /// <summary>The same path as aiming at the station and pressing Interact.</summary>
        void PlaceOn(WorkStation station)
        {
            string name = player.Held != null ? player.Held.name : "nothing";
            Assert.IsTrue(station.CanInteract(player, out string label), $"Should be able to place {name} on {station.DisplayName}: {label}");
            station.Interact(player);
            Assert.IsNull(player.Held);
            AssertOwnershipConsistent();
        }

        /// <summary>Every delivered item is referenced by exactly the one owner its state says, and no other.</summary>
        void AssertOwnershipConsistent()
        {
            ToyBody[] robots = Object.FindObjectsByType<ToyBody>();

            foreach (Carryable item in chute.SpawnedItems)
            {
                int robotRefs = robots.Count(r => (r.Battery != null && r.Battery.Carryable == item) || (r.WindingKey != null && r.WindingKey.Carryable == item));
                int handRefs = player.Held == item ? 1 : 0;
                int reserveRefs = cleaning.Cleaning != null && cleaning.Cleaning.Carryable == item ? 1 : 0;

                string where = $"{item.name} is {item.State} (robots {robotRefs}, hands {handRefs}, cleaning {reserveRefs})";
                Assert.AreEqual(item.State == CarryState.Installed ? 1 : 0, robotRefs, where);
                Assert.AreEqual(item.State == CarryState.Held ? 1 : 0, handRefs, where);
                Assert.AreEqual(item.State == CarryState.Reserved ? 1 : 0, reserveRefs, where);
            }
        }
    }
}
