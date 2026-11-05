using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Cheese Game
///  - Set Up Everything: both scenes below, MainMenu first in the build, Game second.
///  - Create Main Menu Scene: an empty scene with the MainMenu title screen.
///  - Create Game Scene: copies FormSwitch_test (the scene with mouse + human + form wheel) to
///    Assets/Scenes/Game.unity, sets it up, and puts it first in the build list.
///    FormSwitch_test itself is not changed.
///  - Set Up Open Scene: does the same setup on whatever scene is open.
/// Setup = Cheese component on every cheese, one "Game Manager" object,
/// and the human's smell meter switched off. Safe to run more than once.
/// </summary>
public static class CheeseGameSetup
{
    const string SourceScene = "Assets/Scenes/FormSwitch_test.unity";
    const string GameScene = "Assets/Scenes/Game.unity";
    const string MenuScene = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Cheese Game/Set Up Everything (Main Menu + Game)", priority = -10)]
    static void SetUpEverything()
    {
        if (!CreateGameSceneInternal(quiet: true)) return;
        if (!CreateMainMenuInternal()) return;
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("Cheese Game",
            "Done.\n\n- MainMenu.unity (first in the build)\n- Game.unity (second)\n\nMainMenu is open: press Play.", "OK");
    }

    [MenuItem("Tools/Cheese Game/Create Main Menu Scene", priority = 10)]
    static void CreateMainMenuScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (CreateMainMenuInternal())
            EditorUtility.DisplayDialog("Cheese Game", $"Created {MenuScene} and put it first in the build.\nPress Play to see it.", "OK");
    }

    static bool CreateMainMenuInternal()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) != null &&
            !EditorUtility.DisplayDialog("Cheese Game", $"{MenuScene} already exists. Replace it?", "Replace", "Keep it"))
        {
            ArrangeBuild();
            return true;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.045f, 0.055f, 0.08f);
        }
        GameObject menu = new GameObject("Main Menu");
        menu.AddComponent<MainMenu>();
        if (!EditorSceneManager.SaveScene(scene, MenuScene))
        {
            EditorUtility.DisplayDialog("Cheese Game", $"Couldn't save {MenuScene}.", "OK");
            return false;
        }
        ArrangeBuild();
        Debug.Log("[Cheese Game] Created " + MenuScene);
        return true;
    }

    [MenuItem("Tools/Cheese Game/Create Game Scene (from FormSwitch_test)", priority = 0)]
    static void CreateGameScene() => CreateGameSceneInternal(quiet: false);

    static bool CreateGameSceneInternal(bool quiet)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        string source = SourceScene;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null)
        {
            source = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(source) ||
                !EditorUtility.DisplayDialog("Cheese Game", $"{SourceScene} wasn't found.\n\nUse the open scene ({source}) as the base instead?", "Use open scene", "Cancel"))
                return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) != null &&
            !EditorUtility.DisplayDialog("Cheese Game", $"{GameScene} already exists. Replace it with a fresh copy of {source}?", "Replace", "Keep it"))
        {
            ArrangeBuild();
            return true;
        }

        AssetDatabase.DeleteAsset(GameScene);
        if (!AssetDatabase.CopyAsset(source, GameScene))
        {
            EditorUtility.DisplayDialog("Cheese Game", $"Couldn't copy {source} to {GameScene}.", "OK");
            return false;
        }

        Scene scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        string report = SetUp(scene);
        EditorSceneManager.SaveScene(scene);
        ArrangeBuild();

        Debug.Log("[Cheese Game] Created " + GameScene + "\n" + report);
        if (!quiet)
            EditorUtility.DisplayDialog("Cheese Game", $"Created {GameScene} and added it to the build.\n\n{report}\nPress Play.", "OK");
        return true;
    }

    [MenuItem("Tools/Cheese Game/Set Up Open Scene", priority = 20)]
    static void SetUpOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Set Up Cheese Game");
        int group = Undo.GetCurrentGroup();
        string report = SetUp(scene);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Cheese Game] Set up " + scene.name + "\n" + report);
        EditorUtility.DisplayDialog("Cheese Game", report + "\nSave the scene (Ctrl+S), then press Play.", "OK");
    }

    static string SetUp(Scene scene)
    {
        var log = new System.Text.StringBuilder();
        GameObject[] roots = scene.GetRootGameObjects();
        List<GameObject> all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();

        // 1. Cheese
        int added = 0, total = 0;
        foreach (GameObject go in all)
        {
            if (!IsCheeseRoot(go)) continue;
            total++;
            if (go.GetComponent<Cheese>() != null) continue;
            Undo.AddComponent<Cheese>(go);
            added++;
        }
        log.AppendLine($"- Cheese: {total} found ({added} newly tagged)");

        // 2. Manager
        CheeseGameManager gm = all.Select(g => g.GetComponent<CheeseGameManager>()).FirstOrDefault(c => c != null);
        if (gm == null)
        {
            GameObject go = new GameObject("Game Manager");
            Undo.RegisterCreatedObjectUndo(go, "Game Manager");
            SceneManager.MoveGameObjectToScene(go, scene);
            gm = Undo.AddComponent<CheeseGameManager>(go);
            log.AppendLine("- Added 'Game Manager' object (score, timer, menus, UI)");
        }
        FormSwitcher fs = all.Select(g => g.GetComponent<FormSwitcher>()).FirstOrDefault(c => c != null);
        if (fs != null)
        {
            Undo.RecordObject(gm, "Wire FormSwitcher");
            gm.formSwitcher = fs;
            log.AppendLine($"- Linked FormSwitcher on '{fs.name}'");

            // You start as the mouse and become the human when you need to.
            if (fs.startAsHuman)
            {
                Undo.RecordObject(fs, "Start as mouse");
                fs.startAsHuman = false;
                log.AppendLine("- Player now starts as the mouse");
            }

            // 3. Smell is a mouse sense.
            if (fs.humanForm != null)
                foreach (MonoBehaviour mb in fs.humanForm.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name == "CheeseSmellMeter" && mb.enabled)
                    {
                        Undo.RecordObject(mb, "Disable human smell");
                        mb.enabled = false;
                        log.AppendLine("- Turned off the smell meter on the human");
                    }
        }
        else log.AppendLine("- WARNING: no FormSwitcher found. Add one (mouse + human) or the game can't switch forms");

        return log.ToString();
    }

    // Topmost object called "...cheese..." that has something visible under it (not smell zones).
    static bool IsCheeseRoot(GameObject go)
    {
        string n = go.name.ToLower();
        if (!n.Contains("cheese") || n.Contains("smell") || go.CompareTag("SmellZone")) return false;
        if (go.GetComponentInChildren<Renderer>(true) == null) return false;
        for (Transform p = go.transform.parent; p != null; p = p.parent)
        {
            string pn = p.name.ToLower();
            if (pn.Contains("cheese") && !pn.Contains("smell")) return false;
        }
        return true;
    }

    // Build order: MainMenu (if it exists), then Game, then whatever else was already listed.
    static void ArrangeBuild()
    {
        var front = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) != null) front.Add(MenuScene);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) != null) front.Add(GameScene);
        var scenes = front.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        scenes.AddRange(EditorBuildSettings.scenes.Where(s => !front.Contains(s.path)));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
