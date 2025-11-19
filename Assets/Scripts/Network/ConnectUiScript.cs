using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Network
{
    public class ConnectUiSCript : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;


        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            hostButton.onClick.AddListener(HostButtonOnClick);
            clientButton.onClick.AddListener(clientButtonOnClick);
        }

        private void HostButtonOnClick()
        {
            NetworkManager.Singleton.StartHost();
        }

        private void clientButtonOnClick()
        {
            NetworkManager.Singleton.StartClient();
        }
    }
}