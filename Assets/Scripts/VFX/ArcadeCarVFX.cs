using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCarVFX : MonoBehaviour
{
    [Header("VFX Materials (Auto-Assigned by Builder)")]
    public Material trailNeonMat;
    public Material tireMarkMat;
    public Material smokeSprayMat;
    public Material nitroFlameMat;
    public Material sparkMat;

    [Header("Lighting Colors")]
    public Color headlightColor = new Color(0.85f, 0.95f, 1.0f, 1f);
    public Color taillightNeonColor = new Color(3.5f, 0.1f, 0.25f, 1f);
    public Color nitroCoreColor = new Color(0.2f, 2.5f, 4.0f, 1f);

    private Rigidbody rb;
    private ArcadeCarController playerCtrl;
    private ArcadeCarAI aiCtrl;
    private TrailRenderer[] taillightTrails = new TrailRenderer[2];
    private TrailRenderer[] nitroJetTrails = new TrailRenderer[2];
    private TrailRenderer[] tireSkidTrails = new TrailRenderer[2];
    private ParticleSystem driftSprayPS;
    private ParticleSystem driftSparksPS;
    private ParticleSystem nitroFlamePS;
    private ParticleSystem collisionSparksPS;
    private Light rearGlowLight;
    private bool wasNitroActive = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCtrl = GetComponent<ArcadeCarController>();
        aiCtrl = GetComponent<ArcadeCarAI>();
        BuildHeadlightsAndRearGlow();
        BuildTaillightRibbons();
        BuildNitroJetRibbons();
        BuildTireSkidMarks();
        BuildDriftWaterSpray();
        BuildDriftSparks();
        BuildNitroExhaustFlames();
        BuildCollisionSparks();
    }

    private void Update()
    {
        if (rb == null) return;
        float speed = rb.linearVelocity.magnitude;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float lateralSlip = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.right));

        bool isPlayerDrifting = playerCtrl != null && (playerCtrl.IsDrifting || (Mathf.Abs(playerCtrl.VisualDriftYaw) > 3.5f && speed > 7f));
        bool isDrifting = isPlayerDrifting || (lateralSlip > 1.8f && speed > 6f);

        bool isTurbo = playerCtrl != null && playerCtrl.IsTurboActive && forwardSpeed > 3f;
        bool isDriftBoost = playerCtrl != null && playerCtrl.IsDriftBoostActive && forwardSpeed > 3f;
        bool isAiNitro = aiCtrl != null && aiCtrl.IsNitroActive && forwardSpeed > 3f;
        bool isNitro = isTurbo || isDriftBoost || isAiNitro;

        // 1. Fitas de luz traseiras (ficam mais largas durante o Turbo)
        bool emitRibbons = speed > 4.5f;
        foreach (var tr in taillightTrails)
        {
            if (tr != null)
            {
                tr.emitting = emitRibbons;
                tr.startWidth = isNitro ? 0.12f : 0.06f;
            }
        }

        // 2. Jatos duplos de plasma azul/ciano no escapamento durante o Turbo / Drift Boost
        foreach (var nt in nitroJetTrails)
        {
            if (nt != null)
            {
                nt.emitting = isNitro;
            }
        }

        // 3. Marcas de pneu no asfalto durante o deslize nas curvas e Drift
        foreach (var skid in tireSkidTrails)
        {
            if (skid != null) skid.emitting = isDrifting;
        }

        // 4. Fumaça e spray denso nos pneus traseiros durante o Drift / deslize de curva
        if (driftSprayPS != null)
        {
            var emission = driftSprayPS.emission;
            emission.rateOverTime = isDrifting ? 65f : 0f;
        }

        // 5. Faíscas de Drift nas rodas traseiras (mudam de Laranja para Azul Ciano quando o Drift Boost está pronto!)
        if (driftSparksPS != null)
        {
            var emission = driftSparksPS.emission;
            bool emitDriftSparks = isDrifting && speed > 7.5f;
            emission.rateOverTime = emitDriftSparks ? 48f : 0f;
            if (emitDriftSparks && playerCtrl != null)
            {
                var main = driftSparksPS.main;
                main.startColor = playerCtrl.DriftCharge >= 0.40f
                    ? new Color(0.15f, 0.90f, 1.0f, 1f) // Azul Ciano: Drift Boost Carregado!
                    : new Color(1.0f, 0.60f, 0.12f, 1f); // Laranja Fogo: Carregando Drift!
            }
        }

        // 6. Chamas intensas de Turbo / Nitro no escapamento
        if (nitroFlamePS != null)
        {
            if (isNitro && !wasNitroActive)
            {
                // Explosão imediata ao apertar o botão B (Turbo)
                nitroFlamePS.Emit(28);
            }
            var emission = nitroFlamePS.emission;
            emission.rateOverTime = isNitro ? 95f : 0f;
            var main = nitroFlamePS.main;
            main.startColor = isDriftBoost && !isTurbo
                ? new Color(1.0f, 0.65f, 0.15f, 0.95f)
                : new Color(0.15f, 0.85f, 1.0f, 0.95f);
        }
        wasNitroActive = isNitro;

        // 7. Iluminação intensa do Turbo / Freio no asfalto
        if (rearGlowLight != null)
        {
            bool braking = (playerCtrl != null && playerCtrl.ThrottleInput < -0.1f && forwardSpeed > 1.0f);
            rearGlowLight.intensity = isNitro ? 6.5f : (braking ? 2.5f : 1.1f);
            rearGlowLight.range = isNitro ? 9.5f : 5.5f;
            rearGlowLight.color = isNitro
                ? (isDriftBoost && !isTurbo ? new Color(1.0f, 0.55f, 0.1f) : new Color(0.1f, 0.85f, 1.0f))
                : new Color(1.0f, 0.08f, 0.12f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact < 4.5f) return;
        if (collisionSparksPS != null && collision.contactCount > 0)
        {
            ContactPoint cp = collision.GetContact(0);
            collisionSparksPS.transform.position = cp.point + Vector3.up * 0.2f;
            collisionSparksPS.transform.rotation = Quaternion.LookRotation(cp.normal + Vector3.up * 0.5f);
            collisionSparksPS.Emit(Mathf.Clamp(Mathf.RoundToInt(impact * 1.4f), 4, 14));
        }
    }

    private void BuildHeadlightsAndRearGlow()
    {
        float[] xOffsets = { -0.55f, 0.55f };
        foreach (float x in xOffsets)
        {
            GameObject hlGo = new GameObject("Headlight_Spot");
            hlGo.transform.SetParent(transform, false);
            hlGo.transform.localPosition = new Vector3(x, 0.55f, 1.55f);
            hlGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Light spot = hlGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = headlightColor;
            spot.intensity = 8.5f;
            spot.range = 26f;
            spot.spotAngle = 52f;
            spot.innerSpotAngle = 25f;
            spot.shadows = LightShadows.None;
        }

        GameObject rearLightGo = new GameObject("Taillight_FloorGlow");
        rearLightGo.transform.SetParent(transform, false);
        rearLightGo.transform.localPosition = new Vector3(0f, 0.45f, -1.75f);
        rearGlowLight = rearLightGo.AddComponent<Light>();
        rearGlowLight.type = LightType.Point;
        rearGlowLight.color = new Color(1f, 0.08f, 0.12f);
        rearGlowLight.range = 5.5f;
        rearGlowLight.intensity = 2.2f;
    }

    private void BuildTaillightRibbons()
    {
        float[] xOffsets = { -0.52f, 0.52f };
        for (int i = 0; i < 2; i++)
        {
            GameObject trGo = new GameObject($"Taillight_Ribbon_{i}");
            trGo.transform.SetParent(transform, false);
            trGo.transform.localPosition = new Vector3(xOffsets[i], 0.48f, -1.62f);
            TrailRenderer tr = trGo.AddComponent<TrailRenderer>();
            tr.time = 0.22f;
            tr.minVertexDistance = 0.12f;
            tr.startWidth = 0.06f;
            tr.endWidth = 0.0f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            if (trailNeonMat != null) tr.sharedMaterial = trailNeonMat;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.95f, 0.15f, 0.22f), 0f),
                    new GradientColorKey(new Color(0.8f, 0.05f, 0.3f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.75f, 0f),
                    new GradientAlphaKey(0.0f, 1f)
                }
            );
            tr.colorGradient = grad;
            taillightTrails[i] = tr;
        }
    }

    private void BuildNitroJetRibbons()
    {
        float[] xOffsets = { -0.32f, 0.32f };
        for (int i = 0; i < 2; i++)
        {
            GameObject jetGo = new GameObject($"Nitro_Jet_Ribbon_{i}");
            jetGo.transform.SetParent(transform, false);
            jetGo.transform.localPosition = new Vector3(xOffsets[i], 0.30f, -1.68f);
            TrailRenderer tr = jetGo.AddComponent<TrailRenderer>();
            tr.time = 0.28f;
            tr.minVertexDistance = 0.10f;
            tr.startWidth = 0.26f;
            tr.endWidth = 0.0f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            if (nitroFlameMat != null) tr.sharedMaterial = nitroFlameMat;
            else if (trailNeonMat != null) tr.sharedMaterial = trailNeonMat;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.65f, 0.98f, 1.0f), 0f),
                    new GradientColorKey(new Color(0.05f, 0.65f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.15f, 0.25f, 0.95f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.65f, 0.5f),
                    new GradientAlphaKey(0.0f, 1f)
                }
            );
            tr.colorGradient = grad;
            tr.emitting = false;
            nitroJetTrails[i] = tr;
        }
    }

    private void BuildTireSkidMarks()
    {
        float[] xOffsets = { -0.68f, 0.68f };
        for (int i = 0; i < 2; i++)
        {
            GameObject skidGo = new GameObject($"Tire_Skid_{i}");
            skidGo.transform.SetParent(transform, false);
            skidGo.transform.localPosition = new Vector3(xOffsets[i], 0.06f, -1.1f);
            skidGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TrailRenderer tr = skidGo.AddComponent<TrailRenderer>();
            tr.time = 2.8f;
            tr.minVertexDistance = 0.15f;
            tr.startWidth = 0.28f;
            tr.endWidth = 0.20f;
            tr.alignment = LineAlignment.TransformZ;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (tireMarkMat != null) tr.sharedMaterial = tireMarkMat;
            tr.emitting = false;
            tireSkidTrails[i] = tr;
        }
    }

    private void BuildDriftWaterSpray()
    {
        GameObject psGo = new GameObject("Drift_WaterSpray_VFX");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = new Vector3(0f, 0.20f, -1.45f);
        psGo.transform.localRotation = Quaternion.Euler(-15f, 180f, 0f);
        driftSprayPS = psGo.AddComponent<ParticleSystem>();
        var main = driftSprayPS.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.38f, 0.82f);
        main.startColor = new Color(0.82f, 0.90f, 0.98f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;
        var emission = driftSprayPS.emission;
        emission.rateOverTime = 0f;
        var shape = driftSprayPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.35f, 0.18f, 0.20f);
        var sizeOverLifetime = driftSprayPS.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1.35f));
        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        if (smokeSprayMat != null) psRenderer.sharedMaterial = smokeSprayMat;
    }

    private void BuildDriftSparks()
    {
        GameObject sparksGo = new GameObject("Drift_TireSparks_VFX");
        sparksGo.transform.SetParent(transform, false);
        sparksGo.transform.localPosition = new Vector3(0f, 0.14f, -1.35f);
        sparksGo.transform.localRotation = Quaternion.Euler(-18f, 180f, 0f);
        driftSparksPS = sparksGo.AddComponent<ParticleSystem>();
        var main = driftSparksPS.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(1.0f, 0.60f, 0.12f, 1f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;
        var emission = driftSparksPS.emission;
        emission.rateOverTime = 0f;
        var shape = driftSparksPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.3f, 0.1f, 0.1f);
        var psRenderer = sparksGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.lengthScale = 1.6f;
        if (sparkMat != null) psRenderer.sharedMaterial = sparkMat;
        else if (nitroFlameMat != null) psRenderer.sharedMaterial = nitroFlameMat;
    }

    private void BuildNitroExhaustFlames()
    {
        GameObject nitroGo = new GameObject("Nitro_Flames_VFX");
        nitroGo.transform.SetParent(transform, false);
        nitroGo.transform.localPosition = new Vector3(0f, 0.30f, -1.70f);
        nitroGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        nitroFlamePS = nitroGo.AddComponent<ParticleSystem>();
        var main = nitroFlamePS.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f, 12.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.68f);
        main.startColor = new Color(0.15f, 0.85f, 1.0f, 0.95f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;
        var emission = nitroFlamePS.emission;
        emission.rateOverTime = 0f;
        var shape = nitroFlamePS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.75f, 0.15f, 0.15f);
        var psRenderer = nitroGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.lengthScale = 1.8f;
        if (nitroFlameMat != null) psRenderer.sharedMaterial = nitroFlameMat;
    }

    private void BuildCollisionSparks()
    {
        GameObject sparksGo = new GameObject("Collision_Sparks_VFX");
        sparksGo.transform.SetParent(transform, false);
        sparksGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        collisionSparksPS = sparksGo.AddComponent<ParticleSystem>();
        var main = collisionSparksPS.main;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(1.0f, 0.65f, 0.15f, 0.85f);
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = collisionSparksPS.emission;
        emission.rateOverTime = 0f;
        var psRenderer = sparksGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.lengthScale = 1.15f;
        if (sparkMat != null) psRenderer.sharedMaterial = sparkMat;
    }
}
