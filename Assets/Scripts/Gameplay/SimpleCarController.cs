using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : NetworkBehaviour
{
    // ---------- Références de piste ----------
    [Header("Track space")]
    [Tooltip("Transform de référence du circuit côté Host (piste virtuelle).")]
    public Transform circuitRefHost;

    [Tooltip("Transform de MON circuit AR côté client (piste posée en AR).")]
    public Transform myCircuitClient;

    [Tooltip("Transform visuel de la voiture (mesh). Si null, on utilise ce GameObject.")]
    public Transform visualRoot;

    private Transform Visual => visualRoot != null ? visualRoot : transform;
    private Rigidbody _rb;

    // ---------- Paramètres de conduite ----------
    [Header("Car settings")]
    public float maxForwardSpeed = 3f / 25f;      // m/s
    public float maxReverseSpeed = 1.5f / 25f;    // m/s
    public float acceleration    = 4f / 25f;      // m/s²
    public float braking         = 12f / 25f;     // m/s² (pas utilisé ici mais dispo)
    public float steeringSpeed   = 40f;           // degrés / s

    private float _currentSpeed = 0f;

    [Header("Input")]
    public bool useUnityAxesOnOwner = false;

    // La course active ou non la voiture
    public NetworkVariable<bool> canDrive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ---------- Types réseau ----------
    [Serializable]
    public struct InputState : INetworkSerializable
    {
        public float Throttle;   // -1 .. 1
        public float Steer;      // -1 .. 1

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Throttle);
            serializer.SerializeValue(ref Steer);
        }
    }

    // NetworkVariable pour les inputs (écrite par le owner, lue par le serveur)
    private NetworkVariable<InputState> _inputState = new NetworkVariable<InputState>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // Inputs côté UI mobile
    private bool _forwardPressed;
    private bool _reversePressed;
    private bool _leftPressed;
    private bool _rightPressed;

    // =========================================================
    // Init
    // =========================================================

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            _rb.isKinematic = false;
            _rb.useGravity = false;     // circuit AR à plat
        }
        else
        {
            _rb.isKinematic = true;     // pas de physique côté client
            _rb.useGravity = false;
        }
    }

    // =========================================================
    // API Track (host & client)
    // =========================================================

    // Appelé côté serveur juste après spawn pour fixer le circuit de ref
    public void InitServerTrack(Transform circuitRef)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[SimpleCarController] InitServerTrack appelé sur un client.");
            return;
        }

        circuitRefHost = circuitRef;
    }

    // Appelé côté client quand on connaît SON circuit AR
    public void InitClientTrack(Transform myCircuit)
    {
        myCircuitClient = myCircuit;
    }

    // =========================================================
    // Input UI mobile (CarButton)
    // =========================================================

    public void SendInput(CarButton.ButtonType buttonType, bool pressed)
    {
        if (!IsOwner) return; // seul le owner envoie les inputs

        switch (buttonType)
        {
            case CarButton.ButtonType.Forward: _forwardPressed = pressed; break;
            case CarButton.ButtonType.Reverse: _reversePressed = pressed; break;
            case CarButton.ButtonType.Left:    _leftPressed    = pressed; break;
            case CarButton.ButtonType.Right:   _rightPressed   = pressed; break;
        }

        UpdateOwnerInputFromButtons();
    }

    private void UpdateOwnerInputFromButtons()
    {
        float throttle = 0f;
        if (_forwardPressed) throttle += 1f;
        if (_reversePressed) throttle -= 1f;

        float steer = 0f;
        if (_leftPressed)  steer -= 1f;
        if (_rightPressed) steer += 1f;

        InputState state = _inputState.Value;
        state.Throttle = Mathf.Clamp(throttle, -1f, 1f);
        state.Steer    = Mathf.Clamp(steer,    -1f, 1f);
        _inputState.Value = state;

        if (IsOwner)
        {
            Debug.Log($"[INPUT OWNER {OwnerClientId}] Throttle={state.Throttle} Steer={state.Steer}");
        }
    }

    // =========================================================
    // Update (input clavier pour le owner)
    // =========================================================

    private void Update()
    {
        // Input clavier optionnel (éditeur)
        if (IsOwner && useUnityAxesOnOwner)
        {
            float t = Input.GetAxisRaw("Vertical");
            float s = Input.GetAxisRaw("Horizontal");

            InputState state = _inputState.Value;
            state.Throttle = Mathf.Clamp(t, -1f, 1f);
            state.Steer    = Mathf.Clamp(s, -1f, 1f);
            _inputState.Value = state;
        }

        // PAS de smoothing ici -> tout est fait dans le ClientRpc
    }

    // =========================================================
    // Physique côté serveur (Rigidbody)
    // =========================================================

    private void FixedUpdate()
    {
        // Seul le serveur bouge la voiture
        if (!IsServer) return;

        float dt = Time.fixedDeltaTime;

        if (!canDrive.Value)
        {
            _currentSpeed = 0f;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            return;
        }

        InputState input = _inputState.Value;

        // ---------- 1) VITESSE ----------
        float targetSpeed = 0f;
        if (Mathf.Abs(input.Throttle) > 0.01f)
        {
            bool forwardInput = input.Throttle > 0f;
            float max = forwardInput ? maxForwardSpeed : maxReverseSpeed;
            targetSpeed = max * (forwardInput ? 1f : -1f);
        }
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * dt);

        // ---------- 2) ROTATION ----------
        float steerInput  = input.Steer;                 // -1 .. 1
        float steerAmount = steerInput * steeringSpeed * dt;
        Quaternion newRot = _rb.rotation * Quaternion.Euler(0f, steerAmount, 0f);
        _rb.MoveRotation(newRot);

        // ---------- 3) AVANCEMENT ----------
        Vector3 forward = newRot * Vector3.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 delta = forward * _currentSpeed * dt;
        _rb.MovePosition(_rb.position + delta);

        // On neutralise la physique "libre"
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // ---------- 4) Calcul trackPos / trackRot ----------
        Vector3 worldPos = _rb.position;
        Quaternion worldRot = _rb.rotation;

        Vector3 trackPos;
        Quaternion trackRot;

        if (circuitRefHost != null)
        {
            trackPos = circuitRefHost.InverseTransformPoint(worldPos);
            trackRot = Quaternion.Inverse(circuitRefHost.rotation) * worldRot;
        }
        else
        {
            trackPos = worldPos;
            trackRot = worldRot;
        }

        // ---------- 5) Envoi aux clients ----------
        SyncCarClientRpc(trackPos, trackRot);
    }

    // =========================================================
    // Sync visuel côté clients
    // =========================================================

    [ClientRpc]
    private void SyncCarClientRpc(Vector3 trackPos, Quaternion trackRot)
    {
        // Le serveur n'a pas besoin de ce RPC
        if (IsServer) return;

        // S'assurer d'avoir un circuit local
        if (myCircuitClient == null && ARPlacementController.LocalCircuit != null)
        {
            myCircuitClient = ARPlacementController.LocalCircuit;
        }

        // Tant que le joueur n’a pas posé SON circuit, on ne fait rien
        if (myCircuitClient == null)
            return;

        Transform basis = myCircuitClient;

        // track-space -> monde AR local
        Vector3 worldPos = basis.TransformPoint(trackPos);
        Quaternion worldRot = basis.rotation * trackRot;

        Visual.SetPositionAndRotation(worldPos, worldRot);
    }
}
