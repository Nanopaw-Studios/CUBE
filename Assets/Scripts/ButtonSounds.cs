using UnityEngine;
using UnityEngine.EventSystems;
public class ButtonSounds : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public AudioClip click;
    public AudioClip hoverenter;
    public AudioClip hoverexit;
    
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
        AudioSource.PlayClipAtPoint(hoverenter, this.transform.position);
    }

    public void OnPointerExit(PointerEventData pointerEventData)
    {
        if(hoverexit == null) { return; }
        AudioSource.PlayClipAtPoint(hoverexit, this.transform.position);
    }
}