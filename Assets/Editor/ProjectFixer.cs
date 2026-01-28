using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ProjectFixer : EditorWindow
{
    [MenuItem("Tools/Ocean Invader/Fix Project Errors")]
    public static void ShowWindow()
    {
        GetWindow<ProjectFixer>("Project Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Project Troubleshooting Tool", EditorStyles.boldLabel);

        if (GUILayout.Button("Fix 'SerializedObjectNotCreatableException' (Reimport Audio)"))
        {
            FixAudioSerializationErrors();
        }

        if (GUILayout.Button("Validate Prefabs"))
        {
            ValidatePrefabs();
        }
        
        if (GUILayout.Button("Force Rebuild Library (Restart Required)"))
        {
             if(EditorUtility.DisplayDialog("Rebuild Library", "This will close Unity. You need to manually delete the 'Library' folder and open the project again. Continue?", "Yes", "No"))
             {
                 EditorApplication.Exit(0);
             }
        }
    }

    private static void FixAudioSerializationErrors()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        int count = 0;
        
        try 
        {
            AssetDatabase.StartAssetEditing();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        
        Debug.Log($"<color=green>Reimported {count} AudioClips. This should resolve SerializedObjectNotCreatableException.</color>");
        AssetDatabase.Refresh();
    }

    private static void ValidatePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int issuesFound = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                Component[] components = prefab.GetComponentsInChildren<Component>(true);
                foreach (Component c in components)
                {
                    if (c == null)
                    {
                        Debug.LogError($"Prefab '{prefab.name}' at {path} has a MISSING SCRIPT.", prefab);
                        issuesFound++;
                        break;
                    }
                }
            }
        }

        if (issuesFound == 0)
        {
            Debug.Log("<color=green>No prefab issues found.</color>");
        }
        else
        {
            Debug.LogError($"Found {issuesFound} prefabs with missing scripts. Check Console for details.");
        }
    }
}
