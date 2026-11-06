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
    private ParticleSystem turboBlueSmokePS;
    private ParticleSystem nitroFlamePS;
    private ParticleSystem collisionSparksPS;
    private Light rearGlowLight;
    private Material runtimeSoftSmokeMat;
    private bool wasNitroActive = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCtrl = GetComponent<ArcadeCarController>();
        aiCtrl = GetComponent<ArcadeCarAI>();

        CreateRuntimeSoftSmokeMaterial();
        BuildHeadlightsAndRearGlow();
        BuildTaillightRibbons();
        BuildNitroJetRibbons();
        BuildTireSkidMarks();
        BuildDriftWaterSpray();
        BuildDriftSparks();
        BuildTurboBlueSmoke();
        BuildNitroExhaustFlames();
        BuildCollisionSparks();
    }

    private void CreateRuntimeSoftSmokeMaterial()
    {
        // Gera textura radial macia 64x64 para que a fumaça azul do Turbo e do Drift fique volumosa e bem visível
        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((res - 1) * 0.5f, (res - 1) * 0.5f);
        float maxRadius = res * 0.5f;

        Color[] pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * (3f - 2f * alpha); // Smoothstep macio
                pixels[y * res + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        Shader sh = Shader.Find("Sprites/Default") ??
                    Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                    Shader.Find("Particles/Standard Unlit");
        runtimeSoftSmokeMat = new Material(sh);
        runtimeSoftSmokeMat.mainTexture = tex;
        if (runtimeSoftSmokeMat.HasProperty("_BaseMap"))
            runtimeSoftSmokeMat.SetTexture("_BaseMap", tex);
        if (runtimeSoftSmokeMat.HasProperty("_BaseColor"))
            runtimeSoftSmokeMat.SetColor("_BaseColor", Color.white);
        if (runtimeSoftSmokeMat.HasProperty("_Color"))
            runtimeSoftSmokeMat.SetColor("_Color", Color.white);
    }

    private void Update()
    {
        if (rb == null) return;
        float speed = rb.linearVelocity.magnitude;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float lateralSlip = Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.right));

        bool isPlayerDrifting = playerCtrl != null && (playerCtrl.IsDrifting || (Mathf.Abs(playerCtrl.VisualDriftYaw) > 3.5f && speed > 7f));
        bool isWheelie = playerCtrl != null && playerCtrl.IsWheelieActive && forwardSpeed > 3f;
        bool isDrifting = isPlayerDrifting || (lateralSlip > 1.8f && speed > 6f);

        bool isTurbo = playerCtrl != null && playerCtrl.IsTurboActive && forwardSpeed > 2f;
        bool isDriftBoost = playerCtrl != null && playerCtrl.IsDriftBoostActive && forwardSpeed > 2f;
        bool isAiNitro = aiCtrl != null && aiCtrl.IsNitroActive && forwardSpeed > 2f;
        bool isNitro = isTurbo || isDriftBoost || isAiNitro;

        // 1. Fitas de luz traseiras
        bool emitRibbons = speed > 4.5f;
        foreach (var tr in taillightTrails)
        {
            if (tr != null)
            {
                tr.emitting = emitRibbons;
                tr.startWidth = isNitro ? 0.12f : 0.06f;
            }
        }

        // 2. Jatos duplos de plasma azul/ciano no escapamento durante o Turbo
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

        // 4. Fumaça nos pneus traseiros durante o Drift / deslize de curva
        if (driftSprayPS != null)
        {
            var emission = driftSprayPS.emission;
            emission.rateOverTime = isDrifting ? 75f : (isWheelie ? 25f : 0f);
        }

        // 5. Faíscas de Drift e de Rabeta ao Empinar (Grau) nas rodas traseiras
        if (driftSparksPS != null)
        {
            var emission = driftSparksPS.emission;
            bool emitDriftSparks = (isDrifting && speed > 7.5f) || (isWheelie && speed > 6f);
            emission.rateOverTime = emitDriftSparks ? 52f : 0f;
            if (emitDriftSparks && playerCtrl != null)
            {
                var main = driftSparksPS.main;
                main.startColor = (playerCtrl.DriftCharge >= 0.40f || isWheelie)
                    ? new Color(1.0f, 0.45f, 0.85f, 1f) // Rosa/Magenta Anime ou Ciano ao empinar/carregar
                    : new Color(1.0f, 0.60f, 0.12f, 1f);
            }
        }

        // 6. FUMAÇA AZUL DENSA DO TURBO (Nuvens volumosas azuis saindo da traseira do carro!)
        if (turboBlueSmokePS != null)
        {
            if (isNitro && !wasNitroActive)
            {
                turboBlueSmokePS.Emit(25);
            }
            var emission = turboBlueSmokePS.emission;
            emission.rateOverTime = isNitro ? 130f : 0f;
        }

        // 7. Chamas azuis no centro do escapamento
        if (nitroFlamePS != null)
        {
            if (isNitro && !wasNitroActive)
            {
                nitroFlamePS.Emit(20);
            }
            var emission = nitroFlamePS.emission;
            emission.rateOverTime = isNitro ? 85f : 0f;
        }
        wasNitroActive = isNitro;

        // 8. Iluminação azul intensa do Turbo no asfalto
        if (rearGlowLight != null)
        {
            bool braking = (playerCtrl != null && playerCtrl.ThrottleInput < -0.1f && forwardSpeed > 1.0f);
            rearGlowLight.intensity = isNitro ? 6.5f : (braking ? 2.5f : 1.1f);
            rearGlowLight.range = isNitro ? 9.5f : 5.5f;
            rearGlowLight.color = isNitro
                ? new Color(0.12f, 0.75f, 1.0f)
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
        float[] xOffsets = { -0.35f, 0.35f };
        for (int i = 0; i < 2; i++)
        {
            GameObject jetGo = new GameObject($"Nitro_Jet_Ribbon_{i}");
            jetGo.transform.SetParent(transform, false);
            jetGo.transform.localPosition = new Vector3(xOffsets[i], 0.30f, -1.68f);
            TrailRenderer tr = jetGo.AddComponent<TrailRenderer>();
            tr.time = 0.32f;
            tr.minVertexDistance = 0.10f;
            tr.startWidth = 0.32f;
            tr.endWidth = 0.0f;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            if (runtimeSoftSmokeMat != null) tr.sharedMaterial = runtimeSoftSmokeMat;
            else if (nitroFlameMat != null) tr.sharedMaterial = nitroFlameMat;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.55f, 0.95f, 1.0f), 0f),
                    new GradientColorKey(new Color(0.10f, 0.60f, 1.0f), 0.5f),
                    new GradientColorKey(new Color(0.05f, 0.30f, 0.95f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.70f, 0.5f),
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
        psGo.transform.localPosition = new Vector3(0f, 0.22f, -1.45f);
        psGo.transform.localRotation = Quaternion.Euler(-15f, 180f, 0f);
        driftSprayPS = psGo.AddComponent<ParticleSystem>();
        var main = driftSprayPS.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
        main.startColor = new Color(0.85f, 0.90f, 0.96f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var emission = driftSprayPS.emission;
        emission.rateOverTime = 0f;

        var shape = driftSprayPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.35f, 0.18f, 0.20f);

        var colLife = driftSprayPS.colorOverLifetime;
        colLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.90f, 0.94f, 1.0f), 0f),
                new GradientColorKey(new Color(0.70f, 0.78f, 0.88f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.70f, 0f),
                new GradientAlphaKey(0.35f, 0.5f),
                new GradientAlphaKey(0.0f, 1f)
            }
        );
        colLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = driftSprayPS.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.7f));

        var psRenderer = psGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (runtimeSoftSmokeMat != null) psRenderer.sharedMaterial = runtimeSoftSmokeMat;
        else if (smokeSprayMat != null) psRenderer.sharedMaterial = smokeSprayMat;
    }

    private void BuildTurboBlueSmoke()
    {
        GameObject blueSmokeGo = new GameObject("Turbo_BlueSmoke_VFX");
        blueSmokeGo.transform.SetParent(transform, false);
        blueSmokeGo.transform.localPosition = new Vector3(0f, 0.35f, -1.70f);
        blueSmokeGo.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);
        turboBlueSmokePS = blueSmokeGo.AddComponent<ParticleSystem>();

        var main = turboBlueSmokePS.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.80f, 1.55f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 350;

        var emission = turboBlueSmokePS.emission;
        emission.rateOverTime = 0f;

        var shape = turboBlueSmokePS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.15f, 0.25f, 0.25f);

        // Gradiente de Fumaça Azul Vibrante (Ciano Brilhante no escapamento -> Azul Royal denso -> Dissipa suavemente)
        var colLife = turboBlueSmokePS.colorOverLifetime;
        colLife.enabled = true;
        Gradient blueGrad = new Gradient();
        blueGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.35f, 0.88f, 1.0f), 0.0f),
                new GradientColorKey(new Color(0.10f, 0.55f, 1.0f), 0.35f),
                new GradientColorKey(new Color(0.06f, 0.32f, 0.92f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.92f, 0.0f),
                new GradientAlphaKey(0.75f, 0.45f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colLife.color = new ParticleSystem.MinMaxGradient(blueGrad);

        var sizeOverLifetime = turboBlueSmokePS.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.65f, 1f, 2.1f));

        var psRenderer = blueSmokeGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (runtimeSoftSmokeMat != null) psRenderer.sharedMaterial = runtimeSoftSmokeMat;
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
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.72f);
        main.startColor = new Color(0.25f, 0.90f, 1.0f, 0.95f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;
        var emission = nitroFlamePS.emission;
        emission.rateOverTime = 0f;
        var shape = nitroGo.GetComponent<ParticleSystem>().shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.75f, 0.15f, 0.15f);
        var psRenderer = nitroGo.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (runtimeSoftSmokeMat != null) psRenderer.sharedMaterial = runtimeSoftSmokeMat;
        else if (nitroFlameMat != null) psRenderer.sharedMaterial = nitroFlameMat;
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
