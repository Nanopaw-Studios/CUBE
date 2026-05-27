using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class PlayerSizer : MonoBehaviourPun
{
    public GameObject rigObj;

    [Tooltip(
        "Optional: assign in inspector. If left empty the script will try to find the RoundManager at runtime."
    )]
    public RoundManager roundManager;
    private BasePlayer player;

    private bool subscribed = false;

    private void OnEnable()
    {
        var go = GameObject.Find("Multiplayer");
        roundManager =
            go != null ? go.GetComponent<RoundManager>() : FindFirstObjectByType<RoundManager>();

        if (roundManager != null && !subscribed)
        {
            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;
            subscribed = true;
        }

        var camRefs = Camera.main?.GetComponent<PlayerCameraReferences>();
        player = camRefs.GetPlayer().GetComponent<BasePlayer>();
    }

    private void OnIntermissionStart()
    {
        rigObj.transform.localScale = Vector3.one;
    }

    private void OnRoundStart()
    {
        rigObj.transform.localScale = new Vector3(
            player.equippedKiller.Size,
            player.equippedKiller.Size,
            player.equippedKiller.Size
        );
    }

    private void OnDisable()
    {
        if (roundManager != null && subscribed)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
            subscribed = false;
        }
    }

    private void OnDestroy()
    {
        if (roundManager != null && subscribed)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
            subscribed = false;
        }
    }
}
