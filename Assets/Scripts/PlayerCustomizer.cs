using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

public class PlayerCustomizer : MonoBehaviourPunCallbacks
{
    [Header("Renderers to skin / color")]
    public Renderer[] customizedBones;

    private bool isInIntermission = false;
    private Material instanceMaterial; // Only used for intermission colors
    private Material roleMaterial; // Per-player copy used for mastery cosmetics
    private bool roleAppearanceActive;
    private RoundManager roundManager;
    private ProgressionManager progressionManager;

    // Stored player color (hex)
    private string localHex;
    private BasePlayer.PlayerType pendingPlayerType;
    private string pendingCharacterId;
    private bool hasPendingRole = false;
    private bool roleAppliedThisRound = false;
    private readonly System.Collections.Generic.List<GameObject> spawnedAccessories = new();

    // Active mastery cosmetic state. Tier 0 is the normal character look.
    private BasePlayer.PlayerType currentPlayerType = BasePlayer.PlayerType.None;
    private string currentCharacterId;
    private int activeMasteryTier;
    private MasteryCosmeticUnlock activeMasteryCosmetic;
    private Color activeRoleBaseColor = Color.white;

    private static readonly int MainColorId = Shader.PropertyToID("_MainColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

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

        progressionManager = ProgressionManager.EnsureExists();
        if (progressionManager != null)
            progressionManager.OnCharacterMasteryChanged += HandleLocalMasteryChanged;
    }

    private void OnDestroy()
    {
        if (roundManager != null)
        {
            roundManager.OnRoundStart -= RoundStarted;
            roundManager.OnIntermissionStart -= IntermissionStarted;
        }

        if (progressionManager != null)
            progressionManager.OnCharacterMasteryChanged -= HandleLocalMasteryChanged;

        if (instanceMaterial != null)
            Destroy(instanceMaterial);
        if (roleMaterial != null)
            Destroy(roleMaterial);
    }

    private void Update()
    {
        if (isInIntermission || roleMaterial == null || activeMasteryCosmetic == null)
            return;

        if (
            !ResolveMasteryAnimation(
                activeMasteryCosmetic,
                activeMasteryTier,
                out bool forceAnimate
            )
        )
            return;

        ApplyMasteryValues(
            roleMaterial,
            activeRoleBaseColor,
            activeMasteryCosmetic,
            Time.time,
            forceAnimate
        );
    }

