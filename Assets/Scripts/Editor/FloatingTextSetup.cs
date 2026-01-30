using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

public class FloatingTextSetup
{
    [MenuItem("Tools/Floating Text/Reset to Default (English)")]
    public static void ResetToDefault()
    {
        string sdfPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        
        // 1. Verify Default Font Exists
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
        if (fontAsset == null)
        {
            Debug.LogError("Could not find default LiberationSans SDF. Please ensure TextMesh Pro is installed.");
            return;
        }

        // 2. Update Prefab
        string prefabPath = "Assets/Prefabs/FloatingTextPrefab.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            Debug.Log("FloatingTextPrefab not found. Creating a new one...");
            CreateDefaultPrefab(fontAsset);
            return;
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;
            
            // Ensure TMP component
            TextMeshProUGUI tmp = root.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = root.AddComponent<TextMeshProUGUI>();
            
            // Reset Settings
            tmp.font = fontAsset;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            // Force update to fix obsolete warning
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            
            // Clear Material properties (remove outline)
            tmp.fontSharedMaterial = fontAsset.material; // Reset to default material
            
            Debug.Log($"<color=green><b>Success!</b></color> Reset FloatingTextPrefab to standard English font.");
        }
    }

    private static void CreateDefaultPrefab(TMP_FontAsset font)
    {
        // Create Canvas for context
        GameObject canvas = new GameObject("TempCanvas", typeof(Canvas));
        
        GameObject go = new GameObject("FloatingTextPrefab");
        go.transform.SetParent(canvas.transform);
        
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = "+10 XP";
        tmp.color = Color.white;
        
        // Ensure RectTransform is centered
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 50);
        rt.anchoredPosition = Vector2.zero;

        // Save
        string dir = "Assets/Prefabs";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        
        PrefabUtility.SaveAsPrefabAsset(go, dir + "/FloatingTextPrefab.prefab");
        
        // Cleanup
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(canvas);
        
        Debug.Log("Created new FloatingTextPrefab.");
    }
}
