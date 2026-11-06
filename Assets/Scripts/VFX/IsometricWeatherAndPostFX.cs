using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class IsometricWeatherAndPostFX : MonoBehaviour
{
    public Transform cameraToFollow;
    public ArcadeCarController playerController;
    public Rigidbody playerRb;
    public Volume globalVolume;
    public Material turboScreenBlurMaterial;

    private ChromaticAberration chromaticAberration;
    private Bloom bloom;
    private Vignette vignette;
    private LensDistortion lensDistortion;
    private MotionBlur motionBlur;
    private MeshRenderer blurQuadRenderer;

    private void Start()
    {
        Camera mainCam = Camera.main;
        if (cameraToFollow == null && mainCam != null)
            cameraToFollow = mainCam.transform;

        if (mainCam != null)
        {
            // Método 1: Câmera com Fundo Sólido mantendo reflexos e luz ambiente da Skybox intactos
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);

            var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.requiresColorOption = CameraOverrideOption.On;
                camData.requiresColorTexture = true;
            }
            SetupCameraBlurQuad(mainCam);
        }

        // Calibração de Luz Ambiente (Gradient/Trilight) e Reflexos de Environment
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.56f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.92f, 0.65f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.38f, 0.40f, 0.52f);
        RenderSettings.ambientIntensity = 1.35f;
        RenderSettings.reflectionIntensity = 1.25f;

        if (playerController == null)
        {
            GameObject player = GameObject.Find("Player_Car");
            if (player != null)
            {
                playerController = player.GetComponent<ArcadeCarController>();
                playerRb = player.GetComponent<Rigidbody>();
            }
        }

        if (LapManager.Instance == null)
        {
            GameObject lmGo = new GameObject("LapManager");
            lmGo.AddComponent<LapManager>();
        }

        VolumeProfile volProf = globalVolume != null ? globalVolume.sharedProfile : null;
        if (volProf != null)
        {
            try
            {
                volProf.TryGet(out chromaticAberration);
                volProf.TryGet(out bloom);
                volProf.TryGet(out vignette);
                volProf.TryGet(out lensDistortion);
                volProf.TryGet(out motionBlur);
                if (volProf.TryGet(out ColorAdjustments colorAdj))
                {
                    colorAdj.postExposure.value = Mathf.Max(colorAdj.postExposure.value, 0.45f);
                }
            }
            catch
            {
                // Ignora componentes nulos ou corrompidos no VolumeProfile
            }
        }
    }

    private void SetupCameraBlurQuad(Camera cam)
    {
        if (turboScreenBlurMaterial == null) return;
        Transform existing = cam.transform.Find("Turbo_ScreenBlur_Overlay");
        GameObject quadGo;
        if (existing != null)
        {
            quadGo = existing.gameObject;
        }
        else
        {
            quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "Turbo_ScreenBlur_Overlay";
            Destroy(quadGo.GetComponent<Collider>());
            quadGo.transform.SetParent(cam.transform, false);
        }

        quadGo.transform.localPosition = new Vector3(0f, 0f, 1.2f);
        quadGo.transform.localRotation = Quaternion.identity;
        quadGo.transform.localScale = new Vector3(95f, 60f, 1f);

        blurQuadRenderer = quadGo.GetComponent<MeshRenderer>();
        blurQuadRenderer.sharedMaterial = turboScreenBlurMaterial;
        blurQuadRenderer.shadowCastingMode = ShadowCastingMode.Off;
        blurQuadRenderer.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (cameraToFollow != null)
        {
            Vector3 targetPos = cameraToFollow.position + cameraToFollow.forward * 22f;
            transform.position = new Vector3(targetPos.x, 22f, targetPos.z);
        }

        if (playerRb == null || playerController == null) return;
        float speedNorm = Mathf.Clamp01(playerRb.linearVelocity.magnitude / 28f);
        float turboBlur = playerController.CurrentTurboBlurFactor;

        // 1. Embaçamento radial de tela reduzido drasticamente (apenas 12% sutil nas bordas para manter a imagem limpa e nítida)
        if (turboScreenBlurMaterial != null)
        {
            turboScreenBlurMaterial.SetFloat("_BlurStrength", turboBlur * 0.12f);
            turboScreenBlurMaterial.SetFloat("_CenterFocusRadius", 0.45f);
            if (blurQuadRenderer != null)
            {
                blurQuadRenderer.enabled = turboBlur > 0.01f;
            }
        }

        // 2. Pós-Processamento URP suave sem embaçar a visão do carro
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Lerp(Mathf.Lerp(0.03f, 0.08f, speedNorm), 0.18f, turboBlur);
        }
        if (bloom != null)
        {
            bloom.intensity.value = Mathf.Lerp(Mathf.Lerp(1.10f, 1.25f, speedNorm), 1.45f, turboBlur);
            bloom.scatter.value = Mathf.Lerp(0.68f, 0.75f, turboBlur);
        }
        if (lensDistortion != null)
        {
            lensDistortion.intensity.value = Mathf.Lerp(0f, -0.10f, turboBlur);
            lensDistortion.scale.value = Mathf.Lerp(1.0f, 1.02f, turboBlur);
        }
        if (motionBlur != null)
        {
            motionBlur.intensity.value = Mathf.Lerp(0.05f, 0.18f, turboBlur);
        }
        if (vignette != null)
        {
            vignette.intensity.value = Mathf.Lerp(0.28f, 0.36f, turboBlur);
        }
    }

    private void OnGUI()
    {
        if (playerController == null) return;

        float panelW = 290f;
        float panelH = 112f;
        float x = 24f;
        float y = Screen.height - panelH - 22f;

        // Container Principal (Azul Royal Profundo #3852B4 com transparencia escura)
        GUI.color = new Color(0.08f, 0.12f, 0.26f, 0.88f);
        GUI.DrawTexture(new Rect(x - 6f, y - 6f, panelW + 12f, panelH + 12f), Texture2D.whiteTexture);

        // Borda superior destaque
        GUI.color = playerController.IsTurboActive
            ? new Color(0.2f, 0.95f, 1f, 1f)
            : new Color(0.94f, 0.55f, 0.22f, 0.95f);
        GUI.DrawTexture(new Rect(x - 6f, y - 6f, panelW + 12f, 3f), Texture2D.whiteTexture);

        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = new Color(0.95f, 0.75f, 0.48f); // Dourado #F3BE7A

        GUIStyle speedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        speedStyle.normal.textColor = Color.white;

        GUIStyle subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        subStyle.normal.textColor = Color.white;

        int pos = LapManager.Instance != null ? LapManager.Instance.GetPlayerRacePosition() : 1;
        var prog = LapManager.Instance != null ? LapManager.Instance.GetProgress("Player_Car") : null;
        int lap = prog != null ? Mathf.Min(prog.currentLap, LapManager.Instance.TotalLaps) : 1;
        int totalLaps = LapManager.Instance != null ? LapManager.Instance.TotalLaps : 3;
        float lapTime = prog != null ? prog.currentLapTime : 0f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, panelW, 20f), $"POS: {pos}º LUGAR   |   VOLTA {lap}/{totalLaps}   |   {lapTime:F1}s", headerStyle);

        string gearStr = playerController.ThrottleInput < -0.1f && playerController.CurrentSpeedKmh > 3f
            ? "R"
            : $"{playerController.CurrentGear}ª";
        GUI.Label(new Rect(x, y + 22f, panelW + 30f, 22f), $"VEL: {Mathf.RoundToInt(playerController.CurrentSpeedKmh)} KM/H  [{gearStr}]  GRAU [LB/RB/E]", speedStyle);

        // Barra de Turbo
        float barY = y + 48f;
        float barW = panelW - 12f;
        GUI.Label(new Rect(x, barY, 130f, 18f), "TURBO [B / SHIFT]", subStyle);
        GUI.color = new Color(0.16f, 0.18f, 0.24f, 1f);
        GUI.DrawTexture(new Rect(x + 112f, barY + 3f, barW - 112f, 12f), Texture2D.whiteTexture);
        GUI.color = playerController.IsTurboActive
            ? new Color(0.15f, 0.95f, 1f, 1f)
            : new Color(0.26f, 0.74f, 0.69f, 0.95f);
        GUI.DrawTexture(new Rect(x + 112f, barY + 3f, (barW - 112f) * playerController.TurboPercentage, 12f), Texture2D.whiteTexture);

        // Barra de Drift Charge / Status de Vacuo
        float driftY = y + 70f;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, driftY, 130f, 18f), "DRIFT [X / SPACE]", subStyle);
        GUI.color = new Color(0.16f, 0.18f, 0.24f, 1f);
        GUI.DrawTexture(new Rect(x + 112f, driftY + 3f, barW - 112f, 12f), Texture2D.whiteTexture);
        GUI.color = playerController.IsDriftBoostActive
            ? new Color(1.0f, 0.65f, 0.15f, 1f)
            : new Color(0.94f, 0.55f, 0.22f, 0.92f);
        float driftFill = playerController.IsDriftBoostActive ? 1f : playerController.DriftCharge;
        GUI.DrawTexture(new Rect(x + 112f, driftY + 3f, (barW - 112f) * driftFill, 12f), Texture2D.whiteTexture);

        // Badges de Estado Ativo (Turbo / Grau / Drift / Drift Boost / Vacuo)
        string statusBadge = "";
        if (playerController.IsTurboActive) statusBadge += "🚀 TURBO NITRO ATIVO!   ";
        else if (playerController.IsDriftBoostActive) statusBadge += "⚡ DRIFT BOOST ATIVO!   ";
        else if (playerController.IsWheelieActive) statusBadge += "🏍️ EMPINANDO A MOTO (GRAU)! +TURBO   ";
        else if (playerController.IsDrifting)
        {
            statusBadge += playerController.DriftCharge >= 0.40f
                ? "🔥 DRIFT CARREGADO! (SOLTE P/ BOOST)   "
                : "🔥 DERRAPANDO (DRIFT)...   ";
        }
        if (playerController.InSlipstream) statusBadge += "💨 VÁCUO (+VELOCIDADE)";
        if (!string.IsNullOrEmpty(statusBadge))
        {
            GUI.color = playerController.IsTurboActive
                ? new Color(0.25f, 0.95f, 1f, 1f)
                : new Color(1f, 0.85f, 0.35f, 1f);
            GUI.Label(new Rect(x, y + 90f, panelW + 90f, 20f), statusBadge, headerStyle);
        }

        DrawRaceStartCountdownGUI();
        GUI.color = Color.white;
    }

    private void DrawRaceStartCountdownGUI()
    {
        if (LapManager.Instance == null) return;

        bool countingDown = !LapManager.Instance.IsRaceStarted;
        bool showingGo = LapManager.Instance.IsRaceStarted && LapManager.Instance.GoTimerRemaining > 0f;
        if (!countingDown && !showingGo) return;

        string mainText;
        string subText;
        Color mainColor;
        int dynamicFontSize;
        float blinkAlpha;

        if (countingDown)
        {
            float rem = LapManager.Instance.CountdownRemaining;
            int num = Mathf.Clamp(Mathf.CeilToInt(rem), 1, 3);
            float frac = rem - Mathf.Floor(rem);
            if (frac <= 0.001f) frac = 1f;

            mainText = num.ToString();
            subText = "PREPARAR PARA LARGADA";
            dynamicFontSize = Mathf.RoundToInt(Mathf.Lerp(84f, 128f, frac));

            // Efeito piscante intenso a cada segundo
            blinkAlpha = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 18f));

            if (num == 3)
                mainColor = new Color(0.94f, 0.55f, 0.22f, blinkAlpha); // Laranja Tangerina Neon #F08D39
            else if (num == 2)
                mainColor = new Color(0.95f, 0.75f, 0.48f, blinkAlpha); // Dourado Quente #F3BE7A
            else
                mainColor = new Color(1.0f, 0.92f, 0.35f, blinkAlpha); // Amarelo Ouro Brilhante
        }
        else
        {
            float goNorm = Mathf.Clamp01(LapManager.Instance.GoTimerRemaining / 1.15f);
            mainText = "GO!";
            subText = "ACELERE!";
            dynamicFontSize = Mathf.RoundToInt(Mathf.Lerp(96f, 134f, goNorm));
            blinkAlpha = (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 24f))) * Mathf.Clamp01(goNorm * 1.5f);
            mainColor = new Color(0.26f, 0.95f, 0.75f, blinkAlpha); // Verde Neon #43BDB0 Brilhante
        }

        float boxW = 340f;
        float boxH = 165f;
        float cx = (Screen.width - boxW) * 0.5f;
        float cy = (Screen.height - boxH) * 0.32f;

        // Card central escuro (Azul Royal Profundo #3852B4 com transparência)
        GUI.color = new Color(0.08f, 0.12f, 0.26f, 0.82f * Mathf.Clamp01(blinkAlpha + 0.35f));
        GUI.DrawTexture(new Rect(cx, cy, boxW, boxH), Texture2D.whiteTexture);

        // Bordas superior e inferior piscando na cor da contagem
        GUI.color = new Color(mainColor.r, mainColor.g, mainColor.b, 0.95f);
        GUI.DrawTexture(new Rect(cx, cy, boxW, 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx, cy + boxH - 4f, boxW, 4f), Texture2D.whiteTexture);

        GUIStyle subBannerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subBannerStyle.normal.textColor = Color.white;
        GUI.color = new Color(1f, 1f, 1f, 0.92f);
        GUI.Label(new Rect(cx, cy + 12f, boxW, 24f), subText, subBannerStyle);

        GUIStyle countStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = dynamicFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        // Sombra do número para contraste máximo
        countStyle.normal.textColor = new Color(0f, 0f, 0f, blinkAlpha * 0.85f);
        GUI.color = new Color(0f, 0f, 0f, blinkAlpha * 0.85f);
        GUI.Label(new Rect(cx + 4f, cy + 36f, boxW, boxH - 36f), mainText, countStyle);

        // Número principal piscando (3, 2, 1, GO!)
        countStyle.normal.textColor = mainColor;
        GUI.color = mainColor;
        GUI.Label(new Rect(cx, cy + 32f, boxW, boxH - 36f), mainText, countStyle);
    }
}
