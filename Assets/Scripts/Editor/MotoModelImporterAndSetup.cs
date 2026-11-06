using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class MotoModelImporterAndSetup
{
    private const string GlbPath = "Assets/Moto_Anime_Rosa.glb";
    private const string BaseTexPath = "Assets/Materials/Cars/Moto_Anime_Rosa_base_color.jpg";
    private const string MatPath = "Assets/Materials/Cars/Mat_Moto_Anime_Rosa.mat";
    private const string MeshAssetPath = "Assets/Models/Moto_Anime_Rosa_Mesh.asset";
    private const string PrefabPath = "Assets/Models/Moto_Anime_Rosa.prefab";

    static MotoModelImporterAndSetup()
    {
        EditorApplication.delayCall += AutoApplyIfNeeded;
    }

    private static void AutoApplyIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(GlbPath)) return;

        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
        GameObject playerGo = GameObject.Find("Player_Car");
        bool needsSceneBind = false;

        if (playerGo != null && existingMesh != null)
        {
            MeshFilter mf = playerGo.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh != existingMesh)
            {
                needsSceneBind = true;
            }
        }

        if (existingMesh == null || needsSceneBind)
        {
            ImportAndApplyMotorcycle();
        }
    }

    [MenuItem("RacePrototype/Apply Anime Pink Motorcycle Model")]
    public static void ImportAndApplyMotorcycle()
    {
        if (!File.Exists(GlbPath))
        {
            Debug.LogError($"[MOTO ANIME] Arquivo GLB não encontrado em {GlbPath}");
            return;
        }

        if (!Directory.Exists("Assets/Models")) Directory.CreateDirectory("Assets/Models");
        if (!Directory.Exists("Assets/Materials/Cars")) Directory.CreateDirectory("Assets/Materials/Cars");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Configura a Textura 8K da Moto Anime Rosa
        Material motoMat = SetupMotoMaterial();

        // 2. Carrega ou Constrói a Malha 3D Diretamente do Binário GLB
        Mesh motoMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
        if (motoMesh == null)
        {
            motoMesh = BuildMeshFromGlbBinary(GlbPath);
            if (motoMesh != null)
            {
                AssetDatabase.CreateAsset(motoMesh, MeshAssetPath);
                AssetDatabase.SaveAssets();
            }
        }

        if (motoMesh == null) return;

        // 3. Cria/Atualiza o Prefab da Moto em Assets/Models/Moto_Anime_Rosa.prefab
        CreateOrUpdateMotoPrefab(motoMesh, motoMat);

        // 4. Aplica a Moto e Textura no Player_Car da cena aberta e salva nas cenas do projeto
        ApplyMotoToCurrentScenePlayer(motoMesh, motoMat);
        ApplyCameraSolidBackgroundAndLightingSettings();

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("🏍️ [MOTO ANIME ROSA] Modelo 3D da Moto com Piloto, Textura e Mecânica de Curva/Empinar aplicados com sucesso!");
    }

    private static Material SetupMotoMaterial()
    {
        TextureImporter ti = AssetImporter.GetAtPath(BaseTexPath) as TextureImporter;
        if (ti != null)
        {
            bool changed = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; changed = true; }
            if (!ti.sRGBTexture) { ti.sRGBTexture = true; changed = true; }
            if (ti.maxTextureSize < 4096) { ti.maxTextureSize = 4096; changed = true; }
            if (ti.anisoLevel < 8) { ti.anisoLevel = 8; changed = true; }
            if (changed) ti.SaveAndReimport();
        }

        Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseTexPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.shader.name.Contains("Universal Render Pipeline"))
        {
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.58f);
            mat.SetFloat("_Metallic", 0.12f);
            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
            }
        }
        else
        {
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Glossiness", 0.58f);
            if (baseTex != null)
            {
                mat.SetTexture("_MainTex", baseTex);
            }
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Mesh BuildMeshFromGlbBinary(string glbFilePath)
    {
        const int vertexCount = 166059;
        const int indexCount = 885582;
        const int binDataStart = 1528;
        const int indicesFileOffset = binDataStart + 0;
        const int positionsFileOffset = binDataStart + 3542328;
        const int uvsFileOffset = binDataStart + 10919360;

        byte[] fileBytes = File.ReadAllBytes(glbFilePath);

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[indexCount];

        // Escala da moto na pista (3.15m de comprimento) e alinhamento da base dos pneus em Y = 0
        const float motoScale = 3.15f;
        const float groundOffsetY = 0.3403756f;

        int posOffset = positionsFileOffset;
        for (int i = 0; i < vertexCount; i++)
        {
            float gx = BitConverter.ToSingle(fileBytes, posOffset);
            float gy = BitConverter.ToSingle(fileBytes, posOffset + 4);
            float gz = BitConverter.ToSingle(fileBytes, posOffset + 8);
            posOffset += 12;

            // Conversão GLTF Right-Handed -> Unity Left-Handed (-X, Y+ground, +Z)
            vertices[i] = new Vector3(
                -gx * motoScale,
                (gy + groundOffsetY) * motoScale,
                gz * motoScale
            );
        }

        int uvOffset = uvsFileOffset;
        for (int i = 0; i < vertexCount; i++)
        {
            float u = BitConverter.ToSingle(fileBytes, uvOffset);
            float v = BitConverter.ToSingle(fileBytes, uvOffset + 4);
            uvOffset += 8;

            // Conversão UV GLTF (origem no topo) -> Unity (origem na base: 1.0 - v)
            uvs[i] = new Vector2(u, 1.0f - v);
        }

        int idxOffset = indicesFileOffset;
        for (int i = 0; i < indexCount; i += 3)
        {
            int i0 = (int)BitConverter.ToUInt32(fileBytes, idxOffset);
            int i1 = (int)BitConverter.ToUInt32(fileBytes, idxOffset + 4);
            int i2 = (int)BitConverter.ToUInt32(fileBytes, idxOffset + 8);
            idxOffset += 12;

            // Inverte a ordem dos vértices do triângulo por conta da inversão de eixo X (Right->Left Handed)
            triangles[i] = i0;
            triangles[i + 1] = i2;
            triangles[i + 2] = i1;
        }

        Mesh mesh = new Mesh
        {
            name = "Moto_Anime_Rosa_Mesh",
            indexFormat = IndexFormat.UInt32
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CreateOrUpdateMotoPrefab(Mesh mesh, Material mat)
    {
        GameObject tempGo = new GameObject("Moto_Anime_Rosa");
        MeshFilter mf = tempGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = tempGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        PrefabUtility.SaveAsPrefabAsset(tempGo, PrefabPath);
        UnityEngine.Object.DestroyImmediate(tempGo);
    }

    private static void ApplyMotoToCurrentScenePlayer(Mesh mesh, Material mat)
    {
        GameObject playerCar = GameObject.Find("Player_Car");
        if (playerCar == null) return;

        // Remove MeshFilter/MeshRenderer na raiz caso existam
        MeshRenderer rootMr = playerCar.GetComponent<MeshRenderer>();
        if (rootMr != null) UnityEngine.Object.DestroyImmediate(rootMr);
        MeshFilter rootMf = playerCar.GetComponent<MeshFilter>();
        if (rootMf != null) UnityEngine.Object.DestroyImmediate(rootMf);

        Transform visualRoot = playerCar.transform.Find("Visual_Mesh");
        if (visualRoot == null)
        {
            GameObject vGo = new GameObject("Visual_Mesh");
            vGo.transform.SetParent(playerCar.transform, false);
            vGo.transform.SetAsFirstSibling();
            visualRoot = vGo.transform;
        }

        // Limpa sub-objetos antigos do carro dentro do Visual_Mesh
        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(visualRoot.GetChild(i).gameObject);
        }

        // Pivô do Visual_Mesh posicionado exatamente na linha de contato dos pneus com o asfalto
        visualRoot.localPosition = new Vector3(0f, -0.45f, 0f);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        MeshFilter mf = visualRoot.GetComponent<MeshFilter>();
        if (mf == null) mf = visualRoot.gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = visualRoot.GetComponent<MeshRenderer>();
        if (mr == null) mr = visualRoot.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new Material[] { mat };
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.receiveShadows = true;

        // Ajusta o BoxCollider para o perfil da moto com piloto
        BoxCollider box = playerCar.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.size = new Vector3(1.15f, 1.55f, 3.10f);
            box.center = new Vector3(0f, 0.32f, 0f);
        }

        EditorUtility.SetDirty(playerCar);
        EditorUtility.SetDirty(visualRoot.gameObject);
    }

    private static void ApplyCameraSolidBackgroundAndLightingSettings()
    {
        // Método 1: Câmera com Fundo Sólido mantendo reflexos e luz ambiente da Skybox intactos
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camGo = GameObject.Find("Isometric_Main_Camera");
            if (camGo != null) mainCam = camGo.GetComponent<Camera>();
        }
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
            EditorUtility.SetDirty(mainCam);
        }

        // Método 2: Desativar visualização de Skybox na Scene View do Editor
        foreach (SceneView sv in SceneView.sceneViews)
        {
            if (sv != null)
            {
                var state = sv.sceneViewState;
                state.showSkybox = false;
                sv.sceneViewState = state;
                sv.Repaint();
            }
        }

        // Método 3 / Calibração URP: Luz Ambiente separada (Gradient / Trilight) + Reflexos da Skybox preservados
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.56f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.92f, 0.65f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.38f, 0.40f, 0.52f);
        RenderSettings.ambientIntensity = 1.35f;
        RenderSettings.reflectionIntensity = 1.25f;
    }
}
