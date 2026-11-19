using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Owner-driven LagSpike trap. The caster's client scans the trap radius and
/// applies the LagSpike slow to the killer; every client receives one RPC to
/// draw the temporary ground circle. No damage is dealt, so survivors standing
/// in the field are unaffected.
/// </summary>
public class LagSpikeTrapRunner : MonoBehaviourPun
{
    private const float ScanInterval = 0.2f;
    private const int VisualSegments = 48;

    private Vector3 center;
    private float radius;
    private int tier;
    private float slowDuration;
    private float activeUntil;
    private float nextScanTime;
    private bool active;

    private readonly HashSet<int> affectedPlayers = new HashSet<int>();
    private static readonly Collider[] ScanBuffer = new Collider[64];

    public void Begin(Vector3 trapCenter, float trapRadius, int trapTier, float slowSeconds, float lifetime)
    {
        center = trapCenter;
        radius = Mathf.Max(0.5f, trapRadius);
        tier = Mathf.Clamp(trapTier, 1, 3);
        slowDuration = Mathf.Max(0.25f, slowSeconds);
        activeUntil = Time.time + Mathf.Max(1f, lifetime);
        nextScanTime = 0f;
        affectedPlayers.Clear();
        active = true;

        if (photonView.IsMine)
            photonView.RPC(nameof(RPC_SpawnTrapVisual), RpcTarget.All, center, radius, lifetime);
    }

    private void Update()
    {
        if (!active)
            return;

        if (Time.time >= activeUntil)
        {
            active = false;
            return;
        }

        if (!photonView.IsMine || Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + ScanInterval;

        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            ScanBuffer,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            BasePlayer target = ScanBuffer[i].GetComponentInParent<BasePlayer>();
            if (target == null || target.GetPlayerType() != BasePlayer.PlayerType.Killer)
                continue;

            PhotonView targetView = target.GetComponent<PhotonView>();
            if (targetView == null || !affectedPlayers.Add(targetView.ViewID))
                continue;

            targetView.RPC(
                nameof(StatusEffectManager.RPC_ApplyStatusEffect),
                RpcTarget.All,
                (byte)StatusEffectType.LagSpike,
                (byte)tier,
                slowDuration
            );

            CubeCoinRewardManager.NotifyLocalSuccessfulAbility("lag_spike_hit_killer", 45);
            Debug.Log($"[LagSpike] Slowed killer {targetView.Owner?.NickName} for {slowDuration:F1}s.");

        }
    }

    [PunRPC]
    public void RPC_SpawnTrapVisual(Vector3 trapCenter, float trapRadius, float lifetime)
    {
        GameObject visual = new GameObject("LagSpikeTrapVisual");
        visual.transform.position = trapCenter + Vector3.up * 0.05f;

        LineRenderer line = visual.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = VisualSegments;
        line.widthMultiplier = 0.08f;

        Material lineMaterial = null;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            lineMaterial = new Material(shader);
            line.sharedMaterial = lineMaterial;
        }

        Color color = new Color(1f, 0.09f, 0.27f, 0.65f);
        line.startColor = color;
        line.endColor = color;

        for (int i = 0; i < VisualSegments; i++)
        {
            float angle = (i / (float)VisualSegments) * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * trapRadius, 0f, Mathf.Sin(angle) * trapRadius));
        }

        // The visual is cosmetic and local to each client; destroy it locally after
        // the trap expires. The owner's logic timer uses the same lifetime.
        float destroyAfter = Mathf.Max(1f, lifetime);
        Object.Destroy(visual, destroyAfter);
        if (lineMaterial != null)
            Object.Destroy(lineMaterial, destroyAfter);
    }
}
