using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
#if MAGICACLOTH2
using MagicaCloth2;
#endif

/// <summary>
/// Controls the activation and real-time parameters of developer-exclusive
/// secondary physics (MagicaCloth BoneSpring) across one or multiple objects on a character.
/// All target objects share identical size multipliers, vertical offsets, and physics settings.
/// Only players verified with the "Developer" badge via PlayFab/Photon receive this physics.
/// </summary>
public class DevPhysicsController : MonoBehaviourPunCallbacks
{
    public enum PlayerVisibilityMode
    {
        AllPlayers,     // Active on both local and remote players
        LocalOnly,      // Active ONLY for the local player (photonView.IsMine)
        RemoteOnly      // Active ONLY for remote players (!photonView.IsMine)
    }

    [System.Serializable]
    public class SecondaryPhysicsTarget
    {
        public GameObject gameObject;
        public PlayerVisibilityMode visibilityMode = PlayerVisibilityMode.AllPlayers;
        public Vector3 initialLocalPosition;
        public Vector3 initialLocalScale;
        public bool baseCaptured;
#if MAGICACLOTH2
        public MagicaCloth magicaCloth;
#endif
        public bool presetApplied;

        public SecondaryPhysicsTarget(GameObject go, PlayerVisibilityMode mode = PlayerVisibilityMode.AllPlayers)
        {
            gameObject = go;
            visibilityMode = mode;
            CaptureInitialTransform();
        }

        public void CaptureInitialTransform()
        {
            if (gameObject != null && !baseCaptured)
            {
                initialLocalPosition = gameObject.transform.localPosition;
                initialLocalScale = gameObject.transform.localScale;
                baseCaptured = true;
            }
        }
    }

    [Header("Target Physics Objects")]
    [Tooltip("Physics objects active on ALL players (both local and remote). If all lists are empty, auto-discovers objects under Torso.")]
    public List<GameObject> targetObjects = new List<GameObject>();

    [Tooltip("Physics objects that ONLY enable for the local player (first-person attachments, etc.).")]
    public List<GameObject> localOnlyObjects = new List<GameObject>();

    [Tooltip("Physics objects that ONLY enable for remote players (e.g. ears/accessories that would obstruct the local player's view).")]
    public List<GameObject> remoteOnlyObjects = new List<GameObject>();

    [Tooltip("Legacy single target field maintained for backwards compatibility.")]
    public GameObject secondaryPhysicsObject;

    [Tooltip("If true, all target objects match the primary target's base scale. If false, each object scales proportionally from its own initial scale.")]
    public bool matchPrimaryObjectScale = false;

    [Header("Runtime State")]
    [SerializeField] private bool hasDeveloperBadge = false;

    private readonly List<SecondaryPhysicsTarget> targets = new List<SecondaryPhysicsTarget>();
    private string cachedPresetJson;

    public bool HasDeveloperBadge => hasDeveloperBadge;
    public IReadOnlyList<SecondaryPhysicsTarget> Targets => targets;

    private void Awake()
    {
        RefreshTargets();
    }

    private void Start()
    {
        DevPhysicsSettings.OnSettingsChanged += OnSettingsChanged;
        PlayfabManager.OnDeveloperStatusChanged += OnDevStatusChanged;

        if (photonView == null || photonView.IsMine)
        {
            // Local player verification (allow editor & debug builds instant access)
            if (PlayfabManager.IsDeveloper || Application.isEditor || Debug.isDebugBuild)
            {
                SetDeveloperBadge(true);
            }
            else if (PlayfabManager.Instance != null)
            {
                PlayfabManager.Instance.CheckDeveloperStatus(isDev =>
                {
                    SetDeveloperBadge(isDev || Application.isEditor || Debug.isDebugBuild);
                });
            }
        }
        else
        {
            // Remote player verification via Photon Custom Properties
            CheckRemotePlayerBadge();
        }

        UpdatePhysicsState();
    }

    private void OnDestroy()
    {
        DevPhysicsSettings.OnSettingsChanged -= OnSettingsChanged;
        PlayfabManager.OnDeveloperStatusChanged -= OnDevStatusChanged;

#if MAGICACLOTH2
        foreach (var t in targets)
        {
            if (t.magicaCloth != null)
            {
                t.magicaCloth.OnBuildComplete -= OnClothBuildComplete;
            }
        }
#endif
    }

