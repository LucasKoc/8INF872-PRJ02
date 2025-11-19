using UnityEngine;
using UnityEngine.UI;
public class menu_manager : MonoBehaviour
{
    [Header("Menu principal")]
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject bouton_jouer;
    [SerializeField] private GameObject groupe_boutons;

    [Header("Fond semi-transparent")]
    [SerializeField] private GameObject panel_fond;

    [Header("Groupes de vues")]
    [SerializeField] private GameObject groupe_options;
    [SerializeField] private GameObject groupe_credits;

    private void Start()
    {
        FermerToutesLesVues();
    }

    private void FermerToutesLesVues()
    {
        if (groupe_options != null) groupe_options.SetActive(false);
        if (groupe_credits != null) groupe_credits.SetActive(false);

        if (panel_fond != null) panel_fond.SetActive(false);

        SetMenuPrincipalInteractif(true);
    }

    private void SetMenuPrincipalInteractif(bool actif)
    {
        if (bouton_jouer != null) bouton_jouer.SetActive(actif);
        if (groupe_boutons != null) groupe_boutons.SetActive(actif);
    }

    // ========= OPTIONS =========

    public void OuvrirOptions()
    {
        SetMenuPrincipalInteractif(false);

        if (panel_fond != null) panel_fond.SetActive(true);

        if (groupe_options != null) groupe_options.SetActive(true);

        if (groupe_credits != null) groupe_credits.SetActive(false);
    }

    public void FermerOptions()
    {
        if (groupe_options != null) groupe_options.SetActive(false);

        if (panel_fond != null) panel_fond.SetActive(false);

        SetMenuPrincipalInteractif(true);
    }

    // ========= CREDITS =========

    public void OuvrirCredits()
    {
        SetMenuPrincipalInteractif(false);

        if (panel_fond != null) panel_fond.SetActive(true);

        if (groupe_credits != null) groupe_credits.SetActive(true);

        if (groupe_options != null) groupe_options.SetActive(false);
    }

    public void FermerCredits()
    {
        if (groupe_credits != null) groupe_credits.SetActive(false);

        if (panel_fond != null) panel_fond.SetActive(false);

        SetMenuPrincipalInteractif(true);
    }

	    // ========= QUITTER L'APPLICATION =========

    public void QuitterApplication()
    {
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

}
