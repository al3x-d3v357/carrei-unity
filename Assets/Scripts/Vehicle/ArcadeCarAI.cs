using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(ArcadeCollisionBumper))]
public class ArcadeCarAI : MonoBehaviour
{
    [Header("Autonomous Circuit Waypoints")]
    public List<Transform> circuitWaypoints = new List<Transform>();
    public Transform playerTarget;
    [SerializeField] private float waypointReachRadius = 9.5f;
    [SerializeField] private int currentWaypointIndex = 0;

    [Header("Simcade AI Engine & Cornering")]
    [SerializeField] private float engineForce = 44f;
    [SerializeField] private float maxStraightSpeed = 29.5f;
    [SerializeField] private float minCornerSpeed = 19.5f;
    [SerializeField] private float brakeForce = 48f;
    [SerializeField] private float lowSpeedTurnTorque = 145f;
    [SerializeField] private float highSpeedTurnTorque = 95f;
    [SerializeField] private float tireGrip = 0.92f;

    [Header("Slipstream & Overtake Behavior")]
    [SerializeField] private float slipstreamMaxDist = 18f;
    [SerializeField] private float slipstreamMinDist = 4.5f;
    [SerializeField] private float slipstreamConeDot = 0.92f;
    [SerializeField] private float slipstreamBoostMax = 1.28f;
    [SerializeField] private float draftChargeSpeed = 0.65f;
    [SerializeField] private float overtakeTriggerDist = 13f;
    [SerializeField] private float lateralLaneOffset = 3.6f;
    [SerializeField] private float sideClearanceCheck = 4.5f;
    private float currentDraftCharge = 0f;
    private int currentOvertakeSide = 0;

    [Header("Nitro Boost System")]
    [SerializeField] private float nitroMultiplier = 1.45f;
    [SerializeField] private float maxNitro = 100f;
    [SerializeField] private float nitroBurnRate = 32f;
    [SerializeField] private float nitroRechargeRate = 11f;
    [SerializeField] private float currentNitro = 100f;
    private bool isNitroActive = false;

    [Header("Raycast Sensors & Anti-Stuck Recovery")]
    [SerializeField] private float frontSensorLength = 9.0f;
    [SerializeField] private float sideSensorLength = 5.5f;
    [SerializeField] private float diagSensorAngle = 22f;
    [SerializeField] private float wideSensorAngle = 48f;
    [SerializeField] private float sensorHeight = 0.4f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    private float stuckTimer = 0f;
    private float reverseRecoveryTimer = 0f;
    private float reverseSteerDirection = 1f;

    private Rigidbody rb;
    private Transform visualMeshRoot;
    private Quaternion initialVisualLocalRot;
    private float currentThrottle;
    private float currentSteer;
    private float desiredTargetSpeed;
    private Vector3 activeTargetPoint;

    public bool InSlipstream { get; private set; }
    public bool IsNitroActive => isNitroActive;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1800f;
        rb.centerOfMass = new Vector3(0f, -0.85f, 0f);
        rb.linearDamping = 0.8f;
        rb.angularDamping = 5.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            PhysicsMaterial slipMat = new PhysicsMaterial("ArcadeCarLowFriction_AI")
            {
                dynamicFriction = 0.04f,
                staticFriction = 0.04f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            box.sharedMaterial = slipMat;
        }

        if (GetComponent<ArcadeCarAudio>() == null)
        {
            gameObject.AddComponent<ArcadeCarAudio>();
        }

