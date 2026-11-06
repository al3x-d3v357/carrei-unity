using System;
using System.Collections.Generic;
using UnityEngine;

public class LapManager : MonoBehaviour
{
    public static LapManager Instance { get; private set; }

    [Header("Race Parameters")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private List<Transform> checkpoints = new List<Transform>();

    [Header("Start Countdown (3, 2, 1, GO!)")]
    [SerializeField] private float startCountdownSeconds = 3.0f;
    [SerializeField] private float goBannerDuration = 1.15f;

    [Header("Vehicle Selection State")]
    [SerializeField] private bool waitingForVehicleSelection = true;

    public int TotalLaps => totalLaps;
    public bool IsWaitingForVehicleSelection => waitingForVehicleSelection;
    public bool IsRaceStarted => raceStarted;
    public float CountdownRemaining => countdownRemaining;
    public float GoTimerRemaining => goTimerRemaining;

    public class CarLapProgress
    {
        public int currentCheckpointIndex = 0;
        public int currentLap = 1;
        public int totalCheckpointsPassed = 0;
        public float currentLapTime = 0f;
        public float bestLapTime = float.MaxValue;
        public bool isFinished = false;
    }

    private Dictionary<string, CarLapProgress> progressMap = new Dictionary<string, CarLapProgress>();
    private Transform playerTransform;
    private Transform aiTransform;

    private float countdownRemaining;
    private float goTimerRemaining;
    private bool raceStarted = false;
    private int lastAnnouncedSecond = 4;
    private AudioSource countdownAudio;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        startCountdownSeconds = 3.0f;
        goBannerDuration = 1.15f;
        countdownRemaining = startCountdownSeconds;
        goTimerRemaining = 0f;
        raceStarted = false;
        waitingForVehicleSelection = true;
        lastAnnouncedSecond = 4;

        countdownAudio = gameObject.GetComponent<AudioSource>();
        if (countdownAudio == null)
        {
            countdownAudio = gameObject.AddComponent<AudioSource>();
            countdownAudio.spatialBlend = 0f;
            countdownAudio.playOnAwake = false;
            countdownAudio.volume = 0.45f;
        }
    }

    private void Start()
    {
        GameObject pGo = GameObject.Find("Player_Car");
        if (pGo != null)
        {
            playerTransform = pGo.transform;
            progressMap["Player_Car"] = new CarLapProgress();
        }
        GameObject aiGo = GameObject.Find("AI_Opponent_Car");
        if (aiGo != null)
        {
            aiTransform = aiGo.transform;
            progressMap["AI_Opponent_Car"] = new CarLapProgress();
        }
    }

    public void ConfirmVehicleSelectionAndStartCountdown()
    {
        waitingForVehicleSelection = false;
        countdownRemaining = startCountdownSeconds;
        raceStarted = false;
        lastAnnouncedSecond = 4;
        Debug.Log("[LapManager] Veículo confirmado! Iniciando contagem 3, 2, 1, GO!...");
    }

    public void RegisterCheckpoint(Transform cp)
    {
        if (!checkpoints.Contains(cp)) checkpoints.Add(cp);
    }

    public void OnCarHitCheckpoint(string carId, int checkpointIndex)
    {
        if (checkpoints.Count == 0) return;
        if (!progressMap.ContainsKey(carId))
        {
            progressMap[carId] = new CarLapProgress();
        }

        CarLapProgress progress = progressMap[carId];
        if (progress.isFinished) return;

        int expectedNext = (progress.currentCheckpointIndex + 1) % checkpoints.Count;

        // Permite sincronizar no Checkpoint 0 na largada ou avancar na sequencia correta
        if (checkpointIndex == expectedNext)
        {
            progress.currentCheckpointIndex = checkpointIndex;
            progress.totalCheckpointsPassed++;

            // Se completou a volta inteira e retornou ao Checkpoint 0
            if (checkpointIndex == 0 && progress.totalCheckpointsPassed >= checkpoints.Count)
            {
                if (progress.currentLapTime < progress.bestLapTime && progress.currentLapTime > 3f)
                {
                    progress.bestLapTime = progress.currentLapTime;
                }
                Debug.Log($"[{carId}] Volta {progress.currentLap}/{totalLaps} concluída! Tempo: {progress.currentLapTime:F2}s | Melhor: {progress.bestLapTime:F2}s");
                progress.currentLap++;
                progress.currentLapTime = 0f;

                if (progress.currentLap > totalLaps)
                {
                    progress.isFinished = true;
                    Debug.Log($"🏁 [{carId}] cruzou a linha final! Corrida finalizada.");
                }
            }
        }
    }

    private void Update()
    {
        // Se ainda está na tela de seleção de veículo, aguarda o jogador escolher e confirmar
        if (waitingForVehicleSelection)
        {
            return;
        }

        if (!raceStarted)
        {
            int currentSec = Mathf.CeilToInt(countdownRemaining);
            if (currentSec < lastAnnouncedSecond && currentSec >= 1)
            {
                lastAnnouncedSecond = currentSec;
                PlayCountdownBeep(440f, 0.14f);
            }

            countdownRemaining -= Time.deltaTime;
            if (countdownRemaining <= 0f)
            {
                countdownRemaining = 0f;
                raceStarted = true;
                goTimerRemaining = goBannerDuration;
                PlayCountdownBeep(880f, 0.32f);
            }
            return;
        }

        if (goTimerRemaining > 0f)
        {
            goTimerRemaining -= Time.deltaTime;
        }

        foreach (var entry in progressMap)
        {
            if (!entry.Value.isFinished && entry.Value.currentLap >= 1)
            {
                entry.Value.currentLapTime += Time.deltaTime;
            }
        }
    }

    private void PlayCountdownBeep(float freq, float duration)
    {
        if (countdownAudio == null) return;
        int sampleRate = 22050;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("CountdownBeep", sampleCount, 1, sampleRate, false);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Clamp01(1f - (t / duration));
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        }
        clip.SetData(samples, 0);
        countdownAudio.PlayOneShot(clip);
    }

    public CarLapProgress GetProgress(string carId)
    {
        return progressMap.ContainsKey(carId) ? progressMap[carId] : null;
    }

    public int GetPlayerRacePosition()
    {
        if (playerTransform == null || aiTransform == null || checkpoints.Count == 0) return 1;
        float pScore = ComputeRaceScore("Player_Car", playerTransform);
        float aiScore = ComputeRaceScore("AI_Opponent_Car", aiTransform);
        return pScore >= aiScore ? 1 : 2;
    }

    private float ComputeRaceScore(string carId, Transform carTransform)
    {
        if (!progressMap.TryGetValue(carId, out CarLapProgress prog)) return 0f;
        int nextCpIdx = (prog.currentCheckpointIndex + 1) % checkpoints.Count;
        Transform nextCp = checkpoints[nextCpIdx];
        float distToNext = nextCp != null ? Vector3.Distance(carTransform.position, nextCp.position) : 0f;
        return (prog.totalCheckpointsPassed * 1000f) - distToNext;
    }
}
