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
    private readonly System.Collections.Generic.List<GameObject> spawnedAccessories = new System.Collections.Generic.List<GameObject>();

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
        ClearSpawnedAccessories();

        // Destroy the intermission color material instance AFTER applying the role material
        // so there is no frame where bones have a null/destroyed material (which causes them
        // to become invisible -- including the arm/hand bones the local player sees as their "hands").
        Material oldInstance = instanceMaterial;
        instanceMaterial = null;

        Debug.Log($"[PlayerCustomizer] RoundStarted -- hasPendingRole={hasPendingRole}");

        if (hasPendingRole)
        {
            hasPendingRole = false;
            ApplyRole(pendingPlayerType, pendingCharacterId);

            // Safe to destroy old instance now that new materials are applied.
            if (oldInstance != null)
                Destroy(oldInstance);
        }
        else
        {
            // ApplyRoleAndCharacter hasn't arrived yet -- wait for it.
            // Destroy the old instance after a short delay so the bones don't
            // go invisible while we wait for the role RPC.
            StartCoroutine(WaitForRoleRPC(oldInstance));
        }
    }

    private IEnumerator WaitForRoleRPC(Material oldInstance)
    {
        // Wait up to 5 seconds for ApplyRoleAndCharacter to arrive.
        const float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (hasPendingRole)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hasPendingRole)
        {
            hasPendingRole = false;
            ApplyRole(pendingPlayerType, pendingCharacterId);
        }
        else
        {
            Debug.LogWarning("[PlayerCustomizer] WaitForRoleRPC timed out -- role RPC never arrived.");
        }

        // Safe to destroy old instance now.
        if (oldInstance != null)
            Destroy(oldInstance);
    }

    private void IntermissionStarted()
    {
        isInIntermission = true;

        // Clear any pending role from the previous round so stale character data
        // does not bleed into the next round if the RPC arrived late or out of order.
        hasPendingRole = false;
        pendingCharacterId = null;

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

        // Apply locally — isInIntermission is already true here
        ApplyIntermissionColor(hexToUse);

        // Tell others: use OthersBuffered so late joiners pick it up.
        // Remotes call ApplyRoleColor directly so the isInIntermission guard doesn't block them.
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_SyncIntermissionColor), RpcTarget.OthersBuffered, hexToUse);
        }
    }

    /// <summary>
    /// Received by remote clients to apply the intermission color unconditionally,
    /// bypassing the isInIntermission guard which is unreliable across clients.
    /// </summary>
    [PunRPC]
    public void RPC_SyncIntermissionColor(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex.Substring(1);

        localHex = hex;

        // Only paint the color if we are currently in intermission on this client.
        // If a round is active, the role material is already applied — just store it.
        if (isInIntermission)
            ApplyIntermissionColor(hex);
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

        // Capture the old intermission instance before nulling so we can destroy it
        // AFTER the new role materials are applied, preventing a frame where bones
        // have a null/destroyed material (which makes hands invisible).
        Material oldInstance = instanceMaterial;
        instanceMaterial = null;

        ClearSpawnedAccessories();

        if (playerType == BasePlayer.PlayerType.Killer)
            ApplyKillerCustomisation(characterId);
        else
            ApplySurvivorCustomisation(characterId);

        // Safe to destroy now — bones already have the new role material.
        if (oldInstance != null)
            Destroy(oldInstance);
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

        const string AccessoryTag = "SpawnedAccessory";
        foreach (Accessory accessory in survivorType.character.accessories)
        {
            if (accessory == null || accessory.prefab == null) continue;

            Transform accTransform = FindDeepChild(transform, accessory.boneName);
            if (accTransform != null)
            {
                GameObject accInstance = Instantiate(accessory.prefab, accTransform);
                accInstance.transform.localPosition = accessory.localPosition;
                accInstance.transform.localRotation = accessory.localRotation;
                accInstance.tag = AccessoryTag;

                // Inherit the layer from the parent bone so the accessory is visible
                // on the same camera culling mask as the rest of the character.
                SetLayerRecursive(accInstance, accTransform.gameObject.layer);

                spawnedAccessories.Add(accInstance);
            }
            else
            {
                Debug.LogWarning($"[PlayerCustomizer] Accessory bone '{accessory.boneName}' not found on {gameObject.name}.");
            }
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void ClearSpawnedAccessories()
    {
        for (int i = spawnedAccessories.Count - 1; i >= 0; i--)
        {
            GameObject accessory = spawnedAccessories[i];
            if (accessory != null) Destroy(accessory);
        }
        spawnedAccessories.Clear();
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

        Transform itemPoses = FindDeepChild(transform, "ItemPoses");
        if (itemPoses != null)
        {
            InputArmController armController = itemPoses.GetComponent<InputArmController>();
            if (armController != null) armController.enabled = true;
        }
    }

    private void SetMaterialsOnBones(Material targetMat)
    {
        if (targetMat == null)
        {
            Debug.LogWarning("[PlayerCustomizer] SetMaterialsOnBones: targetMat is null — skipping to avoid invisible bones.");
            return;
        }

        foreach (Renderer bone in customizedBones)
        {
            if (bone == null) continue;

            // Use material array copy (not sharedMaterials) so we don't mutate the shared asset.
            Material[] mats = new Material[bone.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = targetMat;

            bone.materials = mats;
        }
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName)) return null;

        // Fast path for direct children
        Transform direct = parent.Find(childName);
        if (direct != null) return direct;

        foreach (Transform child in parent)
        {
            Transform found = FindDeepChild(child, childName);
            if (found != null) return found;
        }

        return null;
    }

    // ---------------------------
    //  INTERMISSION COLOR LOGIC
    // ---------------------------

    public void ChangeColorCalled(string hex)
    {
        if (!photonView.IsMine) return;
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex.Substring(1);

        localHex = hex;
        ApplyIntermissionColor(hex);
        photonView.RPC(nameof(RPC_SyncIntermissionColor), RpcTarget.OthersBuffered, hex);
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

        ApplyIntermissionColor(hex);
    }

    /// <summary>
    /// Applies the intermission color material directly without the isInIntermission guard.
    /// Used when syncing late-joining clients who receive color data during a round.
    /// </summary>
    public void ForceApplyIntermissionColor(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex.Substring(1);

        localHex = hex;
        // Only apply visually if we are actually in intermission right now
        if (isInIntermission)
            ApplyIntermissionColor(hex);
    }

    private void ApplyIntermissionColor(string hex)
    {
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
