using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class voitures_choix_manager : MonoBehaviour
{
    [Header("Données voitures")]
    [Tooltip("Sprites utilisés pour l'affichage dans le menu (1 par voiture).")]
    [SerializeField] private Sprite[] voituresSprites;

    [Tooltip("Noms affichés au-dessus de la voiture (même ordre que les sprites).")]
    [SerializeField] private string[] voituresNoms;

    [Header("UI principale")]
    [SerializeField] private Image image_voiture_centre;
    [SerializeField] private TMP_Text texte_nom_voiture;

    [Header("Miniatures (toutes les voitures visibles)")]
    [SerializeField] private Image[] miniatures_voitures;

    [Header("Déverrouillage")]
    [Tooltip("État de déverrouillage des voitures (true = débloquée). Même longueur que voituresSprites.")]
    [SerializeField] private bool[] voituresDebloquees;

    [Tooltip("Image du cadenas affichée sur la voiture verrouillée.")]
    [SerializeField] private Image image_cadenas;

    [Tooltip("Bouton 'Déverrouiller' affiché quand la voiture est verrouillée.")]
    [SerializeField] private GameObject bouton_deverrouiller;

    [Header("Bouton Jouer")]
    [Tooltip("Bouton 'Jouer' utilisé pour lancer la partie avec la voiture sélectionnée.")]
    [SerializeField] private Button bouton_jouer;

    [Header("Etat de sélection")]
    [SerializeField] private int indexSelectionne = 0;

    [SerializeField] private menu_manager menuManager;

    [SerializeField] private publicite_manager pub_manager;


    private void Start()
    {
        if (voituresSprites == null || voituresSprites.Length == 0)
        {
            Debug.LogError("Aucune voiture définie dans voituresSprites !");
            return;
        }

        if (voituresNoms == null || voituresNoms.Length != voituresSprites.Length)
        {
            Debug.LogWarning("Le tableau voituresNoms ne correspond pas à voituresSprites. Ajustement automatique.");
            voituresNoms = new string[voituresSprites.Length];
            for (int i = 0; i < voituresSprites.Length; i++)
            {
                voituresNoms[i] = "Voiture " + (i + 1);
            }
        }

        InitialiserEtatDebloque();
        indexSelectionne = Mathf.Clamp(indexSelectionne, 0, voituresSprites.Length - 1);

        MettreAJourAffichage();
    }

    // ================== INITIALISATION DEBLOCAGE ==================

    private void InitialiserEtatDebloque()
    {
        if (voituresDebloquees == null || voituresDebloquees.Length != voituresSprites.Length)
        {
            voituresDebloquees = new bool[voituresSprites.Length];
        }

        for (int i = 0; i < voituresSprites.Length; i++)
        {
            int valeurParDefaut = (i == 0) ? 1 : 0;
            int etatSauvegarde = PlayerPrefs.GetInt("voiture_debloquee_" + i, valeurParDefaut);

            voituresDebloquees[i] = (etatSauvegarde == 1);
        }
    }

    // ================== AFFICHAGE ==================

    private void MettreAJourAffichage()
    {
        if (image_voiture_centre != null)
        {
            image_voiture_centre.sprite = voituresSprites[indexSelectionne];
        }

        if (texte_nom_voiture != null)
        {
            texte_nom_voiture.text = voituresNoms[indexSelectionne];
        }

        if (miniatures_voitures != null && miniatures_voitures.Length > 0)
        {
            for (int i = 0; i < miniatures_voitures.Length; i++)
            {
                if (i < voituresSprites.Length && miniatures_voitures[i] != null)
                {
                    miniatures_voitures[i].sprite = voituresSprites[i];

                    if (!voituresDebloquees[i])
                    {
                        miniatures_voitures[i].color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
                    }
                    else if (i == indexSelectionne)
                    {
                        miniatures_voitures[i].color = Color.white;
                    }
                    else
                    {
                        miniatures_voitures[i].color = new Color(1f, 1f, 1f, 0.5f);
                    }
                }
            }
        }

        bool estDebloquee = voituresDebloquees[indexSelectionne];

        if (image_cadenas != null)
            image_cadenas.gameObject.SetActive(!estDebloquee);

        if (bouton_deverrouiller != null)
            bouton_deverrouiller.SetActive(!estDebloquee);

        if (image_voiture_centre != null)
        {
            Color c = image_voiture_centre.color;
            c.a = estDebloquee ? 1f : 0.4f;
            image_voiture_centre.color = c;
        }

        if (bouton_jouer != null)
        {
            bouton_jouer.interactable = estDebloquee;
        }

        PlayerPrefs.SetInt("index_voiture_selectionnee", indexSelectionne);
        PlayerPrefs.Save();
    }

    // ================== NAVIGATION ==================

    public void VoitureSuivante()
    {
        if (voituresSprites == null || voituresSprites.Length == 0) return;

        indexSelectionne++;
        if (indexSelectionne >= voituresSprites.Length)
        {
            indexSelectionne = 0;
        }

        MettreAJourAffichage();
    }

    public void VoiturePrecedente()
    {
        if (voituresSprites == null || voituresSprites.Length == 0) return;

        indexSelectionne--;
        if (indexSelectionne < 0)
        {
            indexSelectionne = voituresSprites.Length - 1;
        }

        MettreAJourAffichage();
    }

    // ================== DEVERROUILLAGE ==================

    public void DeverrouillerVoitureCourante()
    {
        if (indexSelectionne < 0 || indexSelectionne >= voituresDebloquees.Length)
            return;

        if (voituresDebloquees[indexSelectionne])
            return;

        voituresDebloquees[indexSelectionne] = true;

        PlayerPrefs.SetInt("voiture_debloquee_" + indexSelectionne, 1);
        PlayerPrefs.Save();

        MettreAJourAffichage();
    }

    public void BoutonDeverrouillerAvecPub()
    {
        if (pub_manager == null)
        {
            Debug.LogError("publicite_manager n'est pas assigné !");
            return;
        }

        // On demande une pub rewarded, et quand elle est terminée on déverrouille
        pub_manager.MontrerPubRewarded(() =>
        {
            DeverrouillerVoitureCourante();
        });
    }


    // ================== LANCEMENT JEU ==================

    public void LancerJeuAvecVoitureSelectionnee()
    {
        // Sécurité : voiture verrouillée → on ne fait rien
        if (!voituresDebloquees[indexSelectionne])
        {
            Debug.Log("Cette voiture est verrouillée, impossible de jouer avec.");
            return;
        }

        // L'index est déjà sauvegardé dans MettreAJourAffichage(),
        // donc rien de plus à faire ici pour les données.

        Debug.Log("Lancement du lobby avec la voiture : " + voituresNoms[indexSelectionne]);

        if (menuManager != null)
        {
            menuManager.OuvrirLobby();
        }
        else
        {
            Debug.LogWarning("menu_manager non assigné dans voitures_choix_manager.");
        }
    }

}
