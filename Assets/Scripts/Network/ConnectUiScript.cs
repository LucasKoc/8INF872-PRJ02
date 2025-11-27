using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using System.Linq;

public class ConnectUI : MonoBehaviour
{
    [Header("UI")] public Button hostButton;
    public Button clientButton;
    public TMP_InputField hostIpInput;
    public TMP_Text localIpText;
    public int port = 7777;

    void Start()
    {
        hostButton.onClick.AddListener(StartAsHost);
        clientButton.onClick.AddListener(StartAsClient);
    }

    private void StartAsHost()
    {
        string localIp = GetLocalIPAddress();
        localIpText.text = $"Adresse serveur: {localIp}";
        localIpText.enabled = true;

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

        // Bind serveur sur toutes les interfaces mais advertise l'IP réelle pour les clients
        transport.ConnectionData.Address = "0.0.0.0";
        transport.ConnectionData.Port = (ushort)port;

        Debug.Log($"Starting HOST on 0.0.0.0:{port} (advertise {localIp})");
        NetworkManager.Singleton.StartHost();
        // on cache les boutons :
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        hostIpInput.gameObject.SetActive(false);
    }

    private void StartAsClient()
    {
        // annule la dernière tentative (optionnel)
        NetworkManager.Singleton.StopAllCoroutines();

        string ip = hostIpInput.text?.Trim();

        if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out var parsedIp) 
            || parsedIp.AddressFamily != AddressFamily.InterNetwork)
        {
            Debug.LogError("ERREUR : IP invalide entrée !");
            return;
        }

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Address = ip;
        transport.ConnectionData.Port = (ushort)port;

        Debug.Log($"Connecting to HOST at {ip}:{port}");
        NetworkManager.Singleton.StartClient();
        // on cache les boutons :
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        hostIpInput.gameObject.SetActive(false);
    }

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
