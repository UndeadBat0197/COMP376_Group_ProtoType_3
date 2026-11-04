using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Menu: Tools > Human Prototype
/// Builds the placeholder human and test props so nobody has to wire components by hand.
/// Cinemachine is only touched by type name / serialized field name, so this file compiles
/// even if the Cinemachine package changes.
/// </summary>
public static class HumanPrototypeBuilder
{
    const string Menu = "Tools/Human Prototype/";
    const string MousePrefabPath = "Assets/Prefabs/mouse.prefab";
    const string ChairPrefabPath = "Assets/Aletheia/Prefabs/chair.prefab";
    const string BoxPrefabPath = "Assets/Aletheia/Prefabs/box.prefab";
    const string FrictionlessPath = "Assets/Physics/Frictionless.physicMaterial";
    const string MaterialFolder = "Assets/Material/HumanPrototype";
    const string TestScenePath = "Assets/Scenes/Human_Interaction_Test.unity";
    const string CheeseWedgePrefabPath = "Assets/CrowAssets/Assets/Low Poly Stylized Food/URP/Prefabs/Cheese Wedge.prefab";

    // ------------------------------------------------------------------ menu

    [MenuItem(Menu + "Create Test Scene", priority = 0)]
    static void CreateTestScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null &&
            !EditorUtility.DisplayDialog("Human Prototype", $"{TestScenePath} already exists. Overwrite it?", "Overwrite", "Cancel"))
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera mainCam = Camera.main;
        EnsureBrain(mainCam);

        // Floor + a couple of walls to push things against.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        floor.GetComponent<Renderer>().sharedMaterial = Mat("Floor", new Color(0.55f, 0.5f, 0.45f));

        Material wallMat = Mat("Wall", new Color(0.8f, 0.78f, 0.72f));
        Box("Wall_Back", null, new Vector3(0f, 1.25f, 6f), new Vector3(12f, 2.5f, 0.2f), wallMat);
        Box("Wall_Left", null, new Vector3(-6f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 12f), wallMat);
        Box("Wall_Right", null, new Vector3(6f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 12f), wallMat);

        GameObject human = BuildHuman(new Vector3(0f, 0f, -1f), mainCam);
        // Cabinet against the back wall with a cheese hidden behind it (pull it out to reach).
        BuildHiddenCheese("Cheese (behind cabinet)", new Vector3(0.2f, 0f, 5.77f));
        BuildCabinet(new Vector3(0f, 0.005f, 5.4f), Quaternion.Euler(0f, 180f, 0f));
        BuildChair(new Vector3(-2.5f, 0f, 2f));
        BuildCrateAgainstWall(new Vector3(3f, 0f, 0f), Vector3.forward, null);
        BuildBoulderOverCheese(new Vector3(-3f, 0f, -1.5f));
        BuildRock("Rock", new Vector3(1.2f, 0f, -2.5f), new Vector3(0.4f, 0.3f, 0.35f), 5f);
        BuildRock("Small Rock", new Vector3(-1.4f, 0f, -2.8f), new Vector3(0.22f, 0.18f, 0.2f), 1.5f);

        // Optional: drop the mouse in too so T switches forms.
        GameObject mouse = AddMouse(human.transform.position + new Vector3(1.5f, 0.1f, 0f), mainCam);
        if (mouse != null)
        {
            GameObject switcher = new GameObject("FormSwitcher");
            FormSwitcher fs = switcher.AddComponent<FormSwitcher>();
            fs.humanForm = human;
            fs.mouseForm = mouse;
            fs.startAsHuman = true;
        }

        EditorSceneManager.SaveScene(scene, TestScenePath);
        Selection.activeGameObject = human;
        Debug.Log($"[Human Prototype] Built and saved {TestScenePath}. Press Play.");
    }

    [MenuItem(Menu + "Add Human Player", priority = 20)]
    static void MenuAddHuman()
    {
        Camera cam = Camera.main;
        EnsureBrain(cam);
        Finish(BuildHuman(SpawnPoint(), cam), "Add Human");
    }

    [MenuItem(Menu + "Add Cabinet (doors + top shelf)", priority = 40)]
    static void MenuAddCabinet() => Finish(BuildCabinet(SpawnPoint(), Quaternion.identity), "Add Cabinet");

    [MenuItem(Menu + "Add Pushable Chair", priority = 41)]
    static void MenuAddChair() => Finish(BuildChair(SpawnPoint()), "Add Chair");

    [MenuItem(Menu + "Add Crate (lid + book inside)", priority = 42)]
    static void MenuAddCrate() => Finish(BuildCrate(SpawnPoint(), Quaternion.identity), "Add Crate");

    [MenuItem(Menu + "Add Boulder (cheese underneath)", priority = 44)]
    static void MenuAddBoulder() => Finish(BuildBoulderOverCheese(SpawnPoint()), "Add Boulder");

    [MenuItem(Menu + "Add Liftable Rock", priority = 43)]
    static void MenuAddRock() => Finish(BuildRock("Rock", SpawnPoint(), new Vector3(0.4f, 0.3f, 0.35f), 5f), "Add Rock");

    [MenuItem(Menu + "Make Selected Liftable", priority = 60)]
    static void MenuMakeLiftable() => MakeSelected(go => { EnsurePhysics(go, 3f); Undo.AddComponent<Liftable>(go); });

    [MenuItem(Menu + "Make Selected Pushable", priority = 61)]
    static void MenuMakePushable() => MakeSelected(go => MakeFurniturePushable(go, go.name, 30f, new System.Text.StringBuilder()));

    [MenuItem(Menu + "Make Selected Liftable", true)]
    [MenuItem(Menu + "Make Selected Pushable", true)]
    static bool ValidateSelection() => Selection.activeGameObject != null;

    // ------------------------------------------------------------------ set up existing scene

    [MenuItem(Menu + "Set Up Open Scene For Human", priority = 10)]
    static void SetUpOpenScene()
    {
        GameObject human = FindHumanInScene();
        if (human == null)
        {
            EditorUtility.DisplayDialog("Human Prototype",
                "Couldn't find an object named 'human' in the open scene.\n\nSelect your human in the Hierarchy and run this again.", "OK");
            return;
        }

        Physics.SyncTransforms(); // so edit-mode raycasts see everything
        Undo.SetCurrentGroupName("Set Up Scene For Human");
        int undoGroup = Undo.GetCurrentGroup();
        var log = new System.Text.StringBuilder();

        human = ConvertToHuman(human, log);

        GameObject armchair = FindInScene("armchair_01");
        if (armchair != null) MakeFurniturePushable(armchair, "Armchair", 15f, log);

        // Wardrobe -> openable AND movable (there's a cheese behind it). Upgrades an older build.
        GameObject cabinet = FindInScene("wardrobe (openable)");
        if (cabinet != null && cabinet.GetComponent<Pushable>() == null)
        {
            Undo.DestroyObjectImmediate(cabinet);
            cabinet = null;
            log.AppendLine("- Removed the older non-movable 'wardrobe (openable)' to rebuild it");
        }
        GameObject wardrobe = FindInScene("wardrobe");
        if (cabinet == null && wardrobe != null) ReplaceWithOpenableCabinet(wardrobe, log);

        Vector3 h = FloorAt(human.transform.position);
        Transform t = human.transform;

        // Crate with a lid and a book inside, against a wall with a cheese behind it.
        GameObject crate = FindInScene("Crate");
        if (crate != null && crate.GetComponentInChildren<Openable>() == null)
        {
            Undo.DestroyObjectImmediate(crate);
            crate = null;
            log.AppendLine("- Removed the older lid-less Crate to rebuild it");
        }
        if (crate == null)
        {
            GameObject cheese = BuildCrateAgainstWall(h + t.right * 2.5f, Flat(t.forward), human);
            if (cheese != null) Track(cheese, log, "Added a cheese behind the crate");
            Track(FindInScene("Crate"), log, "Added a Crate (E: open lid / look inside, book inside; F: push W / pull S)");
        }

        // Boulder (crate-sized, push/pull only) with a cheese under it.
        if (FindInScene("Boulder") == null)
        {
            GameObject boulder = BuildBoulderOverCheese(FloorAt(h + t.forward * 2.5f - t.right * 2f));
            Track(boulder, log, "Added a Boulder (F: push W / pull S)");
            Track(FindInScene("Cheese (under boulder)"), log, "Added a cheese under the boulder");
        }

        // Liftable rocks.
        if (FindInScene("Rock") == null)
            Track(BuildRock("Rock", FloorAt(h + t.forward * 2f + t.right * 1.2f), new Vector3(0.4f, 0.3f, 0.35f), 5f), log, "Added a liftable Rock");
        if (FindInScene("Small Rock") == null)
            Track(BuildRock("Small Rock", FloorAt(h + t.forward * 1.5f - t.right * 1f), new Vector3(0.22f, 0.18f, 0.2f), 1.5f), log, "Added a liftable Small Rock");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(human.scene);
        Selection.activeGameObject = human;
        Debug.Log("[Human Prototype] Scene set up:\n" + log + "Save the scene (Ctrl+S). Ctrl+Z undoes all of it.");
    }

    static GameObject FindHumanInScene()
    {
        GameObject sel = Selection.activeGameObject;
        if (sel != null && sel.scene.IsValid() &&
            (HasComponentNamed(sel, "ThirdPersonMovement") || sel.GetComponent<HumanMovement>() != null))
            return sel;
        return FindInScene("human");
    }

    static GameObject ConvertToHuman(GameObject human, System.Text.StringBuilder log)
    {
        // 1. Cut the link to the mouse prefab so nothing here can ever be applied back onto the mouse.
        if (PrefabUtility.IsPartOfPrefabInstance(human))
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(human);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            log.AppendLine("- Unpacked 'human' from the mouse prefab (mouse prefab untouched)");
        }

        if (human.GetComponent<HumanInteractor>() != null)
        {
            log.AppendLine("- 'human' already had HumanInteractor; left movement/collider as they are");
            return human;
        }

        // 2. Measure the visible body (ignore the camera rig).
        Bounds world = BodyBounds(human);
        Vector3 localMin = human.transform.InverseTransformPoint(world.min);
        Vector3 localMax = human.transform.InverseTransformPoint(world.max);
        float height = Mathf.Max(1f, localMax.y - localMin.y);
        float radius = Mathf.Clamp(Mathf.Min(world.size.x, world.size.z) * 0.5f, 0.2f, 0.45f);
        float bottom = localMin.y;

        // 3. Swap the mouse movement for HumanMovement, keeping the tuned values.
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        float walk = 3.5f, sprint = 6f, jump = 4.5f, turn = 0.1f;
        foreach (MonoBehaviour mb in human.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "ThirdPersonMovement") continue;
            SerializedObject so = new SerializedObject(mb);
            cam = GetRef(so, "cam") as Transform ?? cam;
            walk = GetFloat(so, "walkSpeed", walk);
            sprint = GetFloat(so, "sprintSpeed", sprint);
            jump = GetFloat(so, "jumpForce", jump);
            turn = GetFloat(so, "turnSmoothTime", turn);
            Undo.DestroyObjectImmediate(mb);
            log.AppendLine($"- Replaced ThirdPersonMovement with HumanMovement (kept walk {walk}, sprint {sprint}, jump {jump})");
        }

        // 4. Mouse-sized box collider -> capsule that fits the body.
        PhysicsMaterial physMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FrictionlessPath);
        foreach (BoxCollider bc in human.GetComponents<BoxCollider>())
        {
            if (bc.sharedMaterial != null) physMat = bc.sharedMaterial;
            Undo.DestroyObjectImmediate(bc);
        }
        CapsuleCollider cap = human.GetComponent<CapsuleCollider>();
        if (cap == null) cap = Undo.AddComponent<CapsuleCollider>(human);
        cap.height = height;
        cap.radius = radius;
        cap.center = new Vector3(0f, bottom + height * 0.5f, 0f);
        cap.sharedMaterial = physMat;
        log.AppendLine($"- Collider: mouse box -> capsule {height:0.00} m tall, radius {radius:0.00}");

        Rigidbody rb = human.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(human);
        Undo.RecordObject(rb, "Human rigidbody");
        rb.mass = 70f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        log.AppendLine("- Rigidbody mass 1 -> 70 so it can shove furniture");

        HumanMovement move = Undo.AddComponent<HumanMovement>(human);
        move.cam = cam;
        move.walkSpeed = walk;
        move.sprintSpeed = sprint;
        move.jumpForce = jump;
        move.turnSmoothTime = turn;

        // 5. Hands + interaction.
        Transform hold = human.transform.Find("HoldPoint");
        if (hold == null)
        {
            hold = new GameObject("HoldPoint").transform;
            Undo.RegisterCreatedObjectUndo(hold.gameObject, "HoldPoint");
            hold.SetParent(human.transform, false);
        }
        hold.localPosition = new Vector3(0f, bottom + height * 0.62f, radius + 0.5f);

        HumanInteractor interactor = Undo.AddComponent<HumanInteractor>(human);
        interactor.holdPoint = hold;
        interactor.viewCamera = cam != null ? cam.GetComponent<Camera>() : Camera.main;
        interactor.reachHeight = height * 0.7f;
        log.AppendLine("- Added HumanInteractor + HoldPoint");

        // 6. Camera: aim at head height and pull back for a person-sized body.
        Transform fix = human.transform.Find("camera fixPoint");
        if (fix != null)
        {
            Undo.RecordObject(fix, "Camera target");
            fix.localPosition = new Vector3(0f, bottom + height * 0.88f, 0f);
        }
        Transform freeLook = human.transform.Find("FreeLook Camera");
        if (freeLook != null)
        {
            foreach (MonoBehaviour mb in freeLook.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                SerializedObject so = new SerializedObject(mb);
                string type = mb.GetType().Name;
                if (type == "CinemachineOrbitalFollow") { SetFloat(so, "Radius", 3.5f); SetVector3(so, "TargetOffset", Vector3.zero); }
                else if (type == "CinemachineDecollider") SetFloat(so, "CameraRadius", 0.2f);
                so.ApplyModifiedProperties();
            }
            log.AppendLine("- FreeLook: target moved to head height, orbit radius 1 -> 3.5");
        }

        // Smell is a mouse sense: make sure it's off on the human.
        foreach (MonoBehaviour mb in human.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == "CheeseSmellMeter" && mb.enabled)
            {
                Undo.RecordObject(mb, "Disable smell");
                mb.enabled = false;
            }

        return human;
    }

    static void MakeFurniturePushable(GameObject go, string displayName, float mass, System.Text.StringBuilder log)
    {
        if (go.GetComponent<Pushable>() != null) return;

        // A moving Rigidbody needs a convex MeshCollider.
        foreach (MeshCollider mc in go.GetComponentsInChildren<MeshCollider>())
        {
            Undo.RecordObject(mc, "Convex collider");
            mc.convex = true;
        }
        Rigidbody rb = EnsurePhysics(go, mass);
        rb.angularDamping = 2f;

        Pushable p = Undo.AddComponent<Pushable>(go);
        p.allowPull = true;
        p.canRotate = true;   // chairs can be turned; crates/boulders/cabinets can't
        p.speedMultiplier = 0.6f;
        SetDisplayName(p, displayName);
        log.AppendLine($"- {go.name}: now Pushable (Rigidbody {mass} kg, collider made convex)");
    }

    // The Aletheia wardrobe is one solid mesh (doors baked shut), so swap in an openable one
    // of the same size in the same spot, facing into the room. The original is only disabled.
    static void ReplaceWithOpenableCabinet(GameObject wardrobe, System.Text.StringBuilder log)
    {
        Undo.RecordObject(wardrobe, "Hide original wardrobe");
        wardrobe.SetActive(true); // renderer bounds are empty while disabled
        Bounds b = RendererBounds(wardrobe);
        bool depthAlongX = b.size.x < b.size.z;
        float width = depthAlongX ? b.size.z : b.size.x;
        float depth = Mathf.Clamp(depthAlongX ? b.size.x : b.size.z, 0.35f, 0.8f);
        float height = Mathf.Max(1.6f, b.size.y);

        GameObject floor = FindInScene("Plane");
        Vector3 roomCenter = floor != null ? floor.transform.position : Vector3.zero;
        Vector3 toRoom = roomCenter - b.center;
        Vector3 front = depthAlongX ? new Vector3(Mathf.Sign(toRoom.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toRoom.z));

        // Nudge 3 cm off the wall so it doesn't start overlapping it.
        Vector3 basePos = new Vector3(b.center.x, b.min.y + 0.005f, b.center.z) + front * 0.03f;
        GameObject cab = BuildCabinet(basePos, Quaternion.LookRotation(front, Vector3.up), width, depth, height, "wardrobe");
        cab.name = "wardrobe (openable)";
        Undo.RegisterCreatedObjectUndo(cab, "Openable wardrobe");

        wardrobe.SetActive(false);
        log.AppendLine($"- wardrobe: replaced by 'wardrobe (openable)' ({width:0.0} x {height:0.0} m). E opens the doors, " +
                       "F grabs it so you can pull it off the wall (S) to reach the cheese behind. Original wardrobe is disabled, not deleted");
    }

    static Bounds BodyBounds(GameObject go)
    {
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static GameObject FindInScene(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child.gameObject;
        }
        return null;
    }

    static bool HasComponentNamed(GameObject go, string typeName)
    {
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            if (mb != null && mb.GetType().Name == typeName) return true;
        return false;
    }

    // First static surface below p (skips the human, props and anything else with a Rigidbody).
    static Vector3 FloorAt(Vector3 p)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, p.y + 5f, p.z), Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit hit in hits)
            if (hit.collider.attachedRigidbody == null) return hit.point;
        return new Vector3(p.x, 0f, p.z);
    }

    static void Track(GameObject go, System.Text.StringBuilder log, string line)
    {
        Undo.RegisterCreatedObjectUndo(go, line);
        log.AppendLine("- " + line);
    }

    static Object GetRef(SerializedObject so, string path)
    {
        SerializedProperty p = so.FindProperty(path);
        return p != null ? p.objectReferenceValue : null;
    }

    static float GetFloat(SerializedObject so, string path, float fallback)
    {
        SerializedProperty p = so.FindProperty(path);
        return p != null ? p.floatValue : fallback;
    }

    // ------------------------------------------------------------------ human

    static GameObject BuildHuman(Vector3 feet, Camera mainCam)
    {
        GameObject root = new GameObject("Human");
        root.transform.position = feet;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.freezeRotation = true;

        CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
        cap.height = 1.8f;
        cap.radius = 0.3f;
        cap.center = new Vector3(0f, 0.9f, 0f);
        PhysicsMaterial frictionless = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FrictionlessPath);
        if (frictionless != null) cap.sharedMaterial = frictionless;

        // Placeholder body: capsule + visor so you can tell which way it faces.
        Material skin = Mat("Human_Body", new Color(0.25f, 0.45f, 0.85f));
        Material dark = Mat("Human_Visor", new Color(0.1f, 0.1f, 0.12f));
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
        body.GetComponent<Renderer>().sharedMaterial = skin;
        Box("Visor", root.transform, new Vector3(0f, 1.55f, 0.24f), new Vector3(0.4f, 0.12f, 0.12f), dark, false);
        Box("Arm_L", root.transform, new Vector3(-0.38f, 1.05f, 0.05f), new Vector3(0.12f, 0.6f, 0.12f), skin, false);
        Box("Arm_R", root.transform, new Vector3(0.38f, 1.05f, 0.05f), new Vector3(0.12f, 0.6f, 0.12f), skin, false);

        Transform camTarget = new GameObject("CameraTarget").transform;
        camTarget.SetParent(root.transform, false);
        camTarget.localPosition = new Vector3(0f, 1.6f, 0f);

        Transform hold = new GameObject("HoldPoint").transform;
        hold.SetParent(root.transform, false);
        hold.localPosition = new Vector3(0f, 1.15f, 0.8f);

        HumanMovement move = root.AddComponent<HumanMovement>();
        move.cam = mainCam != null ? mainCam.transform : null;

        HumanInteractor interactor = root.AddComponent<HumanInteractor>();
        interactor.holdPoint = hold;
        interactor.viewCamera = mainCam;

        BuildHumanCamera(root.transform, camTarget);
        return root;
    }

    // Copies the mouse's FreeLook camera so controls/feel match, then retargets and widens it.
    static void BuildHumanCamera(Transform human, Transform target)
    {
        GameObject mousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MousePrefabPath);
        Transform src = mousePrefab != null ? mousePrefab.transform.Find("FreeLook Camera") : null;
        if (src == null)
        {
            Debug.LogWarning("[Human Prototype] Couldn't find 'FreeLook Camera' in " + MousePrefabPath +
                             ". Add a Cinemachine FreeLook to the Human and set its Tracking Target to CameraTarget.");
            return;
        }

        GameObject camGO = Object.Instantiate(src.gameObject, human);
        camGO.name = "Human FreeLook Camera";
        camGO.transform.localPosition = new Vector3(0f, 2.3f, -3.5f);
        camGO.transform.localRotation = Quaternion.identity;

        foreach (MonoBehaviour mb in camGO.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            SerializedObject so = new SerializedObject(mb);

            if (type == "CinemachineCamera")
                SetRef(so, "Target.TrackingTarget", target);
            else if (type == "CinemachineOrbitalFollow")
            {
                SetFloat(so, "Radius", 3.5f);
                SetVector3(so, "TargetOffset", Vector3.zero);
            }
            else if (type == "CinemachineDecollider")
                SetFloat(so, "CameraRadius", 0.2f);

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static GameObject AddMouse(Vector3 pos, Camera mainCam)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MousePrefabPath);
        if (prefab == null) return null;
        GameObject mouse = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        mouse.transform.position = pos;
        foreach (MonoBehaviour mb in mouse.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "ThirdPersonMovement") continue;
            SerializedObject so = new SerializedObject(mb);
            SetRef(so, "cam", mainCam != null ? mainCam.transform : null);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return mouse;
    }

    // ------------------------------------------------------------------ cabinet

    static GameObject BuildCabinet(Vector3 pos, Quaternion rot, float W = 1.2f, float D = 0.5f, float H = 2.2f, string label = "cabinet")
    {
        const float T = 0.04f;
        // Top shelf ends up around the human's eye line (1.6 m on a 2.2 m cabinet): way above the mouse.
        float[] shelves = { H * 0.27f, H * 0.5f, H * 0.73f };

        GameObject root = new GameObject("Cabinet");
        root.transform.SetPositionAndRotation(pos, rot);
        Transform r = root.transform;

        Material wood = Mat("Cabinet_Wood", new Color(0.45f, 0.3f, 0.18f));
        Material woodDark = Mat("Cabinet_Wood_Dark", new Color(0.32f, 0.2f, 0.12f));
        Material metal = Mat("Cabinet_Handle", new Color(0.8f, 0.75f, 0.5f));

        // Carcass (front faces local +Z)
        Box("Back", r, new Vector3(0f, H / 2f, -D / 2f + T / 2f), new Vector3(W, H, T), woodDark);
        Box("Side_L", r, new Vector3(-W / 2f + T / 2f, H / 2f, 0f), new Vector3(T, H, D), wood);
        Box("Side_R", r, new Vector3(W / 2f - T / 2f, H / 2f, 0f), new Vector3(T, H, D), wood);
        Box("Top", r, new Vector3(0f, H - T / 2f, 0f), new Vector3(W, T, D), wood);
        Box("Bottom", r, new Vector3(0f, T / 2f + 0.04f, 0f), new Vector3(W, T, D), wood);
        for (int i = 0; i < shelves.Length; i++)
            Box(i == shelves.Length - 1 ? "Shelf_Top" : $"Shelf_{i + 1}", r,
                new Vector3(0f, shelves[i], -T / 2f), new Vector3(W - 2f * T, T, D - T), wood);

        // Double doors, hinged on the outer edges, swinging outward. Linked so one E opens both.
        float doorW = W / 2f - 0.005f, doorH = H - 0.04f, doorT = 0.03f;
        Openable left = BuildDoor(r, "Door_L", new Vector3(-W / 2f, H / 2f, D / 2f + doorT / 2f), +1f, doorW, doorH, doorT, -105f, wood, metal, label);
        Openable right = BuildDoor(r, "Door_R", new Vector3(W / 2f, H / 2f, D / 2f + doorT / 2f), -1f, doorW, doorH, doorT, 105f, wood, metal, label);
        left.linked = new[] { right };
        right.linked = new[] { left };

        // Top-shelf look zone (trigger inside the top compartment, behind the doors).
        float topBottom = shelves[shelves.Length - 1] + T / 2f;
        float topTop = H - T;
        GameObject zone = new GameObject("TopShelf_LookZone");
        zone.transform.SetParent(r, false);
        zone.transform.localPosition = new Vector3(0f, (topBottom + topTop) / 2f, -0.02f);
        BoxCollider zc = zone.AddComponent<BoxCollider>();
        zc.isTrigger = true;
        zc.size = new Vector3(W - 2f * T - 0.04f, topTop - topBottom - 0.02f, D - T - 0.08f);

        LookPoint look = zone.AddComponent<LookPoint>();
        SetDisplayName(look, "top shelf");
        look.requiresOpen = left;

        // View: just above head height, a step back, looking down into the top compartment.
        GameObject viewGO = new GameObject("TopShelf_ViewCam");
        viewGO.transform.SetParent(r, false);
        viewGO.transform.localPosition = new Vector3(0f, H + 0.15f, D / 2f + 0.7f);
        Vector3 lookAt = r.TransformPoint(new Vector3(0f, topBottom + 0.12f, -0.08f));
        viewGO.transform.rotation = Quaternion.LookRotation(lookAt - viewGO.transform.position, Vector3.up);

        Type vcamType = FindType("Unity.Cinemachine.CinemachineCamera");
        if (vcamType != null)
        {
            viewGO.AddComponent(vcamType);
            look.virtualCamera = viewGO;
            viewGO.SetActive(false);
        }
        else
        {
            look.viewPoint = viewGO.transform; // no Cinemachine: LookPoint moves Camera.main itself
        }

        // Stuff on the shelves.
        // Shelf items have no Rigidbody, so they ride along when the cabinet is moved.
        BuildShelfCheese(r.TransformPoint(new Vector3(W * 0.12f, topBottom + 0.001f, -0.05f)), rot).transform.SetParent(r, true);
        GameObject book = Box("Book", null, r.TransformPoint(new Vector3(-0.25f, shelves[1] + T / 2f + 0.11f, -0.05f)),
                              new Vector3(0.06f, 0.22f, 0.16f), Mat("Book", new Color(0.55f, 0.1f, 0.12f)));
        book.transform.rotation = rot;
        book.transform.SetParent(r, true);
        Liftable bookL = book.AddComponent<Liftable>();
        bookL.massIfNoBody = 0.5f;
        SetDisplayName(bookL, "Book");

        // The whole cabinet: locked in place until grabbed, then straight push (W) / pull (S).
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 60f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.isKinematic = true;
        Pushable push = root.AddComponent<Pushable>();
        push.allowPull = true;
        push.canRotate = false;
        push.speedMultiplier = 0.4f;
        SetDisplayName(push, label);

        return root;
    }

    static Openable BuildDoor(Transform parent, string name, Vector3 hingePos, float dirToCenter,
                              float w, float h, float t, float openAngle, Material wood, Material metal, string label)
    {
        // No Rigidbody on the hinge: the door is part of the cabinet's body so it moves with it.
        GameObject pivot = new GameObject(name + "_Hinge");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = hingePos;

        Box(name, pivot.transform, new Vector3(dirToCenter * w / 2f, 0f, 0f), new Vector3(w, h, t), wood);
        Box(name + "_Handle", pivot.transform, new Vector3(dirToCenter * (w - 0.07f), 0f, t / 2f + 0.02f),
            new Vector3(0.025f, 0.18f, 0.03f), metal, false);

        Openable o = pivot.AddComponent<Openable>();
        o.mode = Openable.Mode.Hinge;
        o.openAngle = openAngle;
        SetDisplayName(o, label);
        return o;
    }

    // Uses the CrowAssets cheese wedge (same art as the rest of the level) if it's there.
    static GameObject BuildShelfCheese(Vector3 bottomPos, Quaternion rot)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CheeseWedgePrefabPath) == null) return BuildCheese(bottomPos);
        GameObject wedge = PropFromPrefab(CheeseWedgePrefabPath, "Cheese Wedge (top shelf)", bottomPos, 0.1f,
                                          Vector3.one * 0.1f, new Color(1f, 0.82f, 0.25f));
        wedge.transform.rotation = rot * wedge.transform.rotation;
        Liftable l = Undo.AddComponent<Liftable>(wedge);
        l.massIfNoBody = 0.5f;
        SetDisplayName(l, "Cheese");
        return wedge;
    }

    static GameObject BuildCheese(Vector3 bottomPos)
    {
        GameObject cheese = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cheese.name = "Cheese Wheel";
        Object.DestroyImmediate(cheese.GetComponent<Collider>());
        cheese.transform.position = bottomPos + Vector3.up * 0.06f;
        cheese.transform.localScale = new Vector3(0.26f, 0.06f, 0.26f);
        cheese.GetComponent<Renderer>().sharedMaterial = Mat("Cheese", new Color(1f, 0.82f, 0.25f));
        MeshCollider mc = cheese.AddComponent<MeshCollider>();
        mc.convex = true;
        SetDisplayName(cheese.AddComponent<Liftable>(), "Cheese");
        return cheese;
    }

    // ------------------------------------------------------------------ props

    static GameObject BuildChair(Vector3 feet)
    {
        GameObject chair = PropFromPrefab(ChairPrefabPath, "Chair", feet, 0.95f,
                                          new Vector3(0.5f, 0.95f, 0.5f), new Color(0.5f, 0.35f, 0.2f));
        Rigidbody rb = EnsurePhysics(chair, 8f);
        rb.angularDamping = 2f;
        Pushable p = chair.AddComponent<Pushable>();
        p.allowPull = true;
        p.canRotate = true;
        p.speedMultiplier = 0.65f;
        SetDisplayName(p, "Chair");
        return chair;
    }

    // Open-top crate built from planks with a hinged lid and a book inside.
    // E on the lid opens it, E inside looks in (then click the book). F grabs it: W push / S pull.
    static GameObject BuildCrate(Vector3 feet, Quaternion rot)
    {
        const float W = 0.9f, D = 0.8f, H = 0.75f, T = 0.04f;
        GameObject root = new GameObject("Crate");
        root.transform.SetPositionAndRotation(feet + Vector3.up * 0.005f, rot);
        Transform r = root.transform;

        Material wood = Mat("Crate_Wood", new Color(0.62f, 0.46f, 0.26f));
        Material woodDark = Mat("Crate_Wood_Dark", new Color(0.45f, 0.32f, 0.18f));

        Box("Bottom", r, new Vector3(0f, T / 2f, 0f), new Vector3(W, T, D), woodDark);
        Box("Side_Front", r, new Vector3(0f, H / 2f, D / 2f - T / 2f), new Vector3(W, H, T), wood);
        Box("Side_Back", r, new Vector3(0f, H / 2f, -D / 2f + T / 2f), new Vector3(W, H, T), wood);
        Box("Side_L", r, new Vector3(-W / 2f + T / 2f, H / 2f, 0f), new Vector3(T, H, D - 2f * T), wood);
        Box("Side_R", r, new Vector3(W / 2f - T / 2f, H / 2f, 0f), new Vector3(T, H, D - 2f * T), wood);
        // Corner posts so it reads as a crate.
        foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Box("Post", r, new Vector3(x * (W / 2f - 0.03f), H / 2f, z * (D / 2f + 0.005f)), new Vector3(0.06f, H, 0.02f), woodDark, false);

        // Lid hinged along the back edge; opening swings the front edge up.
        GameObject hinge = new GameObject("Lid_Hinge");
        hinge.transform.SetParent(r, false);
        hinge.transform.localPosition = new Vector3(0f, H, -D / 2f);
        Box("Lid", hinge.transform, new Vector3(0f, T / 2f, D / 2f), new Vector3(W + 0.02f, T, D + 0.02f), woodDark);
        Box("Lid_Handle", hinge.transform, new Vector3(0f, T + 0.015f, D - 0.06f), new Vector3(0.2f, 0.03f, 0.04f),
            Mat("Cabinet_Handle", new Color(0.8f, 0.75f, 0.5f)), false);
        Openable lid = hinge.AddComponent<Openable>();
        lid.mode = Openable.Mode.Hinge;
        lid.hingeAxis = Vector3.right;
        lid.openAngle = -110f;
        SetDisplayName(lid, "crate");

        // Look-inside zone + overhead view.
        GameObject zone = new GameObject("Inside_LookZone");
        zone.transform.SetParent(r, false);
        zone.transform.localPosition = new Vector3(0f, T + (H - T) / 2f, 0f);
        BoxCollider zc = zone.AddComponent<BoxCollider>();
        zc.isTrigger = true;
        zc.size = new Vector3(W - 2f * T - 0.04f, H - T - 0.06f, D - 2f * T - 0.04f);
        LookPoint look = zone.AddComponent<LookPoint>();
        look.verb = "Look";
        SetDisplayName(look, "inside crate");
        look.requiresOpen = lid;
        AttachViewCamera(look, r, "Inside_ViewCam", new Vector3(0f, H + 1.0f, D / 2f + 0.45f), new Vector3(0f, T, 0f));

        // Book lying inside (no Rigidbody, rides with the crate until picked up).
        GameObject book = Box("Book", r, new Vector3(0.12f, T + 0.025f, 0.05f), new Vector3(0.17f, 0.05f, 0.24f),
                              Mat("Book", new Color(0.55f, 0.1f, 0.12f)));
        book.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        Liftable bookL = book.AddComponent<Liftable>();
        bookL.massIfNoBody = 0.5f;
        SetDisplayName(bookL, "Book");

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 40f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.isKinematic = true;
        Pushable push = root.AddComponent<Pushable>();
        push.allowPull = true;
        push.canRotate = false;
        push.speedMultiplier = 0.45f;
        SetDisplayName(push, "crate");
        return root;
    }

    // Finds a wall from 'origin' along 'dir', puts a cheese at its foot and the crate just in front of it,
    // lid side facing the room. Returns the cheese.
    static GameObject BuildCrateAgainstWall(Vector3 origin, Vector3 dir, GameObject ignore)
    {
        const float crateDepth = 0.8f;
        Physics.SyncTransforms();
        dir = Flat(dir);
        Vector3 wallPoint = origin + dir * 4f;
        Vector3 normal = -dir;

        RaycastHit[] hits = Physics.RaycastAll(origin + Vector3.up * 0.5f, dir, 15f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (ignore != null && hit.collider.transform.IsChildOf(ignore.transform)) continue;
            if (hit.collider.attachedRigidbody != null) continue; // only static stuff counts as a wall
            wallPoint = new Vector3(hit.point.x, origin.y, hit.point.z);
            normal = Flat(hit.normal);
            break;
        }

        GameObject cheese = BuildHiddenCheese("Cheese (behind crate)", FloorAt(wallPoint + normal * 0.12f));
        BuildCrate(FloorAt(wallPoint + normal * (crateDepth / 2f + 0.24f)), Quaternion.LookRotation(normal, Vector3.up));
        return cheese;
    }

    // Crate-sized boulder: can't be lifted, only pushed (W) / pulled (S). Cheese hidden underneath.
    static GameObject BuildBoulderOverCheese(Vector3 feet)
    {
        BuildHiddenCheese("Cheese (under boulder)", feet);

        Vector3 size = new Vector3(1.0f, 0.8f, 0.95f);
        GameObject boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        boulder.name = "Boulder";
        Object.DestroyImmediate(boulder.GetComponent<Collider>());
        boulder.transform.position = feet + Vector3.up * (size.y / 2f + 0.005f);
        boulder.transform.localScale = size;
        boulder.GetComponent<Renderer>().sharedMaterial = Mat("Boulder", new Color(0.38f, 0.37f, 0.36f));
        MeshCollider mc = boulder.AddComponent<MeshCollider>();
        mc.convex = true;

        Rigidbody rb = boulder.AddComponent<Rigidbody>();
        rb.mass = 150f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.isKinematic = true;
        Pushable p = boulder.AddComponent<Pushable>();
        p.allowPull = true;
        p.canRotate = false;
        p.speedMultiplier = 0.35f;
        SetDisplayName(p, "boulder");
        return boulder;
    }

    // A cheese with a trigger collider and no Rigidbody, so it can sit hidden under/behind things
    // without being shoved out. Becomes a normal solid object once the human picks it up.
    static GameObject BuildHiddenCheese(string name, Vector3 floor)
    {
        GameObject cheese;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CheeseWedgePrefabPath) != null)
            cheese = PropFromPrefab(CheeseWedgePrefabPath, name, floor, 0.1f, Vector3.one * 0.1f, new Color(1f, 0.82f, 0.25f));
        else
        {
            cheese = BuildCheese(floor);
            cheese.name = name;
        }
        foreach (Collider c in cheese.GetComponentsInChildren<Collider>())
            c.isTrigger = true;
        Liftable l = cheese.GetComponent<Liftable>();
        if (l == null) l = cheese.AddComponent<Liftable>();
        l.massIfNoBody = 0.5f;
        SetDisplayName(l, "Cheese");
        return cheese;
    }

    static void AttachViewCamera(LookPoint look, Transform parent, string name, Vector3 localPos, Vector3 localLookAt)
    {
        GameObject viewGO = new GameObject(name);
        viewGO.transform.SetParent(parent, false);
        viewGO.transform.localPosition = localPos;
        Vector3 lookAt = parent.TransformPoint(localLookAt);
        viewGO.transform.rotation = Quaternion.LookRotation(lookAt - viewGO.transform.position, Vector3.up);

        Type vcamType = FindType("Unity.Cinemachine.CinemachineCamera");
        if (vcamType != null)
        {
            viewGO.AddComponent(vcamType);
            look.virtualCamera = viewGO;
            viewGO.SetActive(false);
        }
        else look.viewPoint = viewGO.transform;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    static GameObject BuildRock(string name, Vector3 feet, Vector3 size, float mass)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = name;
        Object.DestroyImmediate(rock.GetComponent<Collider>());
        rock.transform.position = feet + Vector3.up * (size.y / 2f + 0.01f);
        rock.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 8f);
        rock.transform.localScale = size;
        rock.GetComponent<Renderer>().sharedMaterial = Mat("Rock", new Color(0.45f, 0.45f, 0.47f));
        MeshCollider mc = rock.AddComponent<MeshCollider>();
        mc.convex = true;

        EnsurePhysics(rock, mass);
        Liftable l = rock.AddComponent<Liftable>();
        l.carrySpeedMultiplier = Mathf.Clamp(1f - mass * 0.04f, 0.5f, 0.95f);
        SetDisplayName(l, name);
        return rock;
    }

    // Uses the Aletheia model if present (scaled to a sensible real-world height), else a cube.
    static GameObject PropFromPrefab(string path, string name, Vector3 feet, float targetHeight, Vector3 fallbackSize, Color fallbackColor)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject go;
        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = feet;

            MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && go.GetComponent<Collider>() == null)
            {
                BoxCollider bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = mf.sharedMesh.bounds.center;
                bc.size = mf.sharedMesh.bounds.size;
            }

            Bounds b = RendererBounds(go);
            if (b.size.y > 0.0001f)
            {
                go.transform.localScale *= targetHeight / b.size.y;
                b = RendererBounds(go);
                go.transform.position += new Vector3(feet.x - b.center.x, feet.y - b.min.y + 0.01f, feet.z - b.center.z);
            }
        }
        else
        {
            go = Box(name, null, feet + Vector3.up * (fallbackSize.y / 2f + 0.01f), fallbackSize, Mat(name, fallbackColor));
        }
        return go;
    }

    // ------------------------------------------------------------------ helpers

    static Rigidbody EnsurePhysics(GameObject go, float mass)
    {
        if (go.GetComponentInChildren<Collider>() == null)
        {
            MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null) Undo.AddComponent<BoxCollider>(mf.gameObject);
            else Undo.AddComponent<BoxCollider>(go);
        }
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(go);
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Speculative works for both kinematic (locked furniture) and moving bodies without warnings.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        return rb;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
        }
        else go.transform.position = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Material Mat(string name, Color color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Material")) AssetDatabase.CreateFolder("Assets", "Material");
            AssetDatabase.CreateFolder("Assets/Material", "HumanPrototype");
        }
        string path = $"{MaterialFolder}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Bounds RendererBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static void SetDisplayName(Interactable it, string name)
    {
        SerializedObject so = new SerializedObject(it);
        SerializedProperty p = so.FindProperty("displayName");
        if (p != null) { p.stringValue = name; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    static void EnsureBrain(Camera cam)
    {
        if (cam == null) return;
        Type brain = FindType("Unity.Cinemachine.CinemachineBrain");
        if (brain != null && cam.GetComponent(brain) == null) cam.gameObject.AddComponent(brain);
    }

    static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    static void SetRef(SerializedObject so, string path, Object value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[Human Prototype] Field '{path}' not found on {so.targetObject.GetType().Name}");
    }

    static void SetFloat(SerializedObject so, string path, float value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) p.floatValue = value;
    }

    static void SetVector3(SerializedObject so, string path, Vector3 value)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) p.vector3Value = value;
    }

    static Vector3 SpawnPoint()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        Vector3 p = sv != null ? sv.pivot : Vector3.zero;
        if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return new Vector3(p.x, 0f, p.z);
    }

    static void Finish(GameObject go, string undoName)
    {
        Undo.RegisterCreatedObjectUndo(go, undoName);
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    static void MakeSelected(Action<GameObject> action)
    {
        foreach (GameObject go in Selection.gameObjects) action(go);
    }
}
