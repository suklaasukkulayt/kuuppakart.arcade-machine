#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;


public static class KuuppaKartLowEndOptimizer
{
    private const string BackupFolderRelative = "Library/KuuppaKartLowEndBackup";

    private static readonly string[] TargetAssetPaths =
    {
        "Assets/Settings/PC_RPAsset.asset",
        "Assets/Racing Starter Kit/RSK Assets/URP Data/URP Mobile Asset.asset"
    };

    [MenuItem("KuuppaKart/Performance/Apply potato PC graphics")]
    private static void ApplyPotatoProfile()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply low-end graphics?",
                "This changes the project's PC and Mobile URP assets: lower render scale, no HDR, no depth/opaque textures, no extra lights and no real-time shadows. Original assets are backed up in Library/KuuppaKartLowEndBackup.",
                "Apply optimization",
                "Cancel"))
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backupDirectory = Path.Combine(projectRoot, BackupFolderRelative);
        Directory.CreateDirectory(backupDirectory);

        int changed = 0;
        foreach (string assetPath in TargetAssetPaths)
        {
            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning("[KuuppaKart optimizer] URP asset not found: " + assetPath);
                continue;
            }

            string absoluteAssetPath = Path.Combine(projectRoot, assetPath);
            string backupPath = Path.Combine(
                backupDirectory,
                Path.GetFileNameWithoutExtension(assetPath) + ".asset");

            if (File.Exists(absoluteAssetPath) && !File.Exists(backupPath))
                File.Copy(absoluteAssetPath, backupPath);

            Undo.RecordObject(asset, "Optimize KuuppaKart URP for low-end PCs");
            SerializedObject serializedAsset = new SerializedObject(asset);
            bool isPcProfile = assetPath.Contains("PC_RPAsset");

            SetBool(serializedAsset, "m_SupportsHDR", false);
            SetBool(serializedAsset, "m_RequireDepthTexture", false);
            SetBool(serializedAsset, "m_RequireOpaqueTexture", false);
            SetInt(serializedAsset, "m_MSAA", 1); 
            SetFloat(serializedAsset, "m_RenderScale", isPcProfile ? 0.75f : 0.70f);
            SetInt(serializedAsset, "m_AdditionalLightsRenderingMode", 0); 
            SetInt(serializedAsset, "m_AdditionalLightsPerObjectLimit", 0);
            SetBool(serializedAsset, "m_MainLightShadowsSupported", false);
            SetBool(serializedAsset, "m_AdditionalLightShadowsSupported", false);
            SetBool(serializedAsset, "m_AnyShadowsSupported", false);
            SetBool(serializedAsset, "m_SoftShadowsSupported", false);
            SetFloat(serializedAsset, "m_ShadowDistance", 0f);
            SetInt(serializedAsset, "m_ShadowCascadeCount", 1);
            SetBool(serializedAsset, "m_ReflectionProbeBlending", false);
            SetBool(serializedAsset, "m_ReflectionProbeBoxProjection", false);

            serializedAsset.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            changed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "KuuppaKart optimization",
            "Updated " + changed + " URP profile(s). Backups are stored in Library/KuuppaKartLowEndBackup. Restart Play mode/build to check the changes.",
            "OK");
    }

    [MenuItem("KuuppaKart/Performance/Restore original URP graphics")]
    private static void RestoreOriginalProfiles()
    {
        if (!EditorUtility.DisplayDialog(
                "Restore original graphics?",
                "Restore the URP assets from the backups created before the first optimization? This does not remove the runtime low-end bootstrap script.",
                "Restore",
                "Cancel"))
            return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backupDirectory = Path.Combine(projectRoot, BackupFolderRelative);
        int restored = 0;

        foreach (string assetPath in TargetAssetPaths)
        {
            string backupPath = Path.Combine(
                backupDirectory,
                Path.GetFileNameWithoutExtension(assetPath) + ".asset");
            string absoluteAssetPath = Path.Combine(projectRoot, assetPath);

            if (!File.Exists(backupPath) || !File.Exists(absoluteAssetPath))
                continue;

            File.Copy(backupPath, absoluteAssetPath, true);
            restored++;
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog(
            "KuuppaKart optimization",
            restored + " URP profile(s) restored. Remove KuuppaKartLowEndBootstrap.cs as well if you want to stop forcing the low-end runtime settings.",
            "OK");
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            property.boolValue = value;
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum))
            property.intValue = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.propertyType == SerializedPropertyType.Float)
            property.floatValue = value;
    }
}
#endif
