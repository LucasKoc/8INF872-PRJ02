using UnityEngine;

public class ui_audio_manager : MonoBehaviour
{
    [SerializeField] private AudioSource audio_source_ui;
    [SerializeField] private AudioClip son_clic;

    public void JouerSonClic()
    {
        if (audio_source_ui != null && son_clic != null)
            audio_source_ui.PlayOneShot(son_clic);
    }
}
