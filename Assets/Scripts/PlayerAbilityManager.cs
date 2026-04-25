using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerAbilityManager : MonoBehaviour
{
    public GameObject AbilityUITemplate;
    public Transform abilitiesParent; // Canvas/SurvivorStats/Abilites
    [Tooltip("Optional: assign in inspector. If left empty the script will try to find the RoundManager at runtime.")]
    public RoundManager roundManager;
    [Header("Input")]
    public InputActionAsset inputActions;


    private BasePlayer player;

    private SurvivorType survivor;
    private KillerType killer;

    // All abilities (passive + active)
    private Ability[] currentMoveset;

    // Active-only collections (indices MUST match)
    private readonly List<Ability> activeMoveset = new List<Ability>();
    private readonly List<InputAction> abilityInputs = new List<InputAction>();
    private readonly List<float> cooldownTimers = new List<float>();
    private readonly List<TMP_Text> cooldownTexts = new List<TMP_Text>();
    private readonly List<AbiltySlider> abilitySliders = new List<AbiltySlider>();

    // Per-action callback refs so we can unsubscribe reliably
    private readonly List<System.Action<InputAction.CallbackContext>> performedHandlers =
        new List<System.Action<InputAction.CallbackContext>>();

    private bool subscribed = false;
    private bool abilitiesEnabled = true;

    private Coroutine passiveLoop;

    private void Awake()
    {
        TryResolveLocalPlayer();
    }

    private void OnEnable()
    {
        var go = GameObject.Find("Multiplayer");
        roundManager = go != null ? go.GetComponent<RoundManager>() : FindFirstObjectByType<RoundManager>();

        if (roundManager != null && !subscribed)
        {
            roundManager.OnRoundStart += OnRoundStart;
            roundManager.OnIntermissionStart += OnIntermissionStart;
            subscribed = true;
        }
        else if (roundManager == null)
        {
            Debug.LogError("PlayerAbilityManager: RoundManager not found in OnEnable!");
        }

        if (roundManager != null && roundManager.roundActive)
        {
            abilitiesEnabled = true;
            OnRoundStart();
        }
        else
        {
            abilitiesEnabled = false;
            SetAllCooldownTexts("Disabled");
        }
    }

    private void OnDisable()
    {
        if (passiveLoop != null) StopCoroutine(passiveLoop);
        passiveLoop = null;

        UnsubscribeAllAbilityCallbacks();

        if (roundManager != null && subscribed)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
            subscribed = false;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeAllAbilityCallbacks();

        if (roundManager != null && subscribed)
        {
            roundManager.OnRoundStart -= OnRoundStart;
            roundManager.OnIntermissionStart -= OnIntermissionStart;
            subscribed = false;
        }
    }

    private void OnIntermissionStart()
    {
        abilitiesEnabled = false;
        SetAllCooldownTexts("Disabled");

        if (passiveLoop != null) StopCoroutine(passiveLoop);
        passiveLoop = null;
    }

    private void OnRoundStart()
    {
        abilitiesEnabled = true;

        if (!TryResolveLocalPlayer())
        {
            Debug.LogError("PlayerAbilityManager: Local BasePlayer not found on round start.");
            return;
        }

        if (passiveLoop != null) StopCoroutine(passiveLoop);
        passiveLoop = StartCoroutine(PassiveTickLoop());

        StartCoroutine(BuildAbilitiesWithDelay());
    }

    private IEnumerator BuildAbilitiesWithDelay()
    {
        // Wait for SetPlayerType and ApplyRoleAndCharacter RPCs to arrive
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (TryResolveLocalPlayer())
            {
                var type = player.GetPlayerType();
                bool hasCharacter = (type == BasePlayer.PlayerType.Survivor && player.GetEquippedSurvivor() != null)
                                 || (type == BasePlayer.PlayerType.Killer  && player.GetEquippedKiller()   != null);
                if (hasCharacter)
                    break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        BuildAbilitiesUI();

        // Second build after a short delay to catch any late material/skin sync
        yield return new WaitForSeconds(1.5f);
        BuildAbilitiesUI();
    }

    private void BuildAbilitiesUI()
    {
        if (!TryResolveLocalPlayer())
        {
            Debug.LogWarning("[BuildAbilitiesUI] No local player yet.");
            return;
        }

        survivor = null;
        killer = null;
        currentMoveset = null;

        var type = player.GetPlayerType();
        if (type == BasePlayer.PlayerType.Survivor)
        {
            survivor = player.GetEquippedSurvivor();
            Debug.Log($"Equipped Survivor: {(survivor != null ? survivor.name : "NULL")}");
            currentMoveset = survivor != null ? survivor.abilities : null;
        }
        else if (type == BasePlayer.PlayerType.Killer)
        {
            killer = player.GetEquippedKiller();
            Debug.Log($"Equipped Killer: {(killer != null ? killer.name : "NULL")}");
            currentMoveset = killer != null ? killer.abilities : null;
        }
        else
        {
            Debug.LogWarning($"Unknown player type {type}");
            return;
        }

        if (currentMoveset == null || currentMoveset.Length == 0)
        {
            Debug.LogWarning($"No abilities found for {type}");
            return;
        }

        if (abilitiesParent == null)
        {
            Debug.LogError("Abilities parent not found under Canvas/SurvivorStats/Abilites");
            return;
        }

        // Rebuild happens twice -> unsubscribe BEFORE clearing lists
        UnsubscribeAllAbilityCallbacks();

        activeMoveset.Clear();
        abilityInputs.Clear();
        cooldownTimers.Clear();
        cooldownTexts.Clear();
        abilitySliders.Clear();

        foreach (Transform child in abilitiesParent)
            Destroy(child.gameObject);

        // Build active-only list first (no passives)
        for (int i = 0; i < currentMoveset.Length; i++)
        {
            var ability = currentMoveset[i];
            if (ability == null) continue;
            if (ability.isPassive) continue;
            activeMoveset.Add(ability);
        }

        // Create UI + data for actives
        for (int i = 0; i < activeMoveset.Count; i++)
        {
            var ability = activeMoveset[i];

            GameObject abilityGO = Instantiate(AbilityUITemplate, abilitiesParent);

            var normalIcon = abilityGO.transform.Find("Icon")?.GetComponent<Image>();
            var grayIcon = abilityGO.transform.Find("IconGrayscale")?.GetComponent<Image>();

            AbiltySlider slider = normalIcon != null ? normalIcon.GetComponent<AbiltySlider>() : null;
            if (slider != null)
            {
                slider.ability = ability;
                slider.SetSliderValue(100f);
                abilitySliders.Add(slider);
            }
            else
            {
                abilitySliders.Add(null);
            }

            if (normalIcon != null) normalIcon.sprite = ability.AbilityIcon;
            if (grayIcon != null)
            {
                grayIcon.sprite = ability.AbilityIconGrayscale;
                grayIcon.fillAmount = 1f;
            }

            var nameText = abilityGO.transform.Find("Name")?.GetComponent<TMP_Text>();
            if (nameText != null) nameText.text = ability.AbilityName;

            TMP_Text cooldownText = abilityGO.transform.Find("Cooldown")?.GetComponent<TMP_Text>();
            if (cooldownText != null) cooldownText.text = abilitiesEnabled ? "Ready" : "Disabled";
            cooldownTexts.Add(cooldownText);

            InputAction act = null;

            if (inputActions != null)
            {
                // Try map/action first
                act = inputActions.FindAction(
                    $"{ability.actionMapName}/{ability.actionName}",
                    false
                );

                // Fallback: search all maps
                if (act == null)
                    act = inputActions.FindAction(ability.actionName, false);
            }

            abilityInputs.Add(act);

            // Start ready
            cooldownTimers.Add(ability.AbilityCooldown);
        }
        inputActions?.Enable();

        DumpActions();
        EnsureAbilityActionsEnabled();
        SubscribeAbilityCallbacks();
        ActivatePassives();
    }

    // makes an ability's cooldown different
    public void ModifyAbilityCooldown(int abilityIndex, int newCooldown)
    {
        if (abilityIndex < 0 || abilityIndex >= activeMoveset.Count)
        {
            Debug.LogWarning($"ModifyAbilityCooldown: abilityIndex {abilityIndex} out of range.");
            return;
        }
        var ability = activeMoveset[abilityIndex];
        if (ability == null)
        {
            Debug.LogWarning($"ModifyAbilityCooldown: ability at index {abilityIndex} is NULL.");
            return;
        }
        ability.AbilityCooldown = newCooldown;
        Debug.Log($"Modified cooldown of ability {ability.AbilityName} to {newCooldown} seconds.");
    }

    public int GetBaseCooldown(int abilityIndex)
    {
        if (abilityIndex < 0 || abilityIndex >= activeMoveset.Count)
        {
            Debug.LogWarning($"GetBaseCooldown: abilityIndex {abilityIndex} out of range.");
            return 0;
        }
        var ability = activeMoveset[abilityIndex];
        if (ability == null)
        {
            Debug.LogWarning($"GetBaseCooldown: ability at index {abilityIndex} is NULL.");
            return 0;
        }
        return ability.AbilityCooldown;
    }

    private void EnsureAbilityActionsEnabled()
    {
        for (int i = 0; i < abilityInputs.Count; i++)
        {
            var act = abilityInputs[i];
            if (act == null) continue;

            // Enable the action map + action
            act.actionMap?.Enable();
            if (!act.enabled) act.Enable();
        }
    }

    private void SubscribeAbilityCallbacks()
    {
        performedHandlers.Clear();

        for (int i = 0; i < abilityInputs.Count; i++)
        {
            int index = i;
            var act = abilityInputs[i];

            if (act == null)
            {
                performedHandlers.Add(null);
                continue;
            }

            // Ensure enabled
            act.actionMap?.Enable();
            act.Enable();

            System.Action<InputAction.CallbackContext> handler = ctx =>
            {
                if (!abilitiesEnabled) return;
                if (player == null) return;

                var pv = player.GetComponent<PhotonView>();
                if (pv == null || !pv.IsMine) return;

                if (index < 0 || index >= activeMoveset.Count) return;

                var ability = activeMoveset[index];
                if (ability == null) return;

                if (cooldownTimers[index] < ability.AbilityCooldown) return;

                ability.ActivateAbility(player);
                cooldownTimers[index] = 0f;

                // Notify the reward manager that an active ability was successfully used.
                CubeCoinRewardManager rewardManager = player.GetComponent<CubeCoinRewardManager>();
                rewardManager?.OnAbilitySuccessfullyUsed();
            };

            act.performed += handler;
            performedHandlers.Add(handler);
        }

        // Debug
        for (int i = 0; i < abilityInputs.Count; i++)
        {
            var a = abilityInputs[i];
            Debug.Log($"[AbilityInput] slot={i} ability={(i < activeMoveset.Count ? activeMoveset[i]?.AbilityName : "OUT_OF_RANGE")} " +
                      $"action={(a != null ? a.name : "NULL")} enabled={(a != null && a.enabled)} map={(a?.actionMap != null ? a.actionMap.name : "NULL")} mapEnabled={(a?.actionMap != null && a.actionMap.enabled)}");
        }
    }

    private void UnsubscribeAllAbilityCallbacks()
    {
        int n = Mathf.Min(abilityInputs.Count, performedHandlers.Count);
        for (int i = 0; i < n; i++)
        {
            var act = abilityInputs[i];
            var handler = performedHandlers[i];
            if (act != null && handler != null)
                act.performed -= handler;
        }
        performedHandlers.Clear();
    }

    private void Update()
    {
        if (!TryResolveLocalPlayer())
            return;

        if (activeMoveset.Count == 0)
            return;

        int safeCount = Mathf.Min(activeMoveset.Count, cooldownTimers.Count, cooldownTexts.Count, abilitySliders.Count);

        for (int i = 0; i < safeCount; i++)
        {
            var ability = activeMoveset[i];
            if (ability == null) continue;

            if (cooldownTimers[i] < ability.AbilityCooldown)
            {
                cooldownTimers[i] += Time.deltaTime;
                if (cooldownTimers[i] > ability.AbilityCooldown)
                    cooldownTimers[i] = ability.AbilityCooldown;
            }

            float remaining = ability.AbilityCooldown - cooldownTimers[i];

            if (!abilitiesEnabled)
            {
                if (cooldownTexts[i] != null) cooldownTexts[i].text = "Disabled";
                if (abilitySliders[i] != null) abilitySliders[i].SetSliderValue(0f);
                continue;
            }

            if (cooldownTexts[i] != null)
                cooldownTexts[i].text = remaining > 0 ? remaining.ToString("F1") : "Ready";

            if (abilitySliders[i] != null)
            {
                float fillPercent = (cooldownTimers[i] / ability.AbilityCooldown) * 100f;
                abilitySliders[i].SetSliderValue(fillPercent);
            }
        }
    }

    private IEnumerator PassiveTickLoop()
    {
        var wait = new WaitForSeconds(0.25f);
        while (true)
        {
            ActivatePassives();
            yield return wait;
        }
    }

    private void ActivatePassives()
    {
        if (currentMoveset == null) return;

        for (int i = 0; i < currentMoveset.Length; i++)
        {
            Ability ability = currentMoveset[i];
            if (ability == null) continue;

            if (ability.isPassive)
            {
                if (ability is PassiveAbility passiveAbility)
                {
                    passiveAbility.PassiveEffect(player);
                }
                else
                {
                    Debug.LogWarning($"Ability {ability.AbilityName} is marked as passive but is not of type PassiveAbility.");
                }
            }
        }
    }

    private void SetAllCooldownTexts(string text)
    {
        for (int i = 0; i < cooldownTexts.Count; i++)
        {
            if (cooldownTexts[i] != null)
                cooldownTexts[i].text = text;
        }
    }

    private void DumpActions()
    {
        if (inputActions == null)
        {
            Debug.LogError("[AbilityInput] inputActions is NULL (not assigned in inspector).");
            return;
        }

        Debug.Log($"[AbilityInput] Dumping InputActionAsset: {inputActions.name}");

        foreach (var map in inputActions.actionMaps)
        {
            Debug.Log($"  Map: {map.name} (enabled={map.enabled})");
            foreach (var a in map.actions)
            {
                Debug.Log($"    Action: {a.name} (type={a.type})");
            }
        }
    }


    private bool TryResolveLocalPlayer()
    {
        if (player != null)
        {
            var pv0 = player.GetComponent<PhotonView>();
            if (pv0 != null && pv0.IsMine) return true;
        }

        var camRefs = Camera.main?.GetComponent<PlayerCameraReferences>();
        var pObj = camRefs != null ? camRefs.GetPlayer() : null;
        if (pObj == null) return false;

        var pv = pObj.GetPhotonView();
        if (pv == null || !pv.IsMine) return false;

        player = pObj.GetComponent<BasePlayer>();
        return player != null;
    }
}
