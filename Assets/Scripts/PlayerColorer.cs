using HSVPicker;
using UnityEngine;

public class PlayerColorer : MonoBehaviour
{
    public ColorPicker picker;

    void Start()
    {
        picker.onValueChanged.AddListener(color =>
        {
            ChangePlayerColor(color);
        });
        ChangePlayerColor(picker.CurrentColor);
    }

    public static void ChangePlayerColor(Color color)
    {
        Material mat = Resources.Load<Material>("PlayerMat");
        mat.SetColor("_MainColor", color);

        // Persist the player's chosen color so it can be read back on spawn
        // without relying on the shared material (which would reflect the last
        // client to write it — usually the Master Client).
        string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
        PlayerPrefs.SetString("PlayerColor", hex);
        PlayerPrefs.Save();
    }
}
