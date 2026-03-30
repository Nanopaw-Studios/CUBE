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

        // Destroy the intermission color material instance AFTER applying the role material
        // so there is no frame where bones have a null/destroyed material (which causes them
        // to become invisible — including the arm/hand bones the local player sees as their "hands").
        Material oldInstance = instanceMaterial;
        instanceMaterial = null;

        Debug.Log($"[PlayerCustomizer] RoundStarted — hasPendingRole={hasPendingRole}");

        if (hasPendingRole)
        {
            hasPendingRole = false;
            ApplyRole(pendingPlayerType, pendingCharacterId);
        }

        // Safe to destroy old instance now that new materials are applied (or no role yet).
        if (oldInstance != null)
            Destroy(oldInstance);
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

        // Destroy any accessories from a previous round before applying new ones.
        DestroySpawnedAccessories();

        if (playerType == BasePlayer.PlayerType.Killer)
            ApplyKillerCustomisation(characterId);
        else
            ApplySurvivorCustomisation(characterId);

        // Discard the intermission instance AFTER role materials are already applied so bones
        // are never left with a null/destroyed material (which makes the local player's hands
        // invisible on their own screen).
        if (instanceMaterial != null)
        {
            Destroy(instanceMaterial);
            instanceMaterial = null;
        }
    }

    /// <summary>
    /// Removes all previously spawned accessory GameObjects tagged as such so
    /// repeated role assignments don't stack duplicates on the same bones.
    /// </summary>
    private void DestroySpawnedAccessories()
    {
        const string AccessoryTag = "SpawnedAccessory";
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.CompareTag(AccessoryTag))
                Destroy(child.gameObject);
        }
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
            // Deep search — bones can be many levels below the player root.
            Transform accTransform = FindDeepChild(transform, accessory.boneName);
            if (accTransform != null)
            {
                GameObject accInstance = Instantiate(accessory.prefab, accTransform);
                accInstance.transform.localPosition = accessory.localPosition;
                accInstance.transform.localRotation = accessory.localRotation;

                // Tag the instance so DestroySpawnedAccessories can find and clean it up.
                accInstance.tag = "SpawnedAccessory";
            }
            else
            {
                Debug.LogWarning($"[PlayerCustomizer] Accessory bone '{accessory.boneName}' not found on {gameObject.name}.");
            }
        }
    }

    /// <summary>
    /// Recursively searches the hierarchy rooted at <paramref name="root"/> for a
    /// child whose name exactly matches <paramref name="boneName"/>.
    /// Unlike <c>Transform.Find</c>, this searches all descendant levels.
    /// </summary>
    private static Transform FindDeepChild(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, boneName);
            if (found != null) return found;
        }
        return null;
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