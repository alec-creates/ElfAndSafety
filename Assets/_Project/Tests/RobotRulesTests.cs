using NUnit.Framework;
using UnityEngine;

namespace ElfAndSafety.Tests
{
    public class RobotRulesTests
    {
        [Test]
        public void CleanRobotWithWorkingBatteryAndKey_HasNoProblems()
        {
            CollectionAssert.IsEmpty(RobotInspection.FindProblems(isDirty: false, hasBattery: true, batteryWorks: true, hasKey: true));
        }

        [Test]
        public void EveryProblemIsReported()
        {
            CollectionAssert.AreEquivalent(
                new[] { RobotInspection.DirtyBody, RobotInspection.MissingBattery, RobotInspection.MissingKey },
                RobotInspection.FindProblems(isDirty: true, hasBattery: false, batteryWorks: false, hasKey: false));
        }

        [Test]
        public void DeadBattery_IsReportedSeparatelyFromMissing()
        {
            CollectionAssert.AreEqual(
                new[] { RobotInspection.DeadBattery },
                RobotInspection.FindProblems(isDirty: false, hasBattery: true, batteryWorks: false, hasKey: true));
        }

        [Test]
        public void DefaultTestBatch_CanCompleteTwoRobots()
        {
            var batch = ScriptableObject.CreateInstance<TestDeliveryBatch>();
            Assert.AreEqual(2, batch.CompletableRobots());
            Assert.AreEqual(6, batch.ItemCount, "One delivery rack slot per item");
            Object.DestroyImmediate(batch);
        }
    }
}
