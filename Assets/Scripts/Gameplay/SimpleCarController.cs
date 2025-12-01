using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : NetworkBehaviour
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
    public NetworkVariable<bool> pressingLeft = new NetworkVariable<bool>(default, 
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> pressingRight = new NetworkVariable<bool>(default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> pressingForward = new NetworkVariable<bool>(default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> pressingReverse = new NetworkVariable<bool>(default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private Rigidbody rb;

    [Header("Race")]
    public NetworkVariable<bool> canDrive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            foreach (CarButton button in FindObjectsOfType<CarButton>(true))
            {
                button.car = this;
            }
        }
    }
    
    void FixedUpdate()
    {
        // n'autoriser les inputs QUE pour la voiture du joueur local
        if (!IsOwner) return;

        // Course démarrée ?
        if (!canDrive.Value) return;
        
        // 1) Vélocité locale : avant/arrière = z, glisse latérale = x
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

        // 2) Vitesse cible en avant ou en arrière
        float targetSpeed = 0f;

        if (pressingForward.Value)
        {
            targetSpeed = maxForwardSpeed;
        }
        else if (pressingReverse.Value)
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
        float accel = (pressingForward.Value || pressingReverse.Value) ? acceleration : braking;

        // Faire tendre la vitesse actuelle vers la vitesse cible
        localVel.z = Mathf.MoveTowards(localVel.z, targetSpeed, accel * Time.fixedDeltaTime);

        // 3) Réduire fortement le glissement latéral (patinage)
        localVel.x = Mathf.MoveTowards(localVel.x, 0f, sideFriction * Time.fixedDeltaTime);

        // Appliquer la nouvelle vitesse dans le monde
        rb.linearVelocity = transform.TransformDirection(localVel);

        // 4) Direction (gauche / droite)
        float steerInput = 0f;
        if (pressingLeft.Value) steerInput -= 1f;
        if (pressingRight.Value) steerInput += 1f;

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
    
    public void SendInput(CarButton.ButtonType type, bool pressed)
    {
        if (!IsOwner) return;

        bool left = pressingLeft.Value;
        bool right = pressingRight.Value;
        bool forward = pressingForward.Value;
        bool reverse = pressingReverse.Value;

        switch (type)
        {
            case CarButton.ButtonType.Left: left = pressed; break;
            case CarButton.ButtonType.Right: right = pressed; break;
            case CarButton.ButtonType.Forward: forward = pressed; break;
            case CarButton.ButtonType.Reverse: reverse = pressed; break;
        }

        SetInputsServerRpc(left, right, forward, reverse);
    }
    
    [ServerRpc]
    public void SetInputsServerRpc(bool left, bool right, bool forward, bool reverse)
    {
        pressingLeft.Value = left;
        pressingRight.Value = right;
        pressingForward.Value = forward;
        pressingReverse.Value = reverse;
    }
}