    /// <summary>
    /// Adds a GameObject to be tracked by the secondary physics controller at runtime with a specific visibility mode.
    /// </summary>
    public void AddTarget(GameObject go, PlayerVisibilityMode mode = PlayerVisibilityMode.AllPlayers)
    {
        if (go == null) return;

        if (mode == PlayerVisibilityMode.LocalOnly)
        {
            if (!localOnlyObjects.Contains(go)) localOnlyObjects.Add(go);
        }
        else if (mode == PlayerVisibilityMode.RemoteOnly)
        {
            if (!remoteOnlyObjects.Contains(go)) remoteOnlyObjects.Add(go);
        }
        else
        {
            if (!targetObjects.Contains(go)) targetObjects.Add(go);
        }

        RefreshTargets();
        UpdatePhysicsState();
    }

    /// <summary>
    /// Removes a GameObject from tracking across all lists.
    /// </summary>
    public void RemoveTarget(GameObject go)
    {
        if (go == null) return;
        bool removed = targetObjects.Remove(go) || localOnlyObjects.Remove(go) || remoteOnlyObjects.Remove(go);
        if (removed)
        {
            RefreshTargets();
            UpdatePhysicsState();
        }
    }

    /// <summary>
    /// Backwards-compatible alias for RefreshTargets.
    /// </summary>
    public void LocateSecondaryPhysics()
    {
        RefreshTargets();
    }

    /// <summary>
    /// Synchronizes target GameObjects, captures baseline transforms, and resolves MagicaCloth components.
    /// </summary>
    public void RefreshTargets()
    {
        // 1. Auto-discover if all lists are empty (and legacy single target is unassigned)
        if (targetObjects.Count == 0 && localOnlyObjects.Count == 0 && remoteOnlyObjects.Count == 0 && secondaryPhysicsObject == null)
        {
            AutoDiscoverSecondaryPhysics();
        }

        // 2. Include legacy single target if assigned and not in any list
        if (secondaryPhysicsObject != null
            && !targetObjects.Contains(secondaryPhysicsObject)
            && !localOnlyObjects.Contains(secondaryPhysicsObject)
            && !remoteOnlyObjects.Contains(secondaryPhysicsObject))
        {
            targetObjects.Insert(0, secondaryPhysicsObject);
        }

        // 3. Remove nulls
        targetObjects.RemoveAll(go => go == null);
        localOnlyObjects.RemoveAll(go => go == null);
        remoteOnlyObjects.RemoveAll(go => go == null);

        // 4. Gather all unique targets with their visibility mode (LocalOnly and RemoteOnly take precedence)
        var desiredTargets = new Dictionary<GameObject, PlayerVisibilityMode>();

        foreach (var go in localOnlyObjects)
        {
            if (go != null) desiredTargets[go] = PlayerVisibilityMode.LocalOnly;
        }

        foreach (var go in remoteOnlyObjects)
        {
            if (go != null && !desiredTargets.ContainsKey(go))
                desiredTargets[go] = PlayerVisibilityMode.RemoteOnly;
        }

        foreach (var go in targetObjects)
        {
            if (go != null && !desiredTargets.ContainsKey(go))
                desiredTargets[go] = PlayerVisibilityMode.AllPlayers;
        }

        // 5. Prune internal targets that are no longer in desiredTargets
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i].gameObject == null || !desiredTargets.ContainsKey(targets[i].gameObject))
            {
#if MAGICACLOTH2
                if (targets[i].magicaCloth != null)
                {
                    targets[i].magicaCloth.OnBuildComplete -= OnClothBuildComplete;
                }
#endif
                targets.RemoveAt(i);
            }
        }

        // 6. Add or update targets
        foreach (var kvp in desiredTargets)
        {
            GameObject go = kvp.Key;
            PlayerVisibilityMode mode = kvp.Value;

            var existing = targets.Find(t => t.gameObject == go);
            if (existing == null)
            {
                var newTarget = new SecondaryPhysicsTarget(go, mode);
#if MAGICACLOTH2
                newTarget.magicaCloth = go.GetComponent<MagicaCloth>() ?? go.GetComponentInChildren<MagicaCloth>();
                if (newTarget.magicaCloth != null)
                {
                    newTarget.magicaCloth.OnBuildComplete -= OnClothBuildComplete;
                    newTarget.magicaCloth.OnBuildComplete += OnClothBuildComplete;
                }
#endif
                targets.Add(newTarget);
            }
            else
            {
                existing.visibilityMode = mode;
                existing.CaptureInitialTransform();
#if MAGICACLOTH2
                if (existing.magicaCloth == null)
                {
                    existing.magicaCloth = go.GetComponent<MagicaCloth>() ?? go.GetComponentInChildren<MagicaCloth>();
                    if (existing.magicaCloth != null)
                    {
                        existing.magicaCloth.OnBuildComplete -= OnClothBuildComplete;
                        existing.magicaCloth.OnBuildComplete += OnClothBuildComplete;
                    }
                }
#endif
            }
        }

        // Keep legacy field pointing to primary object
        if (targets.Count > 0)
        {
            secondaryPhysicsObject = targets[0].gameObject;
        }

        // Deactivate all targets until developer authorization is established
        if (!hasDeveloperBadge)
        {
            foreach (var t in targets)
            {
                if (t.gameObject != null)
                    t.gameObject.SetActive(false);
            }
        }
    }

    private void AutoDiscoverSecondaryPhysics()
    {
        Transform torso = transform.Find("RIG/Torso");
        if (torso == null)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t.name == "Torso")
                {
                    torso = t;
                    break;
                }
            }
        }

        if (torso != null)
        {
            foreach (Transform child in torso)
            {
                bool isEar = child.name.IndexOf("ear", System.StringComparison.OrdinalIgnoreCase) >= 0;
#if MAGICACLOTH2
                if (child.GetComponent<MagicaCloth>() != null)
                {
                    if (isEar)
                    {
                        if (!remoteOnlyObjects.Contains(child.gameObject))
                            remoteOnlyObjects.Add(child.gameObject);
                    }
                    else
                    {
                        if (!targetObjects.Contains(child.gameObject))
                            targetObjects.Add(child.gameObject);
                    }
                    continue;
                }
#endif
                if (child.name.StartsWith("Collider (1)") || child.name.StartsWith("Collider (2)") || child.name.Contains("SecondaryPhysics"))
                {
                    if (!targetObjects.Contains(child.gameObject))
                        targetObjects.Add(child.gameObject);
                }
            }
        }

