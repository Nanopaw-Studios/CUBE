using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameSyncer : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    public TMP_Text nameText;
    public TMP_Text titleText;
    public Image verificationBadge;

    [Header("Verification Badges")]
    public Sprite verifiedBadge;
    public Sprite devBadge;

    [Header("Nametag Root")]
    [Tooltip("The root GameObject of the world-space nametag canvas. Hidden during rounds.")]
    public GameObject nametagRoot;

    private const string PROP_NAME = "n";
    private const string PROP_TITLE = "t";
    private const string PROP_VERIFICATION = "v";

    private RoundManager roundManager;

    private void Start()
    {
        roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;

            // Sync initial state: only show during intermission, never during an active round.
            bool showNow = roundManager.intermissionActive && !roundManager.roundActive;
            SetNametagVisible(showNow);
        }
        else
        {
            // No RoundManager — hide by default (safe fallback).
            SetNametagVisible(false);
        }

        if (!photonView.IsMine)
            return;

        string playerName = PlayfabManager.GetPlayerName();

        string titleId = "";
        string verification = "";

        bool titleLoaded = false;
        bool verificationLoaded = false;

        void TryApply()
        {
            if (!titleLoaded || !verificationLoaded)
                return;

            var props = new Hashtable
            {
                { PROP_NAME, playerName },
                { PROP_TITLE, titleId },
                { PROP_VERIFICATION, verification },
            };

            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        PlayfabManager.Instance.GetUserDataValue(
            "PlayerTitle",
            titleId1 =>
            {
                titleId = string.IsNullOrEmpty(titleId1) ? "" : titleId1;
                titleLoaded = true;

                // Apply locally immediately (optional but nice)
                TitleStyleRegistry.Apply(titleText, titleId);

                TryApply();
            }
        );

        PlayfabManager.Instance.GetUserDataValue(
            "VerificationStatus",
            verification1 =>
            {
                verification = string.IsNullOrEmpty(verification1) ? "" : verification1;
                verificationLoaded = true;

                TryApply();
            }
        );
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
        }
    }

    private void OnRoundStart()
    {
        SetNametagVisible(false);
    }

    private void OnIntermissionStart()
    {
        SetNametagVisible(true);
    }

    /// <summary>
    /// Shows or hides the nametag root during round/intermission transitions.
    /// </summary>
    private void SetNametagVisible(bool visible)
    {
        if (nametagRoot != null)
            nametagRoot.SetActive(visible);
    }

    public override void OnPlayerPropertiesUpdate(
        Photon.Realtime.Player targetPlayer,
        Hashtable changedProps
    )
    {
        // Only update if this PhotonView belongs to that player
        if (photonView.Owner != targetPlayer)
            return;

        ApplyFromPlayer(targetPlayer);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        // In case properties already exist when this object enables
        if (photonView != null && photonView.Owner != null)
            ApplyFromPlayer(photonView.Owner);
    }

    private void ApplyFromPlayer(Photon.Realtime.Player p)
    {
        if (nameText != null && p.CustomProperties.TryGetValue(PROP_NAME, out var nObj))
            nameText.text = nObj as string ?? "";

        if (titleText != null && p.CustomProperties.TryGetValue(PROP_TITLE, out var tObj))
        {
            string titleId = tObj as string ?? "";
            TitleStyleRegistry.Apply(titleText, titleId);
        }

        if (
            verificationBadge != null
            && p.CustomProperties.TryGetValue(PROP_VERIFICATION, out var vObj)
        )
        {
            string verification = vObj as string ?? "";
            switch (verification)
            {
                case "Verified":
                    verificationBadge.sprite = verifiedBadge;
                    verificationBadge.gameObject.SetActive(true);
                    Debug.Log("Applied verified badge");
                    break;
                case "Developer":
                    verificationBadge.sprite = devBadge;
                    verificationBadge.gameObject.SetActive(true);
                    Debug.Log("Applied developer badge");
                    break;
                default:
                    verificationBadge.gameObject.SetActive(false);
                    break;
            }
        }
    }

    public static class TitleStyleRegistry
    {
        // Assign these in a ScriptableObject if you prefer.
        public static TMP_ColorGradient SupporterGradient;
        public static TMP_ColorGradient DevGradient;

        public static void Apply(TMP_Text txt, string titleId)
        {
            // Default
            if (string.IsNullOrWhiteSpace(titleId))
            {
                txt.gameObject.SetActive(false);
                return;
            }

            txt.gameObject.SetActive(true);

            switch (titleId)
            {
                case "Developer":
                    txt.text = "Developer";
                    if (DevGradient != null)
                        txt.colorGradientPreset = DevGradient;
                    txt.enableVertexGradient = true;
                    Debug.Log("Applied developer title style");
                    break;

                case "Supporter":
                    txt.text = "Supporter";
                    if (SupporterGradient != null)
                        txt.colorGradientPreset = SupporterGradient;
                    txt.enableVertexGradient = true;
                    Debug.Log("Applied supporter title style");
                    break;

                default:
                    txt.text = titleId; // fallback
                    txt.enableVertexGradient = false;
                    break;
            }
        }
    }
}
