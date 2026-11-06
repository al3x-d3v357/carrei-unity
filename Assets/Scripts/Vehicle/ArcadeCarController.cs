using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class ArcadeCarController : MonoBehaviour
{
    [Header("Engine & Gearbox (Simcade Moto)")]
    [SerializeField] private float enginePeakForce = 52f;
    [SerializeField] private float maxForwardSpeed = 30.5f;
    [SerializeField] private float maxReverseSpeed = 12f;
    [SerializeField] private float brakeForce = 64f;
    [SerializeField] private float[] gearSpeedThresholds = { 7f, 13f, 19f, 24.5f, 30.5f };
    [SerializeField] private float[] gearTorqueMultipliers = { 1.48f, 1.25f, 1.08f, 0.96f, 0.88f };

    [Header("Turbo Ability & Screen Blur")]
    [SerializeField] private KeyCode turboKey = KeyCode.LeftShift;
    [SerializeField] private float maxTurboCapacity = 100f;
    [SerializeField] private float minTurboToActivate = 15f;
    [SerializeField] private float turboBurnRate = 32f;
    [SerializeField] private float turboRechargeRate = 20f;
    [SerializeField] private float turboAccelerationMultiplier = 2.25f;
    [SerializeField] private float turboTopSpeedMultiplier = 1.50f;
    [SerializeField] private float turboActivationImpulse = 13.5f;
    [SerializeField] private float blurRampUpSpeed = 8.0f;
    [SerializeField] private float blurRampDownSpeed = 4.5f;

    [Header("Motorcycle Cornering, Drift & Tire Physics")]
    [SerializeField] private float lowSpeedSteerTorque = 152f;
    [SerializeField] private float highSpeedSteerTorque = 98f;
    [SerializeField] private float driftSteerMultiplier = 1.45f;
    [SerializeField] private float straightGripDamping = 9.2f;
    [SerializeField] private float corneringGripDamping = 3.5f;
    [SerializeField] private float driftGripDamping = 1.25f;
    [SerializeField] private float driftOutwardSlideForce = 16.5f;
    [SerializeField] private float naturalCornerSlideForce = 6.5f;
    [SerializeField] private float downforceCoefficient = 0.65f;
    [SerializeField] private float extraGravity = 28f;
    [SerializeField] private float rollingResistance = 0.75f;

    [Header("Motorcycle Lean (Curva) & Wheelie (Empinar)")]
    [SerializeField] private float maxMotoLeanAngle = 40f;
    [SerializeField] private float maxDriftLeanAngle = 48f;
    [SerializeField] private float leanSmoothSpeed = 11.5f;
    [SerializeField] private float maxWheelieAngle = 33f;
    [SerializeField] private float wheelieRampUpSpeed = 4.2f;
    [SerializeField] private float wheelieRampDownSpeed = 5.5f;
    [SerializeField] private float rearAxlePivotZ = 1.02f;
    [SerializeField] private float maxBodyPitchAngle = 4.2f;
    [SerializeField] private float maxVisualDriftYaw = 22f;
    [SerializeField] private float maxNaturalCornerYaw = 8.5f;

    private Rigidbody rb;
    private ArcadeCarAudio carAudio;
    private Transform opponentTarget;
    private Transform visualMeshRoot;
    private Quaternion initialVisualLocalRot;
    private Vector3 initialVisualLocalPos;
    private float inputThrottle;
    private float inputSteer;
    private bool inputHandbrake;
    private bool inputTurboKey;
    private bool inputWheelie;
    private float currentTurbo = 100f;
    private bool isTurboActive = false;
    private bool turboOverheated = false;
    private bool isWheelieActive = false;
    private float currentWheelieFactor = 0f;
    private float currentRoll;
    private float currentPitch;
    private float currentDriftYaw;
    private float previousForwardSpeed;
    private int currentGear = 1;
    private float currentGearRpmNorm = 0f;

    // Drift & Slipstream state
    private float driftCharge = 0f;
    private float driftBoostTimer = 0f;
    private bool isDrifting = false;
    private float slipstreamCharge = 0f;
    private bool inSlipstream = false;

    public bool IsTurboActive => isTurboActive;
    public bool IsDriftBoostActive => driftBoostTimer > 0f;
    public bool IsDrifting => isDrifting;
    public bool IsWheelieActive => isWheelieActive;
    public float WheelieFactor => currentWheelieFactor;
    public bool InSlipstream => inSlipstream;
    public float DriftCharge => driftCharge;
    public float SlipstreamCharge => slipstreamCharge;
    public bool IsHandbraking => inputHandbrake;
    public float ThrottleInput => inputThrottle;
    public float SteerInput => inputSteer;
    public float VisualDriftYaw => currentDriftYaw;
    public float TurboPercentage => currentTurbo / maxTurboCapacity;
    public float CurrentTurboBlurFactor { get; private set; }
    public float CurrentSpeedKmh => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
    public int CurrentGear => currentGear;
    public float CurrentGearRpmNormalized => currentGearRpmNorm;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carAudio = GetComponent<ArcadeCarAudio>();
        if (carAudio == null)
        {
            carAudio = gameObject.AddComponent<ArcadeCarAudio>();
        }

        // Veículo pesado e plantado no asfalto (1.8 tonelada equivalente p/ estabilidade + sem capotar)
        rb.mass = 1800f;
        rb.centerOfMass = new Vector3(0f, -0.85f, 0f);
        rb.linearDamping = rollingResistance;
        rb.angularDamping = 5.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Garante parâmetros calibrados de Moto (Curva, Empinar, Turbo e Drift)
        lowSpeedSteerTorque = 152f;
        highSpeedSteerTorque = 98f;
        turboAccelerationMultiplier = 2.25f;
        turboTopSpeedMultiplier = 1.50f;
        turboActivationImpulse = 13.5f;
        driftSteerMultiplier = 1.45f;
        straightGripDamping = 9.2f;
        corneringGripDamping = 3.5f;
        driftGripDamping = 1.25f;
        driftOutwardSlideForce = 16.5f;
        naturalCornerSlideForce = 6.5f;
        extraGravity = 28f;
        maxMotoLeanAngle = 40f;
        maxDriftLeanAngle = 48f;
        leanSmoothSpeed = 11.5f;
        maxWheelieAngle = 33f;
        rearAxlePivotZ = 1.02f;
        maxVisualDriftYaw = 22f;
        maxNaturalCornerYaw = 8.5f;
        currentTurbo = maxTurboCapacity;

        ApplyLowFrictionColliderMaterial();
        EnsureVisualMeshHierarchy();
    }

    private void Start()
    {
        if (carAudio == null) carAudio = GetComponent<ArcadeCarAudio>();
        GameObject aiGo = GameObject.Find("AI_Opponent_Car");
        if (aiGo != null) opponentTarget = aiGo.transform;
    }

    private void ApplyLowFrictionColliderMaterial()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            PhysicsMaterial mat = new PhysicsMaterial("MotoHeavyZeroBounce")
            {
                dynamicFriction = 0.04f,
                staticFriction = 0.04f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            col.sharedMaterial = mat;
        }
    }

    public void EnsureVisualMeshHierarchy()
    {
        Transform existingVisual = transform.Find("Visual_Mesh");
        if (existingVisual == null)
        {
            MeshFilter rootMf = GetComponent<MeshFilter>();
            MeshRenderer rootMr = GetComponent<MeshRenderer>();
            if (rootMf != null && rootMr != null && rootMf.sharedMesh != null)
            {
                GameObject visualGo = new GameObject("Visual_Mesh");
                visualGo.transform.SetParent(transform, false);
                visualGo.transform.SetAsFirstSibling();
                MeshFilter childMf = visualGo.AddComponent<MeshFilter>();
                childMf.sharedMesh = rootMf.sharedMesh;
                MeshRenderer childMr = visualGo.AddComponent<MeshRenderer>();
                childMr.sharedMaterials = rootMr.sharedMaterials;
                childMr.shadowCastingMode = rootMr.shadowCastingMode;
                childMr.receiveShadows = rootMr.receiveShadows;
                if (Application.isPlaying)
                {
                    Destroy(rootMr);
                    Destroy(rootMf);
                }
                existingVisual = visualGo.transform;
            }
            else
            {
                GameObject visualGo = new GameObject("Visual_Mesh");
                visualGo.transform.SetParent(transform, false);
                visualGo.transform.SetAsFirstSibling();
                existingVisual = visualGo.transform;
            }
        }

        if (existingVisual != null)
        {
            EnsureMotoModelLoaded(existingVisual);
            visualMeshRoot = existingVisual;
            initialVisualLocalRot = visualMeshRoot.localRotation;
            initialVisualLocalPos = visualMeshRoot.localPosition;
        }
    }

    private void EnsureMotoModelLoaded(Transform visualTransform)
    {
        MeshFilter mf = visualTransform.GetComponent<MeshFilter>();
        if (mf == null) mf = visualTransform.gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = visualTransform.GetComponent<MeshRenderer>();
        if (mr == null) mr = visualTransform.gameObject.AddComponent<MeshRenderer>();

        if (mf.sharedMesh == null || mf.sharedMesh.name != "Moto_Anime_Rosa_Mesh")
        {
            const string glbPath = "Assets/Moto_Anime_Rosa.glb";
            if (System.IO.File.Exists(glbPath))
            {
                for (int i = visualTransform.childCount - 1; i >= 0; i--)
                {
                    if (Application.isPlaying) Destroy(visualTransform.GetChild(i).gameObject);
                    else DestroyImmediate(visualTransform.GetChild(i).gameObject);
                }

                mf.sharedMesh = LoadMotoGlbMeshDirect(glbPath);
                Material motoMat = CreateMotoMaterialDirect("Assets/Materials/Cars/Moto_Anime_Rosa_base_color.jpg");
                if (motoMat != null)
                {
                    mr.sharedMaterials = new Material[] { motoMat };
                }
            }
        }

        // Alinha o pivô na linha de contato dos pneus com o asfalto para inclinar na curva e empinar com perfeição
        visualTransform.localPosition = new Vector3(0f, -0.45f, 0f);
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = Vector3.one;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.size = new Vector3(1.15f, 1.55f, 3.10f);
            box.center = new Vector3(0f, 0.32f, 0f);
        }
    }

    private static Mesh LoadMotoGlbMeshDirect(string glbFilePath)
    {
        const int vertexCount = 166059;
        const int indexCount = 885582;
        const int binDataStart = 1528;
        const int indicesFileOffset = binDataStart + 0;
        const int positionsFileOffset = binDataStart + 3542328;
        const int uvsFileOffset = binDataStart + 10919360;

        byte[] fileBytes = System.IO.File.ReadAllBytes(glbFilePath);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[indexCount];

        const float motoScale = 3.15f;
        const float groundOffsetY = 0.3403756f;

        int posOffset = positionsFileOffset;
        for (int i = 0; i < vertexCount; i++)
        {
            float gx = System.BitConverter.ToSingle(fileBytes, posOffset);
            float gy = System.BitConverter.ToSingle(fileBytes, posOffset + 4);
            float gz = System.BitConverter.ToSingle(fileBytes, posOffset + 8);
            posOffset += 12;

            vertices[i] = new Vector3(
                -gx * motoScale,
                (gy + groundOffsetY) * motoScale,
                gz * motoScale
            );
        }

        int uvOffset = uvsFileOffset;
        for (int i = 0; i < vertexCount; i++)
        {
            float u = System.BitConverter.ToSingle(fileBytes, uvOffset);
            float v = System.BitConverter.ToSingle(fileBytes, uvOffset + 4);
            uvOffset += 8;
            uvs[i] = new Vector2(u, 1.0f - v);
        }

        int idxOffset = indicesFileOffset;
        for (int i = 0; i < indexCount; i += 3)
        {
            int i0 = (int)System.BitConverter.ToUInt32(fileBytes, idxOffset);
            int i1 = (int)System.BitConverter.ToUInt32(fileBytes, idxOffset + 4);
            int i2 = (int)System.BitConverter.ToUInt32(fileBytes, idxOffset + 8);
            idxOffset += 12;

            triangles[i] = i0;
            triangles[i + 1] = i2;
            triangles[i + 2] = i1;
        }

        Mesh mesh = new Mesh
        {
            name = "Moto_Anime_Rosa_Mesh",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateMotoMaterialDirect(string texPath)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader) { name = "Mat_Moto_Anime_Rosa_Runtime" };

        if (System.IO.File.Exists(texPath))
        {
            byte[] jpgBytes = System.IO.File.ReadAllBytes(texPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, true);
            ImageConversion.LoadImage(tex, jpgBytes, false);
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.Apply(true, false);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.58f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.12f);
        return mat;
    }

    private void ReadPlayerInput()
    {
        float rawThrottle = 0f;
        float rawSteer = 0f;
        bool rawHandbrake = false;
        bool rawTurbo = false;
        bool rawWheelie = false;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawThrottle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawThrottle -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawSteer += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawSteer -= 1f;
            rawHandbrake = kb.spaceKey.isPressed;
            rawTurbo = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            rawWheelie = kb.eKey.isPressed || kb.qKey.isPressed || kb.leftCtrlKey.isPressed;
        }

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            // Controle Xbox 360:
            // A (buttonSouth) = Acelerar
            // Y (buttonNorth) = Frear / Dar Ré
            // B (buttonEast)  = Turbo
            // X (buttonWest)  = Derrapar (Drift)
            // LB / RB / LT / RT / Puxar Analógico p/ Trás = Empinar a Moto (Grau / Wheelie)
            if (gp.buttonSouth.isPressed) rawThrottle += 1f;
            if (gp.buttonNorth.isPressed) rawThrottle -= 1f;

            float gpSteer = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(gpSteer) < 0.12f) gpSteer = 0f;
            float dpadSteer = gp.dpad.x.ReadValue();
            if (Mathf.Abs(dpadSteer) > Mathf.Abs(gpSteer)) gpSteer = dpadSteer;
            if (Mathf.Abs(gpSteer) > Mathf.Abs(rawSteer)) rawSteer = gpSteer;

            if (gp.buttonWest.isPressed) rawHandbrake = true;
            if (gp.buttonEast.isPressed) rawTurbo = true;

            bool bumperOrTriggerWheelie = gp.leftShoulder.isPressed || gp.rightShoulder.isPressed ||
                                          gp.leftTrigger.ReadValue() > 0.35f || gp.rightTrigger.ReadValue() > 0.35f;
            bool pullBackStickWheelie = gp.leftStick.y.ReadValue() < -0.45f && gp.buttonSouth.isPressed;
            if (bumperOrTriggerWheelie || pullBackStickWheelie)
            {
                rawWheelie = true;
            }
        }

        // Suporte complementar para driver Xbox 360 via Legacy Input / Joystick no Linux
        try
        {
            if (Input.GetKey(KeyCode.JoystickButton0)) rawThrottle += 1f; // Botão A -> Acelerar
            if (Input.GetKey(KeyCode.JoystickButton3)) rawThrottle -= 1f; // Botão Y -> Frear / Ré
            if (Input.GetKey(KeyCode.JoystickButton2)) rawHandbrake = true; // Botão X -> Derrapar (Drift)
            if (Input.GetKey(KeyCode.JoystickButton1)) rawTurbo = true; // Botão B -> Turbo
            if (Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5)) rawWheelie = true; // LB / RB -> Empinar Moto

            float legacyHoriz = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(legacyHoriz) > 0.15f && Mathf.Abs(legacyHoriz) > Mathf.Abs(rawSteer))
            {
                rawSteer = legacyHoriz;
            }
        }
        catch
        {
            // Ignorado caso Legacy Input esteja desativado
        }

        // Se apertar o botão de Empinar a Moto, garante aceleração para levantar a roda dianteira
        if (rawWheelie && rawThrottle >= 0f && rawThrottle < 0.9f)
        {
            rawThrottle = 1.0f;
        }

        // Assistência ergonômica de Drift no controle: se segurar X (Drift) em movimento sem apertar Y (Freio/Ré),
        // mantém tração no motor mesmo que o polegar saia do A para apertar o X
        if (rawHandbrake && rawThrottle >= 0f && rawThrottle < 0.85f && rb != null)
        {
            float fwdVel = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (fwdVel > 3.5f)
            {
                rawThrottle = 0.90f;
            }
        }

        inputThrottle = Mathf.Clamp(rawThrottle, -1f, 1f);
        inputSteer = Mathf.MoveTowards(inputSteer, Mathf.Clamp(rawSteer, -1f, 1f), 9.5f * Time.deltaTime);
        inputHandbrake = rawHandbrake;
        inputTurboKey = rawTurbo;
        inputWheelie = rawWheelie;
    }

    private void Update()
    {
        ReadPlayerInput();
        EvaluatePlayerSlipstream();
        HandleDriftAndSlingshot();
        HandleWheelieMechanic();
        HandleTurboAbility();
        UpdateGearbox();
        UpdateVisualWeightTransfer();
    }

    private void FixedUpdate()
    {
        ApplyAerodynamicDownforce();

        // Segura a moto no grid de largada durante a contagem 3, 2, 1
        if (LapManager.Instance != null && !LapManager.Instance.IsRaceStarted)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
            return;
        }

        ApplyEngineAndBrakes();
        ApplySpeedSensitiveSteering();
        ApplyDynamicTireGrip();
    }

    private void HandleWheelieMechanic()
    {
        if (LapManager.Instance != null && !LapManager.Instance.IsRaceStarted)
        {
            isWheelieActive = false;
            currentWheelieFactor = Mathf.MoveTowards(currentWheelieFactor, 0f, wheelieRampDownSpeed * Time.deltaTime);
            return;
        }

        float forwardSpeed = rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : 0f;
        bool canWheelie = inputWheelie && inputThrottle > 0.1f && forwardSpeed > 3.0f && !inputHandbrake;

        if (canWheelie)
        {
            isWheelieActive = true;
            currentWheelieFactor = Mathf.MoveTowards(currentWheelieFactor, 1f, wheelieRampUpSpeed * Time.deltaTime);
            // Empinar a moto recarrega o Turbo continuamente por estilo!
            currentTurbo = Mathf.Min(maxTurboCapacity, currentTurbo + 14f * Time.deltaTime);
        }
        else
        {
            isWheelieActive = false;
            currentWheelieFactor = Mathf.MoveTowards(currentWheelieFactor, 0f, wheelieRampDownSpeed * Time.deltaTime);
        }
    }

    private void EvaluatePlayerSlipstream()
    {
        if (opponentTarget == null)
        {
            inSlipstream = false;
            slipstreamCharge = Mathf.MoveTowards(slipstreamCharge, 0f, 0.5f * Time.deltaTime);
            return;
        }

        Vector3 toOpponent = opponentTarget.position - transform.position;
        float dist = toOpponent.magnitude;
        Vector3 flatFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 oppFwd = Vector3.ProjectOnPlane(opponentTarget.forward, Vector3.up).normalized;
        Vector3 flatToOpp = Vector3.ProjectOnPlane(toOpponent, Vector3.up).normalized;

        bool distOk = dist >= 4.0f && dist <= 19.5f;
        bool sameDir = Vector3.Dot(flatFwd, oppFwd) > 0.72f;
        bool inCone = Vector3.Dot(flatFwd, flatToOpp) > 0.88f;

        if (distOk && sameDir && inCone && rb.linearVelocity.magnitude > 6f)
        {
            inSlipstream = true;
            slipstreamCharge = Mathf.MoveTowards(slipstreamCharge, 1f, 0.75f * Time.deltaTime);
            currentTurbo = Mathf.Min(maxTurboCapacity, currentTurbo + 9f * Time.deltaTime);
        }
        else
        {
            inSlipstream = false;
            slipstreamCharge = Mathf.MoveTowards(slipstreamCharge, 0f, 0.4f * Time.deltaTime);
        }
    }

    private void HandleDriftAndSlingshot()
    {
        if (driftBoostTimer > 0f)
        {
            driftBoostTimer -= Time.deltaTime;
        }

        float speed = rb.linearVelocity.magnitude;
        float lateralVelMag = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.right));
        bool buttonDrift = speed > 5.0f && inputHandbrake && Mathf.Abs(inputSteer) > 0.15f;
        bool highSpeedCornerSlide = speed > 14.0f && Mathf.Abs(inputSteer) > 0.65f && lateralVelMag > 2.2f;

        if (buttonDrift || highSpeedCornerSlide)
        {
            isDrifting = true;
            float chargeRate = buttonDrift ? 0.95f : 0.45f;
            driftCharge = Mathf.MoveTowards(driftCharge, 1f, chargeRate * Time.deltaTime);
            currentTurbo = Mathf.Min(maxTurboCapacity, currentTurbo + (turboRechargeRate * 0.75f) * Time.deltaTime);
        }
        else
        {
            if (isDrifting && driftCharge >= 0.35f && speed > 5.0f)
            {
                driftBoostTimer = Mathf.Lerp(0.75f, 1.35f, driftCharge);
                currentTurbo = Mathf.Min(maxTurboCapacity, currentTurbo + 18f * driftCharge);
                rb.AddForce(transform.forward * (8.5f * driftCharge), ForceMode.VelocityChange);
                if (carAudio != null)
                {
                    carAudio.PlayDriftBoostBurst();
                }
            }
            isDrifting = false;
            driftCharge = Mathf.MoveTowards(driftCharge, 0f, 1.5f * Time.deltaTime);
        }
    }

    private void HandleTurboAbility()
    {
        if (LapManager.Instance != null && !LapManager.Instance.IsRaceStarted)
        {
            isTurboActive = false;
            CurrentTurboBlurFactor = 0f;
            return;
        }

        bool wantsTurbo = inputTurboKey && inputThrottle >= -0.05f;
        if (turboOverheated && currentTurbo >= minTurboToActivate)
        {
            turboOverheated = false;
        }

        if (wantsTurbo && !turboOverheated && currentTurbo > 0f)
        {
            if (!isTurboActive)
            {
                isTurboActive = true;
                rb.AddForce(transform.forward * turboActivationImpulse, ForceMode.VelocityChange);
            }
            if (inputThrottle < 0.8f) inputThrottle = 1.0f;
            currentTurbo -= turboBurnRate * Time.deltaTime;
            if (currentTurbo <= 0f)
            {
                currentTurbo = 0f;
                isTurboActive = false;
                turboOverheated = true;
            }
        }
        else
        {
            isTurboActive = false;
            if (currentTurbo < maxTurboCapacity)
            {
                currentTurbo = Mathf.Min(maxTurboCapacity, currentTurbo + turboRechargeRate * Time.deltaTime);
            }
        }

        float targetBlur = isTurboActive ? 1.0f : (driftBoostTimer > 0f ? 0.65f : 0.0f);
        float rampSpeed = (isTurboActive || driftBoostTimer > 0f) ? blurRampUpSpeed : blurRampDownSpeed;
        CurrentTurboBlurFactor = Mathf.MoveTowards(CurrentTurboBlurFactor, targetBlur, rampSpeed * Time.deltaTime);
    }

    private void UpdateGearbox()
    {
        float speed = rb.linearVelocity.magnitude;
        currentGear = 1;
        for (int i = 0; i < gearSpeedThresholds.Length; i++)
        {
            if (speed > gearSpeedThresholds[i])
            {
                currentGear = Mathf.Min(i + 2, gearSpeedThresholds.Length);
            }
        }

        int gearIdx = Mathf.Clamp(currentGear - 1, 0, gearSpeedThresholds.Length - 1);
        float lowThresh = gearIdx == 0 ? 0f : gearSpeedThresholds[gearIdx - 1];
        float highThresh = gearSpeedThresholds[gearIdx];
        currentGearRpmNorm = Mathf.Clamp01(Mathf.InverseLerp(lowThresh, highThresh, speed));
    }

    private void ApplyAerodynamicDownforce()
    {
        Vector3 vel = rb.linearVelocity;
        if (vel.y > 0.25f)
        {
            vel.y = 0f;
            rb.linearVelocity = vel;
        }

        float speedSq = rb.linearVelocity.sqrMagnitude;
        rb.AddForce(Vector3.down * (extraGravity + speedSq * downforceCoefficient), ForceMode.Acceleration);
    }

    private void ApplyEngineAndBrakes()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (inputHandbrake && inputThrottle <= 0.05f && Mathf.Abs(inputSteer) < 0.12f)
        {
            Vector3 brakeVec = -rb.linearVelocity.normalized * (brakeForce * 0.75f);
            rb.AddForce(new Vector3(brakeVec.x, 0f, brakeVec.z), ForceMode.Acceleration);
            return;
        }

        if (Mathf.Abs(inputThrottle) > 0.01f)
        {
            bool isBrakingAgainstMotion = (inputThrottle > 0f && forwardSpeed < -1.2f) ||
                                          (inputThrottle < 0f && forwardSpeed > 1.2f);
            if (isBrakingAgainstMotion)
            {
                Vector3 brakeDir = -Mathf.Sign(forwardSpeed) * transform.forward;
                rb.AddForce(brakeDir * brakeForce, ForceMode.Acceleration);
            }
            else
            {
                float gearMult = gearTorqueMultipliers[Mathf.Clamp(currentGear - 1, 0, gearTorqueMultipliers.Length - 1)];
                float launchBoost = Mathf.Abs(forwardSpeed) < 6.5f ? 1.28f : 1.0f;
                float turboForceMult = isTurboActive ? turboAccelerationMultiplier : (driftBoostTimer > 0f ? 1.65f : 1.0f);
                float turboSpeedMult = isTurboActive ? turboTopSpeedMultiplier : (driftBoostTimer > 0f ? 1.28f : 1.0f);
                float wheelieForceMult = isWheelieActive ? 1.20f : 1.0f;
                float wheelieSpeedMult = isWheelieActive ? 1.15f : 1.0f;
                float slipstreamMult = 1.0f + (slipstreamCharge * 0.22f);

                float handbrakeDrag = inputHandbrake ? 0.92f : 1.0f;
                float targetMax = (inputThrottle > 0f ? maxForwardSpeed : maxReverseSpeed) * turboSpeedMult * wheelieSpeedMult * slipstreamMult;

                if (Mathf.Abs(forwardSpeed) < targetMax)
                {
                    float finalForce = inputThrottle * enginePeakForce * gearMult * launchBoost * turboForceMult * wheelieForceMult * slipstreamMult * handbrakeDrag;
                    rb.AddForce(transform.forward * finalForce, ForceMode.Acceleration);
                }
            }
        }
    }

    private void ApplySpeedSensitiveSteering()
    {
        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.25f) return;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float reverseSign = forwardSpeed >= -0.2f ? 1f : -1f;
        float speedNormalized = Mathf.Clamp01(speed / maxForwardSpeed);
        float activeSteerTorque = Mathf.Lerp(lowSpeedSteerTorque, highSpeedSteerTorque, speedNormalized);

        if (inputHandbrake && speed > 4.0f)
        {
            activeSteerTorque *= driftSteerMultiplier;
        }
        else if (isWheelieActive)
        {
            // Durante o grau (empinada), a roda dianteira está no ar, então esterça pelo equilíbrio do corpo
            activeSteerTorque *= 0.82f;
        }
        else if (isTurboActive)
        {
            activeSteerTorque *= 0.88f;
        }

        float lowSpeedFactor = Mathf.Clamp01(speed / 1.8f);
        float yawStep = inputSteer * activeSteerTorque * lowSpeedFactor * reverseSign * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yawStep, 0f));
    }

    private void ApplyDynamicTireGrip()
    {
        float speed = rb.linearVelocity.magnitude;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float absSteer = Mathf.Abs(inputSteer);

        float gripDamping = straightGripDamping;
        if (inputHandbrake && speed > 4.0f && absSteer > 0.1f)
        {
            gripDamping = driftGripDamping;
        }
        else if (absSteer > 0.25f && speed > 8.0f)
        {
            float cornerFactor = Mathf.Clamp01((speed - 8.0f) / 18.0f) * absSteer;
            gripDamping = Mathf.Lerp(straightGripDamping, corneringGripDamping, cornerFactor);
        }

        if (forwardSpeed > 5.0f && absSteer > 0.15f)
        {
            float speedFactor = Mathf.Clamp01(forwardSpeed / maxForwardSpeed);
            float slideForce = inputHandbrake ? driftOutwardSlideForce : (naturalCornerSlideForce * absSteer);
            Vector3 outwardDir = -transform.right * Mathf.Sign(inputSteer);
            rb.AddForce(outwardDir * (slideForce * speedFactor), ForceMode.Acceleration);
        }

        Vector3 forwardVel = transform.forward * forwardSpeed;
        Vector3 lateralVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);

        float lateralRetention = Mathf.Exp(-gripDamping * Time.fixedDeltaTime);
        Vector3 newHorizontalVel = forwardVel + (lateralVel * lateralRetention);
        rb.linearVelocity = new Vector3(newHorizontalVel.x, rb.linearVelocity.y, newHorizontalVel.z);
    }

    private void UpdateVisualWeightTransfer()
    {
        if (visualMeshRoot == null) return;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float lateralSpeed = Vector3.Dot(rb.linearVelocity, transform.right);
        float accelerationDelta = (forwardSpeed - previousForwardSpeed) / Mathf.Max(Time.deltaTime, 0.001f);
        previousForwardSpeed = forwardSpeed;

        float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 16f);

        // 1. INCLINAÇÃO REAL DE MOTO NA CURVA (Leans INTO the turn: -inputSteer)
        // Virar p/ Direita (+inputSteer) inclina a moto e o piloto para a Direita (-Z roll) em torno do pneu!
        float activeMaxLean = isDrifting ? maxDriftLeanAngle : maxMotoLeanAngle;
        float targetRoll = -inputSteer * Mathf.Max(0.25f, speedRatio) * activeMaxLean;
        targetRoll = Mathf.Clamp(targetRoll, -maxDriftLeanAngle, maxDriftLeanAngle);

        // 2. EMPINAR A MOTO (Wheelie / Grau) + Transferência de Peso Longitudinal
        float wheeliePitch = -currentWheelieFactor * maxWheelieAngle;
        float turboPitchBoost = (isTurboActive || driftBoostTimer > 0f) ? -3.5f : 0f;
        float accelPitch = Mathf.Clamp((-accelerationDelta * 0.16f) + turboPitchBoost, -maxBodyPitchAngle, maxBodyPitchAngle);
        float targetPitch = wheeliePitch + accelPitch * (1f - currentWheelieFactor * 0.7f);

        // 3. ÂNGULO DE DERRAPAGEM (Yaw) na rabeta da moto durante o Drift
        float targetDriftYaw = 0f;
        if (forwardSpeed > 4.0f)
        {
            if (inputHandbrake && Mathf.Abs(inputSteer) > 0.12f)
            {
                targetDriftYaw = inputSteer * maxVisualDriftYaw * Mathf.Clamp01(forwardSpeed / 14f);
            }
            else
            {
                float slipRatio = Mathf.Clamp(-lateralSpeed / 9.0f, -1f, 1f);
                targetDriftYaw = (inputSteer * 0.6f + slipRatio * 0.4f) * maxNaturalCornerYaw * speedRatio;
            }
        }

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, leanSmoothSpeed * Time.deltaTime);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, leanSmoothSpeed * Time.deltaTime);
        currentDriftYaw = Mathf.Lerp(currentDriftYaw, targetDriftYaw, (leanSmoothSpeed * 0.85f) * Time.deltaTime);

        // Quando a moto empina (currentPitch < -1°), rotaciona em torno do eixo da roda traseira para o pneu traseiro continuar colado no chão!
        if (currentPitch < -0.5f)
        {
            float rad = Mathf.Abs(currentPitch) * Mathf.Deg2Rad;
            float liftY = Mathf.Sin(rad) * rearAxlePivotZ;
            float shiftZ = -(1f - Mathf.Cos(rad)) * rearAxlePivotZ;
            visualMeshRoot.localPosition = initialVisualLocalPos + new Vector3(0f, liftY, shiftZ);
        }
        else
        {
            visualMeshRoot.localPosition = Vector3.Lerp(visualMeshRoot.localPosition, initialVisualLocalPos, 15f * Time.deltaTime);
        }

        visualMeshRoot.localRotation = initialVisualLocalRot * Quaternion.Euler(currentPitch, currentDriftYaw, currentRoll);
    }
}
