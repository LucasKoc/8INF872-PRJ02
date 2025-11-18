using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : MonoBehaviour
{
    [Header("Réglages vitesse")]
    public float acceleration = 60f;
    public float braking = 25f;
    public float maxForwardSpeed = 200f;
    public float maxReverseSpeed = 24f;

    [Header("Réglages direction")]
    // Degré de rotation par seconde à vitesse maximale
    public float steering = 180f;
    // Friction latérale pour réduire le patinage
    public float sideFriction = 20f;

    [Header("État des boutons (remplis par l’UI)")]
    [HideInInspector] public bool pressingLeft = false;
    [HideInInspector] public bool pressingRight = false;
    [HideInInspector] public bool pressingForward = false;
    [HideInInspector] public bool pressingReverse = false;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        // 1) Vélocité locale : avant/arrière = z, glisse latérale = x
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

        // 2) Vitesse cible en avant ou en arrière
        float targetSpeed = 0f;

        if (pressingForward)
        {
            targetSpeed = maxForwardSpeed;
        }
        else if (pressingReverse)
        {
            targetSpeed = -maxReverseSpeed;
        }
        else
        {
            // aucun bouton : on veut tendre vers 0
            targetSpeed = 0f;
        }

        // Si on appuie : accel (accélération normale)
        // Si on n’appuie pas : braking (frein moteur plus fort)
        float accel = (pressingForward || pressingReverse) ? acceleration : braking;

        // Faire tendre la vitesse actuelle vers la vitesse cible
        localVel.z = Mathf.MoveTowards(localVel.z, targetSpeed, accel * Time.fixedDeltaTime);

        // 3) Réduire fortement le glissement latéral (patinage)
        localVel.x = Mathf.MoveTowards(localVel.x, 0f, sideFriction * Time.fixedDeltaTime);

        // Appliquer la nouvelle vitesse dans le monde
        rb.linearVelocity = transform.TransformDirection(localVel);

        // 4) Direction (gauche / droite)
        float steerInput = 0f;
        if (pressingLeft) steerInput -= 1f;
        if (pressingRight) steerInput += 1f;

        // On ne tourne que si on a un peu de vitesse
        float speedFactor = Mathf.Clamp01(Mathf.Abs(localVel.z) / maxForwardSpeed);

        if (Mathf.Abs(steerInput) > 0.01f && speedFactor > 0.01f)
        {
            // Plus on va vite, plus on peut tourner
            float rotation = steerInput * steering * speedFactor * Time.fixedDeltaTime;

            // Si on recule, on inverse le volant
            float directionSign = Mathf.Sign(localVel.z != 0 ? localVel.z : 1f);

            Quaternion turn = Quaternion.Euler(0f, rotation * directionSign, 0f);
            rb.MoveRotation(rb.rotation * turn);
        }
    }
}
