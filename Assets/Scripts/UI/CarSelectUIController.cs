using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class CarSelectUIController : MonoBehaviour
{
    [Header("Base de Dados de Veículos")]
    [SerializeField] private List<CarData> availableCars = new List<CarData>();
    private int currentCarIndex = 0;

    private UIDocument uiDocument;
    private VisualElement root;

    // Elementos da Interface Principal
    private Label carNameLabel;
    private VisualElement carThumbnailImage;
    private Label speedValueLabel;
    private VisualElement speedBarFill;
    private Label accelValueLabel;
    private VisualElement accelBarFill;
    private Label handlingValueLabel;
    private VisualElement handlingBarFill;
    private Label specClassLabel;
    private Label specWeightLabel;
    private Label specDriveLabel;

    private Button btnGarage;
    private Button btnPrevCar;
    private Button btnNextCar;
    private Button btnOptions;
    private Button btnViability;
    private Button btnRace;

    // Modais
    private VisualElement modalOptions;
    private VisualElement modalViability;
    private Button btnCloseOptions;
    private Button btnCloseViability;
    private Slider sliderMasterVol;
    private Slider sliderSfxVol;
    private Slider sliderMusicVol;

    private void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        root = uiDocument.rootVisualElement;

        CacheElements();
        RegisterCallbacks();

        if (availableCars.Count > 0)
        {
            UpdateUI(availableCars[currentCarIndex]);
        }
    }

    private void CacheElements()
    {
        carNameLabel = root.Q<Label>("CarNameLabel");
        carThumbnailImage = root.Q<VisualElement>("CarThumbnailImage");

        speedValueLabel = root.Q<Label>("ValueSpeed");
        speedBarFill = root.Q<VisualElement>("BarFillSpeed");

        accelValueLabel = root.Q<Label>("ValueAccel");
        accelBarFill = root.Q<VisualElement>("BarFillAccel");

        handlingValueLabel = root.Q<Label>("ValueHandling");
        handlingBarFill = root.Q<VisualElement>("BarFillHandling");

        specClassLabel = root.Q<Label>("SpecClass");
        specWeightLabel = root.Q<Label>("SpecWeight");
        specDriveLabel = root.Q<Label>("SpecDrive");

        btnGarage = root.Q<Button>("BtnGarage");
        btnPrevCar = root.Q<Button>("BtnPrevCar");
        btnNextCar = root.Q<Button>("BtnNextCar");
        btnOptions = root.Q<Button>("BtnOptions");
        btnViability = root.Q<Button>("BtnViability");
        btnRace = root.Q<Button>("BtnRace");

        modalOptions = root.Q<VisualElement>("ModalOptions");
        modalViability = root.Q<VisualElement>("ModalViability");
        btnCloseOptions = root.Q<Button>("BtnCloseOptions");
        btnCloseViability = root.Q<Button>("BtnCloseViability");

        sliderMasterVol = root.Q<Slider>("SliderMasterVol");
        sliderSfxVol = root.Q<Slider>("SliderSfxVol");
        sliderMusicVol = root.Q<Slider>("SliderMusicVol");
    }

    private void RegisterCallbacks()
    {
        if (btnRace != null) btnRace.clicked += OnRaceClicked;
        if (btnGarage != null) btnGarage.clicked += OnNextCar;
        if (btnNextCar != null) btnNextCar.clicked += OnNextCar;
        if (btnPrevCar != null) btnPrevCar.clicked += OnPrevCar;
        if (btnOptions != null) btnOptions.clicked += OnOptionsClicked;
        if (btnViability != null) btnViability.clicked += OnViabilityClicked;

        if (btnCloseOptions != null) btnCloseOptions.clicked += () => CloseModal(modalOptions);
        if (btnCloseViability != null) btnCloseViability.clicked += () => CloseModal(modalViability);

        if (sliderMasterVol != null)
        {
            sliderMasterVol.RegisterValueChangedCallback(evt =>
            {
                AudioListener.volume = evt.newValue / 100f;
            });
        }
    }

    public void UpdateUI(CarData data)
    {
        if (data == null) return;

        if (carNameLabel != null) carNameLabel.text = data.carName.ToUpper();

        // Atualiza a imagem/thumbnail 2D do veículo
        if (carThumbnailImage != null)
        {
            if (data.thumbnail != null)
            {
                carThumbnailImage.style.backgroundImage = new StyleBackground(data.thumbnail);
                carThumbnailImage.style.display = DisplayStyle.Flex;
            }
            else
            {
                carThumbnailImage.style.backgroundImage = StyleKeyword.None;
            }
        }

        // Atributos numéricos e barras de porcentagem
        if (speedValueLabel != null) speedValueLabel.text = $"{data.topSpeed:0}/100";
        if (speedBarFill != null) speedBarFill.style.width = Length.Percent(data.topSpeed);

        if (accelValueLabel != null) accelValueLabel.text = $"{data.acceleration:0}/100";
        if (accelBarFill != null) accelBarFill.style.width = Length.Percent(data.acceleration);

        if (handlingValueLabel != null) handlingValueLabel.text = $"{data.handling:0}/100";
        if (handlingBarFill != null) handlingBarFill.style.width = Length.Percent(data.handling);

        // Especificações
        if (specClassLabel != null) specClassLabel.text = data.carClass;
        if (specWeightLabel != null) specWeightLabel.text = data.weight;
        if (specDriveLabel != null) specDriveLabel.text = data.drivetrain;

        // Atualiza o modelo 3D do Player_Car na cena em escala e rotação padronizada
        ApplyVehicleToPlayerCar(data);
    }

    private void ApplyVehicleToPlayerCar(CarData data)
    {
        GameObject playerCar = GameObject.Find("Player_Car");
        if (playerCar == null || data == null) return;

        Transform visualRoot = playerCar.transform.Find("Visual_Mesh");
        if (visualRoot == null) return;

        // Limpa sub-objetos antigos para não sobrepor malhas
        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying) Destroy(visualRoot.GetChild(i).gameObject);
            else DestroyImmediate(visualRoot.GetChild(i).gameObject);
        }

        MeshFilter myMf = visualRoot.GetComponent<MeshFilter>();
        if (myMf == null) myMf = visualRoot.gameObject.AddComponent<MeshFilter>();
        MeshRenderer myMr = visualRoot.GetComponent<MeshRenderer>();
        if (myMr == null) myMr = visualRoot.gameObject.AddComponent<MeshRenderer>();

        if (data.carPrefab != null)
        {
            MeshFilter targetMf = data.carPrefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer targetMr = data.carPrefab.GetComponentInChildren<MeshRenderer>();
            if (targetMf != null && targetMr != null && targetMf.sharedMesh != null)
            {
                myMf.sharedMesh = targetMf.sharedMesh;
                myMr.sharedMaterials = targetMr.sharedMaterials;
                myMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                myMr.receiveShadows = true;
            }
        }

        // PADRONIZAÇÃO DE TAMANHO E ROTAÇÃO PARA NÃO FICAR PEQUENO NEM DESALINHADO
        bool isMoto = data.carName.ToLower().Contains("moto");
        StandardizeVisualTransform(visualRoot, myMf.sharedMesh, isMoto);

        // Notifica o controlador do jogador para salvar a nova rotação base e ajustar física
        ArcadeCarController playerCtrl = playerCar.GetComponent<ArcadeCarController>();
        if (playerCtrl != null)
        {
            playerCtrl.NotifyVisualMeshUpdated(isMoto);
        }

        // Atualiza as dimensões do colisor
        BoxCollider box = playerCar.GetComponent<BoxCollider>();
        if (box != null)
        {
            if (isMoto)
            {
                box.size = new Vector3(1.20f, 1.60f, 3.15f);
                box.center = new Vector3(0f, 0.35f, 0f);
            }
            else
            {
                box.size = new Vector3(1.95f, 1.10f, 4.00f);
                box.center = new Vector3(0f, 0.45f, 0f);
            }
        }
    }

    public static void StandardizeVisualTransform(Transform visualRoot, Mesh mesh, bool isMoto = false)
    {
        if (visualRoot == null || mesh == null) return;

        Bounds b = mesh.bounds;
        float maxHoriz = Mathf.Max(b.size.x, b.size.z);

        // Tamanho padrão proporcional: Carros têm 4.0m de comprimento; Moto tem 3.15m
        float targetLength = isMoto ? 3.15f : 4.00f;
        float finalScale = 1.0f;
        if (maxHoriz > 0.01f)
        {
            finalScale = targetLength / maxHoriz;
        }
        visualRoot.localScale = Vector3.one * finalScale;

        // ROTAÇÃO PADRÃO:
        // Se a malha é mais larga no eixo X que no Z (ex: Car_Model_2 onde L=1.14 em X e 0.52 em Z):
        // Frente está em +X. Para alinhar com a frente do carro (+Z): rotação Euler Y = -90 (270).
        // Se a malha já é longa no eixo Z (ex: Car_Model_1 e Moto): rotação Euler Y = 0.
        Quaternion baseRot = Quaternion.identity;
        if (b.size.x > b.size.z * 1.2f)
        {
            baseRot = Quaternion.Euler(0f, -90f, 0f);
        }

        // Posição no chão: alinha a base da malha com a pista
        float groundOffsetY = (b.min.y * finalScale);
        visualRoot.localPosition = new Vector3(0f, -groundOffsetY - 0.45f, 0f);
        visualRoot.localRotation = baseRot;
    }

    private void OnNextCar()
    {
        if (availableCars.Count == 0) return;
        currentCarIndex = (currentCarIndex + 1) % availableCars.Count;
        UpdateUI(availableCars[currentCarIndex]);
        Debug.Log($"[CarSelect] Próximo veículo: {availableCars[currentCarIndex].carName}");
    }

    private void OnPrevCar()
    {
        if (availableCars.Count == 0) return;
        currentCarIndex = (currentCarIndex - 1 + availableCars.Count) % availableCars.Count;
        UpdateUI(availableCars[currentCarIndex]);
        Debug.Log($"[CarSelect] Veículo anterior: {availableCars[currentCarIndex].carName}");
    }

    private void OnOptionsClicked()
    {
        if (modalOptions != null) modalOptions.style.display = DisplayStyle.Flex;
    }

    private void OnViabilityClicked()
    {
        if (modalViability != null) modalViability.style.display = DisplayStyle.Flex;
    }

    private void CloseModal(VisualElement modal)
    {
        if (modal != null) modal.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        // Se a tela de seleção estiver visível, permite navegar e confirmar pelo controle de Xbox 360 e teclado
        if (root != null && root.style.display != DisplayStyle.None)
        {
            // Botão A do Xbox (ou Enter): Confirmar veículo e iniciar corrida
            if (Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.Return))
            {
                OnRaceClicked();
            }
            // Seta Esquerda (ou Botão X / LB): Veículo Anterior
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.A))
            {
                OnPrevCar();
            }
            // Seta Direita (ou Botão B / RB / Tab): Próximo Veículo
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton5) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Tab))
            {
                OnNextCar();
            }
            // Botão Y do Xbox (ou O): Abrir Opções
            else if (Input.GetKeyDown(KeyCode.JoystickButton3) || Input.GetKeyDown(KeyCode.O))
            {
                OnOptionsClicked();
            }
            // Escape: Fechar modais
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modalOptions != null && modalOptions.style.display == DisplayStyle.Flex)
                    CloseModal(modalOptions);
                else if (modalViability != null && modalViability.style.display == DisplayStyle.Flex)
                    CloseModal(modalViability);
            }
        }
    }

    private void OnRaceClicked()
    {
        if (availableCars.Count > 0)
        {
            Debug.Log($"[CarSelect] Veículo confirmado: {availableCars[currentCarIndex].carName}. Iniciando corrida!");
        }

        // 1. Oculta a tela de seleção de veículo
        if (root != null)
        {
            root.style.display = DisplayStyle.None;
        }

        // 2. Dispara a contagem regressiva 3, 2, 1, GO! no LapManager
        if (LapManager.Instance != null)
        {
            LapManager.Instance.ConfirmVehicleSelectionAndStartCountdown();
        }
    }
}
