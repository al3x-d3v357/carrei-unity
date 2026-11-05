using System.IO;
using UnityEditor;
using UnityEngine;

public static class TrackMaterialBuilder
{
    private const string MaterialPath = "Assets/Materials/Mat_Track_Asphalt.mat";

    [MenuItem("RacePrototype/Generate Asphalt Material")]
    public static Material GetOrCreateAsphaltMaterial()
    {
        // Garante a existência do diretório
        string dir = Path.GetDirectoryName(MaterialPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Carrega material existente ou instancia um novo
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            // Suporte híbrido: URP Lit prioritário com fallback para Standard Built-in
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[ASPHALT] Nenhum shader padrão (URP/Standard) encontrado.");
                return null;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        ConfigureArcadeProperties(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return mat;
    }

    private static void ConfigureArcadeProperties(Material mat)
    {
        // Tom de asfalto arcade escuro levemente azulado
        Color asphaltBase = new Color(0.11f, 0.11f, 0.13f, 1.0f);
        if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
        {
            // Propriedades URP
            mat.SetColor("_BaseColor", asphaltBase);
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Smoothness", 0.28f); // Leve brilho especular sob luz direta

            Texture2D noise = GenerateAsphaltGrainTexture(256);
            mat.SetTexture("_BaseMap", noise);
            mat.SetTextureScale("_BaseMap", new Vector2(40f, 40f)); // Repetição na pista
        }
        else
        {
            // Propriedades Standard (Built-in Pipeline)
            mat.SetColor("_Color", asphaltBase);
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Glossiness", 0.28f);

            Texture2D noise = GenerateAsphaltGrainTexture(256);
            mat.SetTexture("_MainTex", noise);
            mat.SetTextureScale("_MainTex", new Vector2(40f, 40f));
        }
        EditorUtility.SetDirty(mat);
    }

    private static Texture2D GenerateAsphaltGrainTexture(int size)
    {
        string texPath = "Assets/Materials/Tex_Asphalt_Grain.png";
        Texture2D existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (existingTex != null) return existingTex;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Ruído Perlin em duas camadas para granulação de asfalto
                float n1 = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                float n2 = Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.5f;
                float val = Mathf.Clamp01((n1 + n2) * 0.7f);
                // Modulação sutil sobre o cinza escuro
                float grain = 0.85f + (val * 0.3f);
                pixels[y * size + x] = new Color(grain, grain, grain, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(texPath, bytes);
        AssetDatabase.ImportAsset(texPath);

        // Configuração de repetição contínua
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
    }
}
