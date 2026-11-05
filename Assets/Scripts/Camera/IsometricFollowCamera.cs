using UnityEngine;

public class IsometricFollowCamera : MonoBehaviour
{
    public static IsometricFollowCamera Instance { get; private set; }
    public Transform target;
    [SerializeField] private Vector3 isometricAngle = new Vector3(35.264f, 45f, 0f);
    [SerializeField] private float distance = 22f;
    [SerializeField] private float smoothSpeed = 8.5f;

    [Header("Dynamic Speed Zoom & Look-Ahead")]
    [SerializeField] private float baseOrthoSize = 8.5f;
    [SerializeField] private float maxSpeedOrthoSize = 10.5f;
    [SerializeField] private float lookAheadFactor = 0.16f;
    [SerializeField] private float maxLookAheadDist = 3.2f;

    private Camera cam;
    private Rigidbody targetRb;
    private ArcadeCarController targetController;
    private Vector3 currentLookAhead;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        baseOrthoSize = 8.5f;
        maxSpeedOrthoSize = 10.5f;
        distance = 22f;
        lookAheadFactor = 0.16f;
        maxLookAheadDist = 3.2f;
        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = baseOrthoSize;
        }
        if (GetComponent<AudioListener>() == null && Object.FindAnyObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
    }

    private void Start()
    {
        transform.rotation = Quaternion.Euler(isometricAngle);
        if (target == null)
        {
            GameObject playerGo = GameObject.Find("Player_Car");
            if (playerGo != null) target = playerGo.transform;
        }
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
            targetController = target.GetComponent<ArcadeCarController>();
            Vector3 offset = Quaternion.Euler(isometricAngle) * new Vector3(0f, 0f, -distance);
            transform.position = target.position + offset;
        }
    }

    public void TriggerShake(float intensity, float duration)
    {
        // Sem tremor de camera para manter o efeito suave igual a primeira correcao
    }

    private void LateUpdate()
    {
        if (!target) return;
        if (targetRb == null) targetRb = target.GetComponent<Rigidbody>();
        if (targetController == null) targetController = target.GetComponent<ArcadeCarController>();
        Vector3 velocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        float speed = velocity.magnitude;

        // 1. Antecipacao de camera na direcao da velocidade (igual a primeira correcao)
        Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 desiredLookAhead = Vector3.ClampMagnitude(flatVel * lookAheadFactor, maxLookAheadDist);
        currentLookAhead = Vector3.Lerp(currentLookAhead, desiredLookAhead, 4.5f * Time.deltaTime);

        // 2. Posicao base isometrica
        Vector3 offset = Quaternion.Euler(isometricAngle) * new Vector3(0f, 0f, -distance);
        Vector3 desiredPosition = target.position + currentLookAhead + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 3. Zoom ortografico dinamico conforme velocidade, Turbo e Drift Boost
        if (cam != null && cam.orthographic)
        {
            bool nitroBoost = targetController != null && targetController.IsTurboActive && speed > 4f;
            bool driftBoost = targetController != null && targetController.IsDriftBoostActive && speed > 4f;
            float speedNorm = Mathf.Clamp01(speed / 28f);
            float boostExtraOrtho = nitroBoost ? 1.75f : (driftBoost ? 0.95f : 0f);
            float targetOrtho = Mathf.Lerp(baseOrthoSize, maxSpeedOrthoSize, speedNorm) + boostExtraOrtho;
            float zoomSmooth = (nitroBoost || driftBoost) ? 6.5f : 4.2f;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrtho, zoomSmooth * Time.deltaTime);
        }
    }
}