    private void RoundStarted()
    {
        StopAllCoroutines();
        isInIntermission = false;
        roleAppliedThisRound = false;
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
            bool applied = ApplyRole(pendingPlayerType, pendingCharacterId);

            // Only destroy the old intermission material once the new role
            // material is actually on the bones; otherwise keep it visible.
            if (applied)
            {
                if (oldInstance != null)
                    Destroy(oldInstance);
            }
            else
            {
                instanceMaterial = oldInstance;
            }
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
            if (hasPendingRole || roleAppliedThisRound)
                break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hasPendingRole)
        {
            hasPendingRole = false;
            bool applied = ApplyRole(pendingPlayerType, pendingCharacterId);

            // Only destroy the old intermission material once the new role
            // material is actually on the bones; otherwise keep it visible.
            if (applied)
            {
                roleAppliedThisRound = true;
                if (oldInstance != null)
                    Destroy(oldInstance);
            }
            else
            {
                instanceMaterial = oldInstance;
            }
        }
        else if (roleAppliedThisRound)
        {
            // ApplyRoleAndCharacter arrived just after RoundStarted; the role
            // material is already on the bones, so the old one can go now.
            if (oldInstance != null)
                Destroy(oldInstance);
        }
        else
        {
            // Keep the intermission material referenced so the next intermission
            // can reuse or destroy it instead of leaking the instance.
            instanceMaterial = oldInstance;
            Debug.LogWarning(
                "[PlayerCustomizer] WaitForRoleRPC timed out -- role RPC never arrived."
            );
        }
    }

    private void IntermissionStarted()
    {
        isInIntermission = true;

        // bruh no shit it doesnt clear accessories from the last round in intermission
        ClearSpawnedAccessories();

        // Clear any pending role from the previous round so stale character data
        // does not bleed into the next round if the RPC arrived late or out of order.
        hasPendingRole = false;
        pendingCharacterId = null;
        pendingPlayerType = BasePlayer.PlayerType.None;
        currentCharacterId = null;
        currentPlayerType = BasePlayer.PlayerType.None;
        roleAppliedThisRound = false;

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

        // Colors are re-sent at every intermission and are included in the targeted
        // late-join state sync, so buffering them only leaked events into the room
        // cache for the lifetime of the room.
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(RPC_SyncIntermissionColor), RpcTarget.Others, hexToUse);
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
            hex = hex[1..];

        localHex = hex;

        // Only paint the color if we are currently in intermission on this client.
        // If a round is active, the role material is already applied — just store it.
        if (isInIntermission)
            ApplyIntermissionColor(hex);
    }

    [PunRPC]
    public void ApplyRoleAndCharacter(BasePlayer.PlayerType playerType, string characterId)
    {
        Debug.Log(
            $"[PlayerCustomizer] ApplyRoleAndCharacter — playerType={playerType}, characterId={characterId}, isInIntermission={isInIntermission}"
        );

        currentPlayerType = playerType;
        currentCharacterId = characterId;

        if (isInIntermission)
        {
            pendingPlayerType = playerType;
            pendingCharacterId = characterId;
            hasPendingRole = true;

            Debug.Log(
                $"[PlayerCustomizer] Intermission active — caching role, not applying material."
            );

            RefreshIntermissionAppearance();
            return;
        }

        ApplyRole(playerType, characterId);
    }

    private bool ApplyRole(BasePlayer.PlayerType playerType, string characterId)
    {
        Debug.Log(
            $"[PlayerCustomizer] ApplyRole — playerType={playerType}, characterId={characterId}"
        );

        currentPlayerType = playerType;
        currentCharacterId = characterId;
        ResolveActiveMasteryTier();

        // Capture the old role material before nulling so we can destroy it only
        // after the new role appearance is actually applied.
        Material oldRoleMaterial = roleMaterial;
        bool hadRoleAppearance = roleAppearanceActive;
        roleMaterial = null;
        activeMasteryCosmetic = null;

        ClearSpawnedAccessories();

        bool applied = playerType == BasePlayer.PlayerType.Killer
            ? ApplyKillerCustomisation(characterId)
            : ApplySurvivorCustomisation(characterId);

        if (applied)
        {
            roleAppliedThisRound = true;
            roleAppearanceActive = true;

            // Safe to destroy now — bones already have the new role material.
            if (oldRoleMaterial != null)
                Destroy(oldRoleMaterial);
        }
        else
        {
            // Character data could not be resolved; keep the previous appearance.
            roleMaterial = oldRoleMaterial;
            roleAppearanceActive = hadRoleAppearance;
        }

        return applied;
    }

    private bool ApplySurvivorCustomisation(string id)
    {
        SurvivorType survivorType = CharacterDatabase.GetSurvivorByName(id);
        if (survivorType == null)
        {
            Debug.LogWarning($"[PlayerCustomizer] SurvivorType '{id}' not found.");
            return false;
        }

        if (survivorType.character == null)
        {
            Debug.LogWarning($"[PlayerCustomizer] SurvivorType '{id}' has no character data.");
            return false;
        }

        Material mat = ResolveRoleMaterial(survivorType.character, id);
        bool applied = false;
        if (mat != null)
        {
            SetMaterialsOnBones(mat);
            applied = true;
        }

        if (survivorType.character.accessories != null)
        {
            foreach (Accessory accessory in survivorType.character.accessories)
                SpawnAccessory(accessory);
        }

        SpawnMasteryAccessory(survivorType.character);
        return applied;
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
            if (accessory != null)
                Destroy(accessory);
        }
        spawnedAccessories.Clear();

        // Recursively find and destroy any leftover GameObjects tagged "SpawnedAccessory" on child bones
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t != null && t.CompareTag("SpawnedAccessory"))
            {
                Destroy(t.gameObject);
            }
        }
    }

    private bool ApplyKillerCustomisation(string id)
    {
        KillerType killerType = CharacterDatabase.GetKillerByName(id);
        if (killerType == null)
        {
            Debug.LogWarning($"[PlayerCustomizer] KillerType '{id}' not found.");
            return false;
        }

        if (killerType.character == null)
        {
            Debug.LogWarning($"[PlayerCustomizer] KillerType '{id}' has no character data.");
            return false;
        }

        Material mat = ResolveRoleMaterial(killerType.character, id);
        bool applied = false;
        if (mat != null)
        {
            SetMaterialsOnBones(mat);
            applied = true;
        }

        if (killerType.character.accessories != null)
        {
            foreach (Accessory accessory in killerType.character.accessories)
                SpawnAccessory(accessory);
        }

        SpawnMasteryAccessory(killerType.character);

        Transform itemPoses = FindDeepChild(transform, "ItemPoses");
        if (itemPoses != null)
        {
            if (itemPoses.TryGetComponent<InputArmController>(out var armController))
                armController.enabled = true;
        }

        return applied;
    }

    private void SpawnAccessory(Accessory accessory)
    {
        if (accessory == null || accessory.prefab == null)
            return;

        Transform accTransform = FindDeepChild(transform, accessory.boneName);
        if (accTransform == null)
        {
            Debug.LogWarning(
                $"[PlayerCustomizer] Accessory bone '{accessory.boneName}' not found on {gameObject.name}."
            );
            return;
        }

        GameObject accInstance = Instantiate(accessory.prefab, accTransform);
        accInstance.transform.SetLocalPositionAndRotation(
            accessory.localPosition,
            accessory.localRotation
        );
        accInstance.tag = "SpawnedAccessory";

        // Inherit the layer from the parent bone so the accessory is visible
        // on the same camera culling mask as the rest of the character.
        SetLayerRecursive(accInstance, accTransform.gameObject.layer);
        spawnedAccessories.Add(accInstance);
    }

    private void SpawnMasteryAccessory(CubeCharacterData data)
    {
        if (activeMasteryTier <= 0)
            return;

        MasteryCosmeticUnlock unlock =
            activeMasteryCosmetic ?? MasteryCosmeticUnlock.GetFor(data, activeMasteryTier);

        if (unlock != null)
            SpawnAccessory(unlock.ExtraAccessory);
    }

    private Material ResolveRoleMaterial(CubeCharacterData data, string characterId)
    {
        activeMasteryCosmetic = activeMasteryTier > 0
            ? MasteryCosmeticUnlock.GetFor(data, activeMasteryTier)
            : null;

        if (data == null || data.bodyMaterial == null)
            return null;

        if (activeMasteryTier <= 0 || activeMasteryCosmetic == null)
            return data.bodyMaterial;

        Material copy = new Material(data.bodyMaterial)
        {
            name = $"Mastery_{characterId}_T{activeMasteryTier}",
        };

        CopyMasterySourceMaterial(copy, activeMasteryCosmetic);

        activeRoleBaseColor = GetPrimaryColor(copy);
        ResolveMasteryAnimation(
            activeMasteryCosmetic,
            activeMasteryTier,
            out bool forceAnimate
        );
        ApplyMasteryValues(
            copy,
            activeRoleBaseColor,
            activeMasteryCosmetic,
            Time.time,
            forceAnimate
        );
        roleMaterial = copy;
        return copy;
    }

    private void ResolveActiveMasteryTier()
    {
        if (photonView != null && photonView.IsMine)
        {
            ProgressionManager prog = progressionManager != null
                ? progressionManager
                : ProgressionManager.Instance;
            if (prog == null)
                prog = ProgressionManager.EnsureExists();

            activeMasteryTier = prog != null && !string.IsNullOrEmpty(currentCharacterId)
                ? prog.GetActiveMasteryCosmeticTier(currentCharacterId)
                : 0;
            PublishMasteryTier(activeMasteryTier);
        }
        else
        {
            activeMasteryTier = ReadMasteryTierFromOwner();
        }
    }

    private void PublishMasteryTier(int tier)
    {
        if (
            photonView == null
            || !photonView.IsMine
            || !PhotonNetwork.InRoom
            || PhotonNetwork.LocalPlayer == null
        )
            return;

        var props = new ExitGames.Client.Photon.Hashtable
        {
            {
                BasePlayer.PropMasteryCosmeticTier,
                Mathf.Clamp(tier, 0, ProgressionManager.MaxMasteryCosmeticTier)
            },
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private int ReadMasteryTierFromOwner()
    {
        Photon.Realtime.Player owner = photonView != null ? photonView.Owner : null;
        if (owner == null || owner.CustomProperties == null)
            return 0;

        if (
            !owner.CustomProperties.TryGetValue(
                BasePlayer.PropMasteryCosmeticTier,
                out object value
            )
            || value == null
        )
            return 0;

        switch (value)
        {
            case int intValue:
                return Mathf.Clamp(intValue, 0, ProgressionManager.MaxMasteryCosmeticTier);
            case byte byteValue:
                return Mathf.Clamp(byteValue, 0, ProgressionManager.MaxMasteryCosmeticTier);
            case short shortValue:
                return Mathf.Clamp(shortValue, 0, ProgressionManager.MaxMasteryCosmeticTier);
            default:
                return int.TryParse(value.ToString(), out int parsed)
                    ? Mathf.Clamp(parsed, 0, ProgressionManager.MaxMasteryCosmeticTier)
                    : 0;
        }
    }

    private void HandleLocalMasteryChanged(
        string characterName,
        int level,
        int currentXP,
        int requiredXP
    )
    {
        if (photonView == null || !photonView.IsMine || string.IsNullOrEmpty(currentCharacterId))
            return;

        if (!string.Equals(characterName, currentCharacterId, StringComparison.OrdinalIgnoreCase))
            return;

        // Players can choose any unlocked skin; honour that choice instead of always
        // jumping to the highest tier when they level up mid-round.
        int newTier = progressionManager != null
            ? progressionManager.GetActiveMasteryCosmeticTier(characterName)
            : ProgressionManager.GetMasteryCosmeticTier(level);
        if (newTier == activeMasteryTier)
            return;

        activeMasteryTier = newTier;
        PublishMasteryTier(newTier);

        if (!isInIntermission)
            ApplyRole(currentPlayerType, currentCharacterId);
    }

    public override void OnPlayerPropertiesUpdate(
        Photon.Realtime.Player targetPlayer,
        ExitGames.Client.Photon.Hashtable changedProps
    )
    {
        if (photonView == null || photonView.Owner == null || targetPlayer == null)
            return;

        if (targetPlayer.ActorNumber != photonView.Owner.ActorNumber)
            return;

        // The owner computes and publishes its own tier; ignore the echo.
        if (photonView.IsMine)
            return;

        if (changedProps == null || !changedProps.ContainsKey(BasePlayer.PropMasteryCosmeticTier))
            return;

        int newTier = ReadMasteryTierFromOwner();
        if (newTier == activeMasteryTier)
            return;

        activeMasteryTier = newTier;

        if (!isInIntermission && !string.IsNullOrEmpty(currentCharacterId))
            ApplyRole(currentPlayerType, currentCharacterId);
    }

    /// <summary>
    /// Decides whether the mastery cosmetic needs per-frame animation and whether
    /// tier 100 should force it. A dragged source material defines its own look, so
    /// it only animates when Animate Hue is explicitly enabled.
    /// </summary>
    private static bool ResolveMasteryAnimation(
        MasteryCosmeticUnlock cosmetic,
        int tier,
        out bool forceAnimate
    )
    {
        forceAnimate =
            cosmetic != null
            && cosmetic.SourceMaterial == null
            && tier >= ProgressionManager.MaxMasteryCosmeticTier;

        return forceAnimate || (cosmetic != null && cosmetic.AnimateHue);
    }

    /// <summary>
    /// Copies every property from the tier's dragged material (textures, colours,
    /// floats, keywords, render queue and GI flags) onto this player's material
    /// copy. The character shader is kept so the body keeps rendering correctly.
    /// </summary>
    private static void CopyMasterySourceMaterial(
        Material target,
        MasteryCosmeticUnlock cosmetic
    )
    {
        if (target == null || cosmetic == null || cosmetic.SourceMaterial == null)
            return;

        target.CopyPropertiesFromMaterial(cosmetic.SourceMaterial);
    }

    private static Color GetPrimaryColor(Material material)
    {
        if (material.HasProperty(MainColorId))
            return material.GetColor(MainColorId);
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(LegacyColorId))
            return material.GetColor(LegacyColorId);
        return Color.white;
    }

    private static void SetPrimaryColor(Material material, Color color)
    {
        if (material.HasProperty(MainColorId))
            material.SetColor(MainColorId, color);
        else if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        else if (material.HasProperty(LegacyColorId))
            material.SetColor(LegacyColorId, color);
    }

    private static void ApplyMasteryValues(
        Material material,
        Color baseColor,
        MasteryCosmeticUnlock cosmetic,
        float time,
        bool forceAnimate
    )
    {
        if (material == null || cosmetic == null)
            return;

        bool animate = forceAnimate || cosmetic.AnimateHue;

        if (cosmetic.SourceMaterial != null)
        {
            // The dragged material already supplies every shader property it
            // supports, so leave its values untouched unless hue animation is on.
            if (!animate)
                return;

            Color sourceColor = baseColor;
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(time * 0.11f, 1f), 0.78f, 1f);
            float sourceBlend = 0.35f + 0.15f * Mathf.Sin(time * 2.1f);
            Color animatedColor = Color.Lerp(sourceColor, rainbow, sourceBlend);
            animatedColor.a = sourceColor.a;
            SetPrimaryColor(material, animatedColor);
            return;
        }

        Color tint = cosmetic.Tint;

        if (animate)
        {
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(time * 0.11f, 1f), 0.78f, 1f);
            float blend = 0.55f + 0.25f * Mathf.Sin(time * 2.1f);
            tint = Color.Lerp(tint, rainbow, blend);
        }

        Color finalColor = Color.Lerp(baseColor, tint, Mathf.Clamp01(cosmetic.TintStrength));
        finalColor.a = baseColor.a;
        SetPrimaryColor(material, finalColor);

        if (material.HasProperty(EmissionColorId))
        {
            float pulse = animate ? 0.75f + 0.25f * Mathf.Sin(time * 3f) : 1f;
            Color emission =
                cosmetic.EmissionColor
                * (Mathf.Max(0f, cosmetic.EmissionIntensity) * pulse);
            material.SetColor(EmissionColorId, emission);

            if (emission.maxColorComponent > 0.001f)
                material.EnableKeyword("_EMISSION");
            else
                material.DisableKeyword("_EMISSION");
        }

        if (material.HasProperty(MetallicId))
            material.SetFloat(MetallicId, cosmetic.Metallic);
        if (material.HasProperty(SmoothnessId))
            material.SetFloat(SmoothnessId, cosmetic.Smoothness);
        if (material.HasProperty(GlossinessId))
            material.SetFloat(GlossinessId, cosmetic.Smoothness);
    }

    private void SetMaterialsOnBones(Material targetMat)
    {
        if (targetMat == null)
        {
            Debug.LogWarning(
                "[PlayerCustomizer] SetMaterialsOnBones: targetMat is null — skipping to avoid invisible bones."
            );
            return;
        }

        foreach (Renderer bone in customizedBones)
        {
            if (bone == null)
                continue;

            // Use material array copy (not sharedMaterials) so we don't mutate the shared asset.
            Material[] mats = new Material[bone.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = targetMat;

            bone.materials = mats;
        }
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return null;

        // Fast path for direct children
        Transform direct = parent.Find(childName);
        if (direct != null)
            return direct;

        foreach (Transform child in parent)
        {
            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    // ---------------------------
    //  INTERMISSION COLOR LOGIC
    // ---------------------------

    public void ChangeColorCalled(string hex)
    {
        if (!photonView.IsMine)
            return;
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex[1..];

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
            hex = hex[1..];

        localHex = hex;

        if (!isInIntermission)
        {
            Debug.LogWarning(
                $"[PlayerCustomizer] ChangeColor called during a round — ignoring. hex={hex}"
            );
            return;
        }

        ApplyIntermissionColor(hex);
    }

    /// <summary>
    /// Applies the intermission color material directly without the isInIntermission guard.
    /// Used when syncing late-joining clients who receive color data during a round.
    /// </summary>
    public void ForceApplyIntermissionColor(string hex, bool force = false)
    {
        if (!string.IsNullOrEmpty(hex) && hex[0] == '#')
            hex = hex[1..];

        localHex = hex;
        // Normally only apply visually during intermission. A late-join state sync
        // passes force:true because that client is intentionally placed in
        // intermission/spectator mode even while the master is mid-round.
        if (force || isInIntermission)
            ApplyIntermissionColor(hex);
    }

    private void ApplyIntermissionColor(string hex)
    {
        Color converted = PlayfabManager.FromHex(hex);

        // Destroy the per-player mastery material only after the intermission
        // material is on the bones, so there is no invisible frame.
        Material oldRoleMaterial = roleMaterial;
        roleMaterial = null;
        activeMasteryCosmetic = null;

        // If a role appearance is currently on the bones, the intermission material
        // must be rebound before the role copy is destroyed. This also covers tier 0,
        // where the bones use the shared character material rather than a copy.
        bool needsRebind = instanceMaterial == null || roleAppearanceActive;

        if (instanceMaterial == null)
        {
            Material baseMat = Resources.Load<Material>("PlayerMat");
            if (baseMat == null)
            {
                Debug.LogError("[PlayerCustomizer] Could not load PlayerMat from Resources.");
                roleMaterial = oldRoleMaterial;
                return;
            }

            instanceMaterial = new Material(baseMat)
            {
                name = "Intermission_Mat_" + photonView.ViewID,
            };
        }

        if (needsRebind)
            SetMaterialsOnBones(instanceMaterial);

        instanceMaterial.SetColor("_MainColor", converted);
        roleAppearanceActive = false;

        if (oldRoleMaterial != null)
            Destroy(oldRoleMaterial);
    }
}
