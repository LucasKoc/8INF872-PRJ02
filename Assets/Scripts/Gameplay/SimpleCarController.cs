using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
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

    // ---------- Paramètres de conduite ----------
    [Header("Car settings")]
    public float maxForwardSpeed = 10f;   // m/s dans l’espace piste
    public float maxReverseSpeed = 5f;
    public float acceleration   = 8f;    // m/s²
    public float braking        = 10f;   // m/s²
    public float steeringSpeed  = 90f;   // degrés/s à vitesse max

    [Header("Input")]
    [Tooltip("Utiliser Input.GetAxis (clavier) sur le owner (pratique en mode éditeur).")]
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

    // ---------- Etat local côté serveur ----------
    private float _currentSpeed = 0f;   // m/s dans l’espace piste

    // Etat des boutons mobiles (CarButton)
    private bool _forwardPressed;
    private bool _reversePressed;
    private bool _leftPressed;
    private bool _rightPressed;

    // =========================================================
    // Initialisation réseau
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _trackState.OnValueChanged += OnTrackStateChanged;

        if (IsServer)
        {
            // Initialiser l’état piste à partir de la position monde actuelle
            Vector3 trackPos;
            Quaternion trackRot;

            if (circuitRefHost != null)
            {
                trackPos = circuitRefHost.InverseTransformPoint(Visual.position);
                trackRot = Quaternion.Inverse(circuitRefHost.rotation) * Visual.rotation;
            }
            else
            {
                // Fallback : pas de circuitRef, on reste en espace monde
                trackPos = Visual.position;
                trackRot = Visual.rotation;
            }

            _trackState.Value = new TrackState
            {
                TrackPos = trackPos,
                TrackRot = trackRot
            };
        }

        // Appliquer une première fois
        ApplyTrackState(_trackState.Value);
    }

    private void OnDestroy()
    {
        _trackState.OnValueChanged -= OnTrackStateChanged;
    }

    private void OnTrackStateChanged(TrackState previous, TrackState current)
    {
        ApplyTrackState(current);
    }

    // =========================================================
    // API pour RaceManager (serveur) & client AR
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
        // Recalcule la position monde avec ce circuit
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
    }

    // =========================================================
    // Input clavier (optionnel pour tests)
    // =========================================================

    private void Update()
    {
        if (!IsOwner) return;
        if (!useUnityAxesOnOwner) return;

        float t = Input.GetAxisRaw("Vertical");   // Z / flèches
        float s = Input.GetAxisRaw("Horizontal"); // QD / flèches

        InputState state = _inputState.Value;
        state.Throttle = Mathf.Clamp(t, -1f, 1f);
        state.Steer    = Mathf.Clamp(s, -1f, 1f);
        _inputState.Value = state;
    }

    // =========================================================
    // Simulation côté serveur (physique simple en espace piste)
    // =========================================================

    private void FixedUpdate()
    {
        if (!IsServer) return;     // seul le serveur bouge la voiture logique
        if (!canDrive.Value) return; // tant que la course n’a pas commencé : immobile

        float dt = Time.fixedDeltaTime;
        InputState input = _inputState.Value;
        TrackState ts = _trackState.Value;

        // --- Gestion de la vitesse ---
        float targetSpeed = 0f;

        if (Mathf.Abs(input.Throttle) > 0.01f)
        {
            if (input.Throttle > 0f)
            {
                targetSpeed = maxForwardSpeed * input.Throttle;
            }
            else
            {
                // marche arrière
                targetSpeed = -maxReverseSpeed * -input.Throttle;
            }

            float accelAmount = acceleration * dt;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accelAmount);
        }
        else
        {
            // Freinage / ralentissement
            float decelAmount = braking * dt;
            if (_currentSpeed > 0f)
                _currentSpeed = Mathf.Max(0f, _currentSpeed - decelAmount);
            else if (_currentSpeed < 0f)
                _currentSpeed = Mathf.Min(0f, _currentSpeed + decelAmount);
        }

        // --- Rotation dans l’espace piste ---
        float speedFactor = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / (maxForwardSpeed + 0.001f));
        float steerSign = Mathf.Sign(_currentSpeed); // inverse en marche arrière
        float steerAngle = input.Steer * steeringSpeed * speedFactor * steerSign * dt;

        Quaternion newTrackRot = ts.TrackRot * Quaternion.Euler(0f, steerAngle, 0f);

        // --- Déplacement dans l’espace piste ---
        Vector3 forwardTrack = newTrackRot * Vector3.forward;
        Vector3 newTrackPos  = ts.TrackPos + forwardTrack * _currentSpeed * dt;

        ts.TrackPos = newTrackPos;
        ts.TrackRot = newTrackRot;

        // Ecrit dans la NetworkVariable → tous les clients reçoivent
        _trackState.Value = ts;
    }

    // =========================================================
    // Conversion trackLocal -> monde (Host / Client)
    // =========================================================

    private void ApplyTrackState(TrackState state)
    {
        Transform basis = null;

        // Host = serveur → utilise circuitRefHost
        if (IsServer && circuitRefHost != null)
        {
            basis = circuitRefHost;
        }
        // Clients → utilisent leur circuit AR local
        else if (myCircuitClient != null)
        {
            basis = myCircuitClient;
        }

        Vector3 worldPos;
        Quaternion worldRot;

        if (basis != null)
        {
            worldPos = basis.TransformPoint(state.TrackPos);
            worldRot = basis.rotation * state.TrackRot;
        }
        else
        {
            // Fallback : pas de circuit, on reste dans le même repère
            worldPos = state.TrackPos;
            worldRot = state.TrackRot;
        }

        Visual.SetPositionAndRotation(worldPos, worldRot);
    }
}
