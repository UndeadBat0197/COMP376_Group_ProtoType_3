using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Human Prototype > Repair Missing Scripts In Open Scene
///
/// If the Human scripts' .meta files were regenerated (folder deleted and re-copied, or .meta files
/// not committed), Unity gives the scripts new IDs and the scene's components show
/// "Missing (Mono Script)". The scene file still records each component's class name, so this
/// rewrites those components to point at the scripts' current IDs. All field values are kept.
/// </summary>
public static class HumanScriptRepair
{
    static readonly string[] ScriptNames =
    {
        "HumanMovement", "HumanInteractor", "Liftable", "Pushable", "Openable", "LookPoint", "FormSwitcher"
    };

    // m_Script line, then m_Name, then m_EditorClassIdentifier: Assembly-CSharp::ClassName
    static readonly Regex ComponentPattern = new Regex(
        @"(m_Script: \{fileID: 11500000, guid: )([0-9a-f]{32})(, type: 3\}\r?\n[ \t]*m_Name:[^\n]*\n[ \t]*m_EditorClassIdentifier: Assembly-CSharp::)(\w+)");

    [MenuItem("Tools/Human Prototype/Repair Missing Scripts In Open Scene", priority = 100)]
    static void Repair()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Repair Missing Scripts", "Save the scene first, then run this again.", "OK");
            return;
        }
        if (scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog("Repair Missing Scripts", "The scene has unsaved changes. Save it, then run this again.", "OK");
            return;
        }

        // Current ID of each Human script.
        var currentGuid = new Dictionary<string, string>();
        foreach (string name in ScriptNames)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:MonoScript"))
            {
                MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() != null && ms.GetClass().Name == name)
                {
                    currentGuid[name] = guid;
                    break;
                }
            }
        }

        if (currentGuid.Count == 0)
        {
            EditorUtility.DisplayDialog("Repair Missing Scripts",
                "Couldn't find the Human scripts in the project.\n\nCheck the Console for red compile errors first; " +
                "Unity can't load any script while there are errors.", "OK");
            return;
        }

        string scenePath = scene.path;
        string text = File.ReadAllText(scenePath);
        int fixedCount = 0;
        var fixedNames = new SortedSet<string>();

        string repaired = ComponentPattern.Replace(text, m =>
        {
            string cls = m.Groups[4].Value;
            if (currentGuid.TryGetValue(cls, out string guid) && m.Groups[2].Value != guid)
            {
                fixedCount++;
                fixedNames.Add(cls);
                return m.Groups[1].Value + guid + m.Groups[3].Value + cls;
            }
            return m.Value;
        });

        if (fixedCount == 0)
        {
            EditorUtility.DisplayDialog("Repair Missing Scripts",
                "Nothing to repair: every Human component in this scene already points at the right script.\n\n" +
                "If components still show as Missing, check the Console for red compile errors.", "OK");
            return;
        }

        File.WriteAllText(scenePath, repaired);
        AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        string list = string.Join(", ", fixedNames);
        Debug.Log($"[Human Prototype] Repaired {fixedCount} component(s) in {scenePath}: {list}");
        EditorUtility.DisplayDialog("Repair Missing Scripts", $"Repaired {fixedCount} component(s):\n{list}\n\nThe scene was reloaded.", "OK");
    }
}
