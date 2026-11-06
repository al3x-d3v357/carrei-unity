using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CarSelectSceneSetup
{
    private const string UiFolder = "Assets/UI/CarSelect";
    private const string ThumbFolder = "Assets/UI/CarSelect/Thumbnails";
    private const string UxmlPath = "Assets/UI/CarSelect/CarSelectScreen.uxml";
    private const string PanelSettingsPath = "Assets/UI/CarSelect/CarSelectPanelSettings.asset";

    static CarSelectSceneSetup()
    {
        EditorApplication.delayCall += AutoSetupIfNeeded;
    }

    private static void AutoSetupIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(UxmlPath)) return;

        SetupCarSelectInScene();
    }

    [MenuItem("RacePrototype/Setup Car Select UI")]
    public static void SetupCarSelectInScene()
    {
        if (!Directory.Exists(UiFolder)) Directory.CreateDirectory(UiFolder);
        if (!Directory.Exists(ThumbFolder)) Directory.CreateDirectory(ThumbFolder);

        // Configura importadores de textura das thumbnails como Sprite/Default para UI Toolkit
        ConfigureThumbnailImporter($"{ThumbFolder}/Car_Model_1_thumb.png");
        ConfigureThumbnailImporter($"{ThumbFolder}/Car_Model_2_thumb.png");
        ConfigureThumbnailImporter($"{ThumbFolder}/Moto_Anime_Rosa_thumb.png");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Garante PanelSettings para o UIDocument
        PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        }

        // 2. Cria/Atualiza os ScriptableObjects CarData com as thumbnails dos modelos liberados do projeto
        List<CarData> carList = new List<CarData>
        {
            GetOrCreateCarData(
                "CarData_VX9Phantom",
                "VX-9 Phantom", "A",
                80f, 75f, 65f,
                "1,320 kg", "AWD",
                "Assets/Models/Car_Model_1.fbx",
                $"{ThumbFolder}/Car_Model_1_thumb.png"
            ),
            GetOrCreateCarData(
                "CarData_MotoAnimeRosa",
                "Moto Anime Rosa", "S",
                92f, 95f, 88f,
                "215 kg", "RWD",
                "Assets/Models/Moto_Anime_Rosa.prefab",
                $"{ThumbFolder}/Moto_Anime_Rosa_thumb.png"
            ),
            GetOrCreateCarData(
                "CarData_GTRApex",
                "GT-R Apex", "A+",
                86f, 82f, 78f,
                "1,450 kg", "AWD",
                "Assets/Models/Car_Model_2.fbx",
                $"{ThumbFolder}/Car_Model_2_thumb.png"
            )
        };

        // 3. Instancia e vincula UIDocument + CarSelectUIController na cena
        VisualTreeAsset uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        GameObject uiGo = GameObject.Find("CarSelect_UI");
        if (uiGo == null)
        {
            uiGo = new GameObject("CarSelect_UI");
        }

        UIDocument doc = uiGo.GetComponent<UIDocument>();
        if (doc == null)
        {
            doc = uiGo.AddComponent<UIDocument>();
        }
        doc.panelSettings = panelSettings;
        doc.visualTreeAsset = uxmlAsset;

        CarSelectUIController ctrl = uiGo.GetComponent<CarSelectUIController>();
        if (ctrl == null)
        {
            ctrl = uiGo.AddComponent<CarSelectUIController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SerializedProperty listProp = so.FindProperty("availableCars");
        if (listProp != null)
        {
            listProp.ClearArray();
            for (int i = 0; i < carList.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = carList[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(uiGo);
        EditorUtility.SetDirty(ctrl);

        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CarSelect] UI de Seleção de Veículos com Thumbnails e Modais configurada!");
    }

    private static void ConfigureThumbnailImporter(string path)
    {
        if (!File.Exists(path)) return;
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null && (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single))
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }
    }

    private static CarData GetOrCreateCarData(
        string fileName, string carName, string carClass,
        float speed, float accel, float handling,
        string weight, string drive, string prefabPath, string thumbPath)
    {
        string assetPath = $"{UiFolder}/{fileName}.asset";
        CarData data = AssetDatabase.LoadAssetAtPath<CarData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CarData>();
            AssetDatabase.CreateAsset(data, assetPath);
        }

        data.carName = carName;
        data.carClass = carClass;
        data.topSpeed = speed;
        data.acceleration = accel;
        data.handling = handling;
        data.weight = weight;
        data.drivetrain = drive;
        if (!string.IsNullOrEmpty(prefabPath))
        {
            data.carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        if (!string.IsNullOrEmpty(thumbPath))
        {
            data.thumbnail = AssetDatabase.LoadAssetAtPath<Sprite>(thumbPath);
        }

        EditorUtility.SetDirty(data);
        return data;
    }
}
