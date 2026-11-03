using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElfAndSafety.Tests
{
    /// <summary>
    /// Host side of the co-op session, in one process: what the host does with a client's forwarded
    /// requests, and what it publishes. A second (client) process can't run inside a PlayMode test, so
    /// joining and the client's view are checked by hand with Multiplayer Play Mode.
    /// </summary>
    public class NetworkSessionTests
    {
        NetworkSession session;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("Workshop");
            session = Object.FindAnyObjectByType<NetworkSession>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Never leave a NetworkManager behind for the next test.
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                if (manager.IsListening) manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }
            yield return null;
        }

        [Test]
        public void SoloPlay_HasNoNetworkManager()
        {
            Assert.IsNull(NetworkManager.Singleton, "Solo play needs no networking");
            Assert.IsFalse(NetworkSession.IsOnline);
            Assert.AreEqual(1, Object.FindObjectsByType<PlayerInteractor>().Length);
            Assert.IsNull(Object.FindAnyObjectByType<PlayerInteractor>().GetComponent<NetworkPlayer>());
        }

        [UnityTest]
        public IEnumerator Hosting_ReplacesSoloElf_WithANetworkedElf()
        {
            var delivery = Object.FindAnyObjectByType<DeliveryChute>();
            Assert.IsTrue(delivery.TryRequestDelivery(), "A practice batch before going online");

            Assert.IsTrue(session.StartHost());
            yield return WaitFor(() => NetworkPlayer.Find(NetworkManager.ServerClientId) != null);

            Assert.IsTrue(NetworkSession.IsServer);
            Assert.IsFalse(NetworkSession.IsRemoteClient);
            PlayerInteractor[] players = Object.FindObjectsByType<PlayerInteractor>();
            Assert.AreEqual(1, players.Length, "The solo elf is gone; only the host's networked elf");
            Assert.IsTrue(players[0].GetComponent<NetworkPlayer>().IsOwner);
            Assert.AreEqual(0, delivery.SpawnedItems.Count, "Local practice items are cleared before going online");
        }

        [UnityTest]
        public IEnumerator Host_DeliveryIsNetworked_AndCarryStateIsPublished()
        {
            Assert.IsTrue(session.StartHost());
            yield return WaitFor(() => NetworkPlayer.Find(NetworkManager.ServerClientId) != null);
            PlayerInteractor host = NetworkPlayer.Find(NetworkManager.ServerClientId).Interactor;

            var delivery = Object.FindAnyObjectByType<DeliveryChute>();
            Assert.IsTrue(delivery.TryRequestDelivery());
            Assert.IsTrue(delivery.SpawnedItems.All(i => i.GetComponent<NetworkObject>().IsSpawned), "Every delivered item exists for clients");

            ToyBody robot = delivery.SpawnedItems.Select(i => i.GetComponent<ToyBody>()).First(r => r != null && r.Battery != null && r.WindingKey != null);
            yield return null;
            var syncedRobot = robot.GetComponent<NetworkToy>();
            Assert.IsTrue(syncedRobot.SyncedSlot(0).TryGet(out NetworkObject battery));
            Assert.AreSame(robot.Battery.gameObject, battery.gameObject, "The synced slot points at the actual part");
            Assert.IsTrue(syncedRobot.SyncedDirty);

            Assert.IsTrue(host.RequestPickUp(robot.Carryable), "The host applies its own requests directly");
            yield return null;
            var syncedCarry = robot.GetComponent<NetworkCarryable>();
            Assert.AreEqual(CarryState.Held, syncedCarry.SyncedState);
            Assert.AreEqual(NetworkManager.ServerClientId, syncedCarry.SyncedHolder);

            host.RequestDrop();
            yield return null;
            Assert.AreEqual(CarryState.Loose, syncedCarry.SyncedState);
            Assert.AreEqual(ulong.MaxValue, syncedCarry.SyncedHolder);

            Assert.IsTrue(robot.transform.Find("Battery Mount").GetComponentInChildren<ToyPart>() == robot.Battery, "Installed parts still hang on their mount");
        }

        [UnityTest]
        public IEnumerator Host_ResolvesForwardedTargets_AndRunsTheStationCode()
        {
            Assert.IsTrue(session.StartHost());
            yield return WaitFor(() => NetworkPlayer.Find(NetworkManager.ServerClientId) != null);
            NetworkPlayer host = NetworkPlayer.Find(NetworkManager.ServerClientId);
            var delivery = Object.FindAnyObjectByType<DeliveryChute>();

            // A scene station, named by its scene path, as a client would send it.
            var requestButton = Object.FindObjectsByType<DeliveryButton>().First(b => b.name == "Request Delivery Button");
            Assert.IsTrue(NetworkPlayer.DescribeTarget(requestButton, out NetworkObjectReference sceneRef, out string scenePath));
            Assert.AreEqual("Stations/Delivery Chute/Request Delivery Button", scenePath);
            Assert.AreSame(requestButton, NetworkPlayer.ResolveTarget(sceneRef, scenePath));
            Assert.IsTrue(host.ApplyInteract(sceneRef, scenePath), "Forwarded delivery request runs on the host");
            Assert.AreEqual(9, delivery.SpawnedItems.Count);

            // A part mount on a networked robot, named by the robot plus the path below it.
            ToyBody robot = delivery.SpawnedItems.Select(i => i.GetComponent<ToyBody>()).First(r => r != null && r.Battery != null && r.WindingKey != null);
            var salvage = Object.FindAnyObjectByType<SalvageBench>();
            Assert.IsTrue(host.Interactor.RequestPickUp(robot.Carryable));
            Assert.IsTrue(NetworkPlayer.DescribeTarget(salvage, out NetworkObjectReference benchRef, out string benchPath));
            Assert.IsTrue(host.ApplyInteract(benchRef, benchPath), "Forwarded place on the salvage bench");
            Assert.AreSame(robot, salvage.FindToy());

            var batteryMount = robot.GetComponentsInChildren<ToySlotTarget>().First(t => t.Slot == PartKind.Battery);
            Assert.IsTrue(NetworkPlayer.DescribeTarget(batteryMount, out NetworkObjectReference mountRef, out string mountPath));
            Assert.AreEqual("Battery Mount", mountPath);
            ToyPart battery = robot.Battery;
            Assert.IsTrue(host.ApplyInteract(mountRef, mountPath), "Forwarded salvage removes the part");
            Assert.IsNull(robot.Battery);
            Assert.AreEqual(CarryState.Loose, battery.Carryable.State);
            Assert.IsFalse(host.ApplyInteract(mountRef, mountPath), "A second request finds nothing to remove");
        }

        [UnityTest]
        public IEnumerator Host_RunsASharedShift_ThenResetsInPlace()
        {
            Assert.IsTrue(session.StartHost());
            yield return WaitFor(() => NetworkWorkshop.Instance != null && NetworkPlayer.Find(NetworkManager.ServerClientId) != null);
            PlayerInteractor host = NetworkPlayer.Find(NetworkManager.ServerClientId).Interactor;
            var workday = Object.FindAnyObjectByType<WorkdayManager>();
            var delivery = Object.FindAnyObjectByType<DeliveryChute>();
            var shipping = Object.FindAnyObjectByType<ShippingChute>();

            Assert.IsTrue(workday.StartShift(), "Shifts work online now");
            Assert.IsTrue(delivery.SpawnedItems.All(i => i.GetComponent<NetworkObject>().IsSpawned));
            yield return null;
            Assert.AreEqual(ShiftState.Running, NetworkWorkshop.Instance.SyncedShiftState, "Shift state is shared");

            ToyBody[] robots = delivery.SpawnedItems.Select(i => i.GetComponent<ToyBody>()).Where(r => r != null).ToArray();
            foreach (ToyBody robot in new[] { robots[2], robots[0] })
            {
                MakeValid(robot, delivery);
                Assert.IsTrue(host.RequestPickUp(robot.Carryable));
                Assert.IsTrue(shipping.TrySubmit(host), shipping.LastResult);
            }

            Assert.AreEqual(ShiftState.Succeeded, workday.State);
            Assert.AreEqual(1, workday.VerdictCount);
            yield return null;
            Assert.AreEqual(ShiftState.Succeeded, NetworkWorkshop.Instance.SyncedShiftState);
            Assert.AreEqual(2, NetworkWorkshop.Instance.SyncedShipped);

            // Play Again online: the session stays up and the workshop resets in place.
            workday.Restart();
            yield return WaitFor(() => workday.State == ShiftState.NotStarted);
            yield return null;

            Assert.IsTrue(NetworkSession.IsServer, "Still hosting");
            Assert.AreEqual(0, Object.FindAnyObjectByType<ShipmentCounter>().Shipped);
            Assert.AreEqual(workday.ShiftLengthSeconds, workday.RemainingSeconds);
            Assert.IsFalse(workday.VerdictVisible);
            Assert.AreEqual(0, Object.FindObjectsByType<ToyBody>().Length, "Every toy despawned");
            Assert.IsTrue(host.InteractionEnabled, "The host's elf is unfrozen");
            Assert.AreEqual(ShiftState.NotStarted, NetworkWorkshop.Instance.SyncedShiftState);
            Assert.IsTrue(workday.StartShift(), "A second shift can start");
        }

        [UnityTest]
        public IEnumerator Host_NextDay_StartsInPlace_AndIsShared()
        {
            Assert.IsTrue(session.StartHost());
            yield return WaitFor(() => NetworkWorkshop.Instance != null && NetworkPlayer.Find(NetworkManager.ServerClientId) != null);
            var workday = Object.FindAnyObjectByType<WorkdayManager>();
            var counter = Object.FindAnyObjectByType<ShipmentCounter>();

            Assert.IsTrue(workday.StartShift());
            for (int shipped = 0; shipped < workday.Quota; shipped++) counter.RecordShipment();
            Assert.AreEqual(ShiftState.Succeeded, workday.State);

            workday.Continue();
            yield return WaitFor(() => workday.State == ShiftState.NotStarted);
            yield return null;

            Assert.IsTrue(NetworkSession.IsServer, "Still hosting");
            Assert.AreEqual(2, workday.DayNumber);
            Assert.AreEqual(1, NetworkWorkshop.Instance.SyncedDayIndex, "The day is shared");
            Assert.IsTrue(NetworkPlayer.Find(NetworkManager.ServerClientId).Interactor.InteractionEnabled);
        }

        static void MakeValid(ToyBody robot, DeliveryChute delivery)
        {
            ToyPart[] loose = delivery.SpawnedItems.Select(i => i.GetComponent<ToyPart>())
                .Where(p => p != null && p.Carryable.State == CarryState.Loose).ToArray();
            robot.SetDirty(false);
            if (robot.Battery != null && !robot.Battery.IsWorking)
            {
                robot.Battery.Carryable.SetLoose(robot.transform.position + Vector3.right, Quaternion.identity, Vector3.zero);
            }
            if (robot.Battery == null) Assert.IsTrue(robot.TryInstall(loose.First(p => p.Kind == PartKind.Battery && p.IsWorking)));
            if (robot.WindingKey == null) Assert.IsTrue(robot.TryInstall(loose.First(p => p.Kind == PartKind.WindingKey)));
            Assert.IsTrue(ToyInspection.IsShippable(robot, out string reason), reason);
        }

        [UnityTest]
        public IEnumerator Leaving_ReturnsToTheMainMenu()
        {
            Assert.IsTrue(session.StartHost());
            yield return WaitFor(() => NetworkSession.IsServer);

            session.Leave();
            yield return WaitFor(() => NetworkManager.Singleton == null && SceneManager.GetActiveScene().name == GameLaunch.MainMenuScene);

            Assert.IsNull(NetworkManager.Singleton, "No network manager left behind");
            Assert.IsFalse(NetworkSession.IsOnline);
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenu>());
            Assert.IsNull(GameLaunch.TakeMenuMessage(), "Leaving on purpose shows no error");
        }

        static IEnumerator WaitFor(System.Func<bool> condition, float timeout = 5f)
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
