using System.Collections.Generic;
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

public class LobbyUI : MonoBehaviour
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

    // Scène précédente pour le bouton "Retour" lorsque dans le MainMenuPanel
    public string previousSceneName = "MainMenuScene"; // TODO : Changer lorsque le menu principal sera implémenté

    // Nombre de tours global pour la partie
    public static int GameLaps = 3;

    // État de connexion en cours
    private bool isConnecting = false;

    // Pour garder un nombre random stable par client
    private readonly Dictionary<ulong, int> clientRandomIds = new Dictionary<ulong, int>();

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


    // ---------- 0. Navigation UI ----------

    private void OnCreateGameClicked()
    {
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
            GameLaps = Mathf.Max(1, laps);
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

        // 2) Si on est dans HostPanel -> on "clear" le lobby et retour MainMenu
        if (hostPanel.activeSelf)
        {
            Debug.Log("Retour depuis HostPanel : arrêt du host et nettoyage du lobby.");

            // Stopper proprement le NetworkManager (server + clients déconnectés)
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Vider la liste des joueurs UI
            foreach (Transform child in playersContainer)
            {
                if (child == playerEntryTemplate.transform) continue;
                Destroy(child.gameObject);
            }

            // Vider les IDs random
            clientRandomIds.Clear();

            // Masquer le hostPanel, revenir au menu principal
            hostPanel.SetActive(false);
            mainMenuPanel.SetActive(true);

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

    // ---------- 3. Callbacks Netcode ----------

    private void HandleClientConnected(ulong clientId)
    {
        // On ne s'intéresse qu'au client local, et pas quand on est Host
        if (clientId != NetworkManager.Singleton.LocalClientId || NetworkManager.Singleton.IsHost)
            return;

        isConnecting = false;

        joinErrorText.enabled = true;
        joinErrorText.text = "Connecté au serveur.";
        joinErrorText.color = Color.green;
        joinErrorText.fontStyle = FontStyles.Normal;

        // Désactivation des champs
        if (connectButton != null)
            connectButton.interactable = false;
        if (ipInputField != null)
            ipInputField.interactable = false;

        // Changement de panel
        hostPanel.SetActive(true);
        joinPanel.SetActive(false);

        RefreshPlayersList();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        // On ne s'intéresse qu'au client local, et pas au host
        if (clientId != NetworkManager.Singleton.LocalClientId || NetworkManager.Singleton.IsHost)
            return;

        joinErrorText.enabled = true;
        joinErrorText.fontStyle = FontStyles.Normal;

        if (isConnecting)
        {
            // Déconnexion pendant la phase de connexion => erreur
            joinErrorText.text = "Erreur lors de la connexion.";
        }
        else
        {
            // Déconnexion après coup => connexion fermée
            joinErrorText.text = "Connexion fermée.";
        }

        joinErrorText.color = Color.red;
        isConnecting = false;

        // On re-permet au joueur de retenter
        if (connectButton != null)
            connectButton.interactable = true;
        if (ipInputField != null)
            ipInputField.interactable = true;

        // Actualisation côté host et clients
        RefreshPlayersList();
    }

    // ---------- 4. Liste des joueurs ----------

    private void RefreshPlayersList()
    {
        // Supprimer les anciennes entrées
        foreach (Transform child in playersContainer)
        {
            if (child == playerEntryTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        var ids = NetworkManager.Singleton.ConnectedClientsIds;

        foreach (ulong id in ids)
        {
            // Si ce client n’a pas encore de random ID, on lui en donne un
            if (!clientRandomIds.ContainsKey(id))
            {
                // 0 à 9999 -> affiché en 4 chiffres
                clientRandomIds[id] = Random.Range(0, 10000);
            }

            int rand = clientRandomIds[id];
            string baseName = $"Joueur{rand:0000}";

            // On clone le template
            TMP_Text entry = Instantiate(playerEntryTemplate, playersContainer);
            entry.gameObject.SetActive(true);

            if (id == NetworkManager.Singleton.LocalClientId)
            {
                entry.text = baseName + " (Vous)";
                entry.color = Color.red;
            }
            else
            {
                entry.text = baseName;
                // Optionnel : couleur par défaut
                entry.color = Color.white;
            }
        }

        // Nettoyer les IDs des clients qui ne sont plus connectés
        var connectedSet = ids.ToHashSet();
        var keysToRemove = clientRandomIds.Keys.Where(k => !connectedSet.Contains(k)).ToList();
        foreach (var key in keysToRemove)
        {
            clientRandomIds.Remove(key);
        }
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
