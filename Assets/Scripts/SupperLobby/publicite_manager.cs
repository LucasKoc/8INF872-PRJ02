using UnityEngine;
using UnityEngine.Advertisements;
using System;

public class publicite_manager : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
{
    [Header("IDs Unity Ads")]
    [SerializeField] private string androidGameId;
    [SerializeField] private string iOSGameId;
    [SerializeField] private bool testMode = true;

    [Header("Placement Rewarded")]
    [SerializeField] private string rewardedPlacementId = "Rewarded_Android"; // ou ton ID réel

    private string gameId;
    private Action _onRewardedCompleted; // callback à appeler quand la pub est vue entièrement

    private void Awake()
    {
#if UNITY_ANDROID
        gameId = androidGameId;
#elif UNITY_IOS
        gameId = iOSGameId;
#else
        gameId = androidGameId; // fallback
#endif

        if (!Advertisement.isInitialized && !string.IsNullOrEmpty(gameId))
        {
            Advertisement.Initialize(gameId, testMode, this);
        }
    }

    // ================== API PUB POUR LE RESTE DU JEU ==================

    /// <summary>
    /// Appelé par un autre script pour lancer une pub rewarded.
    /// onRewardedCompleted sera exécuté uniquement si la pub a été regardée jusqu'au bout.
    /// </summary>
    public void MontrerPubRewarded(Action onRewardedCompleted)
    {
        if (!Advertisement.isInitialized)
        {
            Debug.LogWarning("Unity Ads n'est pas initialisé.");
            return;
        }

        _onRewardedCompleted = onRewardedCompleted;

        Debug.Log("Chargement de la pub rewarded...");
        Advertisement.Load(rewardedPlacementId, this);
    }

    // ================== INITIALISATION CALLBACKS ==================

    public void OnInitializationComplete()
    {
        Debug.Log("Unity Ads initialisé !");
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        Debug.LogError($"Unity Ads échec init : {error} - {message}");
    }

    // ================== LOAD CALLBACKS ==================

    public void OnUnityAdsAdLoaded(string placementId)
    {
        Debug.Log("Pub chargée : " + placementId);

        if (placementId == rewardedPlacementId)
        {
            Advertisement.Show(placementId, this);
        }
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.LogError($"Échec load pub {placementId} : {error} - {message}");
    }

    // ================== SHOW CALLBACKS ==================

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log($"Fin de la pub {placementId} avec état {showCompletionState}");

        if (placementId == rewardedPlacementId && showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            // La pub a été vue jusqu'au bout : on donne la récompense
            _onRewardedCompleted?.Invoke();
        }

        _onRewardedCompleted = null;
    }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogError($"Erreur pendant la pub {placementId} : {error} - {message}");
        _onRewardedCompleted = null;
    }

    public void OnUnityAdsShowStart(string placementId) { }
    public void OnUnityAdsShowClick(string placementId) { }
}
