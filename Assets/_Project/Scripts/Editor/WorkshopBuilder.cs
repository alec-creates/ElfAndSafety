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
    /// Builds the milestone 1 graybox workshop: layers, flat-colour materials, player and placeholder prefabs,
    /// and the Workshop scene. Safe to re-run: materials and prefabs that already exist are reused untouched,
    /// so swapped-in art survives; only the scene itself is regenerated.
    /// </summary>
    public static class WorkshopBuilder
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Workshop.unity";
        const string MaterialFolder = Root + "/Art/Materials";
        const string PrefabFolder = Root + "/Prefabs";

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Font labelFont;

        [MenuItem("Elf & Safety/Build Milestone 1 Workshop")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild workshop?",
                    $"{ScenePath} already exists and will be regenerated. Existing materials and prefabs are kept.",
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

            materials.Clear();
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildRoom();
            BuildStations();
            BuildProps();
            var player = BuildPlayer();
            BuildHud(player.GetComponent<PlayerInteractor>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Elf & Safety: built {ScenePath}. Press Play to test.");
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

        static void BuildStations()
        {
            var stations = new GameObject("Stations").transform;
            var metal = Mat("Metal Grey", new Color(0.5f, 0.52f, 0.55f));
            var metalDark = Mat("Metal Dark", new Color(0.22f, 0.22f, 0.24f));
            var red = Mat("Santa Red", Color.red);
            var green = Mat("Elf Green", new Color(0.15f, 0.55f, 0.25f));
            var blue = Mat("Wash Blue", new Color(0.25f, 0.55f, 0.85f));
            var gold = Mat("Ribbon Gold", new Color(0.95f, 0.72f, 0.2f));
            var crate = Mat("Crate Brown", new Color(0.45f, 0.3f, 0.16f));

            // Delivery chute: a slanted ramp from the wall down to a receiving tray (north-west corner).
            var chute = Station(stations, "Delivery Chute", new Vector3(-6.5f, 0f, 4.6f), Quaternion.identity, new Vector3(1.6f, 0.8f, 1.2f), red);
            Block("Hopper", chute, new Vector3(0f, 3f, 0.9f), new Vector3(1.4f, 1.2f, 0.8f), red);
            Block("Ramp", chute, new Vector3(0f, 1.65f, 0.55f), new Vector3(1f, 0.08f, 2.2f), metal, eulerAngles: new Vector3(-50f, 0f, 0f));
            Block("Tray", chute, new Vector3(0f, 0.8f, -0.1f), new Vector3(1.4f, 0.05f, 0.9f), metalDark);
            Socket(chute, "Delivery Tray", new Vector3(0f, 0.82f, -0.1f), gold);
            Label(chute, "DELIVERIES", new Vector3(0f, 2.2f, -0.2f), Quaternion.identity, 0.06f, Color.white);

            var salvage = Station(stations, "Salvage Bench", new Vector3(-3f, 0f, 4.8f), Quaternion.identity, new Vector3(2f, 0.9f, 0.9f), metal);
            Block("Vice", salvage, new Vector3(0.75f, 1.05f, 0.2f), new Vector3(0.25f, 0.3f, 0.25f), red);
            Block("Saw Blade", salvage, new Vector3(-0.7f, 1.2f, 0.25f), new Vector3(0.6f, 0.02f, 0.6f), metalDark, PrimitiveType.Cylinder, eulerAngles: new Vector3(90f, 0f, 0f));
            Socket(salvage, "Salvage Bench", new Vector3(0f, 0.9f, 0f), gold);
            Label(salvage, "SALVAGE", new Vector3(0f, 1.9f, 0.4f), Quaternion.identity, 0.06f, Color.white);

            var cleaning = Station(stations, "Cleaning Station", new Vector3(0.5f, 0f, 4.8f), Quaternion.identity, new Vector3(1.6f, 0.75f, 0.9f), blue);
            Block("Tub Rim", cleaning, new Vector3(0f, 0.78f, 0f), new Vector3(1.7f, 0.06f, 1f), Mat("Metal Grey", Color.grey));
            Block("Soap Nozzle", cleaning, new Vector3(0f, 1.6f, 0.35f), new Vector3(0.12f, 0.6f, 0.12f), green, PrimitiveType.Cylinder);
            Socket(cleaning, "Cleaning Station", new Vector3(0f, 0.81f, 0f), gold);
            Label(cleaning, "SCRUB", new Vector3(0f, 2.35f, 0.4f), Quaternion.identity, 0.06f, Color.white);

            var assembly = Station(stations, "Assembly Bench", new Vector3(4f, 0f, 4.8f), Quaternion.identity, new Vector3(2f, 0.9f, 0.9f), green);
            Block("Lamp Arm", assembly, new Vector3(-0.8f, 1.4f, 0.3f), new Vector3(0.08f, 1f, 0.08f), metalDark);
            Socket(assembly, "Assembly Bench", new Vector3(0f, 0.9f, 0f), gold);
            Label(assembly, "ASSEMBLY", new Vector3(0f, 1.9f, 0.4f), Quaternion.identity, 0.06f, Color.white);

            // Shipping and rejects on the east wall, facing into the room.
            var east = Quaternion.Euler(0f, 90f, 0f);
            var shipping = Station(stations, "Shipping Chute", new Vector3(7.3f, 0f, 2f), east, new Vector3(2f, 1f, 1f), red);
            Block("Ribbon", shipping, new Vector3(0f, 0.5f, -0.51f), new Vector3(0.2f, 1f, 0.02f), gold);
            Block("Mouth", shipping, new Vector3(0f, 1.6f, 0.3f), new Vector3(1.4f, 1.2f, 0.4f), metalDark);
            Socket(shipping, "Shipping Chute", new Vector3(0f, 1f, -0.2f), gold);
            Label(shipping, "SHIPPING", new Vector3(0f, 2.5f, -0.2f), Quaternion.identity, 0.06f, Color.white);

            var rejects = Station(stations, "Reject Bin", new Vector3(7.2f, 0f, -2.5f), east, new Vector3(1.4f, 0.6f, 1f), crate);
            Socket(rejects, "Reject Bin", new Vector3(0f, 0.6f, 0f), gold);
            Label(rejects, "REJECTS\n(try again)", new Vector3(0f, 1.3f, 0f), Quaternion.identity, 0.05f, Color.white);
        }

        static void BuildProps()
        {
            var props = new GameObject("Loose Props").transform;
            var robot = PlaceholderPrefab("Placeholder Robot", BuildPlaceholderRobot);
            var battery = PlaceholderPrefab("Placeholder Battery", BuildPlaceholderBattery);
            var key = PlaceholderPrefab("Placeholder Winding Key", BuildPlaceholderKey);

            Spawn(robot, props, new Vector3(-1.5f, 0.05f, 0.5f), 20f);
            Spawn(robot, props, new Vector3(-5.5f, 0.05f, 2.5f), -15f);
            Spawn(battery, props, new Vector3(0f, 0.05f, 0.8f), 0f);
            Spawn(battery, props, new Vector3(0.4f, 0.05f, 0.9f), 40f);
            Spawn(key, props, new Vector3(1.5f, 0.05f, 0.5f), 70f);
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

        static void BuildHud(PlayerInteractor player)
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
            var carry = HudText(canvasObject.transform, "Carry Label", new Vector2(0f, 60f), 28, TextAnchor.LowerCenter, new Vector2(0.5f, 0f));
            var controls = HudText(canvasObject.transform, "Controls", new Vector2(24f, -20f), 22, TextAnchor.UpperLeft, new Vector2(0f, 1f));
            controls.text = "WASD move   Mouse look   Shift sprint   Space hop\nE / Click interact   Q drop   Esc release mouse";
            controls.color = new Color(1f, 1f, 1f, 0.7f);

            var hud = canvasObject.AddComponent<InteractionHud>();
            Assign(hud, "player", player);
            Assign(hud, "promptLabel", prompt);
            Assign(hud, "carryLabel", carry);
            Assign(hud, "crosshair", crosshair);
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

        // ---------------------------------------------------------------- Placeholder toys

        static GameObject PlaceholderPrefab(string name, Func<GameObject> build)
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

        static GameObject BuildPlaceholderRobot()
        {
            var root = CarryableRoot("Wind-up Robot", 1.5f, new Vector3(0.42f, 0.62f, 0.32f), new Vector3(0f, -0.3f, 0f));
            var visual = root.transform.Find("Visual");
            var tin = Mat("Robot Tin Red", new Color(0.85f, 0.2f, 0.18f));
            var dark = Mat("Metal Dark", Color.black);
            Block("Torso", visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.36f, 0.32f, 0.28f), tin);
            Block("Head", visual, new Vector3(0f, 0.53f, 0f), new Vector3(0.26f, 0.18f, 0.22f), Mat("Metal Grey", Color.grey));
            Block("Eye Left", visual, new Vector3(-0.06f, 0.55f, 0.115f), Vector3.one * 0.05f, Mat("Bulb", Color.yellow), PrimitiveType.Sphere);
            Block("Eye Right", visual, new Vector3(0.06f, 0.55f, 0.115f), Vector3.one * 0.05f, Mat("Bulb", Color.yellow), PrimitiveType.Sphere);
            Block("Leg Left", visual, new Vector3(-0.09f, 0.07f, 0f), new Vector3(0.1f, 0.14f, 0.14f), dark);
            Block("Leg Right", visual, new Vector3(0.09f, 0.07f, 0f), new Vector3(0.1f, 0.14f, 0.14f), dark);
            return root;
        }

        static GameObject BuildPlaceholderBattery()
        {
            var root = CarryableRoot("Battery", 0.3f, new Vector3(0.12f, 0.2f, 0.12f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            Block("Cell", visual, new Vector3(0f, 0.09f, 0f), new Vector3(0.12f, 0.09f, 0.12f), Mat("Battery Yellow", new Color(0.95f, 0.8f, 0.15f)), PrimitiveType.Cylinder);
            Block("Terminal", visual, new Vector3(0f, 0.19f, 0f), new Vector3(0.05f, 0.015f, 0.05f), Mat("Copper", new Color(0.75f, 0.45f, 0.2f)), PrimitiveType.Cylinder);
            return root;
        }

        static GameObject BuildPlaceholderKey()
        {
            var root = CarryableRoot("Winding Key", 0.2f, new Vector3(0.06f, 0.24f, 0.2f), Vector3.zero);
            var visual = root.transform.Find("Visual");
            var brass = Mat("Ribbon Gold", Color.yellow);
            Block("Shaft", visual, new Vector3(0f, 0.07f, 0f), new Vector3(0.04f, 0.14f, 0.04f), brass);
            Block("Wing Left", visual, new Vector3(0f, 0.18f, -0.06f), new Vector3(0.03f, 0.1f, 0.1f), brass);
            Block("Wing Right", visual, new Vector3(0f, 0.18f, 0.06f), new Vector3(0.03f, 0.1f, 0.1f), brass);
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

        static void Spawn(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        // ---------------------------------------------------------------- Station helpers

        /// <summary>A station root with a solid base block. Gameplay components go on the root or its sockets.</summary>
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
            return visual;
        }

        /// <summary>A placement slot: trigger volume for aiming, a visible pad, and an attach point on the surface.</summary>
        static void Socket(Transform stationVisual, string displayName, Vector3 surfacePoint, Material padMaterial)
        {
            var stationRoot = stationVisual.parent;
            var slot = new GameObject("Slot").transform;
            slot.SetParent(stationRoot, false);
            slot.localPosition = surfacePoint;

            var trigger = slot.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.9f, 0.6f, 0.7f);
            trigger.center = new Vector3(0f, 0.3f, 0f);

            var attach = new GameObject("Attach Point").transform;
            attach.SetParent(slot, false);
            attach.localPosition = new Vector3(0f, 0.01f, 0f);

            Block("Placement Pad", slot, new Vector3(0f, 0.005f, 0f), new Vector3(0.6f, 0.01f, 0.5f), padMaterial);

            var socket = slot.gameObject.AddComponent<ItemSocket>();
            AssignString(socket, "displayName", displayName);
            Assign(socket, "attachPoint", attach);
        }

        static void Label(Transform parent, string text, Vector3 localPosition, Quaternion localRotation, float characterSize, Color color)
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
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignMask(Object target, string property, int mask)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).intValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignString(Object target, string property, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignVector(Object target, string property, Vector3 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
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
