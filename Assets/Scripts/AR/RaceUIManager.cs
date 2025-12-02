using UnityEngine;
using Unity.Netcode;

public class RaceUIManager : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject placementUiRoot;   // UIObjects (countdown + place/reset/ready)
    public GameObject drivingUiRoot;     // MobileControls (boutons de conduite)

    private SimpleCarController localCar;
    private bool carBound = false;

    private void Start()
    {
        // Au début : on place le circuit → on voit l’UI AR
        if (placementUiRoot != null)
            placementUiRoot.SetActive(true);

        // L’UI de conduite reste cachée jusqu’au GO
        if (drivingUiRoot != null)
            drivingUiRoot.SetActive(false);
    }

    private void Update()
    {
        // 1) Tant qu’on n’a pas trouvé la voiture locale, on la cherche
        if (!carBound)
        {
            localCar = FindLocalCar();

            if (localCar != null)
            {
                // Lier tous les CarButton du MobileControls à CETTE voiture
                var buttons = drivingUiRoot.GetComponentsInChildren<CarButton>(true);
                foreach (var btn in buttons)
                {
                    btn.car = localCar;
                }

                carBound = true;
            }
        }

        // 2) Quand la course démarre pour cette voiture → on switch d’UI
        if (carBound && localCar != null && localCar.canDrive.Value)
        {
            if (placementUiRoot != null && placementUiRoot.activeSelf)
                placementUiRoot.SetActive(false);

            if (drivingUiRoot != null && !drivingUiRoot.activeSelf)
                drivingUiRoot.SetActive(true);
        }
    }

    private SimpleCarController FindLocalCar()
    {
        // 1) Donner le circuit AR local à TOUTES les voitures de cette scène
        if (ARPlacementController.LocalCircuit != null)
        {
            foreach (var car in FindObjectsOfType<SimpleCarController>())
            {
                car.InitClientTrack(ARPlacementController.LocalCircuit);
            }
        }

        // 2) Puis trouver la voiture possédée par CE joueur pour les contrôles
        foreach (var car in FindObjectsOfType<SimpleCarController>())
        {
            if (car.IsOwner)   // La voiture contrôlée par CE joueur
                return car;
        }
        return null;
    }
}