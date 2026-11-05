using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeSceneBuilder
{
    [MenuItem("RacePrototype/Build Prototype Scene")]
    public static void BuildPrototypeScene()
    {
        EnsureVehicleTagExists();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Iluminacao Direcional com Especular Obliquo (Pitch 45, Yaw 50)
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightGo.transform.rotation = Quaternion.Euler(45f, 50f, 0f);

        // 2. Chao da Pista com Material de Asfalto Arcade
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Track_Floor";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(100f, 1f, 100f);

        Material asphaltMat = TrackMaterialBuilder.GetOrCreateAsphaltMaterial();
        if (asphaltMat != null)
        {
            ground.GetComponent<Renderer>().sharedMaterial = asphaltMat;
        }

        // 3. Reflection Probe com Box Projection para Visao Isometrica
        GameObject probeGo = new GameObject("Track_Reflection_Probe");
        probeGo.transform.position = new Vector3(0f, 5f, 0f);
        ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.boxProjection = true;
        probe.size = new Vector3(100f, 20f, 100f);

        // 4. Paredes Perimetrais e Ilha Central do Circuito Oval
        CreateWall(new Vector3(0f, 1.5f, 45f), new Vector3(90f, 3f, 1f));
        CreateWall(new Vector3(0f, 1.5f, -45f), new Vector3(90f, 3f, 1f));
        CreateWall(new Vector3(45f, 1.5f, 0f), new Vector3(1f, 3f, 90f));
        CreateWall(new Vector3(-45f, 1.5f, 0f), new Vector3(1f, 3f, 90f));

        GameObject centerIsland = GameObject.CreatePrimitive(PrimitiveType.Cube);
        centerIsland.name = "Center_Island";
        centerIsland.transform.position = new Vector3(0f, 1.5f, 0f);
        centerIsland.transform.localScale = new Vector3(40f, 3f, 40f);

        // 5. Instanciacao dos Props Procedurais do Blender (Zebras, Barreiras de Pneus e Portico)
        InstantiateTrackProp("Assets/Models/Track_Curbs.fbx", "Track_Curbs", new Vector3(0f, 0.5f, 0f), false);
        InstantiateTrackProp("Assets/Models/Tire_Barriers.fbx", "Tire_Barriers", new Vector3(0f, 0.5f, 0f), true);

        GameObject gantryAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Start_Gantry.fbx");
        if (gantryAsset != null)
        {
            GameObject gantry = (GameObject)PrefabUtility.InstantiatePrefab(gantryAsset);
            gantry.name = "Start_Finish_Gantry";
            gantry.transform.position = new Vector3(0f, 0.5f, -22f);
            gantry.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // 6. Checkpoints e Contagem de Voltas
        GameObject managerGo = new GameObject("LapManager");
        LapManager lapManager = managerGo.AddComponent<LapManager>();

        CreateCheckpoint(0, new Vector3(0f, 1.5f, -32.5f), new Vector3(2f, 4f, 25f), lapManager); // Linha de Chegada
        CreateCheckpoint(1, new Vector3(32.5f, 1.5f, 0f), new Vector3(25f, 4f, 2f), lapManager);  // Reta Leste
        CreateCheckpoint(2, new Vector3(0f, 1.5f, 32.5f), new Vector3(2f, 4f, 25f), lapManager);  // Reta Norte
        CreateCheckpoint(3, new Vector3(-32.5f, 1.5f, 0f), new Vector3(25f, 4f, 2f), lapManager); // Reta Oeste

        // 7. Geracao dos 8 Waypoints da Linha de Corrida para a IA Offline
        List<Transform> racingWaypoints = CreateRacingLineWaypoints();

        // 8. Instanciacao do Carro do Jogador e do Oponente IA no Grid de Largada
        GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Car_Model_1.fbx");
        GameObject playerCar = SetupVehicle(playerModel, "Player_Car", new Vector3(-4f, 1.0f, -30f));
        playerCar.AddComponent<ArcadeCarController>();
        playerCar.AddComponent<ArcadeCollisionBumper>();
        AttachVehicleAudio(playerCar);

        GameObject aiModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Car_Model_2.fbx");
        GameObject aiCar = SetupVehicle(aiModel, "AI_Opponent_Car", new Vector3(-4f, 1.0f, -35f));
        ArcadeCarAI aiComponent = aiCar.AddComponent<ArcadeCarAI>();
        aiComponent.playerTarget = playerCar.transform;
        aiComponent.circuitWaypoints = racingWaypoints;
        aiCar.AddComponent<ArcadeCollisionBumper>();
        AttachVehicleAudio(aiCar);

        // 9. Camera Isometrica (mais proxima do carro + AudioListener)
        GameObject camGo = new GameObject("Isometric_Main_Camera");
        Camera cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 8.5f;

        IsometricFollowCamera camScript = camGo.AddComponent<IsometricFollowCamera>();
        camScript.target = playerCar.transform;
        camGo.transform.rotation = Quaternion.Euler(35.264f, 45f, 0f);
        camGo.transform.position = playerCar.transform.position + Quaternion.Euler(35.264f, 45f, 0f) * new Vector3(0f, 0f, -22f);

        // 10. Reaplicacao das Texturas Originais GLB nos Carros
        var restorerType = System.Type.GetType("CarMaterialRestorer");
        if (restorerType != null)
        {
            var method = restorerType.GetMethod("RestoreCarTexturesAndScene", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

        if (playerCar != null) playerCar.name = "Player_Car";
        if (aiCar != null) aiCar.name = "AI_Opponent_Car";
        if (camScript != null && playerCar != null) camScript.target = playerCar.transform;
        if (aiComponent != null && playerCar != null) aiComponent.playerTarget = playerCar.transform;

        // 11. Aplicar VFX de Vitrine + Salvar Cenas
        ShowcaseVisualsBuilder.ApplyShowcaseVisuals();

        var activeScene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(activeScene, "Assets/01.unity");
        EditorSceneManager.SaveScene(activeScene, "Assets/02.unity");
        string scenePath = "Assets/Scenes/IsometricRacePrototype.unity";
        EditorSceneManager.SaveScene(activeScene, scenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true),
            new EditorBuildSettingsScene("Assets/01.unity", true),
            new EditorBuildSettingsScene("Assets/02.unity", true)
        };
        AssetDatabase.SaveAssets();

        Debug.Log("[PROTOTYPE] Cena atualizada com fisica Simcade e IA Offline com 8 Waypoints em " + scenePath);
    }

    private static void EnsureVehicleTagExists()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets != null && assets.Length > 0)
        {
            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            bool found = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals("Vehicle"))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = "Vehicle";
                tagManager.ApplyModifiedProperties();
            }
        }
    }

    private static List<Transform> CreateRacingLineWaypoints()
    {
        GameObject wpRoot = new GameObject("RacingLine_Waypoints");
        // Tracado anti-horario acompanhando a sequencia dos Checkpoints 0 -> 1 -> 2 -> 3
        Vector3[] wpPositions = new Vector3[]
        {
            new Vector3(18f, 1f, -31f),  // WP 0: Saida da Reta Sul
            new Vector3(31f, 1f, -18f),  // WP 1: Apice Curva Sudeste
            new Vector3(31f, 1f, 18f),   // WP 2: Fim da Reta Leste
            new Vector3(18f, 1f, 31f),   // WP 3: Apice Curva Nordeste
            new Vector3(-18f, 1f, 31f),  // WP 4: Fim da Reta Norte
            new Vector3(-31f, 1f, 18f),  // WP 5: Apice Curva Noroeste
            new Vector3(-31f, 1f, -18f), // WP 6: Fim da Reta Oeste
            new Vector3(-18f, 1f, -31f)  // WP 7: Apice Curva Sudoeste (Entrada da Reta Principal)
        };

        List<Transform> list = new List<Transform>();
        for (int i = 0; i < wpPositions.Length; i++)
        {
            GameObject wp = new GameObject($"Waypoint_{i}");
            wp.transform.SetParent(wpRoot.transform);
            wp.transform.position = wpPositions[i];
            list.Add(wp.transform);
        }
        return list;
    }

    private static void InstantiateTrackProp(string assetPath, string objName, Vector3 pos, bool addCollider)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = objName;
        instance.transform.position = pos;

        if (addCollider)
        {
            foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.gameObject.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.convex = false;
                }
            }
        }
    }

    private static GameObject SetupVehicle(GameObject modelAsset, string name, Vector3 startPos)
    {
        GameObject carInstance = new GameObject(name);
        carInstance.tag = "Vehicle";
        carInstance.transform.position = startPos;
        carInstance.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // Apontado para Leste (+X) na Reta Sul

        GameObject visualChild;
        if (modelAsset != null)
        {
            visualChild = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            PrefabUtility.UnpackPrefabInstance(visualChild, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visualChild.name = "Visual_Mesh";
            visualChild.transform.SetParent(carInstance.transform, false);
            visualChild.transform.localPosition = Vector3.zero;

            MeshFilter mf = visualChild.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;
                Bounds b = mesh.bounds;
                float maxHoriz = Mathf.Max(b.size.x, b.size.z);
                if (maxHoriz > 0.01f && maxHoriz < 2.0f)
                {
                    float scaleFactor = 3.1f / maxHoriz;
                    visualChild.transform.localScale = Vector3.one * scaleFactor;
                }

                // 1. Alinha o eixo mais longo (comprimento do carro) com o eixo +Z local do veiculo
                Quaternion baseRot = (b.size.x > b.size.z * 1.2f)
                    ? Quaternion.Euler(0f, -90f, 0f)
                    : Quaternion.identity;

                // 2. Verifica nos vertices da malha qual extremidade (+Z ou -Z) tem o capo mais baixo vs teto/traseira mais alta
                Vector3[] verts = mesh.vertices;
                float maxYFront = 0f;
                float maxYRear = 0f;
                float threshold = maxHoriz * 0.22f;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 vAligned = baseRot * verts[i];
                    if (vAligned.z > threshold && vAligned.y > maxYFront) maxYFront = vAligned.y;
                    if (vAligned.z < -threshold && vAligned.y > maxYRear) maxYRear = vAligned.y;
                }

                if (maxYFront > maxYRear)
                {
                    baseRot = Quaternion.Euler(0f, 180f, 0f) * baseRot;
                }

                visualChild.transform.localRotation = baseRot;
                Debug.Log($"[VEHICLE ORIENTATION] {name}: localEuler={visualChild.transform.localRotation.eulerAngles}, scale={visualChild.transform.localScale.x:F2}, maxYFront={maxYFront:F3}, maxYRear={maxYRear:F3}");
            }
        }
        else
        {
            visualChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualChild.name = "Visual_Mesh";
            Object.DestroyImmediate(visualChild.GetComponent<Collider>());
            visualChild.transform.SetParent(carInstance.transform, false);
            visualChild.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            visualChild.transform.localScale = new Vector3(1.6f, 0.8f, 3.2f);
        }

        BoxCollider collider = carInstance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = carInstance.AddComponent<BoxCollider>();
        }
        collider.size = new Vector3(1.8f, 0.9f, 3.4f);
        collider.center = new Vector3(0f, 0.45f, 0f);

        return carInstance;
    }

    private static void AttachVehicleAudio(GameObject car)
    {
        if (car == null) return;

        ArcadeCarAudio audio = car.GetComponent<ArcadeCarAudio>();
        if (audio == null)
        {
            audio = car.AddComponent<ArcadeCarAudio>();
        }

        audio.engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/engine_accel.wav");
        audio.skidClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/skid_tires.wav");
        audio.skidAltClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/skid_synth.wav");
        audio.brakeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/brake_synth.wav");
        audio.turboClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/turbo_synth.wav");
    }

    private static void CreateWall(Vector3 pos, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Border_Wall";
        wall.transform.position = pos;
        wall.transform.localScale = size;
    }

    private static void CreateCheckpoint(int index, Vector3 pos, Vector3 size, LapManager manager)
    {
        GameObject cpGo = new GameObject($"Checkpoint_{index}");
        cpGo.transform.position = pos;

        BoxCollider bc = cpGo.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = size;

        CheckpointTrigger trigger = cpGo.AddComponent<CheckpointTrigger>();
        trigger.index = index;

        manager.RegisterCheckpoint(cpGo.transform);
    }
}
