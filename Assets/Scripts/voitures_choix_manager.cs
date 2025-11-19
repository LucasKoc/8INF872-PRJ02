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

    [Header("Etat de sélection")]
    [SerializeField] private int indexSelectionne = 0;

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

        indexSelectionne = Mathf.Clamp(indexSelectionne, 0, voituresSprites.Length - 1);
        MettreAJourAffichage();
    }

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

                    if (i == indexSelectionne)
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

        PlayerPrefs.SetInt("index_voiture_selectionnee", indexSelectionne);
        PlayerPrefs.Save();
    }

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

    public void LancerJeuAvecVoitureSelectionnee()
    {
        // A REMPLIR PLUS TARD LOL
    }
}