        EnsureVisualMeshHierarchy();
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
                Mesh mesh = mf.sharedMesh;
                Bounds b = mesh.bounds;
                float maxHoriz = Mathf.Max(b.size.x, b.size.z);
                if (maxHoriz > 0.01f && maxHoriz < 2.0f && existingVisual.localScale.magnitude < 2.5f)
                {
                    float scaleFactor = 3.1f / maxHoriz;
                    existingVisual.localScale = Vector3.one * scaleFactor;
                }
                if (b.size.x > b.size.z * 1.2f)
                {
                    Quaternion baseRot = Quaternion.Euler(0f, -90f, 0f);
                    if (mesh.isReadable)
                    {
                        Vector3[] verts = mesh.vertices;
                        float maxYFront = 0f, maxYRear = 0f, threshold = maxHoriz * 0.22f;
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 vAligned = baseRot * verts[i];
                            if (vAligned.z > threshold && vAligned.y > maxYFront) maxYFront = vAligned.y;
                            if (vAligned.z < -threshold && vAligned.y > maxYRear) maxYRear = vAligned.y;
                        }
                        if (maxYFront > maxYRear) baseRot = Quaternion.Euler(0f, 180f, 0f) * baseRot;
                    }
                    existingVisual.localRotation = baseRot;
                }
            }
            visualMeshRoot = existingVisual;
            initialVisualLocalRot = visualMeshRoot.localRotation;
        }
    }

    private void Start()
    {
        gameObject.tag = "Vehicle";
        if (playerTarget == null)
        {
            GameObject playerGo = GameObject.Find("Player_Car");
            if (playerGo != null) playerTarget = playerGo.transform;
        }
        // Busca automaticamente o container de waypoints caso nao tenha sido atribuido
        if (circuitWaypoints == null || circuitWaypoints.Count == 0)
        {
            GameObject wpRoot = GameObject.Find("RacingLine_Waypoints");
            if (wpRoot != null)
            {
                circuitWaypoints = new List<Transform>();
                foreach (Transform child in wpRoot.transform)
                {
                    circuitWaypoints.Add(child);
                }
            }
        }
    }

    private void Update()
    {
        HandleStuckDetection();
        EvaluateSlipstream();
        ComputeAutonomousRacingLogic();
        HandleNitroTank();
        UpdateVisualRoll();
    }

    private void FixedUpdate()
    {
        ApplyDownforce();

        // Segura o oponente IA no grid de largada durante a contagem 3, 2, 1
        if (LapManager.Instance != null && !LapManager.Instance.IsRaceStarted)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
            return;
        }

        ApplyDriveAndBraking();
        ApplySteering();
        ApplyLateralTireFriction();
    }

    private void HandleStuckDetection()
    {
        if (reverseRecoveryTimer > 0f)
        {
            reverseRecoveryTimer -= Time.deltaTime;
            return;
        }
        if (rb.linearVelocity.magnitude < 1.1f && Mathf.Abs(currentThrottle) > 0.2f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.4f)
            {
                // Engata manobra de re por 1.2 segundos para sair da parede
                reverseRecoveryTimer = 1.2f;
                stuckTimer = 0f;
                reverseSteerDirection = (Random.value > 0.5f) ? 1f : -1f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    private void EvaluateSlipstream()
    {
        if (playerTarget == null)
        {
            InSlipstream = false;
            return;
        }
        Vector3 toPlayer = playerTarget.position - transform.position;
        float dist = toPlayer.magnitude;
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 playerForward = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;
        Vector3 flatToPlayer = Vector3.ProjectOnPlane(toPlayer, Vector3.up).normalized;

        bool distOk = dist >= slipstreamMinDist && dist <= slipstreamMaxDist;
        bool sameHeading = Vector3.Dot(flatForward, playerForward) > 0.75f;
        bool inCone = Vector3.Dot(flatForward, flatToPlayer) > slipstreamConeDot;

        if (distOk && sameHeading && inCone)
        {
            InSlipstream = true;
            currentDraftCharge = Mathf.MoveTowards(currentDraftCharge, 1f, draftChargeSpeed * Time.deltaTime);
        }
        else
        {
            InSlipstream = false;
            currentDraftCharge = Mathf.MoveTowards(currentDraftCharge, 0f, (draftChargeSpeed * 0.5f) * Time.deltaTime);
        }
    }

    private void ComputeAutonomousRacingLogic()
    {
        // Se estiver em recuperacao de colisao, executa marcha a re orientada
        if (reverseRecoveryTimer > 0f)
        {
            currentThrottle = -0.85f;
            currentSteer = reverseSteerDirection;
            isNitroActive = false;
            desiredTargetSpeed = maxStraightSpeed;
            return;
        }

        // 1. Determina o ponto base da pista (Waypoint atual)
        Vector3 baseTrackPoint = GetNextCircuitPoint(out float upcomingCornerAngle);

        // 2. Aplica deslocamento lateral se estiver disputando ultrapassagem com o jogador
        activeTargetPoint = ApplyOvertakeOffsetIfCompeting(baseTrackPoint);

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatToTarget = Vector3.ProjectOnPlane(activeTargetPoint - transform.position, Vector3.up).normalized;
        float angleToWaypoint = Vector3.SignedAngle(flatForward, flatToTarget, Vector3.up);
        float targetSteer = Mathf.Clamp(angleToWaypoint / 38f, -1f, 1f);

        // 3. Sensores de desvio de obstaculos e muros
        float avoidanceSteer = ComputeRaycastAvoidance(out bool frontBlockedImmediate);
        if (Mathf.Abs(avoidanceSteer) > 0.08f)
        {
            currentSteer = Mathf.Clamp(targetSteer * 0.35f + avoidanceSteer, -1f, 1f);
            isNitroActive = false;
        }
        else
        {
            currentSteer = targetSteer;
        }

        // 4. Calculo da Velocidade Ideal de Curva (Frenagem Preditiva inteligente apenas perto da curva)
        float distToTarget = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(baseTrackPoint.x, 0f, baseTrackPoint.z)
        );
        float proximityWeight = Mathf.InverseLerp(22f, 8f, distToTarget);
        float totalCornerSeverity = Mathf.Clamp01((Mathf.Abs(angleToWaypoint) + upcomingCornerAngle * 0.45f * proximityWeight) / 68f);
        desiredTargetSpeed = Mathf.Lerp(maxStraightSpeed, minCornerSpeed, totalCornerSeverity);
        if (frontBlockedImmediate)
        {
            desiredTargetSpeed = minCornerSpeed * 0.78f;
        }

        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed > desiredTargetSpeed + 3.5f && Mathf.Abs(angleToWaypoint) > 20f)
        {
            // Freia suavemente na entrada de curvas fechadas
            currentThrottle = -0.55f;
            isNitroActive = false;
        }
        else
        {
            currentThrottle = Mathf.Lerp(1.0f, 0.72f, totalCornerSeverity);
            isNitroActive = ShouldTriggerNitro(totalCornerSeverity, frontBlockedImmediate);
        }
    }

    private Vector3 GetNextCircuitPoint(out float upcomingCornerAngle)
    {
        upcomingCornerAngle = 0f;
        if (circuitWaypoints == null || circuitWaypoints.Count == 0)
        {
            return playerTarget != null ? playerTarget.position : transform.position + transform.forward * 10f;
        }

        Transform currentWp = circuitWaypoints[currentWaypointIndex];
        float distToWp = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(currentWp.position.x, 0f, currentWp.position.z)
        );

        if (distToWp < waypointReachRadius)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % circuitWaypoints.Count;
            currentWp = circuitWaypoints[currentWaypointIndex];
        }

        // Calcula o angulo da curva seguinte para antecipar a frenagem
        int nextWpIdx = (currentWaypointIndex + 1) % circuitWaypoints.Count;
        Transform nextWp = circuitWaypoints[nextWpIdx];
        Vector3 seg1 = Vector3.ProjectOnPlane(currentWp.position - transform.position, Vector3.up).normalized;
        Vector3 seg2 = Vector3.ProjectOnPlane(nextWp.position - currentWp.position, Vector3.up).normalized;
        upcomingCornerAngle = Vector3.Angle(seg1, seg2);

        return currentWp.position;
    }

    private Vector3 ApplyOvertakeOffsetIfCompeting(Vector3 waypointPos)
    {
        if (playerTarget == null) return waypointPos;
        Vector3 toPlayer = playerTarget.position - transform.position;
        float distToPlayer = toPlayer.magnitude;
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 playerForward = Vector3.ProjectOnPlane(playerTarget.forward, Vector3.up).normalized;

        bool approachingPlayerRear = distToPlayer < overtakeTriggerDist &&
            Vector3.Dot(flatForward, toPlayer.normalized) > 0.45f &&
            Vector3.Dot(flatForward, playerForward) > 0.55f;

        if (!approachingPlayerRear)
        {
            currentOvertakeSide = 0;
            return waypointPos;
        }

        Vector3 origin = transform.position + Vector3.up * sensorHeight;
        Vector3 rightVec = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        bool rightBlocked = Physics.Raycast(origin, rightVec, sideClearanceCheck, obstacleMask);
        bool leftBlocked = Physics.Raycast(origin, -rightVec, sideClearanceCheck, obstacleMask);

        if (currentOvertakeSide == 0)
        {
            if (!leftBlocked) currentOvertakeSide = -1;
            else if (!rightBlocked) currentOvertakeSide = 1;
        }
        else if (currentOvertakeSide == 1 && rightBlocked && !leftBlocked)
        {
            currentOvertakeSide = -1;
        }
        else if (currentOvertakeSide == -1 && leftBlocked && !rightBlocked)
        {
            currentOvertakeSide = 1;
        }

        return waypointPos + (rightVec * (currentOvertakeSide * lateralLaneOffset));
    }

    private bool ShouldTriggerNitro(float cornerSeverity, bool frontBlocked)
    {
        if (frontBlocked || currentNitro <= 15f) return false;
        // Aciona nitro apenas quando alinhado na reta ou saindo do vacuo para ultrapassar
        if (cornerSeverity > 0.22f) return false;
        bool slingshotReady = currentDraftCharge > 0.45f && currentOvertakeSide != 0;
        bool straightLineBurst = cornerSeverity < 0.10f && currentNitro > 55f;
        return slingshotReady || straightLineBurst;
    }

    private void HandleNitroTank()
    {
        if (isNitroActive)
        {
            currentNitro -= nitroBurnRate * Time.deltaTime;
            if (currentNitro <= 0f)
            {
                currentNitro = 0f;
                isNitroActive = false;
            }
        }
        else if (currentNitro < maxNitro)
        {
            currentNitro = Mathf.Min(maxNitro, currentNitro + nitroRechargeRate * Time.deltaTime);
        }
    }

    private void ApplyDownforce()
    {
        Vector3 vel = rb.linearVelocity;
        if (vel.y > 0.25f)
        {
            vel.y = 0f;
            rb.linearVelocity = vel;
        }
        float speedSq = rb.linearVelocity.sqrMagnitude;
        rb.AddForce(Vector3.down * (28f + speedSq * 0.65f), ForceMode.Acceleration);
    }

    private void ApplyDriveAndBraking()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (currentThrottle < -0.05f && forwardSpeed > 2.0f && reverseRecoveryTimer <= 0f)
        {
            // Frenagem ativa de curva
            rb.AddForce(-transform.forward * brakeForce, ForceMode.Acceleration);
            return;
        }

        float boost = isNitroActive ? nitroMultiplier : 1.0f;
        float slipstreamMult = 1.0f + (currentDraftCharge * (slipstreamBoostMax - 1.0f));
        float totalMult = boost * slipstreamMult;
        float capSpeed = desiredTargetSpeed * totalMult;

        if (rb.linearVelocity.magnitude < capSpeed || currentThrottle < 0f)
        {
            rb.AddForce(transform.forward * (currentThrottle * engineForce * totalMult), ForceMode.Acceleration);
        }
    }

    private void ApplySteering()
    {
        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.35f) return;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float dirSign = forwardSpeed >= -0.2f ? 1f : -1f;
        float speedNorm = Mathf.Clamp01(speed / maxStraightSpeed);
        float steerAuthority = Mathf.Lerp(lowSpeedTurnTorque, highSpeedTurnTorque, speedNorm);
        if (isNitroActive) steerAuthority *= 0.8f;
        float yaw = currentSteer * steerAuthority * dirSign * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));
    }

    private void ApplyLateralTireFriction()
    {
        Vector3 forwardVel = transform.forward * Vector3.Dot(rb.linearVelocity, transform.forward);
        Vector3 lateralVel = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);
        float retention = 1f - (tireGrip * 0.35f);
        Vector3 horiz = forwardVel + (lateralVel * retention);
        rb.linearVelocity = new Vector3(horiz.x, rb.linearVelocity.y, horiz.z);
    }

    private float ComputeRaycastAvoidance(out bool frontBlockedImmediate)
    {
        frontBlockedImmediate = false;
        float steerAdjust = 0f;
        Vector3 origin = transform.position + Vector3.up * sensorHeight;
        Vector3 fwd = transform.forward;
        Vector3 leftDiag = Quaternion.Euler(0f, -diagSensorAngle, 0f) * fwd;
        Vector3 rightDiag = Quaternion.Euler(0f, diagSensorAngle, 0f) * fwd;
        Vector3 leftWide = Quaternion.Euler(0f, -wideSensorAngle, 0f) * fwd;
        Vector3 rightWide = Quaternion.Euler(0f, wideSensorAngle, 0f) * fwd;

        if (Physics.Raycast(origin, fwd, out RaycastHit hitF, frontSensorLength, obstacleMask) && !hitF.collider.isTrigger)
        {
            frontBlockedImmediate = hitF.distance < frontSensorLength * 0.55f;
            float sideSign = Vector3.Dot(transform.right, hitF.normal) >= 0f ? 1f : -1f;
            steerAdjust += sideSign * 1.0f;
        }
        if (Physics.Raycast(origin, leftDiag, out RaycastHit hitLD, frontSensorLength * 0.85f, obstacleMask) && !hitLD.collider.isTrigger)
        {
            steerAdjust += 0.85f * (1f - (hitLD.distance / (frontSensorLength * 0.85f)));
        }
        else if (Physics.Raycast(origin, rightDiag, out RaycastHit hitRD, frontSensorLength * 0.85f, obstacleMask) && !hitRD.collider.isTrigger)
        {
            steerAdjust -= 0.85f * (1f - (hitRD.distance / (frontSensorLength * 0.85f)));
        }

        if (Physics.Raycast(origin, leftWide, out RaycastHit hitLW, sideSensorLength, obstacleMask) && !hitLW.collider.isTrigger)
        {
            steerAdjust += 0.5f * (1f - (hitLW.distance / sideSensorLength));
        }
        else if (Physics.Raycast(origin, rightWide, out RaycastHit hitRW, sideSensorLength, obstacleMask) && !hitRW.collider.isTrigger)
        {
            steerAdjust -= 0.5f * (1f - (hitRW.distance / sideSensorLength));
        }

        return steerAdjust;
    }

    private void UpdateVisualRoll()
    {
        if (visualMeshRoot == null) return;
        float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / maxStraightSpeed);
        float rollAngle = Mathf.Clamp(currentSteer * speedRatio * 6.0f, -6.0f, 6.0f);
        visualMeshRoot.localRotation = Quaternion.Lerp(
            visualMeshRoot.localRotation,
            initialVisualLocalRot * Quaternion.Euler(0f, 0f, rollAngle),
            10f * Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(activeTargetPoint, 1.0f);
        Gizmos.DrawLine(transform.position, activeTargetPoint);
    }
}
