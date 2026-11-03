using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// The seven-day countdown on the real Workshop scene: day tuning, moving on to the next day in place,
    /// later delivery waves, and failing or winning the week.
    /// </summary>
    public class CountdownTests
    {
        WorkdayManager workday;
        PlayerInteractor player;
        DeliveryChute chute;
        ShippingChute shipping;
        ShipmentCounter counter;

        [UnitySetUp]
        public IEnumerator SetUp()
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
        }

        [Test]
        public void DefaultCountdown_SevenWinnableDays_GettingHarder()
        {
            var countdown = ScriptableObject.CreateInstance<ChristmasCountdown>();

            Assert.AreEqual(7, countdown.DayCount);
            for (int i = 0; i < countdown.DayCount; i++)
            {
                ChristmasCountdown.Day day = countdown.GetDay(i);
                Assert.IsTrue(ChristmasCountdown.IsWinnable(day, 6, out string reason), $"Day {i + 1}: {reason}");
                if (i > 0) Assert.GreaterOrEqual(day.Quota, countdown.GetDay(i - 1).Quota, "Quotas never go down");
            }
            Assert.Greater(countdown.GetDay(6).Quota, countdown.GetDay(0).Quota, "Christmas Eve is harder than day 1");

            Assert.IsTrue(countdown.GetDay(0).orders.All(o => o.kind == OrderKind.WindUpRobot), "Day 1 is the robot-only game");
            var everyOrder = countdown.days.SelectMany(d => d.orders).Select(o => o.kind).Distinct().ToList();
            CollectionAssert.IsSupersetOf(everyOrder, new[] { OrderKind.WindUpRobot, OrderKind.TeddyBear, OrderKind.ToyCar, OrderKind.CyborgBear });

            Object.DestroyImmediate(countdown);
        }

        [Test]
        public void IsWinnable_RefusesTooFewPartsOrLateWaves()
        {
            ChristmasCountdown.Day Day(float length, params OrderKind[] orders)
            {
                var day = new ChristmasCountdown.Day { shiftLengthSeconds = length, secondsBetweenWaves = 100f };
                foreach (OrderKind kind in orders) day.orders.Add(new WishOrder { kind = kind });
                return day;
            }

            var bearWithRobots = Day(300f, OrderKind.TeddyBear);
            bearWithRobots.waves.Add(DeliveryWave.Robots());
            Assert.IsFalse(ChristmasCountdown.IsWinnable(bearWithRobots, 6, out string noBear), "No bear delivered");
            StringAssert.Contains("teddy bear", noBear);

            var cyborgFromBears = Day(300f, OrderKind.CyborgBear);
            cyborgFromBears.waves.Add(DeliveryWave.Bears());
            Assert.IsFalse(ChristmasCountdown.IsWinnable(cyborgFromBears, 6, out string noKey), "The bear wave has no working battery or key");
            StringAssert.Contains("battery", noKey);

            var lateWave = Day(120f, OrderKind.WindUpRobot, OrderKind.WindUpRobot, OrderKind.WindUpRobot);
            lateWave.waves.Add(DeliveryWave.Robots());
            lateWave.waves.Add(DeliveryWave.Robots());
            Assert.IsFalse(ChristmasCountdown.IsWinnable(lateWave, 6, out _), "Second wave arrives with 20 s left");

            var onTime = Day(300f, OrderKind.WindUpRobot, OrderKind.WindUpRobot, OrderKind.WindUpRobot);
            onTime.waves.Add(DeliveryWave.Robots());
            onTime.waves.Add(DeliveryWave.Robots());
            Assert.IsTrue(ChristmasCountdown.IsWinnable(onTime, 6, out string reason), reason);
            Assert.IsFalse(ChristmasCountdown.IsWinnable(onTime, 5, out _), "A wave bigger than the table is refused");
        }

        [Test]
        public void TheWeekStartsSevenDaysBeforeChristmas()
        {
            Assert.AreEqual(1, workday.DayNumber);
            Assert.AreEqual("7 days until Christmas", workday.DayTitle);
            Assert.AreEqual(2, workday.Quota);
            Assert.AreEqual(300f, workday.ShiftLengthSeconds);
            Assert.IsNotEmpty(workday.SantaNote);
        }

        [UnityTest]
        public IEnumerator PassingADay_StartsTheNextDayInPlace()
        {
            Assert.IsTrue(workday.StartShift());
            ToyBody[] robots = Robots();
            Ship(MakeValid(robots[2]));
            Ship(MakeValid(robots[0]));
            Assert.AreEqual(ShiftState.Succeeded, workday.State);
            Assert.AreEqual("Next Day", workday.VerdictActionLabel);
            StringAssert.Contains("6 days until Christmas", workday.VerdictMessage);
            StringAssert.Contains("Santa expects 3 tomorrow", workday.VerdictMessage);
            Assert.IsTrue(player.RequestPickUp(robots[1].Carryable), "Carry something into the next day");

            WorkdayManager sameManager = workday;
            workday.Continue();
            yield return null;

            Assert.IsTrue(sameManager != null && Object.FindAnyObjectByType<WorkdayManager>() == sameManager, "No scene reload between days");
            Assert.AreEqual(2, workday.DayNumber);
            Assert.AreEqual("6 days until Christmas", workday.DayTitle);
            Assert.AreEqual(ShiftState.NotStarted, workday.State);
            Assert.AreEqual(3, workday.Quota);
            Assert.AreEqual(workday.ShiftLengthSeconds, workday.RemainingSeconds);
            Assert.AreEqual(0, counter.Shipped);
            Assert.IsFalse(workday.VerdictVisible);
            Assert.IsNull(player.Held);
            Assert.AreEqual(0, Object.FindObjectsByType<ToyBody>().Length, "Yesterday's toys are gone");
            Assert.IsTrue(player.InteractionEnabled);
            Assert.IsTrue(player.GetComponent<FirstPersonController>().MovementEnabled);
            Assert.AreEqual("Ship finished toys here", shipping.LastResult);
            Assert.IsTrue(workday.StartShift(), "Day 2 can start");
        }

        [Test]
        public void LaterWaves_ArriveOnSchedule_AndWaitForAClearTable()
        {
            workday.SetDayForTest(1);
            Assert.AreEqual(2, workday.DeliveryWaves);
            Assert.IsTrue(workday.StartShift());
            Assert.AreEqual(9, chute.SpawnedItems.Count, "First wave at clock-in");

            workday.Tick(50f);
            Assert.AreEqual(1, workday.WavesDelivered, "Not due yet");

            // Due now, but the first wave still sits on the delivery table.
            workday.Tick(51f);
            Assert.AreEqual(1, workday.WavesDelivered, "Waits while the table is cluttered");
            Assert.AreEqual(9, chute.SpawnedItems.Count);

            int i = 0;
            foreach (Carryable item in chute.SpawnedItems.Where(c => c.State == CarryState.Loose))
            {
                item.SetLoose(new Vector3(-3f + (i % 6) * 0.8f, 0.3f, 1f + (i / 6) * 0.8f), Quaternion.identity, Vector3.zero);
                i++;
            }

            workday.Tick(1.5f);
            Assert.AreEqual(2, workday.WavesDelivered, "Arrives once the table is clear");
            Assert.AreEqual(9 + 8, chute.SpawnedItems.Count, "Day 2's second wave (two bears with one part each, four loose parts) is added, the first kept");
            Assert.AreEqual(2, chute.SpawnedItems.Count(c => c.GetComponent<ToyBody>() != null && c.GetComponent<ToyBody>().Type == ToyType.TeddyBear));
            Assert.AreEqual(ShiftState.Running, workday.State);
        }

        [UnityTest]
        public IEnumerator FailingALaterDay_GoesBackToDayOne()
        {
            workday.SetDayForTest(2);
            Assert.IsTrue(workday.StartShift());
            workday.SetRemainingSeconds(0f);
            yield return null;
            Assert.AreEqual(ShiftState.Failed, workday.State);

            yield return WaitFor(() => workday.VerdictVisible);
            Assert.AreEqual("Back to Day 1", workday.VerdictActionLabel);
            StringAssert.Contains("furnace operations", workday.VerdictMessage);
            StringAssert.Contains("Back to 7 days until Christmas", workday.VerdictMessage);

            WorkdayManager old = workday;
            workday.Continue();
            yield return WaitFor(() => old == null);
            yield return null;
            FindScene();
            Assert.AreEqual(1, workday.DayNumber);
            Assert.AreEqual(ShiftState.NotStarted, workday.State);
        }

        [UnityTest]
        public IEnumerator WinningChristmasEve_IsMerryChristmas_ThenPlayAgainFromDayOne()
        {
            workday.SetDayForTest(6);
            Assert.AreEqual("Christmas Eve", workday.DayTitle);
            Assert.IsTrue(workday.IsFinalDay);
            Assert.IsTrue(workday.StartShift());

            for (int shipped = 0; shipped < workday.Quota; shipped++) counter.RecordShipment();

            Assert.AreEqual(ShiftState.Succeeded, workday.State);
            Assert.AreEqual(1, workday.VerdictCount);
            Assert.AreEqual("MERRY CHRISTMAS!", workday.VerdictTitle);
            Assert.AreEqual("Play Again", workday.VerdictActionLabel);

            WorkdayManager old = workday;
            workday.Continue();
            yield return WaitFor(() => old == null);
            yield return null;
            FindScene();
            Assert.AreEqual(1, workday.DayNumber);
        }

        // ---------------------------------------------------------------- Helpers

        ToyBody[] Robots() => chute.SpawnedItems.Select(i => i.GetComponent<ToyBody>()).Where(r => r != null).ToArray();

        ToyBody MakeValid(ToyBody robot)
        {
            ToyPart[] loose = chute.SpawnedItems.Select(i => i.GetComponent<ToyPart>())
                .Where(p => p != null && p.Carryable.State == CarryState.Loose).ToArray();
            robot.SetDirty(false);
            if (robot.Battery != null && !robot.Battery.IsWorking)
            {
                robot.Battery.Carryable.SetLoose(robot.transform.position + Vector3.right, Quaternion.identity, Vector3.zero);
            }
            if (robot.Battery == null) Assert.IsTrue(robot.TryInstall(loose.First(p => p.Kind == PartKind.Battery && p.IsWorking)));
            if (robot.WindingKey == null) Assert.IsTrue(robot.TryInstall(loose.First(p => p.Kind == PartKind.WindingKey)));
            Assert.IsTrue(ToyInspection.IsShippable(robot, out string reason), reason);
            return robot;
        }

        void Ship(ToyBody robot)
        {
            player.RequestDrop();
            Assert.IsTrue(player.RequestPickUp(robot.Carryable));
            Assert.IsTrue(shipping.TrySubmit(player), shipping.LastResult);
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 6f)
        {
            float waited = 0f;
            while (!condition() && waited < timeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), "Timed out waiting");
        }
    }
}
