using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ShowcaseVisualsBuilder
{
    private const string VfxFolder = "Assets/Materials/VFX";

    [MenuItem("RacePrototype/Apply Social Media Showcase VFX")]
    public static void ApplyShowcaseVisuals()
    {
        if (!Directory.Exists(VfxFolder))
        {
            Directory.CreateDirectory(VfxFolder);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Garante mapa de normais de chuva e atualiza o Asfalto Molhado
        RainTextureGenerator.GenerateRippleTexture();
        UpgradeAsphaltToWetSpecular();

        // 2. Cria Materiais HDR de Particulas, Rastros e Embacamento Radial de Turbo
        Material neonRibbonMat = CreateParticleMaterial("Mat_VFX_TaillightRibbon", new Color(1.4f, 0.08f, 0.18f, 0.65f), true);
        Material skidMat = CreateParticleMaterial("Mat_VFX_TireSkid", new Color(0.03f, 0.03f, 0.04f, 0.68f), false);
        Material sprayMat = CreateParticleMaterial("Mat_VFX_WaterSpray", new Color(0.65f, 0.75f, 0.85f, 0.16f), false);
        Material nitroMat = CreateParticleMaterial("Mat_VFX_NitroFlame", new Color(0.15f, 1.1f, 2.0f, 0.75f), true);
        Material sparkMat = CreateParticleMaterial("Mat_VFX_Sparks", new Color(1.8f, 0.85f, 0.18f, 0.85f), true);
        Material rainMat = CreateParticleMaterial("Mat_VFX_RainStreaks", new Color(0.65f, 0.8f, 1.0f, 0.22f), true);
        Material turboBlurMat = CreateTurboScreenBlurMaterial();

        // 3. Configura Iluminacao Dramatica Noturna e Refletores de Pista
        SetupDramaticTrackLighting();

        // 4. Configura URP Global Volume (Bloom, ACES, Vignette, Lens Distortion, Motion Blur, Chromatic Aberration)
        Volume globalVol = SetupURPPostProcessingVolume();

        // 5. Ativa Post-Processing, Opaque Texture e Anti-Aliasing na Camera Isometrica Principal
        ConfigureMainCameraURP();

        // 6. Equipa Player_Car e AI_Opponent_Car com o pacote completo de VFX
        AttachCarVFX("Player_Car", neonRibbonMat, skidMat, sprayMat, nitroMat, sparkMat, new Color(3.5f, 0.1f, 0.25f, 1f));
        AttachCarVFX("AI_Opponent_Car", neonRibbonMat, skidMat, sprayMat, nitroMat, sparkMat, new Color(3.5f, 0.1f, 0.25f, 1f));

        // 7. Instancia Sistema de Chuva Isometrica e Controlador de Embacamento de Turbo
        SetupIsometricRainAndTurboBlur(rainMat, turboBlurMat, globalVol);

        // 8. Salva Cena e Assets
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SHOWCASE VFX + TURBO BLUR] Habilidade de Turbo com embacamento radial de tela configurado!");
    }

    private static Material CreateTurboScreenBlurMaterial()
    {
        string path = $"{VfxFolder}/Mat_VFX_TurboScreenBlur.mat";
        Shader shader = Shader.Find("Custom/RadialTurboBlur") ?? Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetFloat("_BlurStrength", 0f);
        mat.SetFloat("_BlurWidth", 0.24f);
        mat.SetFloat("_CenterFocusRadius", 0.16f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void UpgradeAsphaltToWetSpecular()
    {
        Material asphalt = TrackMaterialBuilder.GetOrCreateAsphaltMaterial();
        Texture2D rippleNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/Tex_RainRipples_Normal.png");
        if (asphalt != null)
        {
            if (asphalt.shader.name.Contains("Universal Render Pipeline"))
            {
                asphalt.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.09f, 1f));
                asphalt.SetFloat("_Metallic", 0.12f);
                asphalt.SetFloat("_Smoothness", 0.78f);
                if (rippleNormal != null)
                {
                    asphalt.SetTexture("_BumpMap", rippleNormal);
                    asphalt.SetTextureScale("_BumpMap", new Vector2(35f, 35f));
                    asphalt.SetFloat("_BumpScale", 0.45f);
                    asphalt.EnableKeyword("_NORMALMAP");
                }
            }
            EditorUtility.SetDirty(asphalt);
        }
    }

    private static Material CreateParticleMaterial(string name, Color hdrColor, bool additive)
    {
        string path = $"{VfxFolder}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", hdrColor);
        mat.SetColor("_Color", hdrColor);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void SetupDramaticTrackLighting()
    {
        Material skyboxMat = CreateOrUpdateSky82SkyboxMaterial();
        if (skyboxMat != null)
        {
            RenderSettings.skybox = skyboxMat;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.45f, 0.48f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.88f, 0.60f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.34f, 0.46f);
        RenderSettings.ambientIntensity = 1.25f;
        RenderSettings.reflectionIntensity = 1.2f;

        GameObject dirLightGo = GameObject.Find("Directional Light");
        if (dirLightGo != null)
        {
            Light dirLight = dirLightGo.GetComponent<Light>();
            dirLight.color = new Color(1.0f, 0.82f, 0.64f);
            dirLight.intensity = 1.65f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.72f;
            dirLightGo.transform.rotation = Quaternion.Euler(38f, 48f, 0f);
        }

        GameObject probeGo = GameObject.Find("Track_Reflection_Probe");
        if (probeGo != null)
        {
            ReflectionProbe probe = probeGo.GetComponent<ReflectionProbe>();
            if (probe != null)
            {
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                probe.intensity = 1.2f;
                probe.boxProjection = true;
            }
        }

        GameObject oldLights = GameObject.Find("Showcase_Track_Lights");
        if (oldLights != null) Object.DestroyImmediate(oldLights);
        GameObject lightsRoot = new GameObject("Showcase_Track_Lights");

        // Luz Direcional Secundaria de Preenchimento (Sky Fill Light) para iluminar sombras do cenario
        GameObject fillLightGo = new GameObject("Sky_Fill_Directional_Light");
        fillLightGo.transform.SetParent(lightsRoot.transform, false);
        fillLightGo.transform.rotation = Quaternion.Euler(50f, -132f, 0f);
        Light fillLight = fillLightGo.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.55f, 0.65f, 0.95f);
        fillLight.intensity = 0.55f;
        fillLight.shadows = LightShadows.None;

        // Refletor do Portico de Largada
        CreateTrackFloodlight(lightsRoot.transform, "Gantry_Spotlight", new Vector3(0f, 10.5f, -22f), new Vector3(62f, 0f, 0f), new Color(1.0f, 0.68f, 0.32f), 14f, 42f, 70f);

        // Refletores nos 4 Cantos do Circuito
        Vector3[] cornerPositions = {
            new Vector3(32f, 11f, 32f),
            new Vector3(-32f, 11f, 32f),
            new Vector3(-32f, 11f, -32f),
            new Vector3(32f, 11f, -32f)
        };

        for (int i = 0; i < cornerPositions.Length; i++)
        {
            Color floodCol = (i % 2 == 0) ? new Color(1.0f, 0.65f, 0.35f) : new Color(0.45f, 0.72f, 1.0f);
            Vector3 euler = new Vector3(52f, 45f + i * 90f, 0f);
            CreateTrackFloodlight(lightsRoot.transform, $"Corner_Floodlight_{i}", cornerPositions[i], euler, floodCol, 12f, 48f, 68f);
        }

        // Refletores nas 4 Retas Principais (Norte, Sul, Leste, Oeste)
        Vector3[] straightPositions = {
            new Vector3(0f, 11f, 34f),
            new Vector3(0f, 11f, -34f),
            new Vector3(34f, 11f, 0f),
            new Vector3(-34f, 11f, 0f)
        };
        Vector3[] straightEulers = {
            new Vector3(55f, 180f, 0f),
            new Vector3(55f, 0f, 0f),
            new Vector3(55f, 270f, 0f),
            new Vector3(55f, 90f, 0f)
        };
        for (int i = 0; i < straightPositions.Length; i++)
        {
            CreateTrackFloodlight(lightsRoot.transform, $"Straight_Floodlight_{i}", straightPositions[i], straightEulers[i], new Color(1.0f, 0.78f, 0.55f), 10.5f, 45f, 72f);
        }

        // Luzes de Destaque na Ilha Central
        GameObject centerLightGo = new GameObject("Center_Island_WarmGlow");
        centerLightGo.transform.SetParent(lightsRoot.transform, false);
        centerLightGo.transform.position = new Vector3(0f, 9f, 0f);
        Light centerPoint = centerLightGo.AddComponent<Light>();
        centerPoint.type = LightType.Point;
        centerPoint.color = new Color(1.0f, 0.72f, 0.48f);
        centerPoint.intensity = 6.5f;
        centerPoint.range = 45f;
    }

    public static Material CreateOrUpdateSky82SkyboxMaterial()
    {
        string panoPath = "Assets/Textures/Skybox/sky_82_2k.png";
        TextureImporter ti = AssetImporter.GetAtPath(panoPath) as TextureImporter;
        if (ti != null)
        {
            bool changed = false;
            if (ti.textureShape != TextureImporterShape.TextureCube)
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.FullCubemap;
                changed = true;
            }
            if (!ti.sRGBTexture || ti.maxTextureSize < 2048 || ti.wrapMode != TextureWrapMode.Clamp)
            {
                ti.sRGBTexture = true;
                ti.maxTextureSize = 2048;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                changed = true;
            }
            if (changed)
            {
                ti.SaveAndReimport();
            }
        }

        Cubemap skyCube = AssetDatabase.LoadAssetAtPath<Cubemap>(panoPath);
        string matPath = $"{VfxFolder}/Mat_Sky_82_Skybox.mat";
        Shader cubemapShader = Shader.Find("Skybox/Cubemap") ?? Shader.Find("Skybox/Panoramic");
        Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (skyMat == null)
        {
            skyMat = new Material(cubemapShader);
            AssetDatabase.CreateAsset(skyMat, matPath);
        }
        else
        {
            skyMat.shader = cubemapShader;
        }

        if (skyCube != null)
        {
            skyMat.SetTexture("_Tex", skyCube);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = skyCube;
        }
        skyMat.SetColor("_Tint", new Color(0.55f, 0.55f, 0.55f, 1f));
        skyMat.SetFloat("_Exposure", 1.18f);
        skyMat.SetFloat("_Rotation", 45f);
        EditorUtility.SetDirty(skyMat);
        return skyMat;
    }

    private static void CreateTrackFloodlight(Transform parent, string name, Vector3 pos, Vector3 euler, Color col, float intensity, float range, float angle)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(euler);

        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = col;
        l.intensity = intensity;
        l.range = range;
        l.spotAngle = angle;
        l.innerSpotAngle = angle * 0.5f;
    }

    private static Volume SetupURPPostProcessingVolume()
    {
        GameObject oldVol = GameObject.Find("Global_PostProcess_Volume");
        if (oldVol != null) Object.DestroyImmediate(oldVol);

        GameObject volGo = new GameObject("Global_PostProcess_Volume");
        Volume volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        string profilePath = "Assets/Materials/VFX/ShowcasePostProfile.asset";
        AssetDatabase.DeleteAsset(profilePath);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.name = "Bloom";
        bloom.threshold.Override(0.82f);
        bloom.intensity.Override(1.25f);
        bloom.scatter.Override(0.72f);
        bloom.highQualityFiltering.Override(true);
        AssetDatabase.AddObjectToAsset(bloom, profile);

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.name = "Tonemapping";
        tonemapping.mode.Override(TonemappingMode.ACES);
        AssetDatabase.AddObjectToAsset(tonemapping, profile);

        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.name = "ColorAdjustments";
        colorAdj.postExposure.Override(0.35f);
        colorAdj.contrast.Override(18f);
        colorAdj.saturation.Override(22f);
        AssetDatabase.AddObjectToAsset(colorAdj, profile);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.name = "Vignette";
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);
        AssetDatabase.AddObjectToAsset(vignette, profile);

        ChromaticAberration ca = profile.Add<ChromaticAberration>(true);
        ca.name = "ChromaticAberration";
        ca.intensity.Override(0.08f);
        AssetDatabase.AddObjectToAsset(ca, profile);

        LensDistortion ld = profile.Add<LensDistortion>(true);
        ld.name = "LensDistortion";
        ld.intensity.Override(0f);
        ld.scale.Override(1f);
        AssetDatabase.AddObjectToAsset(ld, profile);

        MotionBlur mb = profile.Add<MotionBlur>(true);
        mb.name = "MotionBlur";
        mb.quality.Override(MotionBlurQuality.High);
        mb.intensity.Override(0.15f);
        AssetDatabase.AddObjectToAsset(mb, profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        volume.sharedProfile = profile;
        return volume;
    }

    private static void ConfigureMainCameraURP()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        mainCam.backgroundColor = new Color(0.27f, 0.31f, 0.43f, 1f);
        mainCam.clearFlags = CameraClearFlags.Skybox;
        mainCam.allowHDR = true;

        UniversalAdditionalCameraData camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null)
        {
            camData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        camData.renderPostProcessing = true;
        camData.requiresColorOption = CameraOverrideOption.On;
        camData.requiresColorTexture = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
    }

    private static void AttachCarVFX(string carName, Material ribbonMat, Material skidMat, Material sprayMat, Material nitroMat, Material sparkMat, Color neonCol)
    {
        GameObject car = GameObject.Find(carName);
        if (car == null)
        {
            if (carName == "Player_Car") car = GameObject.Find("Car_Model_1");
            if (carName == "AI_Opponent_Car") car = GameObject.Find("Car_Model_2");
            if (car != null) car.name = carName;
        }
        if (car == null) return;

        ArcadeCarVFX vfx = car.GetComponent<ArcadeCarVFX>();
        if (vfx == null) vfx = car.AddComponent<ArcadeCarVFX>();

        vfx.trailNeonMat = ribbonMat;
        vfx.tireMarkMat = skidMat;
        vfx.smokeSprayMat = sprayMat;
        vfx.nitroFlameMat = nitroMat;
        vfx.sparkMat = sparkMat;
        vfx.taillightNeonColor = neonCol;
        EditorUtility.SetDirty(vfx);
    }

    private static void SetupIsometricRainAndTurboBlur(Material rainMat, Material turboBlurMat, Volume globalVol)
    {
        GameObject oldRain = GameObject.Find("Isometric_Rain_System");
        if (oldRain != null) Object.DestroyImmediate(oldRain);

        GameObject rainGo = new GameObject("Isometric_Rain_System");
        rainGo.transform.position = new Vector3(0f, 22f, 0f);
        rainGo.transform.rotation = Quaternion.Euler(78f, 45f, 0f);

        ParticleSystem ps = rainGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.85f;
        main.startSpeed = 32f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.maxParticles = 1200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 650f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(55f, 55f, 1f);

        var psRenderer = rainGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.lengthScale = 6.5f;
        psRenderer.sharedMaterial = rainMat;

        IsometricWeatherAndPostFX controller = rainGo.AddComponent<IsometricWeatherAndPostFX>();
        controller.globalVolume = globalVol;
        controller.turboScreenBlurMaterial = turboBlurMat;
    }
}
