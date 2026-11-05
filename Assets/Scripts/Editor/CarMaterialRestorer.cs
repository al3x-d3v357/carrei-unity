using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CarMaterialRestorer
{
    private const string ManifestPath = "Assets/Materials/Cars/car_materials_manifest.json";
    private const string CarMaterialsFolder = "Assets/Materials/Cars";

    [Serializable]
    private class TextureEntry
    {
        public string material_name;
        public float[] base_color_factor;
        public float metallic;
        public float roughness;
    }

    [MenuItem("RacePrototype/Restore GLB Car Textures")]
    public static void RestoreCarTexturesAndScene()
    {
        if (!Directory.Exists(CarMaterialsFolder))
        {
            Directory.CreateDirectory(CarMaterialsFolder);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        string[] carIds = { "Car_Model_1", "Car_Model_2" };
        Dictionary<string, List<Material>> generatedCarMaterials = new Dictionary<string, List<Material>>();

        foreach (string carId in carIds)
        {
            string fbxPath = $"Assets/Models/{carId}.fbx";
            ModelImporter modelImporter = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (modelImporter != null)
            {
                // Extrai texturas embutidas no FBX caso ainda nao estejam descompactadas
                try
                {
                    modelImporter.ExtractTextures(CarMaterialsFolder);
                }
                catch { /* Ignora caso ja extraido pelo script BPY / python */ }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            List<Material> matsForCar = BuildMaterialsForCar(carId, fbxPath, modelImporter);
            generatedCarMaterials[carId] = matsForCar;
        }

        // Atualiza as instancias de Player_Car e AI_Opponent_Car na cena ativa
        ApplyMaterialsToSceneVehicles(generatedCarMaterials);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CAR TEXTURES] Texturas originais do GLB aplicadas com sucesso nos veiculos!");
    }

    private static List<Material> BuildMaterialsForCar(string carId, string fbxPath, ModelImporter importer)
    {
        List<Material> resultMats = new List<Material>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // Localiza todas as texturas PNG geradas para este carro em Assets/Materials/Cars
        string[] texGuids = AssetDatabase.FindAssets($"{carId} t:Texture2D", new[] { CarMaterialsFolder });
        Texture2D primaryBaseColorTex = null;
        Texture2D primaryNormalTex = null;

        foreach (string guid in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lower = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            if (lower.Contains("normal"))
            {
                ConfigureNormalMapImporter(path);
                primaryNormalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            else if (lower.Contains("base_color") || primaryBaseColorTex == null)
            {
                ConfigureAlbedoImporter(path);
                primaryBaseColorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        // Inspeciona os materiais internos do FBX para mapear cada slot
        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (UnityEngine.Object sub in subAssets)
        {
            if (sub is Material embeddedMat)
            {
                string safeMatName = embeddedMat.name.Replace(" ", "_").Replace(".", "_");
                string matAssetPath = $"{CarMaterialsFolder}/Mat_{carId}_{safeMatName}.mat";
                Material urpMat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
                if (urpMat == null)
                {
                    urpMat = new Material(shader);
                    AssetDatabase.CreateAsset(urpMat, matAssetPath);
                }
                else
                {
                    urpMat.shader = shader;
                }

                // Busca textura especifica do material ou utiliza a textura principal do GLB do carro
                Texture2D specificBase = FindSpecificTexture(carId, safeMatName, "base_color") ?? primaryBaseColorTex;
                Texture2D specificNormal = FindSpecificTexture(carId, safeMatName, "normal") ?? primaryNormalTex;

                // Se o FBX ja referenciava uma textura no slot _MainTex/_BaseMap, preserva como fallback
                if (specificBase == null)
                {
                    if (embeddedMat.HasProperty("_BaseMap"))
                        specificBase = embeddedMat.GetTexture("_BaseMap") as Texture2D;
                    if (specificBase == null && embeddedMat.HasProperty("_MainTex"))
                        specificBase = embeddedMat.GetTexture("_MainTex") as Texture2D;
                }

                Color baseCol = specificBase != null ? Color.white : embeddedMat.color;

                if (urpMat.shader.name.Contains("Universal Render Pipeline"))
                {
                    urpMat.SetColor("_BaseColor", baseCol);
                    urpMat.SetFloat("_Smoothness", 0.65f);
                    urpMat.SetFloat("_Metallic", 0.15f);
                    if (specificBase != null)
                    {
                        urpMat.SetTexture("_BaseMap", specificBase);
                    }
                    if (specificNormal != null)
                    {
                        urpMat.SetTexture("_BumpMap", specificNormal);
                        urpMat.EnableKeyword("_NORMALMAP");
                    }
                }
                else
                {
                    urpMat.SetColor("_Color", baseCol);
                    urpMat.SetFloat("_Glossiness", 0.65f);
                    if (specificBase != null)
                    {
                        urpMat.SetTexture("_MainTex", specificBase);
                    }
                }

                EditorUtility.SetDirty(urpMat);
                resultMats.Add(urpMat);

                if (importer != null)
                {
                    AssetImporter.SourceAssetIdentifier id = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedMat.name);
                    importer.AddRemap(id, urpMat);
                }
            }
        }

        if (importer != null)
        {
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
        }

        return resultMats;
    }

    private static Texture2D FindSpecificTexture(string carId, string safeMatName, string role)
    {
        string candidatePath = $"{CarMaterialsFolder}/{carId}_{safeMatName}_{role}.png";
        return AssetDatabase.LoadAssetAtPath<Texture2D>(candidatePath);
    }

    private static void ConfigureAlbedoImporter(string texPath)
    {
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Default || !ti.sRGBTexture || ti.anisoLevel < 8))
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            ti.SaveAndReimport();
        }
    }

    private static void ConfigureNormalMapImporter(string texPath)
    {
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            ti.SaveAndReimport();
        }
    }

    private static void ApplyMaterialsToSceneVehicles(Dictionary<string, List<Material>> carMats)
    {
        BindVehicleRenderers("Player_Car", "Car_Model_1", carMats);
        BindVehicleRenderers("AI_Opponent_Car", "Car_Model_2", carMats);

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
    }

    private static void BindVehicleRenderers(string sceneObjName, string carId, Dictionary<string, List<Material>> carMats)
    {
        GameObject vehicleGo = GameObject.Find(sceneObjName);
        if (vehicleGo == null)
        {
            vehicleGo = GameObject.Find(carId);
            if (vehicleGo != null)
            {
                vehicleGo.name = sceneObjName;
            }
        }
        if (vehicleGo == null || !carMats.ContainsKey(carId) || carMats[carId].Count == 0) return;

        List<Material> mats = carMats[carId];
        MeshRenderer[] renderers = vehicleGo.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mr in renderers)
        {
            Material[] currentShared = mr.sharedMaterials;
            for (int i = 0; i < currentShared.Length; i++)
            {
                currentShared[i] = mats[Mathf.Min(i, mats.Count - 1)];
            }
            mr.sharedMaterials = currentShared;
            EditorUtility.SetDirty(mr);
        }
    }
}
