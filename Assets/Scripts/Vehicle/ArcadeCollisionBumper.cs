using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ArcadeCollisionBumper : MonoBehaviour
{
    [Header("Heavy Simcade Bumper Settings")]
    [SerializeField] private float bounceForce = 3.2f;
    [SerializeField] private float lateralBumpMultiplier = 1.15f;
    [SerializeField] private float minImpactVelocity = 2.0f;
    [SerializeField] private float maxBumpVelocityChange = 3.5f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Garante valores pesados e controlados mesmo em cenas já salvas
        bounceForce = Mathf.Min(bounceForce, 3.2f);
        lateralBumpMultiplier = Mathf.Min(lateralBumpMultiplier, 1.15f);
        maxBumpVelocityChange = 3.5f;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Impede qualquer decolagem vertical após toques em carros ou zebras
        Vector3 vel = rb.linearVelocity;
        if (vel.y > 0.35f)
        {
            vel.y = 0f;
            rb.linearVelocity = vel;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ClampVerticalBounce();
        ResolveArcadeBounce(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        ClampVerticalBounce();

        // Impede que os dois veículos fiquem grudados esfregando as laterais, mas sem jogá-los longe
        if (collision.gameObject.CompareTag("Vehicle") || collision.gameObject.name.Contains("Car"))
        {
            Vector3 pushDir = transform.position - collision.transform.position;
            pushDir.y = 0f;
            if (pushDir.sqrMagnitude > 0.01f)
            {
                rb.AddForce(pushDir.normalized * (bounceForce * 0.8f), ForceMode.Acceleration);
            }
        }
    }

    private void ClampVerticalBounce()
    {
        if (rb == null) return;
        Vector3 v = rb.linearVelocity;
        if (v.y > 0.2f)
        {
            v.y = 0f;
            rb.linearVelocity = v;
        }
    }

    private void ResolveArcadeBounce(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Vehicle") && !collision.gameObject.name.Contains("Car"))
            return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 collisionNormal = contact.normal;
        collisionNormal.y = 0f; // Exclusivamente no plano horizontal da pista
        if (collisionNormal.sqrMagnitude < 0.001f) return;
        collisionNormal.Normalize();

        float relativeVelocity = collision.relativeVelocity.magnitude;
        if (relativeVelocity < minImpactVelocity) return;

        float sideHitDot = Mathf.Abs(Vector3.Dot(transform.right, collisionNormal));
        float rawPush = bounceForce * (1f + sideHitDot * (lateralBumpMultiplier - 1f)) * Mathf.Clamp(relativeVelocity * 0.12f, 0.4f, 1.0f);
        float clampedVelChange = Mathf.Min(rawPush, maxBumpVelocityChange);

        // Aplica empurrão horizontal controlado e pesado (nunca lança o carro para fora do cenário)
        rb.AddForce(collisionNormal * clampedVelChange, ForceMode.VelocityChange);
    }
}
