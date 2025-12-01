using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : NetworkBehaviour
{
    [Header("Réglages vitesse")]
    public float acceleration    = 60f / 10f;
    public float braking         = 25f / 10f;
    public float maxForwardSpeed = 200f / 10f;
    public float maxReverseSpeed = 24f  / 10f;

    [Header("Réglages direction")]
    public float steering     = 180f;        // °/sec
    public float sideFriction = 20f / 10f;

    [Header("État des boutons (remplis par l’UI)")]
    public NetworkVariable<bool> pressingLeft = new NetworkVariable<bool>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> pressingRight = new NetworkVariable<bool>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> pressingForward = new NetworkVariable<bool>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    public NetworkVariable<bool> pressingReverse = new NetworkVariable<bool>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Rigidbody rb;

    [Header("Race")]
    public NetworkVariable<bool> canDrive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Yaw qu'on contrôle nous-mêmes
    private float yaw;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Reste à plat
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Physique uniquement côté serveur
        rb.isKinematic = !IsServer;

        // On initialise notre yaw à la rotation actuelle
        yaw = transform.rotation.eulerAngles.y;

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
        if (!IsServer) return;
        if (!canDrive.Value) return;

        // On annule toute rotation imposée par la physique
        rb.angularVelocity = Vector3.zero;

        // --- SPEED ---
        float targetSpeed = 0f;
        if (pressingForward.Value)
            targetSpeed = maxForwardSpeed;
        else if (pressingReverse.Value)
            targetSpeed = -maxReverseSpeed;

        float accel = (pressingForward.Value || pressingReverse.Value) ? acceleration : braking;

        // On garde une vitesse scalaire manuelle
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        localVel.z = Mathf.MoveTowards(localVel.z, targetSpeed, accel * Time.fixedDeltaTime);
        localVel.x = 0f; // on interdit totalement le drift

        rb.velocity = transform.TransformDirection(localVel);

        // --- STEERING ---
        float steerInput = 0f;
        if (pressingLeft.Value)  steerInput -= 1f;
        if (pressingRight.Value) steerInput += 1f;

        float speedFactor = Mathf.Clamp01(Mathf.Abs(localVel.z) / maxForwardSpeed);

        if (Mathf.Abs(steerInput) > 0.01f && speedFactor > 0.01f)
        {
            float rotationDelta = steerInput * steering * speedFactor * Time.fixedDeltaTime;

            // pas d'inversion en marche arrière pour le moment (pour simplifier)
            yaw += rotationDelta;
        }

        Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);
        rb.MoveRotation(newRot);
    }

    public void SendInput(CarButton.ButtonType type, bool pressed)
    {
        if (!IsOwner) return;

        bool left    = pressingLeft.Value;
        bool right   = pressingRight.Value;
        bool forward = pressingForward.Value;
        bool reverse = pressingReverse.Value;

        switch (type)
        {
            case CarButton.ButtonType.Left:    left    = pressed; break;
            case CarButton.ButtonType.Right:   right   = pressed; break;
            case CarButton.ButtonType.Forward: forward = pressed; break;
            case CarButton.ButtonType.Reverse: reverse = pressed; break;
        }

        SetInputsServerRpc(left, right, forward, reverse);
    }

    [ServerRpc]
    public void SetInputsServerRpc(bool left, bool right, bool forward, bool reverse)
    {
        pressingLeft.Value    = left;
        pressingRight.Value   = right;
        pressingForward.Value = forward;
        pressingReverse.Value = reverse;
    }
}
