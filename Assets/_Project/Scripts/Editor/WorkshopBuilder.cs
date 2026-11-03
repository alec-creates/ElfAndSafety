using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ElfAndSafety.EditorTools
{
    /// <summary>
    /// Builds the graybox workshop: layers, flat-colour materials, player and toy prefabs, the test delivery
    /// batch asset, and the Workshop scene with every milestone 2 station wired up. Safe to re-run:
    /// materials, prefabs and the batch asset that already exist are reused untouched, so swapped-in art and
    /// tuned values survive; only the scene itself is regenerated.
    /// </summary>
    public static class WorkshopBuilder
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Workshop.unity";
        const string MainMenuPath = Root + "/Scenes/MainMenu.unity";
        const string MaterialFolder = Root + "/Art/Materials";
        const string PrefabFolder = Root + "/Prefabs";
        const string DataFolder = Root + "/Data";
        // Milestone 2-5 delivery asset, replaced by the waves in the countdown; deleted if found.
        const string LegacyBatchPath = DataFolder + "/Test Delivery Batch.asset";
        const string CountdownPath = DataFolder + "/Christmas Countdown.asset";
        // Netcode keeps this list up to date with every prefab that has a NetworkObject.
        const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
        const string BackupFolder = "SceneBackups";

        // Placed and spawned items face the room: station forward points at the wall behind it.
        static readonly Quaternion FaceRoom = Quaternion.Euler(0f, 180f, 0f);

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Font labelFont;

        struct ToyPrefabs
        {
            public ToyBody[] Toys;
            public ToyPart[] Parts;

            public IEnumerable<GameObject> All => Toys.Select(t => t.gameObject).Concat(Parts.Select(p => p.gameObject));
        }

        static readonly string[] ToyPrefabNames = { "Robot Body", "Teddy Bear", "Toy Car" };
        static readonly string[] PartPrefabNames =
        {
            "Battery", "Dead Battery", "Winding Key", "Stuffing", "Button Eyes", "Wheel Set",
            // Faulty parts: same names in game, a small visual tell, and a malfunction once installed.
            "Leaky Battery", "Overwound Winding Key", "Loose Button Eyes", "Wobbly Wheel Set",
        };

        [MenuItem("Elf & Safety/Build Game Scenes")]
        public static void BuildFromMenu()
        {
            if ((File.Exists(ScenePath) || File.Exists(MainMenuPath)) &&
                !EditorUtility.DisplayDialog("Rebuild the game scenes?",
                    $"The Main Menu and Workshop scenes will be regenerated. The current files are backed up to {BackupFolder}/ first; existing materials, prefabs and data assets are kept.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        /// <summary>Entry point for -batchmode -executeMethod.</summary>
        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Build()
        {
            EnsureLayer(GameLayers.PlayerName);
            EnsureLayer(GameLayers.CarryableName);
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(DataFolder);

            materials.Clear();
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BackUpExistingScene(ScenePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildToyPrefabs();
            UpgradeLegacyRobotPrefab();
            AddNetworkComponentsToToys();
            ToyPrefabs toys = BuildToyPrefabs();
            DeleteLegacyBatchAsset();

            BuildLighting();
            TrapdoorSequence trapdoor = BuildRoom();
            var counter = new GameObject("Shipment Counter").AddComponent<ShipmentCounter>();
            var workday = new GameObject("Workday").AddComponent<WorkdayManager>();
            DeliveryChute delivery = BuildStations(toys, counter, workday);
            BuildTimeClock(workday);
            var player = BuildPlayer();
            InteractionHud hud = BuildHud(player.GetComponent<PlayerInteractor>(), workday);

            GameObject netPlayer = BuildNetworkPlayerPrefab();
            GameObject workshopSync = BuildWorkshopSyncPrefab();
            NetworkPrefabsList networkPrefabs = BuildNetworkPrefabsList(toys, netPlayer, workshopSync);
            NetworkManager managerPrefab = BuildNetworkManagerPrefab(netPlayer, networkPrefabs);
            BuildNetworkSession(managerPrefab, workshopSync, player.GetComponent<PlayerInteractor>(), delivery, workday, hud);

            Assign(workday, "counter", counter);
            Assign(workday, "delivery", delivery);
            Assign(workday, "trapdoor", trapdoor);
            Assign(workday, "shipping", Object.FindAnyObjectByType<ShippingChute>());
            Assign(workday, "countdown", LoadOrCreateCountdown());

            EditorSceneManager.SaveScene(scene, ScenePath);

            BuildMainMenuScene(toys);

            // The main menu opens first in a build; the Workshop is loaded from it.
            AddSceneToBuildSettings(ScenePath);
            AddSceneToBuildSettings(MainMenuPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Elf & Safety: built {MainMenuPath} and {ScenePath}. Press Play to test.");
        }

        /// <summary>
        /// Copies the scene about to be regenerated into SceneBackups/ next to Assets/, so hand edits are
        /// never lost. Outside Assets on purpose: Unity doesn't import it as a second scene with the same GUID.
        /// </summary>
        static void BackUpExistingScene(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                return;
            }

            Directory.CreateDirectory(BackupFolder);
            string backup = Path.Combine(BackupFolder, $"{Path.GetFileNameWithoutExtension(scenePath)}_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
            File.Copy(scenePath, backup, overwrite: true);
            Debug.Log($"Elf & Safety: backed up {scenePath} to {backup}");
        }

        // ---------------------------------------------------------------- Environment

        static void BuildLighting()
        {
            var sun = new GameObject("Workshop Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.5f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.32f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.14f, 0.12f);

            var lamps = new GameObject("Hanging Lamps").transform;
            foreach (float x in new[] { -4.5f, 0f, 4.5f })
            {
                var lamp = new GameObject("Lamp").transform;
                lamp.SetParent(lamps, false);
                lamp.localPosition = new Vector3(x, 3.4f, 1f);
                Block("Shade", lamp, new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.15f, 0.6f), Mat("Metal Dark", new Color(0.22f, 0.22f, 0.24f)));
                Block("Bulb", lamp, Vector3.zero, Vector3.one * 0.2f, Mat("Bulb", new Color(1f, 0.9f, 0.6f), emission: new Color(1f, 0.8f, 0.45f) * 2f), PrimitiveType.Sphere);
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(lamp, false);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.82f, 0.55f);
                light.range = 7f;
                light.intensity = 3f;
            }
        }

        static TrapdoorSequence BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var wood = Mat("Floor Wood", new Color(0.55f, 0.36f, 0.22f));
            var brick = Mat("Wall Brick", new Color(0.55f, 0.18f, 0.16f));
            var trim = Mat("Wall Trim Green", new Color(0.12f, 0.38f, 0.22f));

            // Floor in four pieces around the trapdoor hole (x -1.3..1.3, z -3.8..-1.2).
            Block("Floor North", room, new Vector3(0f, -0.1f, 2.4f), new Vector3(16f, 0.2f, 7.2f), wood, keepCollider: true);
            Block("Floor South", room, new Vector3(0f, -0.1f, -4.9f), new Vector3(16f, 0.2f, 2.2f), wood, keepCollider: true);
            Block("Floor West", room, new Vector3(-4.65f, -0.1f, -2.5f), new Vector3(6.7f, 0.2f, 2.6f), wood, keepCollider: true);
            Block("Floor East", room, new Vector3(4.65f, -0.1f, -2.5f), new Vector3(6.7f, 0.2f, 2.6f), wood, keepCollider: true);
            Block("Wall North", room, new Vector3(0f, 2f, 6.2f), new Vector3(16.8f, 4f, 0.4f), brick, keepCollider: true);
            Block("Wall South", room, new Vector3(0f, 2f, -6.2f), new Vector3(16.8f, 4f, 0.4f), brick, keepCollider: true);
            Block("Wall East", room, new Vector3(8.2f, 2f, 0f), new Vector3(0.4f, 4f, 12f), brick, keepCollider: true);
            Block("Wall West", room, new Vector3(-8.2f, 2f, 0f), new Vector3(0.4f, 4f, 12f), brick, keepCollider: true);

            // Green skirting so the room reads as "festive" even in graybox.
            Block("Trim North", room, new Vector3(0f, 0.3f, 5.98f), new Vector3(16f, 0.6f, 0.05f), trim);
            Block("Trim South", room, new Vector3(0f, 0.3f, -5.98f), new Vector3(16f, 0.6f, 0.05f), trim);
            Block("Trim East", room, new Vector3(7.98f, 0.3f, 0f), new Vector3(0.05f, 0.6f, 12f), trim);
            Block("Trim West", room, new Vector3(-7.98f, 0.3f, 0f), new Vector3(0.05f, 0.6f, 12f), trim);

            TrapdoorSequence trapdoor = BuildTrapdoor(room);

            // Santa's office window on the south wall.
            var office = new GameObject("Santa's Office Window").transform;
            office.SetParent(room, false);
            office.localPosition = new Vector3(0f, 2.4f, -5.95f);
            Block("Frame", office, Vector3.zero, new Vector3(3.2f, 1.6f, 0.15f), Mat("Santa Red", new Color(0.78f, 0.08f, 0.1f)));
            Block("Glass", office, new Vector3(0f, 0f, 0.05f), new Vector3(2.8f, 1.2f, 0.1f),
                Mat("Office Glow", new Color(1f, 0.85f, 0.55f), emission: new Color(1f, 0.6f, 0.25f) * 1.5f));
            Label(office, "SANTA'S OFFICE\nsafety first (yours last)", new Vector3(0f, 1.1f, 0.1f), Quaternion.Euler(0f, 180f, 0f), 0.06f, Color.white);

            // A few unsafe-looking hazard posts.
            var posts = Mat("Hazard Yellow", Color.yellow);
            Block("Hazard Post", room, new Vector3(-1.9f, 0.5f, -1.2f), new Vector3(0.15f, 1f, 0.15f), posts, PrimitiveType.Cylinder, keepCollider: true);
            Block("Hazard Post", room, new Vector3(1.9f, 0.5f, -1.2f), new Vector3(0.15f, 1f, 0.15f), posts, PrimitiveType.Cylinder, keepCollider: true);
            return trapdoor;
        }

        /// <summary>
        /// Two hinged doors over a hole in front of Santa's window, with a furnace pit below. The doors are
        /// solid floor until the failure sequence swings them open.
        /// </summary>
        static TrapdoorSequence BuildTrapdoor(Transform room)
        {
            var yellow = Mat("Hazard Yellow", new Color(0.98f, 0.78f, 0.1f));
            var black = Mat("Hazard Black", new Color(0.08f, 0.08f, 0.09f));
            var soot = Mat("Pit Soot", new Color(0.18f, 0.1f, 0.08f));
            var glow = Mat("Furnace Glow", new Color(1f, 0.45f, 0.1f), emission: new Color(1f, 0.35f, 0.05f) * 3f);

            var root = new GameObject("Trapdoor").transform;
            root.SetParent(room, false);
            root.localPosition = new Vector3(0f, 0f, -2.5f);

            // Hazard frame around the hole.
            Block("Frame North", root, new Vector3(0f, 0.003f, 1.4f), new Vector3(3f, 0.006f, 0.2f), yellow);
            Block("Frame South", root, new Vector3(0f, 0.003f, -1.4f), new Vector3(3f, 0.006f, 0.2f), yellow);
            Block("Frame West", root, new Vector3(-1.4f, 0.003f, 0f), new Vector3(0.2f, 0.006f, 2.6f), yellow);
            Block("Frame East", root, new Vector3(1.4f, 0.003f, 0f), new Vector3(0.2f, 0.006f, 2.6f), yellow);

            // Doors: each hinge sits on an outer edge; its door swings down around the hinge's Z axis.
            var leftHinge = new GameObject("Left Hinge").transform;
            leftHinge.SetParent(root, false);
            leftHinge.localPosition = new Vector3(-1.3f, -0.025f, 0f);
            Block("Left Door", leftHinge, new Vector3(0.65f, 0f, 0f), new Vector3(1.3f, 0.05f, 2.6f), black, keepCollider: true);
            Block("Left Stripe", leftHinge, new Vector3(0.65f, 0.026f, 0f), new Vector3(0.12f, 0.002f, 2.4f), yellow);

            var rightHinge = new GameObject("Right Hinge").transform;
            rightHinge.SetParent(root, false);
            rightHinge.localPosition = new Vector3(1.3f, -0.025f, 0f);
            Block("Right Door", rightHinge, new Vector3(-0.65f, 0f, 0f), new Vector3(1.3f, 0.05f, 2.6f), black, keepCollider: true);
            Block("Right Stripe", rightHinge, new Vector3(-0.65f, 0.026f, 0f), new Vector3(0.12f, 0.002f, 2.4f), yellow);

            Label(root, "PERFORMANCE REVIEW AREA", new Vector3(0f, 0.01f, -1.75f), Quaternion.Euler(90f, 0f, 0f), 0.05f, new Color(1f, 0.8f, 0.1f));

            // Furnace pit: sooty walls and a glowing floor 6 m down.
            var pit = new GameObject("Furnace Pit").transform;
            pit.SetParent(root, false);
            Block("Pit Wall North", pit, new Vector3(0f, -3.2f, 1.4f), new Vector3(2.8f, 6f, 0.2f), soot, keepCollider: true);
            Block("Pit Wall South", pit, new Vector3(0f, -3.2f, -1.4f), new Vector3(2.8f, 6f, 0.2f), soot, keepCollider: true);
            Block("Pit Wall West", pit, new Vector3(-1.4f, -3.2f, 0f), new Vector3(0.2f, 6f, 2.6f), soot, keepCollider: true);
            Block("Pit Wall East", pit, new Vector3(1.4f, -3.2f, 0f), new Vector3(0.2f, 6f, 2.6f), soot, keepCollider: true);
            Block("Furnace Floor", pit, new Vector3(0f, -6.3f, 0f), new Vector3(2.8f, 0.2f, 2.8f), glow, keepCollider: true);

            var light = new GameObject("Furnace Light").AddComponent<Light>();
            light.transform.SetParent(pit, false);
            light.transform.localPosition = new Vector3(0f, -4.5f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.45f, 0.1f);
            light.range = 9f;
            light.intensity = 0f;

            // The player is pulled here, facing Santa's window, before the drop.
            var standPoint = new GameObject("Stand Point").transform;
            standPoint.SetParent(root, false);
            standPoint.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var sequence = root.gameObject.AddComponent<TrapdoorSequence>();
            Assign(sequence, "leftHinge", leftHinge);
            Assign(sequence, "rightHinge", rightHinge);
            Assign(sequence, "furnaceLight", light);
            Assign(sequence, "standPoint", standPoint);
            return sequence;
        }

        /// <summary>The CLOCK IN post near the spawn point: the explicit Start Shift action.</summary>
        static void BuildTimeClock(WorkdayManager workday)
        {
            var root = new GameObject("Time Clock").transform;
            root.position = new Vector3(-1.2f, 0f, 0.6f);
            var post = Block("Post", root, new Vector3(0f, 0.55f, 0f), new Vector3(0.3f, 1.1f, 0.3f), Mat("Santa Red", Color.red), keepCollider: true);
            post.name = "Post";
            Block("Clock Face", root, new Vector3(0f, 1.3f, -0.16f), new Vector3(0.36f, 0.36f, 0.02f), Mat("Bulb", Color.yellow));

            var button = new GameObject("Clock In Button");
            button.transform.SetParent(root, false);
            button.transform.localPosition = new Vector3(0f, 1.15f, -0.12f);
            var box = button.AddComponent<BoxCollider>();
            box.size = new Vector3(0.34f, 0.12f, 0.3f);
            Block("Visual", button.transform, Vector3.zero, new Vector3(0.34f, 0.12f, 0.3f), Mat("Button Green", new Color(0.2f, 0.85f, 0.3f), emission: new Color(0.1f, 0.5f, 0.15f)));
            var shiftButton = button.AddComponent<ShiftButton>();
            Assign(shiftButton, "workday", workday);

            Label(root, "CLOCK IN", new Vector3(0f, 1.7f, -0.16f), Quaternion.identity, 0.05f, new Color(0.6f, 1f, 0.6f));
        }

        // ---------------------------------------------------------------- Stations

        static DeliveryChute BuildStations(ToyPrefabs toys, ShipmentCounter counter, WorkdayManager workday)
        {
            var stations = new GameObject("Stations").transform;
            var metal = Mat("Metal Grey", new Color(0.5f, 0.52f, 0.55f));
            var metalDark = Mat("Metal Dark", new Color(0.22f, 0.22f, 0.24f));
            var red = Mat("Santa Red", Color.red);
            var green = Mat("Elf Green", new Color(0.15f, 0.55f, 0.25f));
            var blue = Mat("Wash Blue", new Color(0.25f, 0.55f, 0.85f));
            var gold = Mat("Ribbon Gold", new Color(0.95f, 0.72f, 0.2f));
            var crate = Mat("Crate Brown", new Color(0.45f, 0.3f, 0.16f));
            var outputPad = Mat("Output Pad Green", new Color(0.3f, 0.85f, 0.4f));
            var buttonGreen = Mat("Button Green", new Color(0.2f, 0.85f, 0.3f), emission: new Color(0.1f, 0.5f, 0.15f));
            var buttonRed = Mat("Button Red", new Color(0.9f, 0.15f, 0.12f), emission: new Color(0.5f, 0.05f, 0.05f));
            var bar = Mat("Progress Bar Back", new Color(0.12f, 0.12f, 0.14f));
            var barFill = Mat("Progress Bar Fill", new Color(0.4f, 0.85f, 1f), emission: new Color(0.2f, 0.5f, 0.7f));

            // Delivery table along the west wall: items drop in loose at six spots and settle with physics.
            var table = Station(stations, "Delivery Table", new Vector3(-7.4f, 0f, 1.8f), Quaternion.Euler(0f, -90f, 0f), new Vector3(3.6f, 0.75f, 0.9f), crate);
            var tableVisual = table.Find("Visual");
            Lip(tableVisual, new Vector3(0f, 0.8f, -0.43f), new Vector3(3.6f, 0.1f, 0.04f), metalDark);
            Lip(tableVisual, new Vector3(0f, 0.8f, 0.43f), new Vector3(3.6f, 0.1f, 0.04f), metalDark);
            Lip(tableVisual, new Vector3(-1.78f, 0.8f, 0f), new Vector3(0.04f, 0.1f, 0.9f), metalDark);
            Lip(tableVisual, new Vector3(1.78f, 0.8f, 0f), new Vector3(0.04f, 0.1f, 0.9f), metalDark);
            var spawnPoints = new Transform[6];
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                // High enough that the clearance check (0.12 m) does not touch the tabletop at 0.75.
                spawnPoints[i] = Point(table, $"Spawn Point {i + 1}", new Vector3(-1.5f + i * 0.6f, 0.92f, 0f));
            }
            Label(table, "DELIVERY TABLE", new Vector3(0f, 1.5f, 0.3f), Quaternion.identity, 0.06f, Color.white);

            // Delivery chute with its request and development-reset buttons (north-west corner).
            var chute = Station(stations, "Delivery Chute", new Vector3(-6.5f, 0f, 4.6f), Quaternion.identity, new Vector3(1.6f, 0.8f, 1.2f), red);
            var chuteVisual = chute.Find("Visual");
            Block("Hopper", chuteVisual, new Vector3(0f, 3f, 0.9f), new Vector3(1.4f, 1.2f, 0.8f), red);
            Block("Ramp", chuteVisual, new Vector3(0f, 1.65f, 0.55f), new Vector3(1f, 0.08f, 2.2f), metal, eulerAngles: new Vector3(-50f, 0f, 0f));
            var delivery = chute.gameObject.AddComponent<DeliveryChute>();
            Button(chute, "Request Delivery Button", new Vector3(-0.4f, 0.85f, -0.3f), buttonGreen, delivery, DeliveryButton.ButtonAction.RequestDelivery);
            Button(chute, "DEV Reset Button", new Vector3(0.45f, 0.85f, -0.3f), buttonRed, delivery, DeliveryButton.ButtonAction.DevResetBatch);
            Label(chute, "REQUEST\nDELIVERY", new Vector3(-0.4f, 1.15f, -0.3f), Quaternion.identity, 0.035f, new Color(0.6f, 1f, 0.6f));
            Label(chute, "DEV: RESET\nTEST BATCH", new Vector3(0.45f, 1.15f, -0.3f), Quaternion.identity, 0.035f, new Color(1f, 0.6f, 0.55f));
            Label(chute, "DELIVERIES", new Vector3(0f, 2.2f, -0.2f), Quaternion.identity, 0.06f, Color.white);
            var deliveryStatus = Label(chute, "", new Vector3(0f, 1.85f, -0.2f), Quaternion.identity, 0.04f, Color.white);

            AssignArray(delivery, "toyPrefabs", toys.Toys);
            AssignArray(delivery, "partPrefabs", toys.Parts);
            AssignArray(delivery, "spawnPoints", spawnPoints);
            Assign(delivery, "statusLabel", deliveryStatus);
            Assign(delivery, "workday", workday);

            // Salvage bench: work area on the left, loose parts tray on the right.
            var salvage = Station(stations, "Salvage Bench", new Vector3(-3f, 0f, 4.8f), Quaternion.identity, new Vector3(2.4f, 0.9f, 0.9f), metal);
            Block("Saw Blade", salvage.Find("Visual"), new Vector3(-0.55f, 1.45f, 0.42f), new Vector3(0.6f, 0.02f, 0.6f), metalDark, PrimitiveType.Cylinder, eulerAngles: new Vector3(90f, 0f, 0f));
            var salvageArea = WorkArea<SalvageBench>(salvage, "Salvage Bench", new Vector3(-0.55f, 0.9f, 0f), new Vector3(1.1f, 0.7f, 0.85f), gold, new Vector2(0.6f, 0.5f));
            // Parts tray: a lipped green area where removed parts pop out loose.
            var salvageVisual = salvage.Find("Visual");
            Block("Parts Tray", salvageVisual, new Vector3(0.65f, 0.905f, 0f), new Vector3(1f, 0.01f, 0.6f), outputPad);
            Lip(salvageVisual, new Vector3(0.65f, 0.95f, -0.3f), new Vector3(1f, 0.1f, 0.03f), outputPad);
            Lip(salvageVisual, new Vector3(0.65f, 0.95f, 0.3f), new Vector3(1f, 0.1f, 0.03f), outputPad);
            Lip(salvageVisual, new Vector3(0.15f, 0.95f, 0f), new Vector3(0.03f, 0.1f, 0.6f), outputPad);
            Lip(salvageVisual, new Vector3(1.15f, 0.95f, 0f), new Vector3(0.03f, 0.1f, 0.6f), outputPad);
            var outputPoints = new Transform[3];
            for (int i = 0; i < outputPoints.Length; i++)
            {
                outputPoints[i] = Point(salvage, $"Output Point {i + 1}", new Vector3(0.35f + i * 0.3f, 1.02f, 0f));
            }
            Label(salvage, "SALVAGE", new Vector3(0f, 2.1f, 0.4f), Quaternion.identity, 0.06f, Color.white);
            Label(salvage, "parts out >", new Vector3(0.6f, 1.35f, 0.4f), Quaternion.identity, 0.03f, new Color(0.6f, 1f, 0.6f));
            AssignArray(salvageArea, "outputPoints", outputPoints);
            Assign(salvageArea, "statusLabel", Label(salvage, "", new Vector3(0f, 1.8f, 0.4f), Quaternion.identity, 0.04f, Color.white));

            // Cleaning station: scrubs automatically with a progress bar above it.
            var cleaning = Station(stations, "Cleaning Station", new Vector3(0.5f, 0f, 4.8f), Quaternion.identity, new Vector3(1.6f, 0.75f, 0.9f), blue);
            var cleaningVisual = cleaning.Find("Visual");
            Block("Soap Nozzle", cleaningVisual, new Vector3(0.6f, 1.6f, 0.35f), new Vector3(0.12f, 0.6f, 0.12f), green, PrimitiveType.Cylinder);
            var cleaningArea = WorkArea<CleaningStation>(cleaning, "Cleaning Station", new Vector3(0f, 0.75f, 0f), new Vector3(1.5f, 0.8f, 0.85f), gold, new Vector2(0.6f, 0.5f));
            Lip(cleaningVisual, new Vector3(0f, 0.85f, -0.47f), new Vector3(1.7f, 0.2f, 0.06f), metal);
            Lip(cleaningVisual, new Vector3(0f, 0.85f, 0.47f), new Vector3(1.7f, 0.2f, 0.06f), metal);
            Lip(cleaningVisual, new Vector3(-0.82f, 0.85f, 0f), new Vector3(0.06f, 0.2f, 1f), metal);
            Lip(cleaningVisual, new Vector3(0.82f, 0.85f, 0f), new Vector3(0.06f, 0.2f, 1f), metal);
            Block("Progress Back", cleaningVisual, new Vector3(0f, 1.8f, 0.3f), new Vector3(0.94f, 0.12f, 0.03f), bar);
            var fillPivot = new GameObject("Progress Fill Pivot").transform;
            fillPivot.SetParent(cleaning, false);
            fillPivot.localPosition = new Vector3(-0.45f, 1.8f, 0.27f);
            Block("Progress Fill", fillPivot, new Vector3(0.45f, 0f, 0f), new Vector3(0.9f, 0.08f, 0.03f), barFill);
            fillPivot.localScale = new Vector3(0.001f, 1f, 1f);
            Label(cleaning, "SCRUB", new Vector3(0f, 2.4f, 0.4f), Quaternion.identity, 0.06f, Color.white);
            Assign(cleaningArea, "progressFill", fillPivot);
            Assign(cleaningArea, "statusLabel", Label(cleaning, "", new Vector3(0f, 2.05f, 0.3f), Quaternion.identity, 0.04f, Color.white));

            // Assembly bench: hold a body, install carried parts, read the checklist above it.
            var assembly = Station(stations, "Assembly Bench", new Vector3(4f, 0f, 4.8f), Quaternion.identity, new Vector3(2f, 0.9f, 0.9f), green);
            Block("Lamp Arm", assembly.Find("Visual"), new Vector3(-0.8f, 1.4f, 0.3f), new Vector3(0.08f, 1f, 0.08f), metalDark);
            var assemblyArea = WorkArea<AssemblyBench>(assembly, "Assembly Bench", new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 0.7f, 0.85f), gold, new Vector2(0.6f, 0.5f));
            Label(assembly, "ASSEMBLY", new Vector3(0f, 2.55f, 0.4f), Quaternion.identity, 0.06f, Color.white);
            Assign(assemblyArea, "statusLabel", Label(assembly, "", new Vector3(0.55f, 1.9f, 0.4f), Quaternion.identity, 0.04f, Color.white));

            // Reject shelf and shipping chute on the east wall, facing into the room.
            var east = Quaternion.Euler(0f, 90f, 0f);
            // Reject bin: an open crate on the floor; rejected robots are dropped in loose.
            var rejects = Station(stations, "Reject Bin", new Vector3(7.1f, 0f, -2.5f), east, new Vector3(2.1f, 0.1f, 1.3f), crate);
            var rejectsVisual = rejects.Find("Visual");
            Lip(rejectsVisual, new Vector3(0f, 0.35f, -0.63f), new Vector3(2.1f, 0.6f, 0.05f), crate);
            Lip(rejectsVisual, new Vector3(0f, 0.35f, 0.63f), new Vector3(2.1f, 0.6f, 0.05f), crate);
            Lip(rejectsVisual, new Vector3(-1.03f, 0.35f, 0f), new Vector3(0.05f, 0.6f, 1.3f), crate);
            Lip(rejectsVisual, new Vector3(1.03f, 0.35f, 0f), new Vector3(0.05f, 0.6f, 1.3f), crate);
            Block("Hazard Band", rejectsVisual, new Vector3(0f, 0.6f, -0.66f), new Vector3(2.1f, 0.08f, 0.01f), buttonRed);
            var rejectPoints = new Transform[3];
            for (int i = 0; i < rejectPoints.Length; i++)
            {
                rejectPoints[i] = Point(rejects, $"Reject Drop Point {i + 1}", new Vector3(-0.6f + i * 0.6f, 0.9f, 0f));
            }
            Label(rejects, "REJECTS\n(fix and resubmit)", new Vector3(0f, 1.6f, 0.4f), Quaternion.identity, 0.05f, Color.white);

            var shipping = Station(stations, "Shipping Chute", new Vector3(7.3f, 0f, 2f), east, new Vector3(2f, 1f, 1f), red);
            var shippingVisual = shipping.Find("Visual");
            Block("Ribbon", shippingVisual, new Vector3(0f, 0.5f, -0.51f), new Vector3(0.2f, 1f, 0.02f), gold);
            Block("Mouth", shippingVisual, new Vector3(0f, 1.6f, 0.3f), new Vector3(1.4f, 1.2f, 0.4f), metalDark);
            Block("Mouth Lip", shippingVisual, new Vector3(0f, 1.02f, -0.2f), new Vector3(1.4f, 0.04f, 0.6f), gold);
            var mouth = new GameObject("Chute Mouth");
            mouth.transform.SetParent(shipping, false);
            mouth.transform.localPosition = new Vector3(0f, 1.4f, -0.1f);
            var mouthTrigger = mouth.AddComponent<BoxCollider>();
            mouthTrigger.isTrigger = true;
            mouthTrigger.size = new Vector3(1.4f, 0.8f, 0.9f);
            var shippingChute = mouth.AddComponent<ShippingChute>();
            Label(shipping, "SHIPPING", new Vector3(0f, 2.95f, -0.2f), Quaternion.identity, 0.06f, Color.white);

            var board = new GameObject("Wish List Board").transform;
            board.SetParent(stations, false);
            board.SetPositionAndRotation(new Vector3(7.9f, 1.95f, -0.25f), east);
            Block("Backing", board, new Vector3(0f, 0f, 0.04f), new Vector3(1.9f, 1.5f, 0.04f), Mat("Board Green", new Color(0.1f, 0.3f, 0.18f)));
            Block("Frame Top", board, new Vector3(0f, 0.77f, 0.03f), new Vector3(1.96f, 0.06f, 0.06f), red);
            var boardText = Label(board, "", new Vector3(0f, 0.68f, 0f), Quaternion.identity, 0.035f, Color.white);
            boardText.anchor = TextAnchor.UpperCenter;
            var orderBoard = board.gameObject.AddComponent<OrderBoard>();
            Assign(orderBoard, "workday", workday);
            Assign(orderBoard, "label", boardText);
            Assign(shippingChute, "counter", counter);
            Assign(shippingChute, "workday", workday);
            AssignArray(shippingChute, "rejectDropPoints", rejectPoints);
            Assign(shippingChute, "statusLabel", Label(shipping, "", new Vector3(0f, 2.5f, -0.2f), Quaternion.identity, 0.045f, Color.white));
            return delivery;
        }

        // ---------------------------------------------------------------- Player and HUD

        static GameObject BuildPlayer()
        {
            string path = $"{PrefabFolder}/Elf Player.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var temp = CreatePlayerObject();
                prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
                Object.DestroyImmediate(temp);
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, -1f), Quaternion.identity);
            return player;
        }

        static GameObject CreatePlayerObject()
        {
            var root = new GameObject("Elf Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.5f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.75f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.03f;

            root.AddComponent<PlayerInputReader>();
            var movement = root.AddComponent<FirstPersonController>();
            var interactor = root.AddComponent<PlayerInteractor>();

            var pivot = new GameObject("Camera Pivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.3f, 0f);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(pivot, false);
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.gameObject.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            var hold = new GameObject("Hold Point").transform;
            hold.SetParent(cam.transform, false);
            hold.localPosition = new Vector3(0f, -0.3f, 0.85f);

            // Seen by other players later; from inside the body capsule the local player can't see it.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var skin = Mat("Elf Skin", new Color(0.98f, 0.78f, 0.65f));
            Block("Body", visual, new Vector3(0f, 0.75f, 0f), new Vector3(0.6f, 0.75f, 0.6f), Mat("Elf Green", Color.green), PrimitiveType.Capsule);
            Block("Belt", visual, new Vector3(0f, 0.7f, 0f), new Vector3(0.64f, 0.08f, 0.64f), Mat("Metal Dark", Color.black), PrimitiveType.Cylinder);
            Block("Ear Left", visual, new Vector3(-0.36f, 1.3f, 0f), new Vector3(0.08f, 0.12f, 0.22f), skin, eulerAngles: new Vector3(0f, 0f, 35f));
            Block("Ear Right", visual, new Vector3(0.36f, 1.3f, 0f), new Vector3(0.08f, 0.12f, 0.22f), skin, eulerAngles: new Vector3(0f, 0f, -35f));
            Block("Nose", visual, new Vector3(0f, 1.2f, 0.34f), Vector3.one * 0.14f, skin, PrimitiveType.Sphere);
            Block("Hat", visual, new Vector3(0f, 1.58f, -0.05f), new Vector3(0.5f, 0.12f, 0.5f), Mat("Santa Red", Color.red), PrimitiveType.Cylinder);

            SetLayerRecursive(root, GameLayers.Player);

            int playerBit = 1 << GameLayers.Player;
            int carryBit = 1 << GameLayers.Carryable;
            int ignoreRaycastBit = 1 << LayerMask.NameToLayer("Ignore Raycast");

            Assign(movement, "cameraPivot", pivot);
            Assign(interactor, "viewCamera", cam);
            Assign(interactor, "holdPoint", hold);
            AssignMask(interactor, "targetMask", ~(playerBit | ignoreRaycastBit));
            AssignMask(interactor, "holdBlockMask", ~(playerBit | carryBit | ignoreRaycastBit));
            return root;
        }

        static InteractionHud BuildHud(PlayerInteractor player, WorkdayManager workday)
        {
            var canvasObject = new GameObject("HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var topBar = new GameObject("Top Bar").AddComponent<Image>();
            topBar.transform.SetParent(canvasObject.transform, false);
            topBar.color = new Color(0.05f, 0.03f, 0.04f, 0.55f);
            topBar.raycastTarget = false;
            topBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            topBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            topBar.rectTransform.pivot = new Vector2(0.5f, 1f);
            topBar.rectTransform.sizeDelta = new Vector2(0f, 150f);
            topBar.rectTransform.anchoredPosition = Vector2.zero;

            var crosshair = new GameObject("Crosshair").AddComponent<Image>();
            crosshair.transform.SetParent(canvasObject.transform, false);
            crosshair.rectTransform.sizeDelta = new Vector2(8f, 8f);
            crosshair.raycastTarget = false;

            var prompt = HudText(canvasObject.transform, "Interaction Prompt", new Vector2(0f, -70f), 34, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f));
            var info = HudText(canvasObject.transform, "Look Info", new Vector2(0f, -115f), 24, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f));
            var feedback = HudText(canvasObject.transform, "Feedback", new Vector2(0f, 180f), 32, TextAnchor.LowerCenter, new Vector2(0.5f, 0.5f));
            var carry = HudText(canvasObject.transform, "Carry Label", new Vector2(0f, 60f), 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0f));
            var shift = HudText(canvasObject.transform, "Shift Status", new Vector2(0f, -70f), 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f));
            shift.rectTransform.sizeDelta = new Vector2(1500f, 80f);
            var controls = HudText(canvasObject.transform, "Controls", new Vector2(24f, -20f), 22, TextAnchor.UpperLeft, new Vector2(0f, 1f));
            controls.text = "WASD move   Mouse look   Shift sprint   Space hop\nE / Click interact   Q drop   Esc pause";
            controls.color = new Color(1f, 1f, 1f, 0.7f);
            info.color = new Color(0.85f, 0.95f, 1f, 0.9f);

            var hud = canvasObject.AddComponent<InteractionHud>();
            Assign(hud, "player", player);
            Assign(hud, "promptLabel", prompt);
            Assign(hud, "carryLabel", carry);
            Assign(hud, "crosshair", crosshair);
            Assign(hud, "infoLabel", info);
            Assign(hud, "feedbackLabel", feedback);

            var wishList = HudText(canvasObject.transform, "Wish List", new Vector2(-28f, -170f), 24, TextAnchor.UpperRight, new Vector2(1f, 1f));
            wishList.rectTransform.sizeDelta = new Vector2(560f, 320f);
            wishList.color = new Color(1f, 0.95f, 0.8f, 0.92f);

            BuildVerdictPanel(canvasObject.transform, canvasObject, workday, shift, wishList, controls.gameObject);
            BuildPauseMenu(canvasObject.transform, canvasObject, workday);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return hud;
        }

        /// <summary>Santa's verdict: a dimmed full-screen panel with a title, his line, and the restart button.</summary>
        static void BuildVerdictPanel(Transform canvas, GameObject hudObject, WorkdayManager workday, Text shiftLabel, Text wishList, GameObject controlsHint)
        {
            var panel = new GameObject("Verdict Panel").AddComponent<Image>();
            panel.transform.SetParent(canvas, false);
            panel.color = new Color(0.08f, 0.02f, 0.02f, 0.82f);
            panel.rectTransform.anchorMin = Vector2.zero;
            panel.rectTransform.anchorMax = Vector2.one;
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = Vector2.zero;

            var title = HudText(panel.transform, "Verdict Title", new Vector2(0f, 170f), 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            title.rectTransform.sizeDelta = new Vector2(1400f, 120f);
            title.color = new Color(1f, 0.82f, 0.2f);
            var message = HudText(panel.transform, "Verdict Message", new Vector2(0f, 10f), 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            message.rectTransform.sizeDelta = new Vector2(1400f, 220f);
            message.horizontalOverflow = HorizontalWrapMode.Wrap;

            var buttonImage = new GameObject("Restart Button").AddComponent<Image>();
            buttonImage.transform.SetParent(panel.transform, false);
            buttonImage.color = new Color(0.2f, 0.7f, 0.3f);
            buttonImage.rectTransform.sizeDelta = new Vector2(380f, 90f);
            buttonImage.rectTransform.anchoredPosition = new Vector2(-210f, -190f);
            var button = buttonImage.gameObject.AddComponent<Button>();
            var buttonLabel = HudText(buttonImage.transform, "Label", Vector2.zero, 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            buttonLabel.rectTransform.sizeDelta = new Vector2(380f, 90f);

            var menuImage = new GameObject("Main Menu Button").AddComponent<Image>();
            menuImage.transform.SetParent(panel.transform, false);
            menuImage.color = new Color(0.35f, 0.18f, 0.18f);
            menuImage.rectTransform.sizeDelta = new Vector2(380f, 90f);
            menuImage.rectTransform.anchoredPosition = new Vector2(210f, -190f);
            var menuButton = menuImage.gameObject.AddComponent<Button>();
            var menuLabel = HudText(menuImage.transform, "Label", Vector2.zero, 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            menuLabel.rectTransform.sizeDelta = new Vector2(380f, 90f);
            menuLabel.text = "Main Menu";

            panel.gameObject.SetActive(false);

            var hud = hudObject.AddComponent<WorkdayHud>();
            Assign(hud, "workday", workday);
            Assign(hud, "shiftLabel", shiftLabel);
            Assign(hud, "verdictPanel", panel.gameObject);
            Assign(hud, "verdictTitle", title);
            Assign(hud, "verdictMessage", message);
            Assign(hud, "restartButton", button);
            Assign(hud, "restartButtonLabel", buttonLabel);
            Assign(hud, "mainMenuButton", menuButton);
            Assign(hud, "wishListLabel", wishList);
            Assign(hud, "controlsHint", controlsHint);
        }

        /// <summary>Esc: Resume, Settings, Main Menu, Quit, over a dimmed screen.</summary>
        static void BuildPauseMenu(Transform canvas, GameObject hudObject, WorkdayManager workday)
        {
            var dim = new GameObject("Pause Menu").AddComponent<Image>();
            dim.transform.SetParent(canvas, false);
            dim.color = new Color(0.03f, 0.02f, 0.05f, 0.75f);
            Stretch(dim.rectTransform);

            RectTransform column = UiColumn(dim.transform, "Pause Column", new Vector2(620f, 640f), new Color(0.12f, 0.06f, 0.08f, 0.95f));
            UiLabel(column, "PAUSED", 64, new Color(1f, 0.82f, 0.2f), 90f);
            Text note = UiLabel(column, "", 24, new Color(1f, 1f, 1f, 0.7f), 34f);
            Button resume = UiButton(column, "Resume", new Color(0.2f, 0.62f, 0.3f));
            Button settingsButton = UiButton(column, "Settings");
            Button mainMenu = UiButton(column, "Main Menu");
            Button quit = UiButton(column, "Quit Game", new Color(0.55f, 0.16f, 0.16f));

            SettingsMenu settings = BuildSettingsPanel(canvas, dim.gameObject);

            var pause = hudObject.AddComponent<PauseMenu>();
            Assign(pause, "panel", dim.gameObject);
            Assign(pause, "settings", settings);
            Assign(pause, "workday", workday);
            Assign(pause, "noteLabel", note);
            Assign(pause, "resumeButton", resume);
            Assign(pause, "settingsButton", settingsButton);
            Assign(pause, "mainMenuButton", mainMenu);
            Assign(pause, "quitButton", quit);
            dim.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- Main menu scene

        static void BuildMainMenuScene(ToyPrefabs toys)
        {
            BackUpExistingScene(MainMenuPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // A small festive corner of the workshop behind the menu.
            var cam = new GameObject("Menu Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(1.2f, 1.5f, -3.4f), Quaternion.Euler(10f, -12f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.04f, 0.08f);
            cam.fieldOfView = 55f;
            cam.gameObject.AddComponent<AudioListener>();

            BuildLighting();
            var set = new GameObject("Backdrop").transform;
            Block("Floor", set, new Vector3(0f, -0.1f, 0f), new Vector3(10f, 0.2f, 8f), Mat("Floor Wood", new Color(0.55f, 0.36f, 0.22f)), keepCollider: true);
            Block("Wall", set, new Vector3(0f, 2f, 2.5f), new Vector3(10f, 4f, 0.3f), Mat("Wall Brick", new Color(0.55f, 0.18f, 0.16f)));
            Block("Trim", set, new Vector3(0f, 0.3f, 2.34f), new Vector3(10f, 0.6f, 0.05f), Mat("Wall Trim Green", new Color(0.12f, 0.38f, 0.22f)));

            var tree = new GameObject("Tree").transform;
            tree.SetParent(set, false);
            tree.localPosition = new Vector3(-2.6f, 0f, 1.4f);
            var pine = Mat("Tree Green", new Color(0.1f, 0.42f, 0.2f));
            Block("Trunk", tree, new Vector3(0f, 0.25f, 0f), new Vector3(0.25f, 0.5f, 0.25f), Mat("Crate Brown", new Color(0.45f, 0.3f, 0.16f)));
            Block("Tier 1", tree, new Vector3(0f, 0.8f, 0f), new Vector3(1.4f, 0.6f, 1.4f), pine, eulerAngles: new Vector3(0f, 45f, 0f));
            Block("Tier 2", tree, new Vector3(0f, 1.4f, 0f), new Vector3(1f, 0.55f, 1f), pine, eulerAngles: new Vector3(0f, 20f, 0f));
            Block("Tier 3", tree, new Vector3(0f, 1.9f, 0f), new Vector3(0.6f, 0.5f, 0.6f), pine, eulerAngles: new Vector3(0f, 60f, 0f));
            Block("Star", tree, new Vector3(0f, 2.3f, 0f), Vector3.one * 0.25f, Mat("Bulb", new Color(1f, 0.9f, 0.6f), emission: new Color(1f, 0.8f, 0.45f) * 2f), PrimitiveType.Sphere);

            var gold = Mat("Ribbon Gold", new Color(0.95f, 0.72f, 0.2f));
            (Vector3 pos, Vector3 size, Material paper)[] gifts =
            {
                (new Vector3(-1.7f, 0.2f, 0.6f), new Vector3(0.5f, 0.4f, 0.5f), Mat("Santa Red", new Color(0.78f, 0.08f, 0.1f))),
                (new Vector3(-1.2f, 0.15f, 0.2f), new Vector3(0.35f, 0.3f, 0.35f), Mat("Elf Green", new Color(0.15f, 0.55f, 0.25f))),
                (new Vector3(-2.0f, 0.12f, 0.0f), new Vector3(0.3f, 0.24f, 0.4f), Mat("Wash Blue", new Color(0.25f, 0.55f, 0.85f))),
            };
            foreach (var gift in gifts)
            {
                var box = Block("Gift", set, gift.pos, gift.size, gift.paper).transform;
                Block("Ribbon", box, Vector3.zero, new Vector3(1.02f, 1.02f, 0.18f), gold);
                Block("Ribbon", box, Vector3.zero, new Vector3(0.18f, 1.02f, 1.02f), gold);
            }

            // The three toys on display. Kinematic so they stay put; they're just props here.
            float x = 0.2f;
            foreach (ToyBody toy in toys.Toys)
            {
                var prop = (GameObject)PrefabUtility.InstantiatePrefab(toy.gameObject);
                prop.transform.SetPositionAndRotation(new Vector3(x, 0f, 0.4f), Quaternion.Euler(0f, 160f + x * 10f, 0f));
                prop.GetComponent<Rigidbody>().isKinematic = true;
                x += 0.75f;
            }

            // Menu UI.
            var canvasObject = new GameObject("Menu UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var title = HudText(canvasObject.transform, "Title", new Vector2(110f, -70f), 120, TextAnchor.UpperLeft, new Vector2(0f, 1f));
            title.text = "ELF & SAFETY";
            title.color = new Color(1f, 0.82f, 0.2f);
            title.rectTransform.sizeDelta = new Vector2(1200f, 150f);
            var tagline = HudText(canvasObject.transform, "Tagline", new Vector2(116f, -205f), 34, TextAnchor.UpperLeft, new Vector2(0f, 1f));
            tagline.text = "Santa's workshop. Safety first. Yours last.";
            tagline.rectTransform.sizeDelta = new Vector2(1200f, 60f);
            var version = HudText(canvasObject.transform, "Version", new Vector2(-24f, 20f), 20, TextAnchor.LowerRight, new Vector2(1f, 0f));
            version.text = $"Prototype  \u00b7  Unity {Application.unityVersion}";
            version.color = new Color(1f, 1f, 1f, 0.5f);

            RectTransform main = UiColumn(canvasObject.transform, "Main Panel", new Vector2(600f, 720f), new Color(0.1f, 0.05f, 0.07f, 0.88f), new Vector2(110f, -280f));
            Button playSolo = UiButton(main, "Play Solo", new Color(0.2f, 0.62f, 0.3f));
            Button host = UiButton(main, "Host Co-op");
            InputField address = UiInputRow(main, "Join address");
            Button join = UiButton(main, "Join Co-op");
            Button howTo = UiButton(main, "How to Play");
            Button settingsButton = UiButton(main, "Settings");
            Button quit = UiButton(main, "Quit", new Color(0.55f, 0.16f, 0.16f));
            Text message = UiLabel(main, "", 24, new Color(1f, 0.55f, 0.45f), 40f);

            RectTransform howToPanel = UiColumn(canvasObject.transform, "How To Play Panel", new Vector2(1100f, 760f), new Color(0.1f, 0.05f, 0.07f, 0.94f));
            UiLabel(howToPanel, "HOW TO PLAY", 56, new Color(1f, 0.82f, 0.2f), 80f);
            Text howToText = UiLabel(howToPanel,
                "Santa needs every present out before Christmas. Each day has a WISH LIST of toys.\n\n" +
                "1. Clock in at the green CLOCK IN button to start the day's shift.\n" +
                "2. Broken toys arrive on the delivery table. Salvage good parts at the SALVAGE bench,\n" +
                "    scrub dirty toys in the tub, and fit parts at the ASSEMBLY bench.\n" +
                "3. Ship finished toys at the SHIPPING chute. Each must match a wish exactly.\n" +
                "4. Watch for faulty parts: rusty keys, leaking batteries, wobbly wheels, loose eyes.\n" +
                "5. Fill every wish before time runs out, or meet the trapdoor.\n\n" +
                "WASD move  ·  Mouse look  ·  Shift sprint  ·  Space hop\n" +
                "E / Click interact  ·  Q drop  ·  Esc pause", 26, Color.white, 470f);
            howToText.alignment = TextAnchor.UpperLeft;
            Button howToBack = UiButton(howToPanel, "Back");

            SettingsMenu settings = BuildSettingsPanel(canvasObject.transform, main.gameObject);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var menu = canvasObject.AddComponent<MainMenu>();
            Assign(menu, "mainPanel", main.gameObject);
            Assign(menu, "howToPlayPanel", howToPanel.gameObject);
            Assign(menu, "settings", settings);
            Assign(menu, "joinAddress", address);
            Assign(menu, "messageLabel", message);
            Assign(menu, "playSoloButton", playSolo);
            Assign(menu, "hostButton", host);
            Assign(menu, "joinButton", join);
            Assign(menu, "howToPlayButton", howTo);
            Assign(menu, "settingsButton", settingsButton);
            Assign(menu, "quitButton", quit);
            Assign(menu, "howToPlayBackButton", howToBack);
            howToPanel.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        /// <summary>The settings panel shared by both menus. Back returns to <paramref name="returnTo"/>.</summary>
        static SettingsMenu BuildSettingsPanel(Transform canvas, GameObject returnTo)
        {
            RectTransform panel = UiColumn(canvas, "Settings Panel", new Vector2(980f, 960f), new Color(0.1f, 0.05f, 0.07f, 0.96f));
            UiLabel(panel, "SETTINGS", 56, new Color(1f, 0.82f, 0.2f), 76f);
            var (sensitivity, sensitivityValue) = UiSliderRow(panel, "Mouse sensitivity");
            Toggle invert = UiToggleRow(panel, "Invert mouse Y");
            var (fov, fovValue) = UiSliderRow(panel, "Field of view");
            var (volume, volumeValue) = UiSliderRow(panel, "Master volume");
            Toggle fullscreen = UiToggleRow(panel, "Fullscreen");
            Dropdown resolution = UiDropdownRow(panel, "Resolution");
            Dropdown quality = UiDropdownRow(panel, "Graphics quality");
            Toggle vSync = UiToggleRow(panel, "VSync");
            Toggle showControls = UiToggleRow(panel, "Show controls hint");
            Toggle showWishList = UiToggleRow(panel, "Show wish list on screen");

            RectTransform buttons = UiRow(panel, "Buttons", 80f);
            Button reset = UiButton(buttons, "Reset to defaults", null, 360f);
            Button back = UiButton(buttons, "Back", new Color(0.2f, 0.62f, 0.3f), 360f);

            var settings = panel.gameObject.AddComponent<SettingsMenu>();
            Assign(settings, "sensitivity", sensitivity);
            Assign(settings, "sensitivityValue", sensitivityValue);
            Assign(settings, "invertY", invert);
            Assign(settings, "fieldOfView", fov);
            Assign(settings, "fieldOfViewValue", fovValue);
            Assign(settings, "volume", volume);
            Assign(settings, "volumeValue", volumeValue);
            Assign(settings, "fullscreen", fullscreen);
            Assign(settings, "resolution", resolution);
            Assign(settings, "quality", quality);
            Assign(settings, "vSync", vSync);
            Assign(settings, "showControls", showControls);
            Assign(settings, "showWishList", showWishList);
            Assign(settings, "resetButton", reset);
            Assign(settings, "backButton", back);
            Assign(settings, "returnTo", returnTo);
            panel.gameObject.SetActive(false);
            return settings;
        }

        // ---------------------------------------------------------------- UI kit (built on uGUI's DefaultControls)

        static DefaultControls.Resources uiResources;

        static DefaultControls.Resources UiResources()
        {
            if (uiResources.standard == null)
            {
                uiResources = new DefaultControls.Resources
                {
                    standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                    background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                    inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                    knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                    checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                    dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                    mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
                };
            }
            return uiResources;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>A panel that stacks its children top to bottom. Centred, or top-left at <paramref name="topLeft"/>.</summary>
        static RectTransform UiColumn(Transform parent, string name, Vector2 size, Color color, Vector2? topLeft = null)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            RectTransform rect = image.rectTransform;
            rect.sizeDelta = size;
            if (topLeft.HasValue)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = topLeft.Value;
            }

            var layout = image.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 32, 32);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        static RectTransform UiRow(Transform parent, string name, float height)
        {
            var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        static Text UiLabel(Transform parent, string text, int size, Color color, float height, float width = -1f)
        {
            var label = new GameObject("Label").AddComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = labelFont;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            if (width > 0f) element.preferredWidth = width;
            return label;
        }

        static void UseLabelFont(GameObject control, int size)
        {
            foreach (Text text in control.GetComponentsInChildren<Text>(true))
            {
                text.font = labelFont;
                text.fontSize = size;
            }
        }

        static Button UiButton(Transform parent, string label, Color? color = null, float width = -1f)
        {
            GameObject go = DefaultControls.CreateButton(UiResources());
            go.name = $"{label} Button";
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color ?? new Color(0.28f, 0.2f, 0.24f);
            UseLabelFont(go, 32);
            Text text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.color = Color.white;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 70f;
            if (width > 0f) element.preferredWidth = width;
            return go.GetComponent<Button>();
        }

        static (Slider slider, Text value) UiSliderRow(Transform parent, string label)
        {
            RectTransform row = UiRow(parent, label, 50f);
            UiLabel(row, label, 28, Color.white, 50f, 360f).alignment = TextAnchor.MiddleLeft;
            GameObject go = DefaultControls.CreateSlider(UiResources());
            go.name = $"{label} Slider";
            go.transform.SetParent(row, false);
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 380f;
            element.preferredHeight = 30f;
            Text value = UiLabel(row, "", 28, new Color(1f, 0.9f, 0.6f), 50f, 110f);
            return (go.GetComponent<Slider>(), value);
        }

        static Toggle UiToggleRow(Transform parent, string label)
        {
            GameObject go = DefaultControls.CreateToggle(UiResources());
            go.name = $"{label} Toggle";
            go.transform.SetParent(parent, false);
            UseLabelFont(go, 28);
            Text text = go.GetComponentInChildren<Text>();
            text.text = label;
            text.color = Color.white;
            // Make the box and label readable at menu size.
            Transform box = go.transform.Find("Background");
            if (box != null) ((RectTransform)box).sizeDelta = new Vector2(34f, 34f);
            ((RectTransform)text.transform).offsetMin = new Vector2(50f, 0f);
            go.AddComponent<LayoutElement>().preferredHeight = 44f;
            return go.GetComponent<Toggle>();
        }

        static Dropdown UiDropdownRow(Transform parent, string label)
        {
            RectTransform row = UiRow(parent, label, 50f);
            UiLabel(row, label, 28, Color.white, 50f, 360f).alignment = TextAnchor.MiddleLeft;
            GameObject go = DefaultControls.CreateDropdown(UiResources());
            go.name = $"{label} Dropdown";
            go.transform.SetParent(row, false);
            UseLabelFont(go, 24);
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 500f;
            element.preferredHeight = 46f;
            return go.GetComponent<Dropdown>();
        }

        static InputField UiInputRow(Transform parent, string label)
        {
            RectTransform row = UiRow(parent, label, 56f);
            UiLabel(row, label, 26, Color.white, 56f, 200f).alignment = TextAnchor.MiddleLeft;
            GameObject go = DefaultControls.CreateInputField(UiResources());
            go.name = $"{label} Input";
            go.transform.SetParent(row, false);
            UseLabelFont(go, 28);
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 300f;
            element.preferredHeight = 52f;
            return go.GetComponent<InputField>();
        }

        static Text HudText(Transform parent, string name, Vector2 position, int size, TextAnchor anchor, Vector2 pivot)
        {
            var text = new GameObject(name).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = labelFont;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = pivot;
            text.rectTransform.anchorMax = pivot;
            text.rectTransform.pivot = pivot;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = new Vector2(900f, 80f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        // ---------------------------------------------------------------- Co-op (milestone 4)

        /// <summary>
        /// Adds a NetworkObject and the sync components to the toy prefabs if they're missing, keeping
        /// everything else (including swapped-in art). Netcode's automatic parenting is switched off:
        /// installed parts hang under mount points, and solo play has no network session at all.
        /// </summary>
        static void AddNetworkComponentsToToys()
        {
            foreach (string toy in ToyPrefabNames)
            {
                EnsurePrefabComponents($"{PrefabFolder}/{toy}.prefab", disableParentSync: true, typeof(NetworkCarryable), typeof(NetworkToy));
            }
            foreach (string part in PartPrefabNames)
            {
                EnsurePrefabComponents($"{PrefabFolder}/{part}.prefab", disableParentSync: true, typeof(NetworkCarryable));
            }
        }

        static void EnsurePrefabComponents(string path, bool disableParentSync, params Type[] componentTypes)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            var networkObject = root.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = root.AddComponent<NetworkObject>();
                changed = true;
            }
            if (disableParentSync && networkObject.AutoObjectParentSync)
            {
                networkObject.AutoObjectParentSync = false;
                changed = true;
            }

            foreach (Type type in componentTypes)
            {
                if (root.GetComponent(type) == null)
                {
                    root.AddComponent(type);
                    changed = true;
                }
            }

            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>
        /// "Elf Player (Net)": a variant of the solo elf with a NetworkObject, an owner-authoritative
        /// NetworkTransform (position and yaw) and NetworkPlayer. Art changes to the solo elf carry over.
        /// </summary>
        static GameObject BuildNetworkPlayerPrefab()
        {
            string path = $"{PrefabFolder}/Elf Player (Net).prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var solo = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Elf Player.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(solo);
                instance.name = "Elf Player (Net)";
                instance.AddComponent<NetworkObject>();
                var networkTransform = instance.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                networkTransform.SyncRotAngleX = false;
                networkTransform.SyncRotAngleZ = false;
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;
                instance.AddComponent<NetworkPlayer>();
                prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
                return prefab;
            }

            EnsurePrefabComponents(path, disableParentSync: false, typeof(NetworkTransform), typeof(NetworkPlayer));
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>"Workshop Sync": the host spawns one per session to share the shift and station state.</summary>
        static GameObject BuildWorkshopSyncPrefab()
        {
            string path = $"{PrefabFolder}/Workshop Sync.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var temp = new GameObject("Workshop Sync");
                temp.AddComponent<NetworkObject>();
                temp.AddComponent<NetworkWorkshop>();
                PrefabUtility.SaveAsPrefabAsset(temp, path);
                Object.DestroyImmediate(temp);
            }
            else
            {
                EnsurePrefabComponents(path, disableParentSync: false, typeof(NetworkWorkshop));
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static NetworkPrefabsList BuildNetworkPrefabsList(ToyPrefabs toys, GameObject netPlayer, GameObject workshopSync)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, NetworkPrefabsPath);
            }

            // Usually Netcode has already filled this in; make sure, in case its import step hasn't run yet.
            foreach (GameObject prefab in new[] { netPlayer, workshopSync }.Concat(toys.All))
            {
                if (!list.PrefabList.Any(entry => entry.Prefab == prefab))
                {
                    list.Add(new NetworkPrefab { Prefab = prefab });
                }
            }

            EditorUtility.SetDirty(list);
            return list;
        }

        /// <summary>
        /// The NetworkManager lives in a prefab, not the scene: Netcode moves it to DontDestroyOnLoad, so a
        /// scene copy would pile up on every restart. NetworkSession instantiates it when hosting or joining.
        /// </summary>
        static NetworkManager BuildNetworkManagerPrefab(GameObject netPlayer, NetworkPrefabsList prefabs)
        {
            string path = $"{PrefabFolder}/Network Manager.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Network Manager");

            // Explicit checks: in the editor a missing component is a "fake null" that ?? doesn't catch.
            var transport = root.GetComponent<UnityTransport>();
            if (transport == null) transport = root.AddComponent<UnityTransport>();
            var manager = root.GetComponent<NetworkManager>();
            if (manager == null) manager = root.AddComponent<NetworkManager>();
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = netPlayer;
            // Each machine already has the same Workshop loaded; nothing in it is networked.
            manager.NetworkConfig.EnableSceneManagement = false;
            // Exactly one list: a prefab registered by two lists is a duplicate-hash error at startup.
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabs);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
            return saved.GetComponent<NetworkManager>();
        }

        static void BuildNetworkSession(NetworkManager managerPrefab, GameObject workshopSync, PlayerInteractor soloPlayer, DeliveryChute delivery, WorkdayManager workday, InteractionHud hud)
        {
            var status = HudText(hud.transform, "Co-op Status", new Vector2(24f, 24f), 24, TextAnchor.LowerLeft, new Vector2(0f, 0f));
            status.color = new Color(0.7f, 0.9f, 1f, 0.85f);

            var session = new GameObject("Network Session").AddComponent<NetworkSession>();
            Assign(session, "networkManagerPrefab", managerPrefab);
            Assign(session, "workshopSyncPrefab", workshopSync.GetComponent<NetworkObject>());
            Assign(session, "soloPlayer", soloPlayer);
            Assign(session, "delivery", delivery);
            Assign(session, "workday", workday);
            Assign(session, "hud", hud);
            Assign(session, "statusLabel", status);
        }

        // ---------------------------------------------------------------- Toys

        static ToyPrefabs BuildToyPrefabs()
        {
            var builders = new Dictionary<string, Func<GameObject>>
            {
                ["Robot Body"] = BuildRobotBody,
                ["Teddy Bear"] = BuildTeddyBear,
                ["Toy Car"] = BuildToyCar,
                ["Battery"] = () => BuildBattery("Battery", true),
                ["Dead Battery"] = () => BuildBattery("Dead Battery", false),
                ["Winding Key"] = BuildWindingKey,
                ["Stuffing"] = BuildStuffing,
                ["Button Eyes"] = BuildButtonEyes,
                ["Wheel Set"] = BuildWheelSet,
                ["Leaky Battery"] = () => Faulty(() => BuildBattery("Battery", true), PartFault.Leaky, visual =>
                {
                    var goo = Mat("Battery Goo Green", new Color(0.45f, 1f, 0.2f), emission: new Color(0.2f, 0.6f, 0.05f));
                    Block("Goo", visual, new Vector3(0.055f, 0.06f, 0.03f), new Vector3(0.03f, 0.05f, 0.03f), goo, PrimitiveType.Sphere);
                    Block("Drip", visual, new Vector3(0.06f, 0.025f, 0.035f), Vector3.one * 0.018f, goo, PrimitiveType.Sphere);
                }),
                ["Overwound Winding Key"] = () => Faulty(BuildWindingKey, PartFault.Overwound, visual =>
                {
                    var rust = Mat("Rust Brown", new Color(0.55f, 0.25f, 0.1f));
                    Block("Rust", visual, new Vector3(0f, 0.1f, 0f), new Vector3(0.046f, 0.05f, 0.046f), rust);
                    Block("Rust Wing", visual, new Vector3(0f, 0.21f, 0.08f), new Vector3(0.032f, 0.04f, 0.04f), rust);
                }),
                ["Loose Button Eyes"] = () => Faulty(BuildButtonEyes, PartFault.LooseThreads, visual =>
                {
                    Transform eye = visual.Find("Eye Right");
                    if (eye != null) eye.localPosition += new Vector3(0.01f, -0.02f, 0f);
                    Block("Dangling Thread", visual, new Vector3(0.06f, -0.01f, 0f), new Vector3(0.004f, 0.06f, 0.004f), Mat("Stuffing White", Color.white));
                }),
                ["Wobbly Wheel Set"] = () => Faulty(BuildWheelSet, PartFault.Wobbly, visual =>
                {
                    Transform wheel = visual.Find("Wheel Right");
                    if (wheel != null) wheel.localEulerAngles = new Vector3(25f, 0f, 90f);
                }),
            };

            return new ToyPrefabs
            {
                Toys = ToyPrefabNames.Select(name => ToyPrefab(name, builders[name]).GetComponent<ToyBody>()).ToArray(),
                Parts = PartPrefabNames.Select(name => ToyPrefab(name, builders[name]).GetComponent<ToyPart>()).ToArray(),
            };
        }

        /// <summary>Loads the prefab, or builds and saves it if missing, so replaced visuals are kept.</summary>
        static GameObject ToyPrefab(string name, Func<GameObject> build)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                return prefab;
            }

            var temp = build();
            temp.name = name;
            SetLayerRecursive(temp, GameLayers.Carryable);
            prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        /// <summary>
        /// The milestone 2 robot prefab stored its battery and key mounts in fields that became the general slot
        /// list. Fill the slots in from the mounts it already has, keeping its art.
        /// </summary>
        static void UpgradeLegacyRobotPrefab()
        {
            string path = $"{PrefabFolder}/Robot Body.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            var toy = root.GetComponent<ToyBody>();
            Transform batteryMount = root.transform.Find("Battery Mount");
            Transform keyMount = root.transform.Find("Key Mount");

            if (toy != null && toy.SlotCount == 0 && batteryMount != null && keyMount != null)
            {
                ConfigureSlots(toy, ToyType.WindUpRobot, null, ("Battery", PartKind.Battery, batteryMount), ("Winding Key", PartKind.WindingKey, keyMount));
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("Elf & Safety: upgraded Robot Body.prefab to the general toy slots");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Sets a toy's type and slots, and points each mount's aim target at its slot.</summary>
        static void ConfigureSlots(ToyBody toy, ToyType type, GameObject dirtVisual, params (string name, PartKind kind, Transform mount)[] slots)
        {
            var so = new SerializedObject(toy);
            FindProperty(so, "type").intValue = (int)type;
            if (dirtVisual != null) FindProperty(so, "dirtVisual").objectReferenceValue = dirtVisual;

            SerializedProperty list = FindProperty(so, "slots");
            list.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty slot = list.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("name").stringValue = slots[i].name;
                slot.FindPropertyRelative("accepts").intValue = (int)slots[i].kind;
                slot.FindPropertyRelative("mount").objectReferenceValue = slots[i].mount;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            for (int i = 0; i < slots.Length; i++)
            {
                var target = slots[i].mount.GetComponent<ToySlotTarget>();
                if (target == null) target = slots[i].mount.gameObject.AddComponent<ToySlotTarget>();
                AssignEnum(target, "slotIndex", i);
            }
        }

        static void DeleteLegacyBatchAsset()
        {
            if (File.Exists(LegacyBatchPath))
            {
                AssetDatabase.DeleteAsset(LegacyBatchPath);
                Debug.Log($"Elf & Safety: removed {LegacyBatchPath}; deliveries now come from the Christmas Countdown waves");
            }
        }

        static GameObject Dirt(Transform visual, params (Vector3 position, Vector3 size)[] smudges)
        {
            var dirt = new GameObject("Dirt").transform;
            dirt.SetParent(visual, false);
            var grime = Mat("Grime Brown", new Color(0.33f, 0.24f, 0.12f));
            int i = 0;
            foreach (var smudge in smudges)
            {
                Block($"Smudge {++i}", dirt, smudge.position, smudge.size, grime);
            }
            return dirt.gameObject;
        }

        /// <summary>
        /// Robot body facing +Z with its pivot at the feet. Battery mounts on the chest, key on the right side.
        /// Mount objects carry the aim triggers; installed parts are parented to them.
        /// </summary>
        static GameObject BuildRobotBody()
        {
            var root = CarryableRoot("Wind-up Robot", 1.5f, new Vector3(0.42f, 0.62f, 0.32f), new Vector3(0f, -0.3f, 0f));
            var visual = root.transform.Find("Visual");
            var tin = Mat("Robot Tin Red", new Color(0.85f, 0.2f, 0.18f));
            var dark = Mat("Metal Dark", Color.black);
            var bulb = Mat("Bulb", Color.yellow);
            Block("Torso", visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.36f, 0.32f, 0.28f), tin);
            Block("Head", visual, new Vector3(0f, 0.53f, 0f), new Vector3(0.26f, 0.18f, 0.22f), Mat("Metal Grey", Color.grey));
            Block("Eye Left", visual, new Vector3(-0.06f, 0.55f, 0.115f), Vector3.one * 0.05f, bulb, PrimitiveType.Sphere);
            Block("Eye Right", visual, new Vector3(0.06f, 0.55f, 0.115f), Vector3.one * 0.05f, bulb, PrimitiveType.Sphere);
            Block("Leg Left", visual, new Vector3(-0.09f, 0.07f, 0f), new Vector3(0.1f, 0.14f, 0.14f), dark);
            Block("Leg Right", visual, new Vector3(0.09f, 0.07f, 0f), new Vector3(0.1f, 0.14f, 0.14f), dark);
            Block("Battery Hatch", visual, new Vector3(0f, 0.31f, 0.141f), new Vector3(0.16f, 0.22f, 0.02f), dark);
            Block("Key Hole", visual, new Vector3(0.181f, 0.36f, 0f), new Vector3(0.02f, 0.07f, 0.07f), dark);
            GameObject dirt = Dirt(visual,
                (new Vector3(-0.12f, 0.38f, 0.145f), new Vector3(0.1f, 0.12f, 0.02f)),
                (new Vector3(0.12f, 0.2f, 0.145f), new Vector3(0.09f, 0.08f, 0.02f)),
                (new Vector3(-0.185f, 0.3f, 0.03f), new Vector3(0.02f, 0.14f, 0.16f)),
                (new Vector3(0.03f, 0.625f, 0f), new Vector3(0.14f, 0.03f, 0.14f)));

            var batteryMount = Mount(root.transform, "Battery Mount", new Vector3(0f, 0.2f, 0.2f), Quaternion.identity, new Vector3(0.18f, 0.26f, 0.14f));
            var keyMount = Mount(root.transform, "Key Mount", new Vector3(0.19f, 0.36f, 0f), Quaternion.Euler(0f, 0f, -90f), new Vector3(0.12f, 0.26f, 0.24f));

            var toy = root.AddComponent<ToyBody>();
            AssignBool(toy, "isDirty", true);
            ConfigureSlots(toy, ToyType.WindUpRobot, dirt, ("Battery", PartKind.Battery, batteryMount), ("Winding Key", PartKind.WindingKey, keyMount));
            return root;
        }

        /// <summary>
        /// Teddy bear facing +Z, pivot at the feet. Stuffing goes in the belly seam, button eyes on the face.
        /// The battery and key slots on its back are only filled for a cyborg bear.
        /// </summary>
        static GameObject BuildTeddyBear()
        {
            var root = CarryableRoot("Teddy Bear", 0.8f, new Vector3(0.4f, 0.64f, 0.34f), new Vector3(0f, -0.3f, 0f));
            var visual = root.transform.Find("Visual");
            var fur = Mat("Bear Fur Brown", new Color(0.62f, 0.4f, 0.22f));
            var muzzle = Mat("Bear Muzzle Tan", new Color(0.88f, 0.72f, 0.52f));
            var dark = Mat("Metal Dark", Color.black);
            Block("Body", visual, new Vector3(0f, 0.24f, 0f), new Vector3(0.36f, 0.4f, 0.3f), fur, PrimitiveType.Sphere);
            Block("Head", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.28f, 0.26f, 0.26f), fur, PrimitiveType.Sphere);
            Block("Ear Left", visual, new Vector3(-0.11f, 0.62f, 0f), Vector3.one * 0.09f, fur, PrimitiveType.Sphere);
            Block("Ear Right", visual, new Vector3(0.11f, 0.62f, 0f), Vector3.one * 0.09f, fur, PrimitiveType.Sphere);
            Block("Muzzle", visual, new Vector3(0f, 0.46f, 0.12f), new Vector3(0.12f, 0.09f, 0.06f), muzzle, PrimitiveType.Sphere);
            Block("Nose", visual, new Vector3(0f, 0.48f, 0.15f), Vector3.one * 0.04f, dark, PrimitiveType.Sphere);
            Block("Arm Left", visual, new Vector3(-0.2f, 0.28f, 0.04f), new Vector3(0.1f, 0.18f, 0.1f), fur, PrimitiveType.Sphere);
            Block("Arm Right", visual, new Vector3(0.2f, 0.28f, 0.04f), new Vector3(0.1f, 0.18f, 0.1f), fur, PrimitiveType.Sphere);
            Block("Leg Left", visual, new Vector3(-0.1f, 0.06f, 0.06f), new Vector3(0.12f, 0.12f, 0.14f), fur, PrimitiveType.Sphere);
            Block("Leg Right", visual, new Vector3(0.1f, 0.06f, 0.06f), new Vector3(0.12f, 0.12f, 0.14f), fur, PrimitiveType.Sphere);
            Block("Belly Seam", visual, new Vector3(0f, 0.22f, 0.15f), new Vector3(0.12f, 0.14f, 0.01f), dark);
            GameObject dirt = Dirt(visual,
                (new Vector3(-0.1f, 0.3f, 0.14f), new Vector3(0.1f, 0.1f, 0.02f)),
                (new Vector3(0.09f, 0.14f, 0.13f), new Vector3(0.09f, 0.07f, 0.02f)),
                (new Vector3(0.06f, 0.56f, 0.1f), new Vector3(0.08f, 0.05f, 0.02f)));

            var stuffing = Mount(root.transform, "Stuffing Mount", new Vector3(0f, 0.16f, 0.16f), Quaternion.identity, new Vector3(0.18f, 0.18f, 0.12f));
            var eyes = Mount(root.transform, "Eyes Mount", new Vector3(0f, 0.5f, 0.13f), Quaternion.identity, new Vector3(0.2f, 0.1f, 0.08f));
            var battery = Mount(root.transform, "Battery Mount", new Vector3(0f, 0.2f, -0.17f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.18f, 0.26f, 0.14f));
            var key = Mount(root.transform, "Key Mount", new Vector3(0.2f, 0.34f, -0.06f), Quaternion.Euler(0f, 0f, -90f), new Vector3(0.12f, 0.26f, 0.24f));

            var toy = root.AddComponent<ToyBody>();
            AssignBool(toy, "isDirty", true);
            ConfigureSlots(toy, ToyType.TeddyBear, dirt,
                ("Stuffing", PartKind.Stuffing, stuffing), ("Button Eyes", PartKind.ButtonEyes, eyes),
                ("Battery", PartKind.Battery, battery), ("Winding Key", PartKind.WindingKey, key));
            return root;
        }

        /// <summary>Toy car facing +Z, pivot at the bottom: a battery on the rear deck and two wheel sets.</summary>
        static GameObject BuildToyCar()
        {
            var root = CarryableRoot("Toy Car", 1f, new Vector3(0.34f, 0.3f, 0.54f), new Vector3(0f, -0.15f, 0f));
            var visual = root.transform.Find("Visual");
            var paint = Mat("Car Paint Blue", new Color(0.2f, 0.45f, 0.9f));
            var glass = Mat("Car Window", new Color(0.7f, 0.9f, 1f));
            Block("Chassis", visual, new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.12f, 0.5f), paint);
            Block("Cabin", visual, new Vector3(0f, 0.22f, -0.03f), new Vector3(0.26f, 0.1f, 0.26f), paint);
            Block("Windshield", visual, new Vector3(0f, 0.22f, 0.105f), new Vector3(0.24f, 0.08f, 0.01f), glass);
            Block("Headlight Left", visual, new Vector3(-0.09f, 0.13f, 0.25f), Vector3.one * 0.04f, Mat("Bulb", Color.yellow), PrimitiveType.Sphere);
            Block("Headlight Right", visual, new Vector3(0.09f, 0.13f, 0.25f), Vector3.one * 0.04f, Mat("Bulb", Color.yellow), PrimitiveType.Sphere);
            GameObject dirt = Dirt(visual,
                (new Vector3(-0.151f, 0.12f, 0.08f), new Vector3(0.01f, 0.08f, 0.14f)),
                (new Vector3(0.151f, 0.14f, -0.1f), new Vector3(0.01f, 0.06f, 0.12f)),
                (new Vector3(0.05f, 0.271f, -0.05f), new Vector3(0.1f, 0.01f, 0.1f)));

            var battery = Mount(root.transform, "Battery Mount", new Vector3(0f, 0.18f, -0.2f), Quaternion.identity, new Vector3(0.18f, 0.26f, 0.14f));
            var front = Mount(root.transform, "Front Wheels Mount", new Vector3(0f, 0f, 0.16f), Quaternion.identity, new Vector3(0.42f, 0.12f, 0.14f));
            var rear = Mount(root.transform, "Rear Wheels Mount", new Vector3(0f, 0f, -0.16f), Quaternion.identity, new Vector3(0.42f, 0.12f, 0.14f));

            var toy = root.AddComponent<ToyBody>();
            AssignBool(toy, "isDirty", true);
            ConfigureSlots(toy, ToyType.ToyCar, dirt,
                ("Battery", PartKind.Battery, battery), ("Front Wheels", PartKind.WheelSet, front), ("Rear Wheels", PartKind.WheelSet, rear));
            return root;
        }

        static Transform Mount(Transform toy, string name, Vector3 position, Quaternion rotation, Vector3 triggerSize)
        {
            var mount = new GameObject(name).transform;
            mount.SetParent(toy, false);
            mount.localPosition = position;
            mount.localRotation = rotation;
            var trigger = mount.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;
            trigger.center = new Vector3(0f, triggerSize.y * 0.5f - 0.02f, 0f);
            mount.gameObject.AddComponent<ToySlotTarget>();
            return mount;
        }

        static GameObject BuildBattery(string displayName, bool working)
        {
            var root = CarryableRoot(displayName, 0.3f, new Vector3(0.12f, 0.2f, 0.12f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var cell = working ? Mat("Battery Yellow", new Color(0.95f, 0.8f, 0.15f)) : Mat("Dead Battery Grey", new Color(0.25f, 0.25f, 0.27f));
            var band = working ? Mat("Battery OK Green", new Color(0.3f, 0.9f, 0.35f)) : Mat("Dead Battery Red", new Color(0.85f, 0.1f, 0.1f));
            Block("Cell", visual, new Vector3(0f, 0.09f, 0f), new Vector3(0.12f, 0.09f, 0.12f), cell, PrimitiveType.Cylinder);
            Block("Band", visual, new Vector3(0f, 0.12f, 0f), new Vector3(0.126f, 0.02f, 0.126f), band, PrimitiveType.Cylinder);
            Block("Terminal", visual, new Vector3(0f, 0.19f, 0f), new Vector3(0.05f, 0.015f, 0.05f), working ? Mat("Copper", new Color(0.75f, 0.45f, 0.2f)) : Mat("Metal Dark", Color.black), PrimitiveType.Cylinder);
            return Part(root, PartKind.Battery, working);
        }

        static GameObject BuildWindingKey()
        {
            var root = CarryableRoot("Winding Key", 0.2f, new Vector3(0.06f, 0.24f, 0.2f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var brass = Mat("Ribbon Gold", Color.yellow);
            Block("Shaft", visual, new Vector3(0f, 0.07f, 0f), new Vector3(0.04f, 0.14f, 0.04f), brass);
            Block("Wing Left", visual, new Vector3(0f, 0.18f, -0.06f), new Vector3(0.03f, 0.1f, 0.1f), brass);
            Block("Wing Right", visual, new Vector3(0f, 0.18f, 0.06f), new Vector3(0.03f, 0.1f, 0.1f), brass);
            return Part(root, PartKind.WindingKey, true);
        }

        static GameObject BuildStuffing()
        {
            var root = CarryableRoot("Stuffing", 0.1f, new Vector3(0.14f, 0.14f, 0.14f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var fluff = Mat("Stuffing White", new Color(0.96f, 0.95f, 0.9f));
            Block("Puff", visual, new Vector3(0f, 0.07f, 0f), new Vector3(0.14f, 0.12f, 0.14f), fluff, PrimitiveType.Sphere);
            Block("Puff Top", visual, new Vector3(0.03f, 0.11f, 0.02f), Vector3.one * 0.08f, fluff, PrimitiveType.Sphere);
            return Part(root, PartKind.Stuffing, true);
        }

        static GameObject BuildButtonEyes()
        {
            var root = CarryableRoot("Button Eyes", 0.05f, new Vector3(0.16f, 0.06f, 0.04f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var button = Mat("Button Black", new Color(0.05f, 0.05f, 0.06f));
            Block("Eye Left", visual, new Vector3(-0.05f, 0.03f, 0f), new Vector3(0.05f, 0.05f, 0.03f), button, PrimitiveType.Sphere);
            Block("Eye Right", visual, new Vector3(0.05f, 0.03f, 0f), new Vector3(0.05f, 0.05f, 0.03f), button, PrimitiveType.Sphere);
            Block("Thread", visual, new Vector3(0f, 0.03f, -0.005f), new Vector3(0.1f, 0.005f, 0.005f), Mat("Stuffing White", Color.white));
            return Part(root, PartKind.ButtonEyes, true);
        }

        static GameObject BuildWheelSet()
        {
            var root = CarryableRoot("Wheel Set", 0.25f, new Vector3(0.38f, 0.1f, 0.1f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var rubber = Mat("Tyre Black", new Color(0.08f, 0.08f, 0.08f));
            Block("Axle", visual, new Vector3(0f, 0.05f, 0f), new Vector3(0.02f, 0.17f, 0.02f), Mat("Metal Grey", Color.grey), PrimitiveType.Cylinder, eulerAngles: new Vector3(0f, 0f, 90f));
            Block("Wheel Left", visual, new Vector3(-0.17f, 0.05f, 0f), new Vector3(0.1f, 0.02f, 0.1f), rubber, PrimitiveType.Cylinder, eulerAngles: new Vector3(0f, 0f, 90f));
            Block("Wheel Right", visual, new Vector3(0.17f, 0.05f, 0f), new Vector3(0.1f, 0.02f, 0.1f), rubber, PrimitiveType.Cylinder, eulerAngles: new Vector3(0f, 0f, 90f));
            return Part(root, PartKind.WheelSet, true);
        }

        /// <summary>A part with a hidden defect: the normal part, a small visual tell, and its malfunction.</summary>
        static GameObject Faulty(Func<GameObject> buildNormal, PartFault fault, Action<Transform> addTell)
        {
            GameObject root = buildNormal();
            AssignEnum(root.GetComponent<ToyPart>(), "fault", (int)fault);
            addTell(root.transform.Find("Visual"));
            root.AddComponent<PartMalfunction>();
            return root;
        }

        static GameObject Part(GameObject root, PartKind kind, bool working)
        {
            var part = root.AddComponent<ToyPart>();
            AssignEnum(part, "kind", (int)kind);
            AssignBool(part, "isWorking", working);
            return root;
        }

        /// <summary>Gameplay root: rigidbody, one box collider with its base at the pivot, and an empty Visual child.</summary>
        static GameObject CarryableRoot(string displayName, float mass, Vector3 size, Vector3 holdOffset)
        {
            var root = new GameObject(displayName);
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var box = root.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);

            var carryable = root.AddComponent<Carryable>();
            AssignString(carryable, "displayName", displayName);
            AssignVector(carryable, "holdOffset", holdOffset);

            new GameObject("Visual").transform.SetParent(root.transform, false);
            return root;
        }

        /// <summary>The seven days; created with the default tuning only if missing, so tuned values are kept.</summary>
        static ChristmasCountdown LoadOrCreateCountdown()
        {
            var countdown = AssetDatabase.LoadAssetAtPath<ChristmasCountdown>(CountdownPath);
            if (countdown == null)
            {
                countdown = ScriptableObject.CreateInstance<ChristmasCountdown>();
                AssetDatabase.CreateAsset(countdown, CountdownPath);
            }
            else if (countdown.days.Any(day => day.orders.Count == 0 || day.waves.Count == 0))
            {
                // Saved before days had wish orders and typed waves: those fields are empty, so start from the defaults.
                countdown.days = ChristmasCountdown.DefaultWeek();
                EditorUtility.SetDirty(countdown);
                Debug.Log($"Elf & Safety: {CountdownPath} had no wish orders yet; reset it to the default week");
            }
            return countdown;
        }

        // ---------------------------------------------------------------- Station helpers

        /// <summary>A station root with a solid base block and a Visual child for decoration.</summary>
        static Transform Station(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 baseSize, Material baseMaterial)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(position, rotation);
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = baseSize;
            box.center = new Vector3(0f, baseSize.y * 0.5f, 0f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            Block("Base", visual, box.center, baseSize, baseMaterial);
            return root;
        }

        /// <summary>
        /// A station work area: trigger box for aiming and for finding the robot on it, a visible pad,
        /// and a place point facing the room. Nothing is locked here; items stay loose physics objects.
        /// </summary>
        static T WorkArea<T>(Transform stationRoot, string displayName, Vector3 surfacePoint, Vector3 areaSize, Material padMaterial, Vector2 padSize) where T : WorkStation
        {
            var areaObject = new GameObject("Work Area").transform;
            areaObject.SetParent(stationRoot, false);
            areaObject.localPosition = surfacePoint;

            // Starts a little below the surface so a robot whose feet rest on it is inside.
            var area = areaObject.gameObject.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.size = areaSize;
            area.center = new Vector3(0f, areaSize.y * 0.5f - 0.05f, 0f);

            var placePoint = Point(areaObject, "Place Point", new Vector3(0f, 0.02f, 0f));
            Block("Placement Pad", areaObject, new Vector3(0f, 0.005f, 0f), new Vector3(padSize.x, 0.01f, padSize.y), padMaterial);

            var station = areaObject.gameObject.AddComponent<T>();
            AssignString(station, "displayName", displayName);
            Assign(station, "placePoint", placePoint);
            return station;
        }

        /// <summary>An empty marker facing the room, used for spawn, output, drop and place positions.</summary>
        static Transform Point(Transform parent, string name, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            point.localRotation = FaceRoom;
            return point;
        }

        /// <summary>A low solid edge that keeps loose items from rolling off a surface.</summary>
        static void Lip(Transform visual, Vector3 localPosition, Vector3 size, Material material)
        {
            Block("Lip", visual, localPosition, size, material, keepCollider: true);
        }

        static void Button(Transform stationRoot, string name, Vector3 localPosition, Material material, DeliveryChute chute, DeliveryButton.ButtonAction action)
        {
            var button = new GameObject(name);
            button.transform.SetParent(stationRoot, false);
            button.transform.localPosition = localPosition;
            var box = button.AddComponent<BoxCollider>();
            box.size = new Vector3(0.3f, 0.1f, 0.25f);
            Block("Visual", button.transform, Vector3.zero, new Vector3(0.3f, 0.1f, 0.25f), material);

            var component = button.AddComponent<DeliveryButton>();
            Assign(component, "chute", chute);
            AssignEnum(component, "action", (int)action);
        }

        static TextMesh Label(Transform parent, string text, Vector3 localPosition, Quaternion localRotation, float characterSize, Color color)
        {
            var label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            // Text meshes read correctly when the viewer looks along the label's +Z.
            label.transform.localRotation = localRotation;

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = labelFont;
            mesh.fontSize = 64;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            label.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
            return mesh;
        }

        // ---------------------------------------------------------------- Generic helpers

        static GameObject Block(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material,
            PrimitiveType type = PrimitiveType.Cube, bool keepCollider = false, Vector3 eulerAngles = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localEulerAngles = eulerAngles;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        /// <summary>Loads a flat-colour material by name, creating it only if missing so later tweaks are kept.</summary>
        static Material Mat(string name, Color color, Color? emission = null)
        {
            if (materials.TryGetValue(name, out var cached))
            {
                return cached;
            }

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                material.SetColor("_BaseColor", color);
                material.color = color;
                material.SetFloat("_Smoothness", 0.15f);
                if (emission.HasValue)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission.Value);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                AssetDatabase.CreateAsset(material, path);
            }

            materials[name] = material;
            return material;
        }

        static void Assign(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            FindProperty(so, property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = FindProperty(so, property);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignMask(Object target, string property, int mask)
        {
            var so = new SerializedObject(target);
            FindProperty(so, property).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignEnum(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            FindProperty(so, property).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            FindProperty(so, property).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            FindProperty(so, property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignVector(Object target, string property, Vector3 value)
        {
            var so = new SerializedObject(target);
            FindProperty(so, property).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Fails loudly if a field was renamed, instead of silently leaving a reference empty.</summary>
        static SerializedProperty FindProperty(SerializedObject so, string property)
        {
            return so.FindProperty(property) ?? throw new InvalidOperationException($"{so.targetObject.GetType().Name} has no serialized field '{property}'.");
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        static void EnsureLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0)
            {
                return;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            throw new InvalidOperationException($"No free user layer for '{layerName}'.");
        }

        static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        /// <summary>Puts the scene first in the build list (so the last one added opens first).</summary>
        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
