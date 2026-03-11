#nullable enable

namespace PampaSkylines.PC.Editor
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class PcBootstrapSceneBuilder
{
    private const string MaterialsFolder = "Assets/Game/PC/Materials";
    private const string ResourcesFolder = "Assets/Game/PC/Resources";
    private const string ScenesFolder = "Assets/Game/PC/Scenes";
    private const string SettingsFolder = "Assets/Game/PC/Settings";
    private const string Cc0ArtFolder = "Assets/Game/PC/Art/CC0";
    private const string ThemeAssetPath = ResourcesFolder + "/PcCleanSimTheme.asset";
    private const string PipelineAssetPath = SettingsFolder + "/PcUniversalRenderPipeline.asset";
    private const string RendererAssetPath = SettingsFolder + "/PcUniversalRenderer.asset";
    private const string PostProcessProfilePath = SettingsFolder + "/PcPostProcessVolumeProfile.asset";

    [MenuItem("Pampa Skylines/PC/Rebuild Bootstrap Scene")]
    public static void RebuildBootstrapScene()
    {
        EnsureFolders();
        EnsureRenderPipelineConfigured();
        var theme = EnsureThemeAsset();
        BuildScene(theme);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"PC bootstrap scene rebuilt at '{PcBootstrapRuntime.ScenePath}'.");
    }

    [MenuItem("Pampa Skylines/PC/Create Clean Sim Theme")]
    public static void CreateCleanSimTheme()
    {
        EnsureFolders();
        EnsureThemeAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void BuildAll()
    {
        RebuildBootstrapScene();
    }

    private static UniversalRenderPipelineAsset EnsureRenderPipelineConfigured()
    {
        var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
        if (pipelineAsset is null)
        {
            var rendererData = CreateRendererAsset(RendererAssetPath);
            pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
        }

        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        var originalQualityLevel = QualitySettings.GetQualityLevel();
        for (var qualityIndex = 0; qualityIndex < QualitySettings.names.Length; qualityIndex++)
        {
            QualitySettings.SetQualityLevel(qualityIndex, false);
            QualitySettings.renderPipeline = pipelineAsset;
        }

        QualitySettings.SetQualityLevel(originalQualityLevel, false);
        QualitySettings.renderPipeline = pipelineAsset;
        return pipelineAsset;
    }

    private static void BuildScene(PcVisualTheme theme)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var bootstrapObject = new GameObject("PcBootstrap");
        var controller = bootstrapObject.AddComponent<PcBootstrapController>();

        var cameraObject = new GameObject("PcCamera");
        cameraObject.transform.SetParent(bootstrapObject.transform, false);
        cameraObject.transform.SetPositionAndRotation(new Vector3(12f, 18f, -12f), Quaternion.Euler(55f, 45f, 0f));
        var camera = cameraObject.AddComponent<Camera>();
        camera.backgroundColor = theme.CameraBackgroundColor;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.orthographic = true;
        camera.orthographicSize = 14f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<PcTopDownCameraController>();

        var lightObject = new GameObject("PcKeyLight");
        lightObject.transform.SetParent(bootstrapObject.transform, false);
        lightObject.transform.SetPositionAndRotation(new Vector3(0f, 12f, 0f), Quaternion.Euler(50f, -25f, 0f));
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.08f;
        light.color = new Color(1f, 0.97f, 0.93f);
        light.shadows = LightShadows.Soft;

        var volumeObject = new GameObject("GlobalVolume");
        volumeObject.transform.SetParent(bootstrapObject.transform, false);
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = EnsurePostProcessProfile();

        var worldViewObject = new GameObject("PcWorldView");
        worldViewObject.transform.SetParent(bootstrapObject.transform, false);
        var worldView = worldViewObject.AddComponent<PcWorldView>();

        var canvasObject = new GameObject("PcHudCanvas");
        canvasObject.transform.SetParent(bootstrapObject.transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.55f;
        canvasObject.AddComponent<GraphicRaycaster>();
        var hud = canvasObject.AddComponent<PcHudController>();

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();

        AssignObjectReference(controller, "visualTheme", theme);
        AssignObjectReference(controller, "worldView", worldView);
        AssignObjectReference(controller, "hud", hud);
        AssignObjectReference(controller, "managedCamera", camera);
        AssignObjectReference(controller, "managedLight", light);
        AssignObjectReference(worldView, "visualTheme", theme);

        EditorSceneManager.SaveScene(scene, PcBootstrapRuntime.ScenePath, true);
        if (!Application.isBatchMode)
        {
            EditorSceneManager.OpenScene(PcBootstrapRuntime.ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(PcBootstrapRuntime.ScenePath);
        }
    }

    private static PcVisualTheme EnsureThemeAsset()
    {
        var groundMaterial = EnsureOpaqueMaterial($"{MaterialsFolder}/PcGround.mat");
        var gridMaterial = EnsureTransparentMaterial($"{MaterialsFolder}/PcGrid.mat");
        var roadMaterial = EnsureOpaqueMaterial($"{MaterialsFolder}/PcRoad.mat");
        var zoneMaterial = EnsureTransparentMaterial($"{MaterialsFolder}/PcZoneOverlay.mat");
        var buildingMaterial = EnsureOpaqueMaterial($"{MaterialsFolder}/PcBuilding.mat");
        var hoverMaterial = EnsureTransparentMaterial($"{MaterialsFolder}/PcHover.mat");
        var dragMaterial = EnsureTransparentMaterial($"{MaterialsFolder}/PcDragPreview.mat");

        ApplyPbrSet(
            groundMaterial,
            $"{Cc0ArtFolder}/Grass004_1K-JPG/Grass004_1K-JPG_Color.jpg",
            $"{Cc0ArtFolder}/Grass004_1K-JPG/Grass004_1K-JPG_NormalGL.jpg",
            $"{Cc0ArtFolder}/Grass004_1K-JPG/Grass004_1K-JPG_Roughness.jpg",
            new Vector2(6f, 6f));
        ApplyPbrSet(
            roadMaterial,
            $"{Cc0ArtFolder}/Asphalt014_1K-JPG/Asphalt014_1K-JPG_Color.jpg",
            $"{Cc0ArtFolder}/Asphalt014_1K-JPG/Asphalt014_1K-JPG_NormalGL.jpg",
            $"{Cc0ArtFolder}/Asphalt014_1K-JPG/Asphalt014_1K-JPG_Roughness.jpg",
            new Vector2(5f, 5f));
        ApplyPbrSet(
            buildingMaterial,
            $"{Cc0ArtFolder}/Plaster005_1K-JPG/Plaster005_1K-JPG_Color.jpg",
            $"{Cc0ArtFolder}/Plaster005_1K-JPG/Plaster005_1K-JPG_NormalGL.jpg",
            $"{Cc0ArtFolder}/Plaster005_1K-JPG/Plaster005_1K-JPG_Roughness.jpg",
            new Vector2(2f, 2f));

        var theme = AssetDatabase.LoadAssetAtPath<PcVisualTheme>(ThemeAssetPath);
        if (theme is null)
        {
            theme = ScriptableObject.CreateInstance<PcVisualTheme>();
            AssetDatabase.CreateAsset(theme, ThemeAssetPath);
        }

        theme.GroundMaterial = groundMaterial;
        theme.GridLineMaterial = gridMaterial;
        theme.RoadMaterial = roadMaterial;
        theme.ZoneMaterial = zoneMaterial;
        theme.BuildingMaterial = buildingMaterial;
        theme.HoverMaterial = hoverMaterial;
        theme.DragPreviewMaterial = dragMaterial;

        theme.CameraBackgroundColor = new Color(0.76f, 0.82f, 0.88f);
        theme.GroundColor = new Color(0.52f, 0.58f, 0.48f);
        theme.GridMinorColor = new Color(0.48f, 0.52f, 0.49f, 0.16f);
        theme.GridMajorColor = new Color(0.34f, 0.38f, 0.36f, 0.32f);
        theme.RoadColor = new Color(0.16f, 0.17f, 0.19f);
        theme.RoadCongestedColor = new Color(0.78f, 0.36f, 0.20f);
        theme.ResidentialColor = new Color(0.26f, 0.62f, 0.33f, 0.36f);
        theme.CommercialColor = new Color(0.20f, 0.53f, 0.73f, 0.36f);
        theme.IndustrialColor = new Color(0.68f, 0.50f, 0.17f, 0.36f);
        theme.OfficeColor = new Color(0.31f, 0.44f, 0.68f, 0.36f);
        theme.ElectricityColor = new Color(0.90f, 0.69f, 0.18f);
        theme.WaterColor = new Color(0.18f, 0.57f, 0.82f);
        theme.SewageColor = new Color(0.41f, 0.51f, 0.24f);
        theme.WasteColor = new Color(0.51f, 0.44f, 0.18f);
        theme.FireColor = new Color(0.79f, 0.26f, 0.21f);
        theme.PoliceColor = new Color(0.19f, 0.30f, 0.65f);
        theme.HealthColor = new Color(0.16f, 0.61f, 0.47f);
        theme.EducationColor = new Color(0.68f, 0.40f, 0.21f);
        theme.HoverOutlineColor = new Color(0.98f, 0.98f, 0.98f, 0.95f);
        theme.DragFillColor = new Color(0.17f, 0.60f, 0.84f, 0.20f);
        theme.DragOutlineColor = new Color(0.15f, 0.56f, 0.79f, 0.85f);
        theme.HudPanelColor = new Color(0.08f, 0.10f, 0.13f, 0.90f);
        theme.HudPanelSecondaryColor = new Color(0.12f, 0.15f, 0.19f, 0.93f);
        theme.HudCardColor = new Color(0.15f, 0.18f, 0.22f, 0.97f);
        theme.HudAccentColor = new Color(0.23f, 0.66f, 0.80f, 1f);
        theme.HudTextColor = new Color(0.92f, 0.95f, 0.97f, 1f);
        theme.HudMutedTextColor = new Color(0.66f, 0.73f, 0.79f, 1f);
        theme.HudButtonColor = new Color(0.20f, 0.25f, 0.31f, 1f);
        theme.HudButtonActiveColor = new Color(0.25f, 0.61f, 0.75f, 1f);
        theme.HudButtonTextColor = new Color(0.96f, 0.97f, 0.98f, 1f);
        theme.HudWarningColor = new Color(0.93f, 0.63f, 0.22f, 1f);
        theme.HudErrorColor = new Color(0.90f, 0.36f, 0.32f, 1f);
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static void ApplyPbrSet(Material material, string baseMapPath, string normalMapPath, string roughnessPath, Vector2 tiling)
    {
        var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(baseMapPath);
        var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);
        var roughnessMap = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
        if (baseMap is null || normalMap is null || roughnessMap is null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", baseMap);
            material.SetTextureScale("_BaseMap", tiling);
        }

        if (material.HasProperty("_BumpMap"))
        {
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_BumpMap", normalMap);
            material.SetTextureScale("_BumpMap", tiling);
            SetIfPresent(material, "_BumpScale", 0.5f);
        }

        if (material.HasProperty("_MetallicGlossMap"))
        {
            material.SetTexture("_MetallicGlossMap", roughnessMap);
            material.SetTextureScale("_MetallicGlossMap", tiling);
        }

        if (material.HasProperty("_OcclusionMap"))
        {
            material.SetTexture("_OcclusionMap", roughnessMap);
            material.SetTextureScale("_OcclusionMap", tiling);
            SetIfPresent(material, "_OcclusionStrength", 0.4f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            SetIfPresent(material, "_Smoothness", 0.22f);
        }

        EditorUtility.SetDirty(material);
    }

    private static VolumeProfile EnsurePostProcessProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
        if (profile is null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessProfilePath);
        }

        if (!profile.TryGet<ColorAdjustments>(out var colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
        }

        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = 0.25f;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.contrast.value = 12f;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = -6f;
        colorAdjustments.colorFilter.overrideState = true;
        colorAdjustments.colorFilter.value = new Color(1.02f, 1.01f, 0.98f, 1f);

        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.1f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.24f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.65f;

        if (!profile.TryGet<Vignette>(out var vignette))
        {
            vignette = profile.Add<Vignette>(true);
        }

        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.18f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.42f;

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static Material EnsureOpaqueMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material is null)
        {
            material = new Material(ResolveShader("Universal Render Pipeline/Lit", "Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.shader != ResolveShader("Universal Render Pipeline/Lit", "Standard"))
        {
            material.shader = ResolveShader("Universal Render Pipeline/Lit", "Standard");
        }

        material.enableInstancing = true;
        SetIfPresent(material, "_Surface", 0f);
        SetIfPresent(material, "_Blend", 0f);
        SetIfPresent(material, "_ZWrite", 1f);
        SetIfPresent(material, "_Cull", (float)CullMode.Back);
        SetIfPresent(material, "_Smoothness", 0.34f);
        SetIfPresent(material, "_Metallic", 0.05f);
        SetColorIfPresent(material, new Color(1f, 1f, 1f, 1f));
        material.renderQueue = -1;
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureTransparentMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material is null)
        {
            material = new Material(ResolveShader("Universal Render Pipeline/Unlit", "Sprites/Default", "Unlit/Color"));
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.shader != ResolveShader("Universal Render Pipeline/Unlit", "Sprites/Default", "Unlit/Color"))
        {
            material.shader = ResolveShader("Universal Render Pipeline/Unlit", "Sprites/Default", "Unlit/Color");
        }

        material.enableInstancing = true;
        SetIfPresent(material, "_Surface", 1f);
        SetIfPresent(material, "_Blend", 0f);
        SetIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetIfPresent(material, "_ZWrite", 0f);
        SetIfPresent(material, "_Cull", (float)CullMode.Back);
        SetIfPresent(material, "_Smoothness", 0.18f);
        SetColorIfPresent(material, new Color(1f, 1f, 1f, 0.6f));
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader ResolveShader(params string[] shaderNames)
    {
        foreach (var shaderName in shaderNames)
        {
            var shader = Shader.Find(shaderName);
            if (shader is not null)
            {
                return shader;
            }
        }

        throw new FileNotFoundException($"Unable to resolve any shader from: {string.Join(", ", shaderNames)}");
    }

    private static void SetIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfPresent(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void AssignObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName)!.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void EnsureBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        var existingIndex = scenes.FindIndex(scene => scene.path == PcBootstrapRuntime.ScenePath);
        var bootstrapScene = new EditorBuildSettingsScene(PcBootstrapRuntime.ScenePath, true);
        if (existingIndex >= 0)
        {
            scenes[existingIndex] = bootstrapScene;
        }
        else
        {
            scenes.Insert(0, bootstrapScene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Game/PC");
        EnsureFolder(MaterialsFolder);
        EnsureFolder(ResourcesFolder);
        EnsureFolder(ScenesFolder);
        EnsureFolder(SettingsFolder);
        EnsureFolder(Cc0ArtFolder);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        var folderName = Path.GetFileName(path);
        if (parent is null)
        {
            throw new InvalidDataException($"Unable to determine parent folder for '{path}'.");
        }

        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new InvalidDataException($"Parent folder for '{path}' resolved to an empty path.");
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static ScriptableRendererData CreateRendererAsset(string path)
    {
        var existingAsset = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
        if (existingAsset is not null)
        {
            return existingAsset;
        }

        var createRendererAssetMethod = typeof(UniversalRenderPipelineAsset).GetMethod(
            "CreateRendererAsset",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (createRendererAssetMethod is null)
        {
            throw new MissingMethodException(typeof(UniversalRenderPipelineAsset).FullName, "CreateRendererAsset");
        }

        var rendererAsset = createRendererAssetMethod.Invoke(
            null,
            new object[]
            {
                path,
                RendererType.UniversalRenderer,
                false,
                "Renderer"
            }) as ScriptableRendererData;

        if (rendererAsset is null)
        {
            throw new InvalidDataException($"Unable to create URP renderer asset at '{path}'.");
        }

        return rendererAsset;
    }
}
}
