using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;

public class RaceHUD : MonoBehaviour
{
    [Header("Références course")]
    [SerializeField] private LapCounter lapCounter;
    [SerializeField] private RaceManager raceManager;

    [Header("UI tours")]
    [SerializeField] private TMP_Text lapsText;

    [Header("UI victoire")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TMP_Text victoryText;
    [SerializeField] private string lobbySceneName = "SupperLobby";

    [Header("Other UI elements")]
    [SerializeField] private GameObject mobileControlsUI;
    [SerializeField] private GameObject lapsLayout;

    private bool victoryShown = false;

    private void Start()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (lapCounter == null)
            lapCounter = FindObjectOfType<LapCounter>();

        if (raceManager == null)
            raceManager = FindObjectOfType<RaceManager>();

        // On s’abonne au changement de state de fin de course (coté réseau)
        if (raceManager != null)
            raceManager.raceOver.OnValueChanged += OnRaceOverChanged;

        MettreAJourTexteTours();
    }

    private void OnDestroy()
    {
        if (raceManager != null)
            raceManager.raceOver.OnValueChanged -= OnRaceOverChanged;
    }

    private void Update()
    {
        if (lapCounter == null)
        {
            lapCounter = FindObjectOfType<LapCounter>();
            if (lapCounter == null)
                return;
        }

        MettreAJourTexteTours();
    }

    private void MettreAJourTexteTours()
    {
        if (lapCounter == null || lapsText == null) return;

        int total = Mathf.Max(1, lapCounter.totalLaps);
        int courant = lapCounter.currentLap;

        if (courant <= 0 && !lapCounter.raceFinished)
            courant = 1;

        lapsText.text = $"{courant}/{total}";
    }

    // Appelé automatiquement sur tous les clients quand raceOver passe à true
    private void OnRaceOverChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return; // on ne gère que le passage false -> true
        AfficherVictoire();
    }

    private void AfficherVictoire()
    {
        if (victoryShown) return;
        victoryShown = true;

        if (mobileControlsUI != null)
            mobileControlsUI.SetActive(false);
        if (lapsLayout != null)
            lapsLayout.SetActive(false);

        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        if (victoryText != null)
        {
            // Option bonus : message différent si c’est toi le gagnant
            bool isWinner = (raceManager != null &&
                             raceManager.winnerClientId.Value == NetworkManager.Singleton.LocalClientId);

            victoryText.text = isWinner ? "Victoire !" : "Course terminée";
        }
    }

    public void OnBackToLobbyButtonClicked()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogWarning("[RaceHUD] lobbySceneName n'est pas renseigné.");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
