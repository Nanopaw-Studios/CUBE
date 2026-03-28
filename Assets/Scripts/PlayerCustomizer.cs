using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using System.Collections;

public class PlayerCustomizer : MonoBehaviourPunCallbacks
{
    [Header("Renderers to skin / color")]
    public Renderer[] customizedBones;

    private bool isInIntermission = false;
    private Material instanceMaterial; // Only used for intermission colors
    private RoundManager roundManager;

    // Stored player color (hex)
    private string localHex;
    private BasePlayer.PlayerType pendingPlayerType;
    private string pendingCharacterId;
    private bool hasPendingRole = false;

    private void Start()
    {
        roundManager = GameObject.Find("Multiplayer")?.GetComponent<RoundManager>();
        if (roundManager != null)
        {
            roundManager.OnRoundStart += RoundStarted;
            roundManager.OnIntermissionStart += IntermissionStarted;

            if (roundManager.intermissionActive)
            {
                IntermissionStarted();
            }
        }
        else
        {
            Debug.LogWarning("[PlayerCustomizer] RoundManager not found.");
        }
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= RoundStarted;
            roundManager.OnIntermissionStart -= IntermissionStarted;
        }
    }

    private void RoundStarted()
    {
        StopAllCoroutines();
        isInIntermission = false;

        if (instanceMaterial != null)
        {
            Destroy(instanceMaterial);
            instanceMaterial = null;
        }

        // Force-clear bones so no stale intermission material lingers
        foreach (Renderer bone in customizedBones)
        {
            if (bone == null) continue;
            var mats = bone.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = null;
            bone.sharedMaterials = mats;
        }

        Debug.Log($"[PlayerCustomizer] RoundStarted — hasPendingRole={hasPendingRole}, isInIntermission={isInIntermission}");

        if (hasPendingRole)
        {
            hasPendingRole = false;
            ApplyRole(pendingPlayerType, pendingCharacterId);
        }
    }

    private void IntermissionStarted()
    {
        isInIntermission = true;
        StartCoroutine(ApplyIntermissionNextFrame());
    }

    private IEnumerator ApplyIntermissionNextFrame()
    {
        yield return null;
        RefreshIntermissionAppearance();
    }

    private void RefreshIntermissionAppearance()
    {
        string hexToUse = string.IsNullOrEmpty(localHex) ? "FFFFFF" : localHex;
        ChangeColor(hexToUse);

        if (photonView.IsMine)
        {
            photonView.RPC(nameof(ChangeColor), RpcTarget.OthersBuffered, hexToUse);
        }
    }

    [PunRPC]
    public void ApplyRoleAndCharacter(BasePlayer.PlayerType playerType, string characterId)
    {
        Debug.Log($"[PlayerCustomizer] ApplyRoleAndCharacter — playerType={playerType}, characterId={characterId}, isInIntermission={isInIntermission}");

        if (isInIntermission)
        {
            pendingPlayerType = playerType;
            pendingCharacterId = characterId;
            hasPendingRole = true;

            Debug.Log($"[PlayerCustomizer] Intermission active — caching role, not applying material.");

            RefreshIntermissionAppearance();
            return;
        }

        ApplyRole(playerType, characterId);
    }

    private void ApplyRole(BasePlayer.PlayerType playerType, string characterId)
    {
        Debug.Log($"[PlayerCustomizer] ApplyRole — playerType={playerType}, characterId={characterId}");

        instanceMaterial = null;

        if (playerType == BasePlayer.PlayerType.Killer)
            ApplyKillerCustomisation(characterId);
        else
            ApplySurvivorCustomisation(characterId);
    }

    private void ApplySurvivorCustomisation(string id)
    {
        SurvivorType survivorType = CharacterDatabase.GetSurvivorByName(id);
        if (survivorType == null)
        {
            Debug.LogWarning($"[PlayerCustomizer] SurvivorType '{id}' not found.");
            return;
        }

        Material mat = survivorType.character.bodyMaterial;
        SetMaterialsOnBones(mat);

        foreach (Accessory accessory in survivorType.character.accessories)
        {
            Transform accTransform = transform.Find(accessory.boneName);
            if (accTransform != null)
            {
                GameObject accInstance = Instantiate(accessory.prefab, accTransform);
                accInstance.transform.localPosition = Vector3.zero;
                accInstance.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning($"[PlayerCustomizer] Accessory bone '{accessory.boneName}' not found.");
            }
        }
    }

    private void ApplyKillerCustomisation(string id)
    {
        KillerType killerType = CharacterDatabase.GetKillerByName(id);
        if (killerType == null)
        {
            Debug.LogWarning($"[PlayerCustomizer] KillerType '{id}' not found.");
            return;
        }

        Material mat = killerType.character.bodyMaterial;
        SetMaterialsOnBones(mat);

        Transform itemPoses = transform.Find("ItemPoses");
        if (itemPoses != null)
        {
            InputArmController armController = itemPoses.GetComponent<InputArmController>();
            if (armController != null) armController.enabled = true;
        }
    }

    private void SetMaterialsOnBones(Material targetMat)
    {
        foreach (Renderer bone in customizedBones)
        {
            if (bone == null) continue;
            Material[] mats = new Material[bone.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = targetMat;
            }
            bone.sharedMaterials = mats;
        }
    }

    // ---------------------------
    //  INTERMISSION COLOR LOGIC
    // ---------------------------

    public void ChangeColorCalled(string hex)
    {
        if (!photonView.IsMine) return;
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex.Substring(1);

        ChangeColor(hex);
        photonView.RPC(nameof(ChangeColor), RpcTarget.OthersBuffered, hex);
    }

    public string GetCurrentHex()
    {
        return localHex;
    }

    [PunRPC]
    public void ChangeColor(string hex)
    {
        // Strip # prefix if present — FromHex expects raw hex digits only
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex.Substring(1);

        localHex = hex;

        if (!isInIntermission)
        {
            Debug.LogWarning($"[PlayerCustomizer] ChangeColor called during a round — ignoring. hex={hex}");
            return;
        }

        Color converted = PlayfabManager.FromHex(hex);

        if (instanceMaterial == null)
        {
            Material baseMat = Resources.Load<Material>("PlayerMat");
            if (baseMat == null)
            {
                Debug.LogError("[PlayerCustomizer] Could not load PlayerMat from Resources.");
                return;
            }

            instanceMaterial = new Material(baseMat);
            instanceMaterial.name = "Intermission_Mat_" + photonView.ViewID;

            SetMaterialsOnBones(instanceMaterial);
        }

        instanceMaterial.SetColor("_MainColor", converted);
    }
}