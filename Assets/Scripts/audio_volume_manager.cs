using UnityEngine;
using UnityEngine.UI;

public class audio_volume_manager : MonoBehaviour
{
    [Header("Sources audio")]
    [SerializeField] private AudioSource audio_source_musique_menu;
    [SerializeField] private AudioSource audio_source_musique_choix_voiture;
    [SerializeField] private AudioSource audio_source_ui;

    [Header("Sliders UI")]
    [SerializeField] private Slider slider_volume_musique;
    [SerializeField] private Slider slider_volume_effets;

    private const string PREF_VOLUME_MUSIQUE = "volume_musique";
    private const string PREF_VOLUME_EFFETS  = "volume_effets";

    private void Start()
    {
        float volumeMusique = PlayerPrefs.GetFloat(PREF_VOLUME_MUSIQUE, 100);
        float volumeEffets  = PlayerPrefs.GetFloat(PREF_VOLUME_EFFETS, 100);

        if (slider_volume_musique != null)
            slider_volume_musique.value = volumeMusique;

        if (slider_volume_effets != null)
            slider_volume_effets.value = volumeEffets;

        AppliquerVolumeMusique(volumeMusique);
        AppliquerVolumeEffets(volumeEffets);
    }

    public void ChangerVolumeMusique(float valeur)
    {
        AppliquerVolumeMusique(valeur);
        PlayerPrefs.SetFloat(PREF_VOLUME_MUSIQUE, valeur);
    }

    public void ChangerVolumeEffets(float valeur)
    {
        AppliquerVolumeEffets(valeur);
        PlayerPrefs.SetFloat(PREF_VOLUME_EFFETS, valeur);
    }

    private void AppliquerVolumeMusique(float valeur)
    {
        float volume = valeur / 100f;

        if (audio_source_musique_menu != null)
            audio_source_musique_menu.volume = volume;

        if (audio_source_musique_choix_voiture != null)
            audio_source_musique_choix_voiture.volume = volume;
    }

    private void AppliquerVolumeEffets(float valeur)
    {
        float volume = valeur / 100f;

        if (audio_source_ui != null)
            audio_source_ui.volume = volume;
    }
}
