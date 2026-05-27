using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerText : MonoBehaviourPunCallbacks
{
    // in both cases, it should return the same
    // just in case
    public enum PlayerTextSource
    {
        Photon,
        Playfab,
    }

    public PlayerTextSource source;
    public TMP_Text text;
    public bool GetTextComponent;

    public override void OnJoinedRoom()
    {
        if (GetTextComponent)
        {
            text = GetComponent<TMP_Text>();
        }

        if (source == PlayerTextSource.Photon)
        {
            text.text = PhotonNetwork.NickName;
        }
        if (source == PlayerTextSource.Playfab)
        {
            text.text = PlayfabManager.GetPlayerName();
        }
    }
}
