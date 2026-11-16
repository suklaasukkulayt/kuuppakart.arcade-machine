#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class LevelScreenshotTool
{
    const int Width = 3840;
    const int Height = 2160;
    const float Padding = 1.04f;
    const float YawDegrees = 0f;
    const string Folder = "Assets/OmatAssetit/Graphics/LevelShots";

    [MenuItem("KuuppaKart/Capture Level Screenshot")]
    static void Capture()
    {
        var hiddenRenderers = new List<Renderer>();
        var hiddenCanvases = new List<Canvas>();
        HideNonLevel(hiddenRenderers, hiddenCanvases);

        Bounds bounds = GetLevelBounds();
        float oldShadowDistance = QualitySettings.shadowDistance;

        Texture2D tex = null;
        try
        {
            tex = Render(bounds);
        }
        finally
        {
            QualitySettings.shadowDistance = oldShadowDistance;
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
        }

        if (tex == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled";

        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/{sceneName}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        Debug.Log("IMG saved: " + path);
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
    }

    static void HideNonLevel(List<Renderer> renderers, List<Canvas> canvases)
    {
        foreach (var p in Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            DisableRenderers(p.gameObject, renderers);

        foreach (var a in Object.FindObjectsByType<AICar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            DisableRenderers(a.gameObject, renderers);

        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c.enabled)
            {
                c.enabled = false;
                canvases.Add(c);
            }
        }
    }

    static void DisableRenderers(GameObject root, List<Renderer> list)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (r.enabled)
            {
                r.enabled = false;
                list.Add(r);
            }
        }
    }

    static Bounds GetLevelBounds()
    {
        bool has = false;
        Bounds b = new Bounds();

        foreach (var t in Terrain.activeTerrains)
        {
            Bounds tb = t.terrainData.bounds;
            tb.center += t.transform.position;
            if (!has) { b = tb; has = true; }
            else b.Encapsulate(tb);
        }

        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (r.bounds.extents.magnitude > 2000f) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }

        if (!has) b = new Bounds(Vector3.zero, Vector3.one * 100f);
        return b;
    }

    static Texture2D Render(Bounds bounds)
    {
        var go = new GameObject("TmpShotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.cameraType = CameraType.Game;
        cam.enabled = true;
        cam.cullingMask = ~0;
        cam.orthographic = true;
        cam.aspect = (float)Width / Height;
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.55f, 0.7f);

        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;

        Quaternion rot = Quaternion.Euler(90f, YawDegrees, 0f);
        Vector3 right = rot * Vector3.right;
        Vector3 up = rot * Vector3.up;

        float halfW = 0f;
        float halfH = 0f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 d = corner - bounds.center;
            halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(d, right)));
            halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(d, up)));
        }

        cam.orthographicSize = Mathf.Max(halfH, halfW / cam.aspect, 1f) * Padding;

        float height = bounds.extents.y + 50f;
        cam.transform.SetPositionAndRotation(bounds.center + Vector3.up * height, rot);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = height + bounds.extents.y + 50f;

        QualitySettings.shadowDistance = cam.farClipPlane;

        bool urp = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
        Debug.Log($"Graphics: pipe={(urp ? "URP" : "Built-in")}, center={bounds.center}, size={bounds.size}, orthographic size={cam.orthographicSize:F0}");

        var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, urp ? 1 : 8);

        Texture2D tex = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool useRequest = urp && attempt == 0;
            Clear(rt);

            if (useRequest)
            {
                cam.targetTexture = null;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else continue;
            }
            else
            {
                cam.targetTexture = rt;
                cam.Render();
            }

            if (tex != null) Object.DestroyImmediate(tex);
            tex = Read(rt);
            if (!IsBlack(tex)) break;
            Debug.LogWarning("IMG is fully black, retrying with a different method");
        }

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(go);

        if (tex == null || IsBlack(tex))
            Debug.LogError("IMG is still black, check console");

        return tex;
    }

    static void Clear(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, new Color(0.45f, 0.55f, 0.7f, 1f));
        RenderTexture.active = prev;
    }

    static Texture2D Read(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        return tex;
    }

    static bool IsBlack(Texture2D tex)
    {
        for (int i = 1; i < 10; i++)
        {
            for (int j = 1; j < 10; j++)
            {
                Color c = tex.GetPixel(tex.width * i / 10, tex.height * j / 10);
                if (c.r + c.g + c.b > 0.02f) return false;
            }
        }
        return true;
    }
}
#endif