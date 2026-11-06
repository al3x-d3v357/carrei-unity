using UnityEngine;

[CreateAssetMenu(fileName = "NewCarData", menuName = "Racing/Car Data")]
public class CarData : ScriptableObject
{
    [Header("Identificação")]
    public string carName = "VX-9 Phantom";
    public string carClass = "A";

    [Header("Estatísticas (0 a 100)")]
    [Range(0, 100)] public float topSpeed = 80f;
    [Range(0, 100)] public float acceleration = 75f;
    [Range(0, 100)] public float handling = 65f;

    [Header("Especificações")]
    public string weight = "1,320 kg";
    public string drivetrain = "AWD";

    [Header("Thumbnail 2D")]
    public Sprite thumbnail;

    [Header("Prefab 3D (Opcional)")]
    public GameObject carPrefab;
}