#if MAGICACLOTH2
        // If still empty, search entire character hierarchy for any MagicaCloth
        if (targetObjects.Count == 0 && remoteOnlyObjects.Count == 0 && localOnlyObjects.Count == 0)
        {
            var cloths = GetComponentsInChildren<MagicaCloth>(true);
            foreach (var cloth in cloths)
            {
                bool isEar = cloth.name.IndexOf("ear", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (isEar)
                {
                    if (!remoteOnlyObjects.Contains(cloth.gameObject))
                        remoteOnlyObjects.Add(cloth.gameObject);
                }
                else
                {
                    if (!targetObjects.Contains(cloth.gameObject))
                        targetObjects.Add(cloth.gameObject);
                }
            }
        }
#endif
    }

    /// <summary>
    /// Sets whether this player has verified developer status.
    /// </summary>
    public void SetDeveloperBadge(bool isDeveloper)
    {
        hasDeveloperBadge = isDeveloper;
        UpdatePhysicsState();
    }

    /// <summary>
    /// Called by PlayerNameSyncer when verification badge status is parsed.
    /// </summary>
    public void OnVerificationBadgeReceived(string status)
    {
        bool isDev = string.Equals(status, "Developer", System.StringComparison.OrdinalIgnoreCase);
        SetDeveloperBadge(isDev);
    }

    private void OnDevStatusChanged(bool isDev)
    {
        if (photonView == null || photonView.IsMine)
        {
            SetDeveloperBadge(isDev);
        }
    }

    private void OnSettingsChanged()
    {
        UpdatePhysicsState();
    }

    private void CheckRemotePlayerBadge()
    {
        if (photonView != null && photonView.Owner != null)
        {
            if (photonView.Owner.CustomProperties.TryGetValue("v", out var vObj))
            {
                string status = vObj as string ?? "";
                SetDeveloperBadge(string.Equals(status, "Developer", System.StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (photonView != null && photonView.Owner == targetPlayer)
        {
            CheckRemotePlayerBadge();
        }
    }

    /// <summary>
    /// Evaluates if secondary physics objects should be active and applies current parameters to all targets.
    /// Respects LocalOnly, RemoteOnly, and AllPlayers visibility rules.
    /// </summary>
    public void UpdatePhysicsState()
    {
        RefreshTargets();

        if (targets.Count == 0)
            return;

        bool isLocal = photonView == null || photonView.IsMine;
        bool hasAuth = hasDeveloperBadge && DevPhysicsSettings.IsDevPhysicsEnabled;
        bool anyActive = false;

        foreach (var t in targets)
        {
            if (t.gameObject == null) continue;

            bool matchesVisibility = t.visibilityMode switch
            {
                PlayerVisibilityMode.LocalOnly => isLocal,
                PlayerVisibilityMode.RemoteOnly => !isLocal,
                _ => true
            };

            bool shouldBeActive = hasAuth && matchesVisibility;

            if (t.gameObject.activeSelf != shouldBeActive)
            {
                t.gameObject.SetActive(shouldBeActive);
            }

            if (shouldBeActive)
            {
                anyActive = true;
            }
        }

        if (anyActive)
        {
            ApplyPhysicsParameters();
        }
    }

    /// <summary>
    /// Pushes the tuned DevPhysicsSettings values and preset into all target objects and MagicaCloth instances.
    /// </summary>
    public void ApplyPhysicsParameters()
    {
        if (targets.Count == 0)
            return;

        float scaleMult = DevPhysicsSettings.SizeMultiplier;
        float yOffset = DevPhysicsSettings.VerticalOffset;

        // Base scale from the primary object if uniform size is requested
        Vector3 baseScale = targets[0].initialLocalScale;

        foreach (var t in targets)
        {
            if (t.gameObject == null || !t.gameObject.activeInHierarchy) continue;

            // 1. Synchronized Transform scaling and vertical placement
            Vector3 targetBaseScale = matchPrimaryObjectScale ? baseScale : t.initialLocalScale;
            t.gameObject.transform.localScale = targetBaseScale * scaleMult;

            t.gameObject.transform.localPosition = new Vector3(
                t.initialLocalPosition.x,
                t.initialLocalPosition.y + yOffset,
                t.initialLocalPosition.z
            );

#if MAGICACLOTH2
            if (t.magicaCloth == null)
            {
                t.magicaCloth = t.gameObject.GetComponent<MagicaCloth>() ?? t.gameObject.GetComponentInChildren<MagicaCloth>();
                if (t.magicaCloth != null)
                {
                    t.magicaCloth.OnBuildComplete -= OnClothBuildComplete;
                    t.magicaCloth.OnBuildComplete += OnClothBuildComplete;
                }
            }

            if (t.magicaCloth == null || t.magicaCloth.SerializeData == null)
                continue;

            // 2. Default preset import (applied once per cloth target)
            if (!t.presetApplied)
            {
                if (cachedPresetJson == null)
                    cachedPresetJson = LoadPresetJson();

                if (!string.IsNullOrEmpty(cachedPresetJson))
                {
                    t.magicaCloth.SerializeData.ImportJson(cachedPresetJson);
                    t.presetApplied = true;
                }
            }

            // 3. Apply unified physics parameters
            var sdata = t.magicaCloth.SerializeData;
            sdata.springConstraint.springPower = DevPhysicsSettings.SpringPower;
            sdata.springConstraint.limitDistance = DevPhysicsSettings.LimitDistance;
            sdata.damping.value = DevPhysicsSettings.Damping;
            sdata.inertiaConstraint.worldInertia = DevPhysicsSettings.Inertia;
            sdata.inertiaConstraint.localInertia = DevPhysicsSettings.Inertia;

            sdata.inertiaConstraint.movementSpeedLimit.value = 1.0f;
            sdata.inertiaConstraint.movementSpeedLimit.use = true;

            if (t.magicaCloth.IsValid())
            {
                t.magicaCloth.SetParameterChange();
            }
#endif
        }
    }

#if MAGICACLOTH2
    private void OnClothBuildComplete(bool success)
    {
        if (success)
        {
            ApplyPhysicsParameters();
        }
    }

    private string LoadPresetJson()
    {
        // 1. Try Assets/Settings/magicaPreset.json (disk)
        string filePath = System.IO.Path.Combine(Application.dataPath, "Settings", "magicaPreset.json");
        if (System.IO.File.Exists(filePath))
        {
            try { return System.IO.File.ReadAllText(filePath); }
            catch (System.Exception ex) { Debug.LogWarning($"[DevPhysicsController] Disk load failed: {ex.Message}"); }
        }

        // 2. Try Resources/magicaPreset
        TextAsset textAsset = Resources.Load<TextAsset>("magicaPreset");
        if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
        {
            return textAsset.text;
        }

        return null;
    }
#endif
}
