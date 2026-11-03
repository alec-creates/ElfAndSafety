using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// The scored shift on the real Workshop scene: start, win, timeout, the deadline frame, and restarting.
    /// Robots are made valid through the same methods the stations call, to keep each test short.
    /// </summary>
    public class WorkdayTests
    {
        WorkdayManager workday;
        PlayerInteractor player;
        DeliveryChute chute;
        ShippingChute shipping;
        ShipmentCounter counter;
        int verdictEvents;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadFreshWorkshop();
        }

        IEnumerator LoadFreshWorkshop()
        {
            yield return SceneManager.LoadSceneAsync("Workshop");
            FindScene();
        }

        void FindScene()
        {
            workday = Object.FindAnyObjectByType<WorkdayManager>();
            player = Object.FindAnyObjectByType<PlayerInteractor>();
            chute = Object.FindAnyObjectByType<DeliveryChute>();
            shipping = Object.FindAnyObjectByType<ShippingChute>();
            counter = Object.FindAnyObjectByType<ShipmentCounter>();
            verdictEvents = 0;
            workday.ShiftEnded += _ => verdictEvents++;
        }

        [Test]
        public void Defaults_FiveMinutesQuotaTwo_NotStarted()
        {
            Assert.AreEqual(ShiftState.NotStarted, workday.State);
            Assert.AreEqual(300f, workday.ShiftLengthSeconds);
            Assert.AreEqual(2, workday.Quota);
            Assert.AreEqual(300f, workday.RemainingSeconds, "The timer doesn't run before clocking in");
        }

        [Test]
        public void StartShift_ClearsPractice_ResetsCount_DeliversFreshBatch()
        {
            // Practice first: a delivery, a shipment that won't count, and something in hand.
            Assert.IsTrue(chute.TryRequestDelivery());
            Ship(MakeValid(Robot(r => !r.IsDirty)));
            Assert.AreEqual(1, counter.Shipped, "Practice shipments still use the real shipping rules");
            Assert.IsTrue(player.RequestPickUp(Robots()[0].Carryable));

            Assert.IsTrue(workday.StartShift());

            Assert.AreEqual(ShiftState.Running, workday.State);
            Assert.AreEqual(0, counter.Shipped, "Practice shipments don't count");
            Assert.IsNull(player.Held, "Practice items are cleared, including the one in hand");
            Assert.AreEqual(3, Robots().Length, "One fresh batch for the shift");
            Assert.AreEqual(2, chute.SpawnedItems.Select(i => i.GetComponent<RobotPart>()).Count(p => p != null && p.Carryable.State == CarryState.Loose && (p.IsWorking)), "Loose working battery and key");
            Assert.IsFalse(workday.StartShift(), "Can't clock in twice");
        }

        [Test]
        public void DuringShift_DeliveryAndDevResetAreRefused()
        {
            Assert.IsTrue(workday.StartShift());
            int items = chute.SpawnedItems.Count;

            Assert.IsFalse(chute.CanRequestDelivery(out string deliveryReason));
            StringAssert.Contains("scored shift", deliveryReason);
            Assert.IsFalse(chute.TryRequestDelivery());
            Assert.IsFalse(chute.CanDevReset(out _));
            Assert.IsFalse(chute.ResetTestBatch(), "DEV reset can't replenish parts mid-shift");
            Assert.AreEqual(items, chute.SpawnedItems.Count, "Nothing removed or added");

            // Even after every delivered robot has shipped, no new batch can be requested.
            foreach (Carryable item in chute.SpawnedItems.ToList()) item.Consume();
            Assert.IsFalse(chute.TryRequestDelivery());
        }

        [UnityTest]
        public IEnumerator Timer_CountsDownOnlyAfterClockingIn()
        {
            yield return null;
            yield return null;
            Assert.AreEqual(300f, workday.RemainingSeconds);

            workday.StartShift();
            yield return new WaitForSeconds(0.3f);
            Assert.Less(workday.RemainingSeconds, 300f);
            Assert.Greater(workday.RemainingSeconds, 299f);
        }

        [Test]
        public void RejectedRobots_EarnNoCredit()
        {
            Assert.IsTrue(workday.StartShift());

            foreach (RobotBody robot in Robots())
            {
                Assert.IsTrue(player.RequestPickUp(robot.Carryable));
                Assert.IsFalse(shipping.TrySubmit(player), "Every delivered robot starts invalid");
            }

            Assert.AreEqual(0, counter.Shipped);
            Assert.AreEqual(ShiftState.Running, workday.State);
            Assert.AreEqual(0, verdictEvents);
        }

        [Test]
        public void ShippingTheQuota_WinsOnce_AndFreezesTheWorkshop()
        {
            Assert.IsTrue(workday.StartShift());
            RobotBody[] robots = Robots();

            Ship(MakeValid(robots[2]));
            Assert.AreEqual(ShiftState.Running, workday.State, "One of two isn't enough");
            Ship(MakeValid(robots[0]));

            Assert.AreEqual(ShiftState.Succeeded, workday.State);
            Assert.AreEqual(2, counter.Shipped);
            Assert.AreEqual(1, workday.VerdictCount);
            Assert.AreEqual(1, verdictEvents);
            Assert.IsTrue(workday.VerdictVisible, "Success shows Santa's message straight away");
            StringAssert.Contains("Ho ho ho", workday.VerdictMessage);

            Assert.IsFalse(player.InteractionEnabled, "Interactions stop when the shift ends");
            Assert.IsTrue(player.RequestPickUp(robots[1].Carryable));
            Assert.IsFalse(shipping.CanInteract(player, out _));
            Assert.IsFalse(shipping.TrySubmit(player), "No shipments after the shift");
            Assert.IsFalse(chute.ResetTestBatch(), "No DEV reset after the shift either");
            Assert.AreEqual(2, counter.Shipped);
        }

        [UnityTest]
        public IEnumerator Timeout_FailsOnce_PlaysTrapdoor_ThenOffersRetry()
        {
            Assert.IsTrue(workday.StartShift());
            Ship(MakeValid(Robots()[2]));
            workday.SetRemainingSeconds(0.05f);
            float startY = player.transform.position.y;

            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(ShiftState.Failed, workday.State);
            Assert.AreEqual(1, verdictEvents);
            Assert.IsFalse(workday.VerdictVisible, "The verdict waits for the trapdoor sequence");

            float waited = 0f;
            while (!workday.VerdictVisible && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(workday.VerdictVisible, "Retry is offered after the sequence");
            StringAssert.Contains("furnace operations", workday.VerdictMessage);
            Assert.Less(player.transform.position.y, startY - 3f, "The player fell into the pit");
            Assert.IsTrue(Object.FindAnyObjectByType<TrapdoorSequence>().HasPlayed);
            Assert.AreEqual(1, workday.VerdictCount);
            Assert.AreEqual(1, counter.Shipped);
        }

        [Test]
        public void DeadlineFrame_QuotaShipmentFirst_SucceedsOnce()
        {
            Assert.IsTrue(workday.StartShift());
            RobotBody[] robots = Robots();
            Ship(MakeValid(robots[2]));
            MakeValid(robots[0]);
            workday.SetRemainingSeconds(0f);

            // Same frame: the quota shipment lands, then the clock is evaluated.
            Ship(robots[0]);
            workday.Tick(0.016f);

            Assert.AreEqual(ShiftState.Succeeded, workday.State);
            Assert.AreEqual(1, workday.VerdictCount);
            Assert.AreEqual(1, verdictEvents);
        }

        [Test]
        public void DeadlineFrame_TimeoutFirst_RefusesTheLateShipment()
        {
            Assert.IsTrue(workday.StartShift());
            RobotBody[] robots = Robots();
            Ship(MakeValid(robots[2]));
            MakeValid(robots[0]);
            workday.SetRemainingSeconds(0.01f);

            // Same frame, in the real order: the workday ticks before player interactions.
            workday.Tick(0.016f);
            Assert.IsTrue(player.RequestPickUp(robots[0].Carryable));
            Assert.IsFalse(shipping.TrySubmit(player), "Too late: the shift already ended");

            Assert.AreEqual(ShiftState.Failed, workday.State);
            Assert.AreEqual(1, counter.Shipped, "The late robot earned nothing");
            Assert.AreEqual(1, workday.VerdictCount);
            Assert.AreEqual(1, verdictEvents);
        }

        [UnityTest]
        public IEnumerator PlayAgain_AfterWin_ReturnsAFreshWorkshop()
        {
            Assert.IsTrue(workday.StartShift());
            RobotBody[] robots = Robots();
            Ship(MakeValid(robots[2]));
            Ship(MakeValid(robots[0]));
            Assert.AreEqual(ShiftState.Succeeded, workday.State);
            Assert.IsTrue(player.RequestPickUp(robots[1].Carryable), "Carry something into the restart");

            yield return RestartAndWait();
            AssertFreshWorkshop();
        }

        [UnityTest]
        public IEnumerator Retry_AfterTimeout_ReturnsAFreshWorkshop()
        {
            Assert.IsTrue(workday.StartShift());
            Assert.IsTrue(player.RequestPickUp(Robots()[1].Carryable));
            workday.SetRemainingSeconds(0f);
            yield return null;
            Assert.AreEqual(ShiftState.Failed, workday.State);

            float waited = 0f;
            while (!workday.VerdictVisible && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            yield return RestartAndWait();
            AssertFreshWorkshop();
        }

        // ---------------------------------------------------------------- Helpers

        IEnumerator RestartAndWait()
        {
            WorkdayManager old = workday;
            workday.Restart();
            float waited = 0f;
            while (old != null && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            yield return null;
            FindScene();
        }

        void AssertFreshWorkshop()
        {
            Assert.AreEqual(ShiftState.NotStarted, workday.State);
            Assert.AreEqual(workday.ShiftLengthSeconds, workday.RemainingSeconds);
            Assert.AreEqual(0, counter.Shipped);
            Assert.IsFalse(workday.VerdictVisible);
            Assert.AreEqual(0, Object.FindObjectsByType<RobotBody>().Length, "No delivery until clocking in or requesting one");
            Assert.IsNull(player.Held);
            Assert.IsTrue(player.InteractionEnabled);
            Assert.IsTrue(Object.FindAnyObjectByType<CharacterController>().enabled);
            Assert.IsFalse(Object.FindAnyObjectByType<TrapdoorSequence>().HasPlayed, "Trapdoor closed again");
            Assert.IsTrue(chute.CanRequestDelivery(out _), "Practice tools are back");
        }

        RobotBody[] Robots() => chute.SpawnedItems.Select(i => i.GetComponent<RobotBody>()).Where(r => r != null).ToArray();

        RobotBody Robot(System.Func<RobotBody, bool> match) => Robots().First(match);

        RobotPart[] LooseParts(RobotPartKind kind) => chute.SpawnedItems
            .Select(i => i.GetComponent<RobotPart>())
            .Where(p => p != null && p.Kind == kind && p.Carryable.State == CarryState.Loose)
            .ToArray();

        /// <summary>Cleans the robot and swaps in a loose working battery and key where needed.</summary>
        RobotBody MakeValid(RobotBody robot)
        {
            robot.SetDirty(false);

            if (robot.Battery != null && !robot.Battery.IsWorking)
            {
                robot.Battery.Carryable.SetLoose(robot.transform.position + Vector3.right, Quaternion.identity, Vector3.zero);
            }
            if (robot.Battery == null)
            {
                Assert.IsTrue(robot.TryInstall(LooseParts(RobotPartKind.Battery).First(p => p.IsWorking)));
            }
            if (robot.WindingKey == null)
            {
                Assert.IsTrue(robot.TryInstall(LooseParts(RobotPartKind.WindingKey).First()));
            }

            Assert.IsTrue(RobotInspection.IsShippable(robot, out string reason), reason);
            return robot;
        }

        void Ship(RobotBody robot)
        {
            player.RequestDrop();
            Assert.IsTrue(player.RequestPickUp(robot.Carryable));
            Assert.IsTrue(shipping.TrySubmit(player), shipping.LastResult);
        }
    }
}
