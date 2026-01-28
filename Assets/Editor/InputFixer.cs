using UnityEngine;
using UnityEditor;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[InitializeOnLoad]
public class InputFixer
{
    static InputFixer()
    {
        EditorApplication.update += RunOnce;
    }

    static void RunOnce()
    {
        EditorApplication.update -= RunOnce;
        FixInput();
        FixEventSystem();
    }
    
    [MenuItem("Tools/Fix Input Settings")]
    public static void FixInput()
    {
        // Access Project Settings -> Player
        var playerSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (playerSettings != null && playerSettings.Length > 0)
        {
             SerializedObject obj = new SerializedObject(playerSettings[0]);
             SerializedProperty prop = obj.FindProperty("activeInputHandler");
             if (prop != null)
             {
                 if (prop.intValue != 1)
                 {
                     prop.intValue = 1; // Set to New Input System
                     obj.ApplyModifiedProperties();
                     Debug.Log("InputFixer: Active Input Handling set to 'Input System Package (New)'. Restart might be required.");
                 }
             }
        }
    }

    public static void FixEventSystem()
    {
#if ENABLE_INPUT_SYSTEM
        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            var standaloneInput = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInput != null)
            {
                Debug.Log("InputFixer: Found legacy StandaloneInputModule. Replacing with InputSystemUIInputModule...");
                
                // Remove legacy component
                Object.DestroyImmediate(standaloneInput);
                
                // Add new component
                if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                    Debug.Log("InputFixer: Added InputSystemUIInputModule.");
                    
                    // Mark scene as dirty to save changes
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(eventSystem.gameObject.scene);
                }
            }
        }
#else
        Debug.LogWarning("InputFixer: Input System package is not enabled or installed.");
#endif
    }
}
