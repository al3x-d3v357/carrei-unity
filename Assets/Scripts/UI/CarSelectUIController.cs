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
        if (btnGarage != null) btnGarage.clicked += OnGarageNextCar;
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

        // Se o carro possui prefab ou mesh associado, atualiza o Player_Car na cena
        UpdateSceneVehicleMesh(data);
    }

    private void UpdateSceneVehicleMesh(CarData data)
    {
        GameObject playerCar = GameObject.Find("Player_Car");
        if (playerCar == null || data == null) return;

        Transform visualRoot = playerCar.transform.Find("Visual_Mesh");
        if (visualRoot == null) return;

        if (data.carPrefab != null)
        {
            MeshFilter targetMf = data.carPrefab.GetComponentInChildren<MeshFilter>();
            MeshRenderer targetMr = data.carPrefab.GetComponentInChildren<MeshRenderer>();
            if (targetMf != null && targetMr != null)
            {
                MeshFilter myMf = visualRoot.GetComponent<MeshFilter>();
                if (myMf != null) myMf.sharedMesh = targetMf.sharedMesh;
                MeshRenderer myMr = visualRoot.GetComponent<MeshRenderer>();
                if (myMr != null) myMr.sharedMaterials = targetMr.sharedMaterials;
            }
        }
    }

    private void OnGarageNextCar()
    {
        if (availableCars.Count == 0) return;
        currentCarIndex = (currentCarIndex + 1) % availableCars.Count;
        UpdateUI(availableCars[currentCarIndex]);
        Debug.Log($"[CarSelect] Veículo selecionado: {availableCars[currentCarIndex].carName}");
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
            // Botão A do Xbox (ou Enter / Espaço): Confirmar veículo e iniciar corrida
            if (Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.Return))
            {
                OnRaceClicked();
            }
            // Botão X do Xbox (ou Tab / D / Direita): Trocar de veículo
            else if (Input.GetKeyDown(KeyCode.JoystickButton2) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                OnGarageNextCar();
            }
            // Botão Y do Xbox (ou O): Abrir Opções
            else if (Input.GetKeyDown(KeyCode.JoystickButton3) || Input.GetKeyDown(KeyCode.O))
            {
                OnOptionsClicked();
            }
            // Botão B do Xbox (ou Escape): Fechar modais abertos
            else if (Input.GetKeyDown(KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.Escape))
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
