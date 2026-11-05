using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class ArcadeCarController : MonoBehaviour
{
    [Header("Engine & Gearbox (Simcade)")]
    [SerializeField] private float enginePeakForce = 48f;
    [SerializeField] private float maxForwardSpeed = 29.5f;
    [SerializeField] private float maxReverseSpeed = 13f;
    [SerializeField] private float brakeForce = 62f;
    [SerializeField] private float[] gearSpeedThresholds = { 7f, 13f, 19f, 24f, 29.5f };
    [SerializeField] private float[] gearTorqueMultipliers = { 1.45f, 1.22f, 1.06f, 0.94f, 0.85f };

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

    [Header("Drift, Dynamic Corner Slide & Tire Physics")]
    [SerializeField] private float lowSpeedSteerTorque = 140f;
    [SerializeField] private float highSpeedSteerTorque = 88f;
    [SerializeField] private float driftSteerMultiplier = 1.42f;
    [SerializeField] private float straightGripDamping = 9.0f;
    [SerializeField] private float corneringGripDamping = 3.4f;
    [SerializeField] private float driftGripDamping = 1.25f;
    [SerializeField] private float driftOutwardSlideForce = 16.5f;
    [SerializeField] private float naturalCornerSlideForce = 6.5f;
    [SerializeField] private float downforceCoefficient = 0.65f;
    [SerializeField] private float extraGravity = 28f;
    [SerializeField] private float rollingResistance = 0.75f;

    [Header("Chassis Weight Transfer & Visual Drift Angle")]
    [SerializeField] private float maxBodyRollAngle = 7.5f;
    [SerializeField] private float maxBodyPitchAngle = 4.2f;
    [SerializeField] private float maxVisualDriftYaw = 23f;
    [SerializeField] private float maxNaturalCornerYaw = 8.5f;
    [SerializeField] private float suspensionSmoothSpeed = 10f;

    private Rigidbody rb;
    private ArcadeCarAudio carAudio;
    private Transform opponentTarget;
    private Transform visualMeshRoot;
    private Quaternion initialVisualLocalRot;
    private float inputThrottle;
    private float inputSteer;
    private bool inputHandbrake;
    private bool inputTurboKey;
    private float currentTurbo = 100f;
    private bool isTurboActive = false;
    private bool turboOverheated = false;
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

        // Carro pesado e plantado no asfalto (1.8 tonelada + centro de gravidade baixo + sem capotar)
        rb.mass = 1800f;
        rb.centerOfMass = new Vector3(0f, -0.85f, 0f);
        rb.linearDamping = rollingResistance;
        rb.angularDamping = 5.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Garante que os novos parâmetros de Turbo, Drift e Peso sejam aplicados mesmo em objetos já serializados na cena
        turboAccelerationMultiplier = 2.25f;
        turboTopSpeedMultiplier = 1.50f;
        turboActivationImpulse = 13.5f;
        driftSteerMultiplier = 1.42f;
        straightGripDamping = 9.0f;
        corneringGripDamping = 3.4f;
        driftGripDamping = 1.25f;
        driftOutwardSlideForce = 16.5f;
        naturalCornerSlideForce = 6.5f;
        extraGravity = 28f;
        maxVisualDriftYaw = 23f;
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
            PhysicsMaterial mat = new PhysicsMaterial("CarHeavyZeroBounce")
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
        }

        if (existingVisual != null)
        {
            MeshFilter mf = existingVisual.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Bounds b = mf.sharedMesh.bounds;
                float maxHoriz = Mathf.Max(b.size.x, b.size.z);
                if (maxHoriz > 0.01f && maxHoriz < 2.0f && existingVisual.localScale.magnitude < 2.5f)
                {
                    float scaleFactor = 3.1f / maxHoriz;
                    existingVisual.localScale = Vector3.one * scaleFactor;
                }
                if (b.size.x > b.size.z * 1.2f)
                {
                    existingVisual.localRotation = Quaternion.Euler(0f, -90f, 0f);
                }
            }
            visualMeshRoot = existingVisual;
            initialVisualLocalRot = visualMeshRoot.localRotation;
        }
    }

    private void ReadPlayerInput()
    {
        float rawThrottle = 0f;
        float rawSteer = 0f;
        bool rawHandbrake = false;
        bool rawTurbo = false;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawThrottle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawThrottle -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawSteer += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawSteer -= 1f;
            rawHandbrake = kb.spaceKey.isPressed;
            rawTurbo = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        }

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            // Controle Xbox 360:
            // A (buttonSouth) = Acelerar
            // Y (buttonNorth) = Frear / Dar Ré
            // B (buttonEast)  = Turbo
            // X (buttonWest)  = Derrapar (Drift)
            if (gp.buttonSouth.isPressed) rawThrottle += 1f;
            if (gp.buttonNorth.isPressed) rawThrottle -= 1f;

            float gpSteer = gp.leftStick.x.ReadValue();
            if (Mathf.Abs(gpSteer) < 0.12f) gpSteer = 0f;
            float dpadSteer = gp.dpad.x.ReadValue();
            if (Mathf.Abs(dpadSteer) > Mathf.Abs(gpSteer)) gpSteer = dpadSteer;
            if (Mathf.Abs(gpSteer) > Mathf.Abs(rawSteer)) rawSteer = gpSteer;

            if (gp.buttonWest.isPressed) rawHandbrake = true;
            if (gp.buttonEast.isPressed) rawTurbo = true;
        }

        // Suporte complementar para driver Xbox 360 via Legacy Input / Joystick no Linux
        try
        {
            if (Input.GetKey(KeyCode.JoystickButton0)) rawThrottle += 1f; // Botão A -> Acelerar
            if (Input.GetKey(KeyCode.JoystickButton3)) rawThrottle -= 1f; // Botão Y -> Frear / Ré
            if (Input.GetKey(KeyCode.JoystickButton2)) rawHandbrake = true; // Botão X -> Derrapar (Drift)
            if (Input.GetKey(KeyCode.JoystickButton1)) rawTurbo = true; // Botão B -> Turbo

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
        inputSteer = Mathf.MoveTowards(inputSteer, Mathf.Clamp(rawSteer, -1f, 1f), 9.0f * Time.deltaTime);
        inputHandbrake = rawHandbrake;
        inputTurboKey = rawTurbo;
    }

    private void Update()
    {
        ReadPlayerInput();
        EvaluatePlayerSlipstream();
        HandleDriftAndSlingshot();
        HandleTurboAbility();
        UpdateGearbox();
        UpdateVisualWeightTransfer();
    }

    private void FixedUpdate()
    {
        ApplyAerodynamicDownforce();
        ApplyEngineAndBrakes();
        ApplySpeedSensitiveSteering();
        ApplyDynamicTireGrip();
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
                // Dispara Drift Boost ao alinhar o carro na saída da curva!
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
            // Se apertar o botão B (Turbo), força aceleração máxima imediata
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
        // Impede qualquer pulo ou voo vertical após colisões
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

        // Se apertar X (Drift) parado ou em linha reta sem acelerar, freia
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
                float slipstreamMult = 1.0f + (slipstreamCharge * 0.22f);

                float handbrakeDrag = inputHandbrake ? 0.92f : 1.0f;
                float targetMax = (inputThrottle > 0f ? maxForwardSpeed : maxReverseSpeed) * turboSpeedMult * slipstreamMult;

                if (Mathf.Abs(forwardSpeed) < targetMax)
                {
                    float finalForce = inputThrottle * enginePeakForce * gearMult * launchBoost * turboForceMult * slipstreamMult * handbrakeDrag;
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

        // Calcula o nível de deslize lateral:
        // - Em linha reta: alta aderência (straightGripDamping)
        // - Em curva rápida normal: deslize dinâmico progressivo (corneringGripDamping)
        // - Segurando X (Drift): deslize acentuado estilo arcade (driftGripDamping)
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

        // Força centrífuga que joga a traseira para fora da curva, gerando o deslize visível na pista
        if (forwardSpeed > 5.0f && absSteer > 0.15f)
        {
            float speedFactor = Mathf.Clamp01(forwardSpeed / maxForwardSpeed);
            float slideForce = inputHandbrake ? driftOutwardSlideForce : (naturalCornerSlideForce * absSteer);
            Vector3 outwardDir = -transform.right * Mathf.Sign(inputSteer);
            rb.AddForce(outwardDir * (slideForce * speedFactor), ForceMode.Acceleration);
        }

        Vector3 forwardVel = transform.forward * forwardSpeed;
        Vector3 lateralVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);

        // Amortecimento exponencial suave independente de framerate (permite o carro escorregar de lado na curva!)
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

        float speedRatio = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxForwardSpeed);
        float driftRollBoost = isDrifting ? 1.35f : 1.0f;
        float targetRoll = Mathf.Clamp(inputSteer * speedRatio * maxBodyRollAngle * driftRollBoost, -maxBodyRollAngle * 1.3f, maxBodyRollAngle * 1.3f);

        float turboPitchBoost = (isTurboActive || driftBoostTimer > 0f) ? -3.0f : 0f;
        float targetPitch = Mathf.Clamp((-accelerationDelta * 0.18f) + turboPitchBoost, -maxBodyPitchAngle, maxBodyPitchAngle);

        // Ângulo visual de derrapagem (Yaw): atravessa o carro na curva durante o Drift e curvas fortes
        float targetDriftYaw = 0f;
        if (forwardSpeed > 4.0f)
        {
            if (inputHandbrake && Mathf.Abs(inputSteer) > 0.12f)
            {
                targetDriftYaw = inputSteer * maxVisualDriftYaw * Mathf.Clamp01(forwardSpeed / 14f);
            }
            else
            {
                float slipRatio = Mathf.Clamp( -lateralSpeed / 9.0f, -1f, 1f);
                targetDriftYaw = (inputSteer * 0.6f + slipRatio * 0.4f) * maxNaturalCornerYaw * speedRatio;
            }
        }

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, suspensionSmoothSpeed * Time.deltaTime);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, suspensionSmoothSpeed * Time.deltaTime);
        currentDriftYaw = Mathf.Lerp(currentDriftYaw, targetDriftYaw, (suspensionSmoothSpeed * 0.85f) * Time.deltaTime);

        visualMeshRoot.localRotation = initialVisualLocalRot * Quaternion.Euler(currentPitch, currentDriftYaw, currentRoll);
    }
}
