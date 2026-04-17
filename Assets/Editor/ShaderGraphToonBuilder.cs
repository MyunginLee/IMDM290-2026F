using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ShaderGraphToonBuilder
{
    private const string SampleRootFolder = "Assets/ShaderGraphCaptainProton";
    private const string SampleFolder = SampleRootFolder + "/CustomLighting";
    private const string GraphPath = SampleFolder + "/Custom Lighting Toon.shadergraph";
    private const string ScenePath = "Assets/Scenes/ShaderGraphToon.unity";
    private const string MaterialsFolder = "Assets/Materials/ShaderGraphToon";
    private const string ClassicMaterialPath = MaterialsFolder + "/M_SG_Classic.mat";
    private const string CoolMaterialPath = MaterialsFolder + "/M_SG_Cool.mat";
    private const string WarmMaterialPath = MaterialsFolder + "/M_SG_Warm.mat";
    private const string FloorMaterialPath = MaterialsFolder + "/M_SG_Floor.mat";

    [MenuItem("Tools/Toon Demo/Rebuild Shader Graph Toon Scene")]
    public static void CreateSceneFromMenu()
    {
        CreateScene();
    }

    public static void CreateSceneBatch()
    {
        CreateScene();
        EditorApplication.Exit(0);
    }

    private static void CreateScene()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "ShaderGraphToon");
        EnsureFolder("Assets", "ShaderGraphCaptainProton");

        EnsureGraphAssets();

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
        if (shader == null)
        {
            throw new FileNotFoundException("Could not import Shader Graph at " + GraphPath);
        }

        Material classicMaterial = CreateOrUpdateMaterial(
            ClassicMaterialPath,
            shader,
            new Color(0.64f, 0.64f, 0.64f),
            0.14f,
            Color.black);
        Material coolMaterial = CreateOrUpdateMaterial(
            CoolMaterialPath,
            shader,
            new Color(0.62f, 0.78f, 0.87f),
            0.20f,
            new Color(0.02f, 0.03f, 0.04f));
        Material warmMaterial = CreateOrUpdateMaterial(
            WarmMaterialPath,
            shader,
            new Color(0.95f, 0.78f, 0.50f),
            0.28f,
            new Color(0.08f, 0.04f, 0.01f));
        Material floorMaterial = CreateOrUpdateMaterial(
            FloorMaterialPath,
            shader,
            new Color(0.73f, 0.70f, 0.63f),
            0.08f,
            Color.black);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ShaderGraphToon";

        CreateCamera();
        CreateLighting();
        CreateFloor(floorMaterial);
        CreateShowcase(classicMaterial, coolMaterial, warmMaterial);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettingsScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Shader Graph toon scene rebuilt at " + ScenePath);
    }

    private static void EnsureGraphAssets()
    {
        string packageGraph = FindPackageGraphSource();
        if (!File.Exists(packageGraph))
        {
            throw new FileNotFoundException("Could not find URP Custom Lighting Toon sample graph.");
        }

        string packageFolder = Path.GetDirectoryName(packageGraph);
        if (string.IsNullOrEmpty(packageFolder))
        {
            throw new DirectoryNotFoundException("Could not resolve sample graph folder.");
        }

        CopyDirectoryRecursive(packageFolder, Path.Combine(Directory.GetCurrentDirectory(), SampleFolder.Replace('/', Path.DirectorySeparatorChar)));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static string FindPackageGraphSource()
    {
        string[] matches = Directory.GetFiles(
            Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache"),
            "Custom Lighting Toon.shadergraph",
            SearchOption.AllDirectories);

        return matches.Length == 0 ? string.Empty : matches[0];
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));
        }

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, file.Replace(sourceDir, destinationDir), true);
        }
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Color baseColor, float smoothness, Color emission)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);

        SetColorIfPresent(material, baseColor, "_Base_Color", "_BaseColor");
        SetFloatIfPresent(material, smoothness, "_Smoothness");
        SetColorIfPresent(material, emission, "_Emission", "_Emissive_Color");
        SetFloatIfPresent(material, 1.0f, "_Alpha");

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    private static void SetColorIfPresent(Material material, Color color, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }
    }

    private static void SetFloatIfPresent(Material material, float value, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }

    private static void CreateCamera()
    {
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";

        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.90f, 0.92f, 0.91f);
        camera.fieldOfView = 34f;
        camera.transform.position = new Vector3(0f, 2.2f, -8.2f);
        camera.transform.rotation = Quaternion.Euler(11f, 0f, 0f);

        cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
    }

    private static void CreateLighting()
    {
        var lightRig = new GameObject("Light Rig");

        var keyLight = new GameObject("Directional Light");
        keyLight.transform.SetParent(lightRig.transform);
        keyLight.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

        var directional = keyLight.AddComponent<Light>();
        directional.type = LightType.Directional;
        directional.color = Color.white;
        directional.intensity = 1.15f;
        directional.shadows = LightShadows.Soft;

        CreatePointLight(lightRig.transform, "Warm Fill", new Vector3(3.2f, 2.3f, -0.6f), new Color(1.0f, 0.80f, 0.55f), 3.4f, 8f);
        CreatePointLight(lightRig.transform, "Sky Fill", new Vector3(-3.4f, 1.7f, 1.8f), new Color(0.60f, 0.77f, 0.92f), 3.0f, 7f);
    }

    private static void CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        var lightGo = new GameObject(name);
        lightGo.transform.SetParent(parent);
        lightGo.transform.position = position;

        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
    }

    private static void CreateFloor(Material material)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, -0.55f, 0f);
        floor.transform.localScale = new Vector3(9f, 0.6f, 8f);
        floor.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateShowcase(Material left, Material center, Material right)
    {
        CreatePedestal(new Vector3(-2.7f, -0.15f, 0f), left, PrimitiveType.Sphere, new Vector3(1.5f, 1.5f, 1.5f));
        CreatePedestal(new Vector3(0f, -0.05f, 0f), center, PrimitiveType.Capsule, new Vector3(1.2f, 1.8f, 1.2f));
        CreatePedestal(new Vector3(2.7f, -0.15f, 0f), right, PrimitiveType.Sphere, new Vector3(1.5f, 1.5f, 1.5f));
    }

    private static void CreatePedestal(Vector3 position, Material material, PrimitiveType primitiveType, Vector3 scale)
    {
        var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = primitiveType + " Pedestal";
        pedestal.transform.position = new Vector3(position.x, -0.28f, position.z);
        pedestal.transform.localScale = new Vector3(0.8f, 0.27f, 0.8f);
        pedestal.GetComponent<Renderer>().sharedMaterial = material;

        var mesh = GameObject.CreatePrimitive(primitiveType);
        mesh.name = primitiveType.ToString();
        mesh.transform.position = position + new Vector3(0f, 0.85f, 0f);
        mesh.transform.localScale = scale;
        mesh.GetComponent<Renderer>().sharedMaterial = material;
        mesh.AddComponent<ToonDemoSpinner>();
    }

    private static void EnsureBuildSettingsScene()
    {
        var cleanedScenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == "Assets/Scenes/ShaderToon.unity" ||
                scene.path == "Assets/Scenes/ShaderToonDemo.unity" ||
                scene.path == "Assets/Scenes/ShaderGraphToonDemo.unity")
            {
                continue;
            }

            cleanedScenes.Add(scene);
        }

        foreach (EditorBuildSettingsScene scene in cleanedScenes)
        {
            if (scene.path == ScenePath)
            {
                EditorBuildSettings.scenes = cleanedScenes.ToArray();
                return;
            }
        }

        cleanedScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = cleanedScenes.ToArray();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
