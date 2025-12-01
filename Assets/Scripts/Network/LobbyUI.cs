using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using System.Linq;
using UnityEngine.SceneManagement;

public class LobbyUI : NetworkBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject hostPanel;
    public GameObject joinPanel;
    public GameObject settingsPanel;

    [Header("Main Menu")]
    public Button createGameButton;
    public Button joinGameButton;

    [Header("Host UI")]
    public TMP_Text hostIpText;
    public Button settingsButton;
    public Button startGameButton;
    public Transform playersContainer;
    public TMP_Text playerEntryTemplate;

    [Header("Host Panel Texts")]
    public TMP_Text hostTitleText;
    public TMP_Text hostStatusText;
    public TMP_Text hostLapsText;

    [Header("Join UI")]
    public TMP_InputField ipInputField;
    public Button connectButton;
    public TMP_Text joinErrorText;

    [Header("Settings UI")]
    public Slider lapsSlider;
    public TMP_Text lapsValueText;
    public Button closeSettingsButton;

    [Header("Network")]
    public int port = 7777;

    [Header("Navigation")]
    public Button backButton;

    [Header("Game Rules")]
    [SerializeField] private int maxPlayers = 3;
    public static int GameLaps = 3;

    // Scène précédente pour le bouton "Retour" lorsque dans le MainMenuPanel
    public string previousSceneName = "MainMenuScene"; // TODO : Changer lorsque le menu principal sera implémenté

    // Nombre de tours global pour la partie
    private NetworkVariable<int> netGameLaps = new NetworkVariable<int>(
        3,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // État de connexion en cours
    private bool isConnecting = false;

    // Le client a quitté volontairement la partie (bouton Retour depuis le hostPanel)
    private bool leftManually = false;

    // Dernière IP connectée
    private string lastConnectedIp = "";

    private void Start()
    {
        // État initial
        mainMenuPanel.SetActive(true);
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
        settingsPanel.SetActive(false);

        // Nettoyage du texte d’erreur au démarrage
        if (joinErrorText != null)
        {
            joinErrorText.text = "";
            joinErrorText.enabled = false;
        }

        // Validation des caractères de l'IP : uniquement chiffres et points
        if (ipInputField != null)
        {
            ipInputField.onValidateInput += ValidateIpChar;
        }

        // Boutons menu principal
        createGameButton.onClick.AddListener(OnCreateGameClicked);
        joinGameButton.onClick.AddListener(OnJoinGameClicked);

        // Host UI
        settingsButton.onClick.AddListener(OnSettingsClicked);
        startGameButton.onClick.AddListener(OnStartGameClicked);

        // Join UI
        connectButton.onClick.AddListener(OnConnectClicked);
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        // Settings UI
        closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);

        // Abonnements aux callbacks réseau
        var nm = NetworkManager.Singleton;
        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;

        // Valeur initiale du slider = GameLaps
        if (lapsSlider != null)
        {
            lapsSlider.minValue = 1;
            lapsSlider.maxValue = 10;
            lapsSlider.wholeNumbers = true;
            lapsSlider.value = GameLaps;

            lapsSlider.onValueChanged.AddListener(OnLapsSliderChanged);
        }

        // Mettre à jour le texte au démarrage
        OnLapsSliderChanged(lapsSlider != null ? lapsSlider.value : GameLaps);
    }

    private void OnLapsSliderChanged(float value)
    {
        int laps = Mathf.RoundToInt(value);
        if (lapsValueText != null)
        {
            lapsValueText.text = $"{laps}";
        }
    }

    // ---------- Network ----------

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Quand la valeur réseau change → mettre à jour GameLaps + UI
        netGameLaps.OnValueChanged += OnNetGameLapsChanged;

        if (IsServer)
        {
            // Le host est la source de vérité
            netGameLaps.Value = GameLaps;
        }

        // Appliquer la valeur actuelle au démarrage (host ET clients)
        OnNetGameLapsChanged(0, netGameLaps.Value);
    }

    private void OnDestroy()
    {
        // Sécurité pour éviter les leaks d’event
        netGameLaps.OnValueChanged -= OnNetGameLapsChanged;
    }

    private void OnNetGameLapsChanged(int previous, int current)
    {
        GameLaps = current;

        // Texte dans le panel host / client
        if (hostLapsText != null)
            hostLapsText.text = $"Nombre de tours : {GameLaps}";

        // Slider des settings (côté host, principalement)
        if (lapsSlider != null && Mathf.Abs(lapsSlider.value - current) > 0.01f)
            lapsSlider.value = current;

        // Valeur à côté du slider
        if (lapsValueText != null)
            lapsValueText.text = current.ToString();
    }


    // ---------- 0. Navigation UI ----------

    private void OnCreateGameClicked()
    {
        // Settings par défaut
        GameLaps = 3;
        if (lapsSlider != null)
        {
            lapsSlider.value = GameLaps;
            OnLapsSliderChanged(lapsSlider.value);
        }

        mainMenuPanel.SetActive(false);
        hostPanel.SetActive(true);
        joinPanel.SetActive(false);

        StartAsHost();
    }

    private void OnJoinGameClicked()
    {
        mainMenuPanel.SetActive(false);
        hostPanel.SetActive(false);
        joinPanel.SetActive(true);
    }

    private void OnSettingsClicked()
    {
        settingsPanel.SetActive(true);

        // Cacher le bouton retour pendant les settings
        if (backButton != null)
            backButton.gameObject.SetActive(false);
    }

    private void OnCloseSettingsClicked()
    {
        if (lapsSlider != null)
        {
            int laps = Mathf.RoundToInt(lapsSlider.value);
            laps = Mathf.Max(1, laps);

            // Seul le serveur a le droit de changer la valeur réseau
            if (IsServer)
            {
                netGameLaps.Value = laps;   // déclenche OnNetGameLapsChanged partout
            }
            else
            {
                // Sécurité (normalement un client n'a pas accès aux settings)
                GameLaps = laps;
            }
        }

        settingsPanel.SetActive(false);

        // Ré-afficher le bouton retour après fermeture des settings
        if (backButton != null)
            backButton.gameObject.SetActive(true);
    }

    private void OnBackClicked()
    {
        // 1) Si on est dans JoinPanel -> retour au menu principal
        if (joinPanel.activeSelf)
        {
            if (isConnecting &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsClient &&
                !NetworkManager.Singleton.IsHost)
            {
                Debug.Log("Annulation de la tentative de connexion, retour au menu principal.");
                NetworkManager.Singleton.Shutdown();
                isConnecting = false;
            }

            joinPanel.SetActive(false);
            mainMenuPanel.SetActive(true);

            if (joinErrorText != null)
            {
                joinErrorText.text = "";
                joinErrorText.enabled = false;
            }

            if (ipInputField != null)
            {
                ipInputField.text = "";
            }

            if (connectButton != null) connectButton.interactable = true;
            if (ipInputField != null) ipInputField.interactable = true;

            return;
        }

        // 2) Si on est dans HostPanel -> comportement différent Host / Client
        if (hostPanel.activeSelf)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.IsHost)
                {
                    // Cas HOST : on ferme la partie pour tout le monde
                    Debug.Log("Retour depuis HostPanel (host) : arrêt du host et nettoyage du lobby.");

                    nm.Shutdown();

                    // Vider la liste des joueurs UI
                    foreach (Transform child in playersContainer)
                    {
                        if (child == playerEntryTemplate.transform) continue;
                        Destroy(child.gameObject);
                    }

                    hostPanel.SetActive(false);
                    mainMenuPanel.SetActive(true);
                }
                else if (nm.IsClient)
                {
                    // Cas CLIENT : on quitte la partie (mais le host reste)
                    Debug.Log("Retour depuis HostPanel (client) : quitter la partie.");

                    leftManually = true;
                    nm.Shutdown();
                    // On NE change PAS ici les panels : on laisse HandleClientDisconnected faire le boulot
                }
            }

            return;
        }

        // 3) Dans le MainMenuPanel -> revenir à la scène précédente (menu principal global)
        // TODO : Envoyer vers le super-lobby lorsque celui-ci sera implémenté
        Debug.Log("BackButton : aucun panel spécial à quitter (MainMenu).");
    }

    // ---------- 1. HOST : créer la partie ----------

    private void StartAsHost()
    {
        string localIp = GetLocalIPAddress();
        hostIpText.text = $"{localIp}";
        hostIpText.enabled = true;

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        // Bind serveur sur toutes les interfaces mais advertise l'IP réelle pour les clients
        transport.ConnectionData.Address = "0.0.0.0";
        transport.ConnectionData.Port = (ushort)port;

        Debug.Log($"Starting HOST on 0.0.0.0:{port} (advertise {localIp})");
        NetworkManager.Singleton.StartHost();

        // Le host voit déjà sa propre entrée dans la liste
        RefreshPlayersList();
        SetupHostUi();
    }

    private void SetupHostUi()
    {
        if (hostStatusText != null)
            hostStatusText.text = ""; // rien, ou "Prêt à démarrer"

        if (hostLapsText != null)
            hostLapsText.text = $"Nombre de tours : {GameLaps}";

        if (settingsButton != null)
            settingsButton.gameObject.SetActive(true);
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(true);

        UpdateLobbyTitleWithCount();
    }


    // ---------- 2. CLIENT : rejoindre la partie ----------

    private void OnConnectClicked()
    {
        joinErrorText.text = "";
        joinErrorText.color = Color.white;
        joinErrorText.fontStyle = FontStyles.Normal;
        joinErrorText.enabled = true;

        string ip = ipInputField.text?.Trim();

        // Validation stricte IPv4 X.X.X.X
        if (string.IsNullOrEmpty(ip))
        {
            joinErrorText.text = "Adresse IP vide.";
            joinErrorText.color = Color.red;
            return;
        }

        string[] parts = ip.Split('.');
        if (parts.Length != 4)
        {
            joinErrorText.text = "Adresse IP invalide (format X.X.X.X).";
            joinErrorText.color = Color.red;
            return;
        }

        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                joinErrorText.text = "Adresse IP invalide (bloc vide).";
                joinErrorText.color = Color.red;
                return;
            }

            if (!int.TryParse(part, out int value))
            {
                joinErrorText.text = "Adresse IP invalide (caractères non numériques).";
                joinErrorText.color = Color.red;
                return;
            }

            if (value < 0 || value > 255)
            {
                joinErrorText.text = "Adresse IP invalide (valeur hors [0–255]).";
                joinErrorText.color = Color.red;
                return;
            }
        }

        // Utilisation de IPAddress.TryParse pour validation finale
        if (!IPAddress.TryParse(ip, out var parsedIp) || parsedIp.AddressFamily != AddressFamily.InterNetwork)
        {
            joinErrorText.text = "Adresse IP invalide.";
            joinErrorText.color = Color.red;
            return;
        }

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Address = ip;
        transport.ConnectionData.Port = (ushort)port;

        lastConnectedIp = ip;

        // État "connexion en cours"
        isConnecting = true;
        joinErrorText.text = "Connexion en cours...";
        joinErrorText.color = Color.gray;
        joinErrorText.fontStyle = FontStyles.Italic;

        // On bloque le bouton pour éviter de spammer
        if (connectButton != null)
            connectButton.interactable = false;

        Debug.Log($"Connecting to HOST at {ip}:{port}");
        NetworkManager.Singleton.StartClient();
    }

    private char ValidateIpChar(string currentText, int charIndex, char addedChar)
    {
        // Autoriser seulement 0–9 et '.'
        bool isDigit = (addedChar >= '0' && addedChar <= '9');
        bool isDot   = (addedChar == '.');

        if (!isDigit && !isDot)
            return '\0'; // rejet

        // Si on ajoute un point :
        if (isDot)
        {
            // 1) Pas de point en premier caractère
            if (currentText.Length == 0)
                return '\0';

            // 2) Pas de double point ".."
            if (charIndex > 0 && currentText[charIndex - 1] == '.')
                return '\0';

            // 3) Max 3 points (4 blocs X.X.X.X)
            int dotCount = currentText.Count(c => c == '.');
            if (dotCount >= 3)
                return '\0';

            return addedChar;
        }

        // À partir d'ici, c'est un chiffre
        // On doit vérifier la longueur et la valeur du bloc courant (entre 2 points)

        // Trouver le début du bloc (après le dernier '.')
        int lastDotIndex = currentText.LastIndexOf('.');
        int blockStart = lastDotIndex + 1;

        // Longueur actuelle du bloc (avant d'ajouter ce chiffre)
        int blockLength = currentText.Length - blockStart;

        // 4) Max 3 chiffres par bloc
        if (blockLength >= 3)
            return '\0';

        // 5) Vérifier que la valeur du bloc ne dépasse pas 255
        string currentBlock = currentText.Substring(blockStart);
        string newBlock = currentBlock.Insert(charIndex - blockStart, addedChar.ToString());

        // newBlock peut être vide si on tape au début, on vérifie juste si parsable
        if (int.TryParse(newBlock, out int blockValue))
        {
            if (blockValue > 255)
                return '\0';
        }

        return addedChar;
    }

    private void SetupClientHostUi()
    {
        var nm = NetworkManager.Singleton;
        ulong hostId = NetworkManager.ServerClientId;
        string hostName = GetDisplayNameForClient(hostId);

        if (hostStatusText != null)
            hostStatusText.text = "En attente du host";

        if (hostLapsText != null)
            hostLapsText.text = $"Nombre de tours : {GameLaps}";

        if (hostIpText != null)
            hostIpText.text = $"{lastConnectedIp}";

        if (settingsButton != null)
            settingsButton.gameObject.SetActive(false);
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(false);

        UpdateLobbyTitleWithCount();
    }

    // ---------- 3. Callbacks Netcode ----------

    private void HandleClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;

        // --- Vérification côté serveur : lobby plein ? ---
        if (nm.IsServer)
        {
            int currentCount = nm.ConnectedClientsIds.Count; // host + clients (y compris celui qui vient d'arriver)

            if (currentCount > maxPlayers)
            {
                int allowedCount = Mathf.Min(maxPlayers, currentCount - 1); // nb de joueurs déjà dans la partie
                string reason = $"Lobby plein ({allowedCount}/{maxPlayers} joueurs)";

                Debug.Log($"{reason}. On refuse le client {clientId}.");

                // ⚠surcharge avec "reason" -> sera dispo côté client via DisconnectReason
                nm.DisconnectClient(clientId, reason);
                return;
            }
        }

        if (nm.IsHost)
        {
            // Côté host : à chaque nouveau client, on rafraîchit juste la liste
            RefreshPlayersList();
            return;
        }

        // Côté client
        if (clientId == nm.LocalClientId)
        {
            // Nous venons de nous connecter
            isConnecting = false;

            joinErrorText.enabled = true;
            joinErrorText.text = "Connecté au serveur.";
            joinErrorText.color = Color.green;
            joinErrorText.fontStyle = FontStyles.Normal;

            if (connectButton != null) connectButton.interactable = false;
            if (ipInputField != null) ipInputField.interactable = false;

            // On passe à l'écran de partie
            hostPanel.SetActive(true);
            joinPanel.SetActive(false);

            // Adapter l'UI pour un joueur (pas host)
            SetupClientHostUi();

            RefreshPlayersList();
        }
        else
        {
            // Un autre joueur s'est connecté -> on met juste à jour la liste
            RefreshPlayersList();
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;

        // --- Côté HOST ---
        if (nm.IsHost)
        {
            // Un client a quitté la partie => on met à jour la liste
            RefreshPlayersList();
            return;
        }

        // --- Côté CLIENT ---
        if (clientId == nm.LocalClientId)
        {
            string reason = nm.DisconnectReason;

            // Nous (client) venons d'être déconnectés du serveur
            if (joinErrorText != null)
            {
                joinErrorText.enabled = true;
                joinErrorText.fontStyle = FontStyles.Normal;

                if (!string.IsNullOrEmpty(reason))
                {
                    // Message d'erreur prédéfinie - e.g. "Lobby plein (...)"
                    joinErrorText.text = reason;
                    joinErrorText.color = Color.red;
                }
                else if (leftManually)
                {
                    // Cas : on a appuyé sur "Retour" dans le hostPanel
                    joinErrorText.text = "Vous avez quitté la partie.";
                    joinErrorText.color = Color.gray;
                }
                else if (isConnecting)
                {
                    // Échec pendant la tentative de connexion
                    joinErrorText.text = "Erreur lors de la connexion.";
                    joinErrorText.color = Color.red;
                }
                else
                {
                    // Cas : host a quitté / fermé la partie
                    joinErrorText.text = "Partie fermée (hôte déconnecté).";
                    joinErrorText.color = Color.red;
                }
            }

            // Reinitialisation des états
            isConnecting = false;
            leftManually = false;

            // On réactive les contrôles pour permettre de se reconnecter à autre chose
            if (connectButton != null)
                connectButton.interactable = true;
            if (ipInputField != null)
                ipInputField.interactable = true;

            // Navigation UI : retour à l'écran "Rejoindre une partie"
            if (hostPanel != null) hostPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (joinPanel != null) joinPanel.SetActive(true);
        }
        else
        {
            // Un autre client (pas nous) s’est déconnecté => juste refresh liste
            RefreshPlayersList();
        }
    }

    // ---------- 4. Liste des joueurs ----------

    private string GetDisplayNameForClient(ulong clientId)
    {
        // Donne un nombre pseudo-aléatoire stable par clientId
        long hash = (long)clientId * 1103515245L + 12345L;
        if (hash < 0) hash = -hash;
        int rand = (int)(hash % 10000); // 0..9999

        return $"Joueur{rand:0000}";
    }

    private int GetCurrentPlayerCount()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            return nm.ConnectedClientsIds.Count;  // host + clients
        }
        return 0;
    }

    private void UpdateLobbyTitleWithCount()
    {
        if (hostTitleText == null) return;

        int count = GetCurrentPlayerCount();

        var nm = NetworkManager.Singleton;

        if (nm != null && nm.IsHost)
        {
            // Côté host
            hostTitleText.text = $"Création d'une partie ({count}/{maxPlayers})";
        }
        else if (nm != null && nm.IsClient)
        {
            // Côté client : on affiche aussi le nom du host
            ulong hostId = NetworkManager.ServerClientId;
            string hostName = GetDisplayNameForClient(hostId);
            hostTitleText.text = $"Partie de : {hostName} ({count}/{maxPlayers})";
        }
    }

    private void RefreshPlayersList()
    {
        // Supprimer les anciennes entrées
        foreach (Transform child in playersContainer)
        {
            if (child == playerEntryTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        var nm = NetworkManager.Singleton;
        var ids = nm.ConnectedClientsIds;

        foreach (ulong id in ids)
        {
            string baseName = GetDisplayNameForClient(id);

            // On clone le template
            TMP_Text entry = Instantiate(playerEntryTemplate, playersContainer);
            entry.gameObject.SetActive(true);

            if (id == nm.LocalClientId)
            {
                entry.text = baseName + " (Vous)";
                entry.color = Color.red;
            }
            else
            {
                entry.text = baseName;
                entry.color = Color.white;
            }
        }
        UpdateLobbyTitleWithCount();
    }


    // ---------- 5. Lancer la partie (host) ----------

    private void OnStartGameClicked()
    {
        if (!NetworkManager.Singleton.IsHost) return;

        Debug.Log("Host lance la partie, chargement de la scène ARace_Game...");

        // Avec Network Scene Management
        NetworkManager.Singleton.SceneManager.LoadScene("ARace_Game", LoadSceneMode.Single);
    }

    // ---------- 6. Utilitaires ----------

    public static string GetLocalIPAddress()
    {
        // Parcours des interfaces stables (exclut boucle locale / down / non IPv4)
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                // Priorité au Wi-Fi puis Ethernet, sinon première IPv4 trouvée
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                    ni.Name.ToLower().Contains("wlan") ||
                    ni.Name.ToLower().Contains("wifi") ||
                    ni.Name.ToLower().Contains("ap"))
                {
                    var addr = ni.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (addr != null)
                        return addr.Address.ToString();
                }
            }

            // fallback : première IPv4 non loopback
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("GetLocalIPAddress fallback due to: " + ex.Message);
        }

        return "127.0.0.1";
    }
}
