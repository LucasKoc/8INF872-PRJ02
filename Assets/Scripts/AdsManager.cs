using UnityEngine;
using UnityEngine.Advertisements;

public class AdsManager : MonoBehaviour, IUnityAdsLoadListener, IUnityAdsShowListener
{
    [Header("Game IDs")]
    public string androidGameId;
    public string iOSGameId;
    public bool testMode = true;

    private string gameId;

    [Header("Placement")]
    public string rewardedPlacementId = "Rewarded_Android"; // ou rewardedVideo selon ton dashboard

    private void Start()
    {
        gameId = (Application.platform == RuntimePlatform.IPhonePlayer)
            ? iOSGameId
            : androidGameId;

        Advertisement.Initialize(gameId, testMode);
        LoadRewardedAd();
    }

    //--------------- LOAD AD ----------------
    public void LoadRewardedAd()
    {
        Advertisement.Load(rewardedPlacementId, this);
        Debug.Log("Loading rewarded ad...");
    }

    //--------------- SHOW AD ----------------
    public void ShowRewardedAd()
    {
        Advertisement.Show(rewardedPlacementId, this);
    }

    //--------------- CALLBACKS --------------
    public void OnUnityAdsAdLoaded(string placementId)
    {
        Debug.Log("Ad loaded : " + placementId);
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        Debug.LogError($"Failed to load Ad: {placementId} - {error} - {message}");
    }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogError($"Failed to show Ad: {placementId} - {error} - {message}");
    }

    public void OnUnityAdsShowStart(string placementId) { }
    public void OnUnityAdsShowClick(string placementId) { }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
    {
        Debug.Log("Ad Completed");

        if (state == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log(">>> Reward granted !");
            // 🔥 ICI : débloque ta voiture
        }

        // Recharge la pub
        LoadRewardedAd();
    }
}
