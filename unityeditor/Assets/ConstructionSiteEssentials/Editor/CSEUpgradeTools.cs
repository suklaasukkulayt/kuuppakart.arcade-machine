// Additive upgrade tools for Construction Site Essentials 1.1.0.
// Original 1.0.1 prefabs and showcase are intentionally left intact.
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConstructionSiteEssentials.Editor
{
    [Serializable] internal sealed class CSECollisionRoot { public CSECollisionProfile[] profiles; }
    [Serializable] internal sealed class CSECollisionProfile { public string name; public CSECollisionShape[] shapes; }
    [Serializable] internal sealed class CSECollisionShape
    {
        public string name;
        public string kind;
        public Vector3 center;
        public Vector3 size;
        public float radius;
        public float height;
        public int direction;
    }

    public static class CSEUpgradeTools
    {
        private const string Root = "Assets/ConstructionSiteEssentials";
        private static string MetaPath(string path) => path + ".meta";

        [MenuItem("Tools/Construction Site Essentials/5 - Build True-Scale Inspection Gallery")]
        public static void BuildGalleryMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try { BuildGallery(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        [MenuItem("Tools/Construction Site Essentials/6 - Build Optional Primitive Collider Prefabs")]
        public static void BuildCollisionMenu()
        {
            try { BuildCollisionOptions(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        [MenuItem("Tools/Construction Site Essentials/7 - Validate Upgrade and Capture Gallery")]
        public static void ValidateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                BuildCollisionOptions();
                BuildGallery();
                BuildDetailGallery("CSE_Detail_SmallEquipment", new[] { "HardHat", "Toolbox", "CementBag", "RoadSign", "CautionSign" }, 1.55f, 2.8f);
                BuildDetailGallery("CSE_Detail_SiteEquipment", new[] { "SafetyBarrier", "Wheelbarrow", "PortableWorkLight" }, 2.7f, 3.1f);
                var errors = ValidateUpgrade();
                CapturePreview(Root + "/Demo/CSE_InspectionGallery.unity", "CSE_InspectionGallery.png");
                CapturePreview(Root + "/Demo/CSE_Detail_SmallEquipment.unity", "CSE_Detail_SmallEquipment.png");
                CapturePreview(Root + "/Demo/CSE_Detail_SiteEquipment.unity", "CSE_Detail_SiteEquipment.png");
                Debug.Log("CSE 1.1.0 upgrade validation completed with " + errors + " error(s).");
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        [MenuItem("Tools/Construction Site Essentials/8 - Export Unity Package Candidate")]
        public static void ExportCandidateMenu()
        {
            string path = Environment.GetEnvironmentVariable("CSE_PACKAGE_PATH");
            if (string.IsNullOrEmpty(path))
                path = EditorUtility.SaveFilePanel("Export Construction Site Essentials 1.1.0", "",
                    "ConstructionSiteEssentials_1.1.0", "unitypackage");
            if (!string.IsNullOrEmpty(path)) ExportCandidateAtPath(path);
        }

        public static void ExportCandidateBatch()
        {
            string path = Environment.GetEnvironmentVariable("CSE_PACKAGE_PATH");
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("CSE_PACKAGE_PATH must name the final package path.");
            ExportCandidateAtPath(path);
        }

        private static void ExportCandidateAtPath(string path)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
            Debug.Log("Construction Site Essentials candidate exported with AssetDatabase.ExportPackage: " + path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static CSECollisionRoot ReadCollisionProfiles()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Editor/CSECompoundCollisionProfiles.json");
            if (text == null) throw new FileNotFoundException("CSECompoundCollisionProfiles.json is missing.");
            var data = JsonUtility.FromJson<CSECollisionRoot>(text.text);
            if (data == null || data.profiles == null || data.profiles.Length != 12)
                throw new InvalidDataException("Expected one compound collision profile for each of the 12 props.");
            return data;
        }

        private static void BuildCollisionOptions()
        {
            EnsureFolder(Root + "/Prefabs", "CollisionOptions");
            var profiles = ReadCollisionProfiles().profiles;
            var report = new StringBuilder();
            report.AppendLine("# Optional primitive collider profiles");
            report.AppendLine();
            report.AppendLine("Generated as additive prefabs. The original prefab for each prop remains unchanged. These are approximate, static scene colliders made from source-part bounds; they are not dynamic Rigidbody setups or precision contact surfaces. The Wheelbarrow formed-tray box is a solid AABB and fills the basin; it is unsuitable where an open tray or contents need collision interaction.");
            report.AppendLine();
            report.AppendLine("| Prop | Primitive shapes | MeshColliders | Rigidbodies |");
            report.AppendLine("|---|---:|---:|---:|");

            foreach (var profile in profiles)
            {
                if (profile.shapes == null || profile.shapes.Length == 0)
                    throw new InvalidDataException("No collider shapes for " + profile.name);
                string sourcePath = Root + "/Prefabs/CSE_" + profile.name + ".prefab";
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null) throw new FileNotFoundException("Missing source prefab " + sourcePath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                try
                {
                    instance.name = "CSE_" + profile.name + "_PrimitiveCollision";
                    foreach (var meshCollider in instance.GetComponentsInChildren<MeshCollider>(true))
                        UnityEngine.Object.DestroyImmediate(meshCollider, true);

                    foreach (var shape in profile.shapes)
                    {
                        var child = new GameObject("Collision_" + shape.name);
                        child.transform.SetParent(instance.transform, false);
                        child.transform.localPosition = Vector3.zero;
                        child.transform.localRotation = Quaternion.identity;
                        child.transform.localScale = Vector3.one;
                        if (string.Equals(shape.kind, "capsule", StringComparison.OrdinalIgnoreCase))
                        {
                            var capsule = child.AddComponent<CapsuleCollider>();
                            capsule.center = shape.center;
                            capsule.radius = shape.radius;
                            capsule.height = shape.height;
                            capsule.direction = Mathf.Clamp(shape.direction, 0, 2);
                        }
                        else if (string.Equals(shape.kind, "box", StringComparison.OrdinalIgnoreCase))
                        {
                            if (shape.size.x <= 0 || shape.size.y <= 0 || shape.size.z <= 0)
                                throw new InvalidDataException("Non-positive box dimensions in " + profile.name + "/" + shape.name);
                            var box = child.AddComponent<BoxCollider>();
                            box.center = shape.center;
                            box.size = shape.size;
                        }
                        else throw new InvalidDataException("Unsupported collider kind " + shape.kind);
                    }

                    string target = Root + "/Prefabs/CollisionOptions/CSE_" + profile.name + "_PrimitiveCollision.prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, target);
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(target);
                    if (saved == null) throw new IOException("Could not save " + target);
                    int meshCount = saved.GetComponentsInChildren<MeshCollider>(true).Length;
                    int bodyCount = saved.GetComponentsInChildren<Rigidbody>(true).Length;
                    int shapeCount = saved.GetComponentsInChildren<Collider>(true).Length;
                    if (meshCount != 0 || bodyCount != 0 || shapeCount != profile.shapes.Length)
                        throw new InvalidDataException("Unexpected collider setup in " + target);
                    report.AppendLine("| " + profile.name + " | " + shapeCount + " | " + meshCount + " | " + bodyCount + " |");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }

            EnsureFolder(Root, "Documentation");
            File.WriteAllText(Root + "/Documentation/COLLISION_OPTIONS.md", report.ToString());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Material CreateStageMaterial(string name, Color color, float metallic, float smoothness)
        {
            string folder = Root + "/Materials/Showcase";
            EnsureFolder(Root + "/Materials", "Showcase");
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var pipeline = GraphicsSettings.currentRenderPipeline;
            string shaderName = pipeline == null ? "Standard" : "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Could not find stage shader " + shaderName);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor(pipeline == null ? "_Color" : "_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Font BuiltInFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) throw new InvalidOperationException("Unity's built-in runtime font is unavailable.");
            return font;
        }

        private static TextMesh AddLabel(string name, string text, Vector3 position, float size, Color color)
        {
            var label = new GameObject(name);
            label.transform.position = position;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = BuiltInFont();
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.45f;
            label.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            return mesh;
        }

        private static void SetStudioLighting(Color background)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.72f, .75f, .77f);
            RenderSettings.ambientEquatorColor = new Color(.52f, .55f, .57f);
            RenderSettings.ambientGroundColor = new Color(.28f, .30f, .32f);
            var main = new GameObject("Gallery_KeyLight").AddComponent<Light>();
            main.type = LightType.Directional; main.intensity = 1.25f; main.shadows = LightShadows.Soft;
            main.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var fill = new GameObject("Gallery_FillLight").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .65f; fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(18f, 145f, 0f);
            var rim = new GameObject("Gallery_RimLight").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = .28f; rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(24f, 36f, 0f);
        }

        private static Bounds RendererBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException(obj.name + " has no Renderer.");
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void BuildGallery()
        {
            var manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Editor/CSEManifest.json");
            if (manifestAsset == null) throw new FileNotFoundException("CSEManifest.json is missing.");
            var manifest = JsonUtility.FromJson<CSEManifest>(manifestAsset.text);
            if (manifest == null || manifest.props == null || manifest.props.Length != 12)
                throw new InvalidDataException("Expected 12 props in the manifest.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "InspectionGallery_NeutralFloor";
            floor.transform.position = new Vector3(0, -.10f, 0);
            floor.transform.localScale = new Vector3(12.2f, .20f, 10.2f);
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                CreateStageMaterial("CSE_GalleryFloor", new Color(.27f, .29f, .30f), .02f, .32f);

            float[] xs = { -4.2f, -1.4f, 1.4f, 4.2f };
            float[] zs = { 2.75f, 0f, -2.75f };
            for (int i = 0; i < manifest.props.Length; i++)
            {
                var spec = manifest.props[i];
                string path = Root + "/Prefabs/CSE_" + spec.name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new FileNotFoundException("Missing " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Gallery_Item_" + spec.name;
                if (spec.name == "RoadSign" || spec.name == "PortableWorkLight")
                    instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * instance.transform.rotation;
                var bounds = RendererBounds(instance);
                int row, column;
                switch (spec.name)
                {
                    case "TrafficCone": row = 0; column = 0; break;
                    case "SafetyBarrier": row = 0; column = 1; break;
                    case "HardHat": row = 0; column = 2; break;
                    case "Wheelbarrow": row = 0; column = 3; break;
                    case "Shovel": row = 1; column = 0; break;
                    case "CementBag": row = 1; column = 1; break;
                    case "Toolbox": row = 1; column = 2; break;
                    case "CautionSign": row = 1; column = 3; break;
                    case "RoadSign": row = 2; column = 0; break;
                    case "PortableWorkLight": row = 2; column = 1; break;
                    case "BrickStack": row = 2; column = 2; break;
                    case "RebarBundle": row = 2; column = 3; break;
                    default: throw new InvalidDataException("No gallery tile is assigned for " + spec.name);
                }
                float x = xs[column], z = zs[row];
                instance.transform.position += new Vector3(x, -bounds.min.y, z);
                bounds = RendererBounds(instance);
                AddLabel("Gallery_Label_" + spec.name, spec.name,
                    new Vector3(x, .20f, bounds.max.z + .65f),
                    .06f, new Color(.06f, .08f, .10f));
            }

            // A one-metre upright reference makes the true-scale layout legible.
            var ruler = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ruler.name = "OneMetreScaleReference";
            ruler.transform.position = new Vector3(-5.45f, .5f, 3.85f);
            ruler.transform.localScale = new Vector3(.035f, 1f, .035f);
            ruler.GetComponent<MeshRenderer>().sharedMaterial =
                CreateStageMaterial("CSE_GalleryMarker", new Color(.95f, .53f, .12f), .05f, .3f);
            for (int j = 0; j <= 4; j++)
            {
                var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tick.name = "ScaleTick_" + j;
                tick.transform.position = new Vector3(-5.41f + (j % 2 == 0 ? .02f : 0f), j * .25f, 3.85f);
                tick.transform.localScale = new Vector3(.12f, .018f, .04f);
                tick.GetComponent<MeshRenderer>().sharedMaterial = ruler.GetComponent<MeshRenderer>().sharedMaterial;
            }
            AddLabel("ScaleLabel_1m", "1 m", new Vector3(-5.02f, 1.05f, 3.85f), .10f,
                new Color(.05f, .06f, .07f));

            var camObject = new GameObject("InspectionGallery_Camera");
            var camera = camObject.AddComponent<Camera>();
            camObject.tag = "MainCamera";
            camObject.transform.position = new Vector3(0f, 11.6f, 18f);
            camObject.transform.LookAt(new Vector3(0f, .2f, .1f));
            camera.orthographic = true;
            camera.orthographicSize = 3.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.78f, .81f, .83f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            SetStudioLighting(camera.backgroundColor);
            EditorSceneManager.SaveScene(scene, Root + "/Demo/CSE_InspectionGallery.unity");
            AssetDatabase.SaveAssets();
        }

        private static void BuildDetailGallery(string sceneName, string[] propNames, float pitch, float orthoSize)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float stageWidth = Mathf.Max(propNames.Length * pitch + 1.5f, 7.5f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = sceneName + "_NeutralFloor";
            floor.transform.position = new Vector3(0f, -.10f, 0f);
            floor.transform.localScale = new Vector3(stageWidth, .20f, Mathf.Max(orthoSize * 2f + .8f, 7.2f));
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                CreateStageMaterial("CSE_GalleryFloor", new Color(.27f, .29f, .30f), .02f, .32f);

            float startX = -pitch * (propNames.Length - 1) * .5f;
            for (int i = 0; i < propNames.Length; i++)
            {
                string name = propNames[i];
                string path = Root + "/Prefabs/CSE_" + name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new FileNotFoundException("Missing " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = sceneName + "_Item_" + name;
                if (name == "RoadSign" || name == "PortableWorkLight")
                    instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * instance.transform.rotation;
                var bounds = RendererBounds(instance);
                float x = startX + i * pitch;
                instance.transform.position += new Vector3(x, -bounds.min.y, 0f);
                bounds = RendererBounds(instance);
                AddLabel(sceneName + "_Label_" + name, name,
                    new Vector3(x, .20f, bounds.max.z + .55f), .06f, new Color(.06f, .08f, .10f));
            }

            var cameraObject = new GameObject(sceneName + "_Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, orthoSize * 1.35f, orthoSize * 2.15f);
            cameraObject.transform.LookAt(new Vector3(0f, .25f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.78f, .81f, .83f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            SetStudioLighting(camera.backgroundColor);
            EditorSceneManager.SaveScene(scene, Root + "/Demo/" + sceneName + ".unity");
            AssetDatabase.SaveAssets();
        }

        private static int ValidateUpgrade()
        {
            int errors = 0;
            var report = new StringBuilder();
            report.AppendLine("# Construction Site Essentials 1.1.0 — Unity validation");
            report.AppendLine();
            report.AppendLine("- Unity Editor: " + Application.unityVersion);
            report.AppendLine("- Render pipeline: " + (GraphicsSettings.currentRenderPipeline == null ? "Built-In" : GraphicsSettings.currentRenderPipeline.name));
            report.AppendLine("- Checked at UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine();
            report.AppendLine("| Asset | Triangles | UV2 vertices | UV2 result | Default collider |");
            report.AppendLine("|---|---:|---:|---|---|");
            var manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Editor/CSEManifest.json");
            var manifest = JsonUtility.FromJson<CSEManifest>(manifestAsset.text);
            foreach (var spec in manifest.props)
            {
                string modelPath = Root + "/Models/CSE_" + spec.name + ".fbx";
                var mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CSE_" + spec.name + ".prefab");
                string uvResult = "FAIL";
                int uv2Count = mesh == null ? 0 : mesh.uv2.Length;
                if (mesh != null && mesh.vertexCount > 0 && uv2Count == mesh.vertexCount &&
                    mesh.uv2.All(uv => !float.IsNaN(uv.x) && !float.IsNaN(uv.y) &&
                                      uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f))
                    uvResult = "PASS: finite, in-range, vertex-aligned";
                if (uvResult.StartsWith("FAIL", StringComparison.Ordinal)) errors++;
                ulong indices = 0;
                if (mesh != null) for (int s = 0; s < mesh.subMeshCount; s++) indices += mesh.GetIndexCount(s);
                string collider = prefab != null && prefab.GetComponent<MeshCollider>() != null &&
                    prefab.GetComponent<MeshCollider>().sharedMesh != null ? "Static MeshCollider retained" : "FAIL";
                if (collider == "FAIL") errors++;
                report.AppendLine("| " + spec.name + " | " + (indices / 3) + " | " + uv2Count + " | " +
                    uvResult + " | " + collider + " |");
            }
            var lensMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/BuiltIn/CSE_WorklightLens.mat");
            bool lensEmission = lensMaterial != null && lensMaterial.IsKeywordEnabled("_EMISSION") &&
                lensMaterial.GetTexture("_EmissionMap") != null && lensMaterial.GetColor("_EmissionColor").maxColorComponent > 0f &&
                (lensMaterial.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0;
            if (!lensEmission) errors++;
            report.AppendLine();
            report.AppendLine("- Built-In WorkLight lens emission: " + (lensEmission ? "PASS" : "FAIL") +
                "; keyword=" + (lensMaterial != null && lensMaterial.IsKeywordEnabled("_EMISSION")) +
                "; emission map=" + (lensMaterial != null && lensMaterial.GetTexture("_EmissionMap") != null) +
                "; GI flags=" + (lensMaterial == null ? "(missing)" : lensMaterial.globalIlluminationFlags.ToString()));
            report.AppendLine();
            report.AppendLine("UV2 presence/range is a data check only; it does not prove that a lightmap bake is artifact-free. The original 1.0.1 prefabs and their static MeshColliders remain unchanged. Primitive collision variants are optional approximations.");
            File.WriteAllText(Root + "/Documentation/UPGRADE_VALIDATION.md", report.ToString());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return errors;
        }

        private static void CapturePreview(string scenePath, string fileName)
        {
            var active = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No camera found in " + active.path);
            const int width = 1920, height = 1080;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false, true);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string outputDirectory = Environment.GetEnvironmentVariable("CSE_PREVIEW_DIR");
                if (string.IsNullOrEmpty(outputDirectory)) outputDirectory = "QA";
                string output = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
                output = Path.GetFullPath(output);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output, image.EncodeToPNG());
                Debug.Log("CSE gallery preview captured by Unity Camera.Render: " + output);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
#endif

