using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        const string MaterialFolder = Root + "/Art/Materials";
        const string PrefabFolder = Root + "/Prefabs";
        const string DataFolder = Root + "/Data";
        const string BatchPath = DataFolder + "/Test Delivery Batch.asset";
        const string BackupFolder = "SceneBackups";

        // Placed and spawned items face the room: station forward points at the wall behind it.
        static readonly Quaternion FaceRoom = Quaternion.Euler(0f, 180f, 0f);

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Font labelFont;

        struct ToyPrefabs
        {
            public RobotBody Robot;
            public RobotPart Battery;
            public RobotPart DeadBattery;
            public RobotPart WindingKey;
        }

        [MenuItem("Elf & Safety/Build Workshop Scene")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild workshop?",
                    $"{ScenePath} will be regenerated. The current file is backed up to {BackupFolder}/ first; existing materials, prefabs and the delivery batch asset are kept.",
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

            BackUpExistingScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ToyPrefabs toys = BuildToyPrefabs();
            TestDeliveryBatch batch = LoadOrCreateBatch();

            BuildLighting();
            BuildRoom();
            var counter = new GameObject("Shipment Counter").AddComponent<ShipmentCounter>();
            BuildStations(toys, batch, counter);
            var player = BuildPlayer();
            BuildHud(player.GetComponent<PlayerInteractor>(), counter);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Elf & Safety: built {ScenePath}. Press Play to test.");
        }

        /// <summary>
        /// Copies the scene about to be regenerated into SceneBackups/ next to Assets/, so hand edits are
        /// never lost. Outside Assets on purpose: Unity doesn't import it as a second scene with the same GUID.
        /// </summary>
        static void BackUpExistingScene()
        {
            if (!File.Exists(ScenePath))
            {
                return;
            }

            Directory.CreateDirectory(BackupFolder);
            string backup = Path.Combine(BackupFolder, $"Workshop_{DateTime.Now:yyyyMMdd_HHmmss}.unity");
            File.Copy(ScenePath, backup, overwrite: false);
            Debug.Log($"Elf & Safety: backed up {ScenePath} to {backup}");
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

        static void BuildRoom()
        {
            var room = new GameObject("Room").transform;
            var wood = Mat("Floor Wood", new Color(0.55f, 0.36f, 0.22f));
            var brick = Mat("Wall Brick", new Color(0.55f, 0.18f, 0.16f));
            var trim = Mat("Wall Trim Green", new Color(0.12f, 0.38f, 0.22f));

            Block("Floor", room, new Vector3(0f, -0.1f, 0f), new Vector3(16f, 0.2f, 12f), wood, keepCollider: true);
            Block("Wall North", room, new Vector3(0f, 2f, 6.2f), new Vector3(16.8f, 4f, 0.4f), brick, keepCollider: true);
            Block("Wall South", room, new Vector3(0f, 2f, -6.2f), new Vector3(16.8f, 4f, 0.4f), brick, keepCollider: true);
            Block("Wall East", room, new Vector3(8.2f, 2f, 0f), new Vector3(0.4f, 4f, 12f), brick, keepCollider: true);
            Block("Wall West", room, new Vector3(-8.2f, 2f, 0f), new Vector3(0.4f, 4f, 12f), brick, keepCollider: true);

            // Green skirting so the room reads as "festive" even in graybox.
            Block("Trim North", room, new Vector3(0f, 0.3f, 5.98f), new Vector3(16f, 0.6f, 0.05f), trim);
            Block("Trim South", room, new Vector3(0f, 0.3f, -5.98f), new Vector3(16f, 0.6f, 0.05f), trim);
            Block("Trim East", room, new Vector3(7.98f, 0.3f, 0f), new Vector3(0.05f, 0.6f, 12f), trim);
            Block("Trim West", room, new Vector3(-7.98f, 0.3f, 0f), new Vector3(0.05f, 0.6f, 12f), trim);

            // Trapdoor in front of Santa's window. Milestone 3 makes it open.
            var trapdoor = new GameObject("Trapdoor").transform;
            trapdoor.SetParent(room, false);
            trapdoor.localPosition = new Vector3(0f, 0.005f, -2.5f);
            Block("Hazard Border", trapdoor, Vector3.zero, new Vector3(3f, 0.01f, 3f), Mat("Hazard Yellow", new Color(0.98f, 0.78f, 0.1f)));
            Block("Panel", trapdoor, new Vector3(0f, 0.005f, 0f), new Vector3(2.6f, 0.01f, 2.6f), Mat("Hazard Black", new Color(0.08f, 0.08f, 0.09f)));
            Block("Seam", trapdoor, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.01f, 2.6f), Mat("Hazard Yellow", Color.yellow));
            Label(trapdoor, "PERFORMANCE REVIEW AREA", new Vector3(0f, 0.03f, 0f), Quaternion.Euler(90f, 0f, 0f), 0.05f, new Color(1f, 0.8f, 0.1f));

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
        }

        // ---------------------------------------------------------------- Stations

        static void BuildStations(ToyPrefabs toys, TestDeliveryBatch batch, ShipmentCounter counter)
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

            Assign(delivery, "batch", batch);
            Assign(delivery, "robotPrefab", toys.Robot);
            Assign(delivery, "batteryPrefab", toys.Battery);
            Assign(delivery, "deadBatteryPrefab", toys.DeadBattery);
            Assign(delivery, "windingKeyPrefab", toys.WindingKey);
            AssignArray(delivery, "spawnPoints", spawnPoints);
            Assign(delivery, "statusLabel", deliveryStatus);

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
            Assign(shippingChute, "counter", counter);
            AssignArray(shippingChute, "rejectDropPoints", rejectPoints);
            Assign(shippingChute, "statusLabel", Label(shipping, "", new Vector3(0f, 2.5f, -0.2f), Quaternion.identity, 0.045f, Color.white));
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

        static void BuildHud(PlayerInteractor player, ShipmentCounter counter)
        {
            var canvasObject = new GameObject("HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var crosshair = new GameObject("Crosshair").AddComponent<Image>();
            crosshair.transform.SetParent(canvasObject.transform, false);
            crosshair.rectTransform.sizeDelta = new Vector2(8f, 8f);
            crosshair.raycastTarget = false;

            var prompt = HudText(canvasObject.transform, "Interaction Prompt", new Vector2(0f, -70f), 34, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f));
            var info = HudText(canvasObject.transform, "Look Info", new Vector2(0f, -115f), 24, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f));
            var feedback = HudText(canvasObject.transform, "Feedback", new Vector2(0f, 180f), 32, TextAnchor.LowerCenter, new Vector2(0.5f, 0.5f));
            var carry = HudText(canvasObject.transform, "Carry Label", new Vector2(0f, 60f), 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0f));
            var shipped = HudText(canvasObject.transform, "Shipped Counter", new Vector2(-24f, -20f), 30, TextAnchor.UpperRight, new Vector2(1f, 1f));
            var controls = HudText(canvasObject.transform, "Controls", new Vector2(24f, -20f), 22, TextAnchor.UpperLeft, new Vector2(0f, 1f));
            controls.text = "WASD move   Mouse look   Shift sprint   Space hop\nE / Click interact   Q drop   Esc release mouse";
            controls.color = new Color(1f, 1f, 1f, 0.7f);
            info.color = new Color(0.85f, 0.95f, 1f, 0.9f);

            var hud = canvasObject.AddComponent<InteractionHud>();
            Assign(hud, "player", player);
            Assign(hud, "promptLabel", prompt);
            Assign(hud, "carryLabel", carry);
            Assign(hud, "crosshair", crosshair);
            Assign(hud, "infoLabel", info);
            Assign(hud, "feedbackLabel", feedback);
            Assign(hud, "shippedLabel", shipped);
            Assign(hud, "counter", counter);
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

        // ---------------------------------------------------------------- Toys

        static ToyPrefabs BuildToyPrefabs()
        {
            return new ToyPrefabs
            {
                Robot = ToyPrefab("Robot Body", BuildRobotBody).GetComponent<RobotBody>(),
                Battery = ToyPrefab("Battery", () => BuildBattery("Battery", true)).GetComponent<RobotPart>(),
                DeadBattery = ToyPrefab("Dead Battery", () => BuildBattery("Dead Battery", false)).GetComponent<RobotPart>(),
                WindingKey = ToyPrefab("Winding Key", BuildWindingKey).GetComponent<RobotPart>(),
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

            var dirt = new GameObject("Dirt").transform;
            dirt.SetParent(visual, false);
            var grime = Mat("Grime Brown", new Color(0.33f, 0.24f, 0.12f));
            Block("Smudge Chest", dirt, new Vector3(-0.12f, 0.38f, 0.145f), new Vector3(0.1f, 0.12f, 0.02f), grime, eulerAngles: new Vector3(0f, 0f, 20f));
            Block("Smudge Belly", dirt, new Vector3(0.12f, 0.2f, 0.145f), new Vector3(0.09f, 0.08f, 0.02f), grime);
            Block("Smudge Side", dirt, new Vector3(-0.185f, 0.3f, 0.03f), new Vector3(0.02f, 0.14f, 0.16f), grime);
            Block("Smudge Head", dirt, new Vector3(0.03f, 0.625f, 0f), new Vector3(0.14f, 0.03f, 0.14f), grime);
            Block("Smudge Face", dirt, new Vector3(0.07f, 0.5f, 0.115f), new Vector3(0.08f, 0.05f, 0.02f), grime);

            var batteryMount = Mount(root.transform, "Battery Mount", new Vector3(0f, 0.2f, 0.2f), Quaternion.identity, new Vector3(0.18f, 0.26f, 0.14f), RobotPartKind.Battery);
            var keyMount = Mount(root.transform, "Key Mount", new Vector3(0.19f, 0.36f, 0f), Quaternion.Euler(0f, 0f, -90f), new Vector3(0.12f, 0.26f, 0.24f), RobotPartKind.WindingKey);

            var robot = root.AddComponent<RobotBody>();
            AssignBool(robot, "isDirty", true);
            Assign(robot, "batteryMount", batteryMount);
            Assign(robot, "keyMount", keyMount);
            Assign(robot, "dirtVisual", dirt.gameObject);
            return root;
        }

        static Transform Mount(Transform robot, string name, Vector3 position, Quaternion rotation, Vector3 triggerSize, RobotPartKind slot)
        {
            var mount = new GameObject(name).transform;
            mount.SetParent(robot, false);
            mount.localPosition = position;
            mount.localRotation = rotation;
            var trigger = mount.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;
            trigger.center = new Vector3(0f, triggerSize.y * 0.5f - 0.02f, 0f);
            var target = mount.gameObject.AddComponent<RobotSlotTarget>();
            AssignEnum(target, "slot", (int)slot);
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

            var part = root.AddComponent<RobotPart>();
            AssignEnum(part, "kind", (int)RobotPartKind.Battery);
            AssignBool(part, "isWorking", working);
            return root;
        }

        static GameObject BuildWindingKey()
        {
            var root = CarryableRoot("Winding Key", 0.2f, new Vector3(0.06f, 0.24f, 0.2f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var brass = Mat("Ribbon Gold", Color.yellow);
            Block("Shaft", visual, new Vector3(0f, 0.07f, 0f), new Vector3(0.04f, 0.14f, 0.04f), brass);
            Block("Wing Left", visual, new Vector3(0f, 0.18f, -0.06f), new Vector3(0.03f, 0.1f, 0.1f), brass);
            Block("Wing Right", visual, new Vector3(0f, 0.18f, 0.06f), new Vector3(0.03f, 0.1f, 0.1f), brass);

            var part = root.AddComponent<RobotPart>();
            AssignEnum(part, "kind", (int)RobotPartKind.WindingKey);
            AssignBool(part, "isWorking", true);
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

        static TestDeliveryBatch LoadOrCreateBatch()
        {
            var batch = AssetDatabase.LoadAssetAtPath<TestDeliveryBatch>(BatchPath);
            if (batch == null)
            {
                batch = ScriptableObject.CreateInstance<TestDeliveryBatch>();
                AssetDatabase.CreateAsset(batch, BatchPath);
            }
            return batch;
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

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
