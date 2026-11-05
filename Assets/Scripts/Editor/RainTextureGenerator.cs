using System.IO;
using UnityEditor;
using UnityEngine;

public static class RainTextureGenerator
{
    [MenuItem("RacePrototype/Generate Rain Ripple Normal")]
    public static void GenerateRippleTexture()
    {
        int size = 512;
        string path = "Assets/Materials/Tex_RainRipples_Normal.png";
        
        string outDir = Path.GetDirectoryName(path);
        if (!Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);

        // Gera centros aleatórios em grade 4x4
        int cells = 4;
        float cellSize = (float)size / cells;
        Vector2[] centers = new Vector2[cells * cells];
        for (int cy = 0; cy < cells; cy++)
        {
            for (int cx = 0; cx < cells; cx++)
            {
                float ox = (cx + 0.2f + Random.value * 0.6f) * cellSize;
                float oy = (cy + 0.2f + Random.value * 0.6f) * cellSize;
                centers[cy * cells + cx] = new Vector2(ox, oy);
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float heightDerivX = 0f;
                float heightDerivY = 0f;
                foreach (var c in centers)
                {
                    float dist = Vector2.Distance(p, c);
                    if (dist < cellSize * 0.45f)
                    {
                        // Anéis concêntricos senoidais amortecidos
                        float wave = Mathf.Sin(dist * 0.35f) * Mathf.Exp(-dist * 0.04f);
                        Vector2 dir = (dist > 0.001f) ? (p - c) / dist : Vector2.zero;
                        heightDerivX += dir.x * wave;
                        heightDerivY += dir.y * wave;
                    }
                }

                // Vetor Normal Tangente codificado em RGB (0.5 como neutro)
                Vector3 normal = new Vector3(-heightDerivX * 1.5f, -heightDerivY * 1.5f, 1f).normalized;
                Color pixel = new Color(
                    normal.x * 0.5f + 0.5f,
                    normal.y * 0.5f + 0.5f,
                    normal.z * 0.5f + 0.5f,
                    1f
                );
                tex.SetPixel(x, y, pixel);
            }
        }

        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8; // Essencial para evitar flickering na visão isométrica
            importer.SaveAndReimport();
        }

        Debug.Log("[TEXTURE] Mapa de normais de chuva gerado com sucesso em " + path);
    }
}
