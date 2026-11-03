using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// Faulty parts on the real Workshop scene: how they fail inspection, that they never count toward a
    /// day's solvability, and what each one does once installed in a toy.
    /// </summary>
    public class MalfunctionTests
    {
        WorkdayManager workday;
        PlayerInteractor player;
        DeliveryChute chute;
        ShippingChute shipping;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Workshop");
            workday = Object.FindAnyObjectByType<WorkdayManager>();
            player = Object.FindAnyObjectByType<PlayerInteractor>();
            chute = Object.FindAnyObjectByType<DeliveryChute>();
            shipping = Object.FindAnyObjectByType<ShippingChute>();
        }

        [Test]
        public void FaultyParts_NeverCountTowardSolvability()
        {
            var day = new ChristmasCountdown.Day { waves = { DeliveryWave.RiskyRobots() } };
            day.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot });
            day.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot });
            Assert.IsTrue(ChristmasCountdown.IsWinnable(day, 6, out string reason), reason);

            day.orders.Add(new WishOrder { kind = OrderKind.WindUpRobot });
            Assert.IsFalse(ChristmasCountdown.IsWinnable(day, 6, out string tooFew), "The leaky battery doesn't count as a third");
            StringAssert.Contains("good battery", tooFew);
        }

        [Test]
        public void RiskyRobot_FailsInspection_AndShowsItsTells()
        {
            ToyBody risky = StartRiskyDay();
            Assert.IsTrue(risky.Battery.IsFaulty && risky.WindingKey.IsFaulty);
            Assert.IsTrue(risky.Battery.IsWorking, "Faulty isn't dead: it looks usable");

            string look = risky.Describe();
            StringAssert.Contains("battery leaking", look);
            StringAssert.Contains("winding key rusty", look);
            StringAssert.Contains("looks rusty", risky.WindingKey.Describe());
            Assert.AreEqual("Winding Key", risky.WindingKey.DisplayName, "The prompt doesn't give it away");

            risky.SetDirty(false);
            Assert.IsFalse(ToyInspection.IsShippable(risky, out string reason));
            StringAssert.Contains("battery is faulty", reason);
            StringAssert.Contains("winding key is faulty", reason);

            // Shipped straight away (before it can zap): rejected with the same reasons.
            Assert.IsTrue(player.RequestPickUp(risky.Carryable));
            Assert.IsFalse(shipping.TrySubmit(player));
            StringAssert.Contains("winding key is faulty", shipping.LastResult);
            Assert.AreEqual(0, workday.Shipped);
        }

        [UnityTest]
        public IEnumerator OverwoundKey_RunsAway_UntilTheKeyIsRemoved()
        {
            ToyBody risky = StartRiskyDay();
            risky.Carryable.SetLoose(new Vector3(0f, 0.2f, 1.5f), Quaternion.identity, Vector3.zero);
            Vector3 start = risky.transform.position;

            yield return WaitFor(() => Flat(risky.transform.position - start) > 0.5f, 8f);
            Assert.Greater(Flat(risky.transform.position - start), 0.5f, "It ran off on its own");

            // Take the key out: it stops for good.
            ToyPart key = risky.WindingKey;
            key.Carryable.SetLoose(new Vector3(2f, 0.2f, 0f), Quaternion.identity, Vector3.zero);
            yield return new WaitForSeconds(1.5f);
            Vector3 settled = risky.transform.position;
            yield return new WaitForSeconds(4.5f);
            Assert.Less(Flat(risky.transform.position - settled), 0.15f, "Without the key it stays put");
        }

        [UnityTest]
        public IEnumerator LeakyBattery_ZapsWhoeverPicksItUp()
        {
            ToyBody risky = StartRiskyDay();
            Assert.IsTrue(player.RequestPickUp(risky.Carryable));
            Assert.AreSame(risky.Carryable, player.Held);

            yield return WaitFor(() => player.Held == null, 3f);
            Assert.IsNull(player.Held, "Zapped into dropping it");
            Assert.AreEqual(CarryState.Loose, risky.Carryable.State);

            // A loose leaky battery on its own is safe to carry to the bin.
            ToyPart battery = risky.Battery;
            battery.Carryable.SetLoose(new Vector3(1f, 0.2f, 1f), Quaternion.identity, Vector3.zero);
            Assert.IsTrue(player.RequestPickUp(battery.Carryable));
            yield return new WaitForSeconds(1.2f);
            Assert.AreSame(battery.Carryable, player.Held);
        }

        [UnityTest]
        public IEnumerator LooseButtonEyes_PopOffWhileCarried()
        {
            // Day 2's second wave is bears, with a loosely sewn pair of eyes.
            workday.SetDayForTest(1);
            Assert.IsTrue(workday.StartShift());
            DeliverNextWave();

            ToyBody bear = chute.SpawnedItems.Select(i => i.GetComponent<ToyBody>())
                .First(t => t != null && t.Type == ToyType.TeddyBear && t.GetPart(PartKind.ButtonEyes) == null);
            ToyPart looseEyes = chute.SpawnedItems.Select(i => i.GetComponent<ToyPart>())
                .First(p => p != null && p.Fault == PartFault.LooseThreads);
            Assert.IsTrue(bear.TryInstall(looseEyes));

            Assert.IsTrue(player.RequestPickUp(bear.Carryable));
            yield return WaitFor(() => bear.GetPart(PartKind.ButtonEyes) == null, 4f);

            Assert.IsNull(bear.GetPart(PartKind.ButtonEyes), "The eyes fell off");
            Assert.AreEqual(CarryState.Loose, looseEyes.Carryable.State);
            Assert.AreSame(bear.Carryable, player.Held, "You're still holding the bear");
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>Day 5 starts with the risky robots wave; its first robot carries the faulty parts.</summary>
        ToyBody StartRiskyDay()
        {
            workday.SetDayForTest(4);
            Assert.IsTrue(workday.StartShift());
            return chute.SpawnedItems.Select(i => i.GetComponent<ToyBody>())
                .First(t => t != null && t.Battery != null && t.Battery.IsFaulty);
        }

        void DeliverNextWave()
        {
            int i = 0;
            foreach (Carryable item in chute.SpawnedItems.Where(c => c.State == CarryState.Loose))
            {
                item.SetLoose(new Vector3(-3f + (i % 8) * 0.7f, 0.3f, 0.5f + (i / 8) * 0.7f), Quaternion.identity, Vector3.zero);
                i++;
            }

            int before = workday.WavesDelivered;
            workday.Tick(101f);
            Assert.AreEqual(before + 1, workday.WavesDelivered, "The next wave arrived");
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
        {
            float waited = 0f;
            while (!condition() && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
    }
}
