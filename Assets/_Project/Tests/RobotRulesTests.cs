using NUnit.Framework;
using UnityEngine;

namespace ElfAndSafety.Tests
{
    public class RobotRulesTests
    {
        [Test]
        public void CleanRobotWithWorkingBatteryAndKey_HasNoProblems()
        {
            CollectionAssert.IsEmpty(ToyInspection.FindProblems(isDirty: false, hasBattery: true, batteryWorks: true, hasKey: true));
        }

        [Test]
        public void EveryProblemIsReported()
        {
            CollectionAssert.AreEquivalent(
                new[] { ToyInspection.DirtyBody, ToyInspection.MissingBattery, ToyInspection.MissingKey },
                ToyInspection.FindProblems(isDirty: true, hasBattery: false, batteryWorks: false, hasKey: false));
        }

        [Test]
        public void DeadBattery_IsReportedSeparatelyFromMissing()
        {
            CollectionAssert.AreEqual(
                new[] { ToyInspection.DeadBattery },
                ToyInspection.FindProblems(isDirty: false, hasBattery: true, batteryWorks: false, hasKey: true));
        }

        [Test]
        public void RobotWave_CanCompleteTwoRobots_AndNoMore()
        {
            DeliveryWave wave = DeliveryWave.Robots();
            Assert.AreEqual(6, wave.ItemCount, "One delivery table spot per item");

            var twoRobots = new ChristmasCountdown.Day { waves = { wave } };
            twoRobots.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot });
            twoRobots.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot });
            Assert.IsTrue(ChristmasCountdown.IsWinnable(twoRobots, 6, out string reason), reason);

            twoRobots.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot });
            Assert.IsFalse(ChristmasCountdown.IsWinnable(twoRobots, 6, out _), "Only two working batteries and keys");
        }
    }
}
