using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCarAudio : MonoBehaviour
{
    [Header("Audio Clips (Assets/Audio/CarSFX)")]
    public AudioClip engineClip;
    public AudioClip skidClip;
    public AudioClip skidAltClip;
    public AudioClip brakeClip;
    public AudioClip turboClip;

    [Header("Mix & Pitch Settings")]
    [Range(0.05f, 1.0f)]
    public float masterVolume = 0.28f;
    [SerializeField] private float idlePitch = 0.72f;
    [SerializeField] private float maxEnginePitch = 1.50f;
    [SerializeField] private float turboPitchBoost = 0.18f;
    [SerializeField] private float idleVolume = 0.06f;
    [SerializeField] private float maxEngineVolume = 0.16f;
    [SerializeField] private float maxSkidVolume = 0.10f;
    [SerializeField] private float turboVolume = 0.10f;
    [SerializeField] private float brakeVolume = 0.10f;

    private Rigidbody rb;
    private ArcadeCarController playerCtrl;
    private ArcadeCarAI aiCtrl;
    private bool isPlayerCar;

    private AudioSource engineSource;
    private AudioSource skidSource;
    private AudioSource turboSource;
    private AudioSource oneShotSource;

    private bool wasTurboActive;
    private bool wasBrakingHard;
    private int lastGear = 1;
    private float gearShiftBlipTimer;
    private float brakeCooldownTimer;
    private float collisionCooldownTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCtrl = GetComponent<ArcadeCarController>();
        aiCtrl = GetComponent<ArcadeCarAI>();
        isPlayerCar = (playerCtrl != null);

        EnsureAudioClipsLoaded();
        SetupAudioSources();
    }

    public void EnsureAudioClipsLoaded()
    {
#if UNITY_EDITOR
        if (engineClip == null)
            engineClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/engine_accel.wav");
        if (skidClip == null)
            skidClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/skid_tires.wav");
        if (skidAltClip == null)
            skidAltClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/skid_synth.wav");
        if (brakeClip == null)
            brakeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/brake_synth.wav");
        if (turboClip == null)
            turboClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarSFX/turbo_synth.wav");
#endif
    }

    private void Start()
    {
        if (engineSource != null && engineClip != null)
        {
            engineSource.clip = engineClip;
            engineSource.loop = true;
            engineSource.pitch = idlePitch;
            engineSource.volume = idleVolume * masterVolume * (isPlayerCar ? 1f : 0.35f);
            if (!engineSource.isPlaying) engineSource.Play();
        }

        AudioClip activeSkid = skidClip != null ? skidClip : skidAltClip;
        if (skidSource != null && activeSkid != null)
        {
            skidSource.clip = activeSkid;
            skidSource.loop = true;
            skidSource.volume = 0f;
            if (!skidSource.isPlaying) skidSource.Play();
        }

        if (turboSource != null && turboClip != null)
        {
            turboSource.clip = turboClip;
            turboSource.loop = true;
            turboSource.volume = 0f;
            if (!turboSource.isPlaying) turboSource.Play();
        }
    }

    private void SetupAudioSources()
    {
        float spatial = isPlayerCar ? 0.15f : 0.90f;
        float volumeScale = (isPlayerCar ? 1.0f : 0.35f) * masterVolume;

        engineSource = CreateChildAudioSource("Audio_Engine", true, spatial, 8f, 45f);
        skidSource = CreateChildAudioSource("Audio_Skid", true, spatial, 6f, 40f);
        turboSource = CreateChildAudioSource("Audio_Turbo", true, spatial, 8f, 42f);
        oneShotSource = CreateChildAudioSource("Audio_OneShot", false, spatial, 8f, 42f);
        oneShotSource.volume = volumeScale;
    }

    private AudioSource CreateChildAudioSource(string sourceName, bool loop, float spatialBlend, float minDist, float maxDist)
    {
        Transform existing = transform.Find(sourceName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
        }

        AudioSource src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = spatialBlend;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDist;
        src.maxDistance = maxDist;
        src.dopplerLevel = isPlayerCar ? 0f : 0.35f;
        return src;
    }

    private void Update()
    {
        if (rb == null) return;

        if (brakeCooldownTimer > 0f) brakeCooldownTimer -= Time.deltaTime;
        if (collisionCooldownTimer > 0f) collisionCooldownTimer -= Time.deltaTime;
        if (gearShiftBlipTimer > 0f) gearShiftBlipTimer -= Time.deltaTime;

        Vector3 vel = rb.linearVelocity;
        float speed = vel.magnitude;
        float forwardSpeed = Vector3.Dot(vel, transform.forward);
        float lateralSlip = Mathf.Abs(Vector3.Dot(vel, transform.right));

        bool isTurbo = (playerCtrl != null && (playerCtrl.IsTurboActive || playerCtrl.IsDriftBoostActive)) ||
                       (aiCtrl != null && aiCtrl.IsNitroActive);
        bool isHandbraking = playerCtrl != null && playerCtrl.IsHandbraking;
        float throttle = playerCtrl != null ? Mathf.Abs(playerCtrl.ThrottleInput) : (speed > 1.5f ? 0.85f : 0f);
        bool isBrakingAgainstMotion = playerCtrl != null &&
                                      ((playerCtrl.ThrottleInput < -0.1f && forwardSpeed > 3.5f) ||
                                       (isHandbraking && speed > 5.0f));

        UpdateEngineSound(speed, throttle, isTurbo);
        UpdateSkidSound(speed, lateralSlip, isHandbraking);
        UpdateBrakeSound(isBrakingAgainstMotion, speed);
        UpdateTurboSound(isTurbo);
    }

    private void UpdateEngineSound(float speed, float throttle, bool isTurbo)
    {
        if (engineSource == null || engineClip == null) return;
        if (!engineSource.isPlaying) engineSource.Play();

        float speedNorm = Mathf.Clamp01(speed / 28f);
        float gearRpmEffect = 0f;

        if (playerCtrl != null)
        {
            int gear = playerCtrl.CurrentGear;
            if (gear != lastGear)
            {
                if (gear > lastGear && speed > 4f)
                {
                    // Pequeno corte de giro na troca de marcha ascendente
                    gearShiftBlipTimer = 0.11f;
                }
                lastGear = gear;
            }
            gearRpmEffect = playerCtrl.CurrentGearRpmNormalized * 0.28f;
        }
        else
        {
            // Simula progressao de marchas para a IA
            gearRpmEffect = Mathf.Repeat(speedNorm * 3.8f, 1.0f) * 0.22f;
        }

        float shiftDip = gearShiftBlipTimer > 0f ? -0.18f : 0f;
        float turboBoost = isTurbo ? turboPitchBoost : 0f;
        float targetPitch = Mathf.Lerp(idlePitch, maxEnginePitch, speedNorm * 0.78f + gearRpmEffect)
                            + (throttle * 0.12f) + turboBoost + shiftDip;

        float mixScale = (isPlayerCar ? 1.0f : 0.35f) * masterVolume;
        float targetVol = Mathf.Lerp(idleVolume, maxEngineVolume, Mathf.Clamp01(speedNorm * 0.7f + throttle * 0.45f)) * mixScale;

        engineSource.pitch = Mathf.Lerp(engineSource.pitch, Mathf.Clamp(targetPitch, 0.65f, 1.95f), 10f * Time.deltaTime);
        engineSource.volume = Mathf.Lerp(engineSource.volume, targetVol, 8f * Time.deltaTime);
    }

    private void UpdateSkidSound(float speed, float lateralSlip, bool isHandbraking)
    {
        if (skidSource == null) return;

        float slipIntensity = 0f;
        if (speed > 3.5f)
        {
            float lateralFactor = Mathf.InverseLerp(2.2f, 8.5f, lateralSlip);
            float handbrakeFactor = isHandbraking ? Mathf.InverseLerp(3.5f, 13.0f, speed) * 0.75f : 0f;
            slipIntensity = Mathf.Clamp01(Mathf.Max(lateralFactor, handbrakeFactor));
        }

        float mixScale = (isPlayerCar ? 1.0f : 0.30f) * masterVolume;
        float targetVol = slipIntensity * maxSkidVolume * mixScale;
        float targetPitch = Mathf.Lerp(0.88f, 1.15f, slipIntensity);

        skidSource.volume = Mathf.MoveTowards(skidSource.volume, targetVol, 3.5f * Time.deltaTime);
        skidSource.pitch = Mathf.Lerp(skidSource.pitch, targetPitch, 8f * Time.deltaTime);
    }

    private void UpdateBrakeSound(bool isBrakingHard, float speed)
    {
        if (isBrakingHard && !wasBrakingHard && brakeCooldownTimer <= 0f && brakeClip != null && oneShotSource != null)
        {
            float speedFactor = Mathf.Clamp01(speed / 22f);
            float mixScale = (isPlayerCar ? 1.0f : 0.30f) * masterVolume;
            oneShotSource.pitch = Random.Range(0.94f, 1.06f);
            oneShotSource.PlayOneShot(brakeClip, brakeVolume * (0.4f + 0.6f * speedFactor) * mixScale);
            brakeCooldownTimer = 0.55f;
        }
        wasBrakingHard = isBrakingHard;
    }

    private void UpdateTurboSound(bool isTurbo)
    {
        if (turboSource == null || turboClip == null) return;

        float mixScale = (isPlayerCar ? 1.0f : 0.30f) * masterVolume;
        if (isTurbo && !wasTurboActive && oneShotSource != null)
        {
            oneShotSource.pitch = Random.Range(1.02f, 1.12f);
            oneShotSource.PlayOneShot(turboClip, turboVolume * 0.65f * mixScale);
        }

        float targetVol = isTurbo ? (turboVolume * 0.55f * mixScale) : 0f;
        float targetPitch = isTurbo ? 1.18f : 0.92f;
        turboSource.volume = Mathf.MoveTowards(turboSource.volume, targetVol, 2.5f * Time.deltaTime);
        turboSource.pitch = Mathf.Lerp(turboSource.pitch, targetPitch, 6f * Time.deltaTime);

        wasTurboActive = isTurbo;
    }

    public void PlayDriftBoostBurst()
    {
        if (oneShotSource != null && turboClip != null)
        {
            float mixScale = (isPlayerCar ? 0.75f : 0.25f) * masterVolume;
            oneShotSource.pitch = 1.18f;
            oneShotSource.PlayOneShot(turboClip, turboVolume * mixScale);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collisionCooldownTimer > 0f || oneShotSource == null) return;
        float impact = collision.relativeVelocity.magnitude;
        if (impact < 3.5f) return;

        float impactNorm = Mathf.Clamp01(impact / 20f);
        float mixScale = (isPlayerCar ? 1.0f : 0.35f) * masterVolume;

        if (brakeClip != null)
        {
            oneShotSource.pitch = Random.Range(0.68f, 0.85f);
            oneShotSource.PlayOneShot(brakeClip, Mathf.Clamp01(impactNorm * 0.22f) * mixScale);
        }
        if (skidAltClip != null && impact > 7f)
        {
            oneShotSource.PlayOneShot(skidAltClip, Mathf.Clamp01(impactNorm * 0.16f) * mixScale);
        }
        collisionCooldownTimer = 0.28f;
    }
}
