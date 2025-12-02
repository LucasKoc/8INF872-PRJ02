using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class SimpleCarController : NetworkBehaviour
{
    // ---------- Références de piste ----------
    [Header("Track space")]
    [Tooltip("Transform de référence du circuit côté Host (piste 'virtuelle' connue du serveur).")]
    public Transform circuitRefHost;

    [Tooltip("Transform de MON circuit AR côté client (piste posée en AR).")]
    public Transform myCircuitClient;

    [Tooltip("Transform visuel de la voiture (mesh). Si null, on utilise ce GameObject.")]
    public Transform visualRoot;

    private Transform Visual => visualRoot != null ? visualRoot : transform;
    private Rigidbody _rb;

    // ---------- Paramètres de conduite ----------
    [Header("Car settings")]
    public float maxForwardSpeed = 3f / 25f;  // en m/s
    public float maxReverseSpeed = 1.5f / 25f;  // en m/s
    public float acceleration    = 4f / 25f;  // en m/s²
    public float braking         = 12f / 25f; // en m/s²
    public float steeringSpeed   = 40f;

    private float _currentSpeed = 0f;

    [Header("Input")]
    [Tooltip("Utiliser Input.GetAxis (clavier) sur le owner (pratique en mode éditeur).")]
    public bool useUnityAxesOnOwner = false;

    // La course active ou non la voiture
    public NetworkVariable<bool> canDrive = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ---------- Smoothing côté clients ----------
    private Vector3 _smoothedWorldPos;
    private Quaternion _smoothedWorldRot;
    private Vector3 _targetWorldPos;
    private Quaternion _targetWorldRot;
    private bool _hasSmoothing = false;

    // ---------- Types réseau ----------
    [Serializable]
    public struct InputState : INetworkSerializable
    {
        public float Throttle;   // -1 (arrière) -> 1 (avant)
        public float Steer;      // -1 (gauche) -> 1 (droite)

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Throttle);
            serializer.SerializeValue(ref Steer);
        }
    }

    [Serializable]
    public struct TrackState : INetworkSerializable
    {
        public Vector3 TrackPos;   // position dans l’espace piste
        public Quaternion TrackRot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TrackPos);
            serializer.SerializeValue(ref TrackRot);
        }
    }

    // ---------- NetworkVariables ----------
    private NetworkVariable<InputState> _inputState = new NetworkVariable<InputState>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<TrackState> _trackState = new NetworkVariable<TrackState>(
        new TrackState
        {
            TrackPos = Vector3.zero,
            TrackRot = Quaternion.identity
        },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Etat des boutons mobiles (CarButton)
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

        _trackState.OnValueChanged += OnTrackStateChanged;

        if (IsServer)
        {
            _rb.isKinematic = false;
            _rb.useGravity = false;

            UpdateTrackStateFromRigidbody();
        }
        else
        {
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
    }


    private void OnDestroy()
    {
        _trackState.OnValueChanged -= OnTrackStateChanged;
    }

    private void OnTrackStateChanged(TrackState previous, TrackState current)
    {
        // Le serveur a déjà la vraie physique → ne pas le forcer
        if (IsServer) return;
        ApplyTrackState(current);
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
        // Recalcule TrackState à partir de la nouvelle ref
        UpdateTrackStateFromRigidbody();
    }

    // Appelé côté client quand on connaît SON circuit AR
    public void InitClientTrack(Transform myCircuit)
    {
        myCircuitClient = myCircuit;
        // On recalcule la position monde client à partir du dernier TrackState reçu
        ApplyTrackState(_trackState.Value);
    }

    // =========================================================
    // Input UI mobile (CarButton)
    // =========================================================

    public void SendInput(CarButton.ButtonType buttonType, bool pressed)
    {
        if (!IsOwner) return; // seul le owner envoie les inputs

        switch (buttonType)
        {
            case CarButton.ButtonType.Forward:
                _forwardPressed = pressed;
                break;
            case CarButton.ButtonType.Reverse:
                _reversePressed = pressed;
                break;
            case CarButton.ButtonType.Left:
                _leftPressed = pressed;
                break;
            case CarButton.ButtonType.Right:
                _rightPressed = pressed;
                break;
        }

        UpdateOwnerInputFromButtons();
    }

    private void UpdateOwnerInputFromButtons()
    {
        float throttle = 0f;
        if (_forwardPressed) throttle += 1f;
        if (_reversePressed) throttle -= 1f;

        float steer = 0f;
        if (_leftPressed) steer -= 1f;
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
    // Update (input clavier + smoothing clients)
    // =========================================================

    private void Update()
    {
        // 1) Input clavier pour le owner (facultatif)
        if (IsOwner && useUnityAxesOnOwner)
        {
            float t = Input.GetAxisRaw("Vertical");
            float s = Input.GetAxisRaw("Horizontal");

            InputState state = _inputState.Value;
            state.Throttle = Mathf.Clamp(t, -1f, 1f);
            state.Steer    = Mathf.Clamp(s, -1f, 1f);
            _inputState.Value = state;
        }

        // 2) Smoothing pour les CLIENTS seulement (le host voit la vraie physique Rigidbody)
        if (IsServer) return;

        if (_hasSmoothing)
        {
            float lerpSpeed = 15f; // à ajuster
            float factor = Time.deltaTime * lerpSpeed;

            _smoothedWorldPos = Vector3.Lerp(_smoothedWorldPos, _targetWorldPos, factor);
            _smoothedWorldRot = Quaternion.Slerp(_smoothedWorldRot, _targetWorldRot, factor);

            Visual.SetPositionAndRotation(_smoothedWorldPos, _smoothedWorldRot);
        }
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

        // ---------- 1) VITESSE (avant / arrière) ----------
        float targetSpeed = 0f;

        if (Mathf.Abs(input.Throttle) > 0.01f)
        {
            bool forwardInput = input.Throttle > 0f;
            float max = forwardInput ? maxForwardSpeed : maxReverseSpeed;
            targetSpeed = max * (forwardInput ? 1f : -1f);
        }

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, acceleration * dt);

        // ---------- 2) ROTATION (gauche / droite) ----------
        float steerInput  = input.Steer;          // -1 .. 1
        float steerAmount = steerInput * steeringSpeed * dt;

        Quaternion newRot = _rb.rotation * Quaternion.Euler(0f, steerAmount, 0f);
        _rb.MoveRotation(newRot);

        // ---------- 3) AVANCEMENT EN LIGNE DROITE ----------
        // Direction avant sur le plan horizontal
        Vector3 forward = newRot * Vector3.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 delta = forward * _currentSpeed * dt;
        _rb.MovePosition(_rb.position + delta);

        // ---------- 4) On tue les vitesses "physiques" parasites ----------
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // ---------- 5) Sync réseau ----------
        UpdateTrackStateFromRigidbody();
    }

    private void UpdateTrackStateFromRigidbody()
    {
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

        _trackState.Value = new TrackState
        {
            TrackPos = trackPos,
            TrackRot = trackRot
        };
    }

    // =========================================================
    // Conversion trackLocal -> monde (Clients uniquement)
    // =========================================================

    private void ApplyTrackState(TrackState state)
    {
        // Les clients projettent trackLocal -> monde AR local
        Transform basis = myCircuitClient;

        Vector3 worldPos;
        Quaternion worldRot;

        if (basis != null)
        {
            worldPos = basis.TransformPoint(state.TrackPos);
            worldRot = basis.rotation * state.TrackRot;
        }
        else
        {
            // Fallback : pas de circuit client
            worldPos = state.TrackPos;
            worldRot = state.TrackRot;
        }

        _targetWorldPos = worldPos;
        _targetWorldRot = worldRot;

        // Première fois : on téléporte directement
        if (!_hasSmoothing)
        {
            _smoothedWorldPos = worldPos;
            _smoothedWorldRot = worldRot;
            Visual.SetPositionAndRotation(worldPos, worldRot);
            _hasSmoothing = true;
        }
    }
}
