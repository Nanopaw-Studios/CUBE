using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
public class ButtonSounds : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public AudioClip click;
    public AudioClip hoverenter;
    public AudioClip hoverexit;
    
    public AudioMixerGroup mixer;

    public void OnPointerClick(PointerEventData pointerEventData)
    {
        //Use this to tell when the user left-clicks on the Button
        if (pointerEventData.button == PointerEventData.InputButton.Left)
        {
            AudioSource.PlayClipAtPoint(click, this.transform.position);
        }
    }

    public void OnPointerEnter(PointerEventData pointerEventData)
    {
        if(hoverenter == null) { return; }
        PlaySoundOnMixer(hoverenter, this.transform.position, mixer);
    }

    public void OnPointerExit(PointerEventData pointerEventData)
    {
        if(hoverexit == null) { return; }
        PlaySoundOnMixer(hoverexit, this.transform.position, mixer);
    }

    public void PlaySoundOnMixer(AudioClip clip, Vector3 position, AudioMixerGroup mixerGroup)
    {
        GameObject tempGO = new("TempAudio");
        tempGO.transform.position = position;
        AudioSource aSource = tempGO.AddComponent<AudioSource>();

        // Assign the mixer group
        aSource.outputAudioMixerGroup = mixerGroup;
        aSource.clip = clip;
        aSource.Play();

        // Destroy after clip finishes
        Destroy(tempGO, clip.length);
    }
}