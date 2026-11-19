using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public enum DefaultTab
    {
        ShopKillers,
        ShopSurvivors,
        OwnedKillers,
        OwnedSurvivors,
    }

    [Header("References")]
    public GameObject shop;
    public GameObject uiSelectionActive;
    public InputActionReference toggleShop;
    public RoundManager roundManager;

    [Header("Shop Grids")]
    public GameObject shopKillerContent;
    public GameObject shopSurvivorContent;

    [Header("Owned Grids")]
    public GameObject ownedKillerContent;
    public GameObject ownedSurvivorContent;

    [Header("Entry Prefabs")]
    public GameObject characterPrefab;

    [Header("Data")]
    public KillerType[] killers;
    public SurvivorType[] survivors;

    [Header("PlayFab")]
    public string catalogVersion = "shop";
    public string virtualCurrencyCode = "CC";

    [Header("UI")]
    public GameObject CharDetails;
    public Image image;
    public TMP_Text Name;
    public TMP_Text Description;
    public TMP_Text Difficulty;
    public TMP_Text Cost;
    public TMP_Text Health;
    public TMP_Text Speed;
    public TMP_Text Stamina;
    public Button purchaseButton;

    [Header("Character Mastery UI")]
    public Button masteryButton;
    public GameObject characterMasteryModal;
    public Image masteryCharImage;
    public TMP_Text masteryTitleText;
    public TMP_Text masteryTypeText;
    public TMP_Text masteryNameText;
    public Button masteryCloseButton;
    public Slider masteryLevelSlider;
    public TMP_Text masteryLevelValueText;
    public TMP_Text masteryXpText;

    [Header("Mastery Skin Swapping")]
    public RectTransform masterySkinsRow;
    public Transform masterySkinButtonContainer;
    public TMP_Text masterySkinsLabel;

    [Header("Behavior")]
    public DefaultTab defaultTab = DefaultTab.ShopKillers;

    private CubePlayerBase selectedCharacter;
    private HashSet<string> ownedItemIds = new HashSet<string>();
    private readonly List<Button> masterySkinButtons = new List<Button>();
    private bool masterySkinButtonsBound;

    public event Action<CubePlayerBase> OnCharacterPurchased;


    private void OnEnable()
    {
        AutoBindMasteryUI();

        if (toggleShop != null)
            toggleShop.action.performed += ShopTriggered;

        if (purchaseButton != null)
        {
            // Prevent duplicate listeners (e.g., if this component is enabled/disabled or
            // if you also wired the button in the Inspector)
            purchaseButton.onClick.RemoveListener(PurchaseSelected);
            purchaseButton.onClick.AddListener(PurchaseSelected);
        }

        if (masteryButton != null)
        {
            masteryButton.onClick.RemoveListener(OpenCharacterMastery);
            masteryButton.onClick.AddListener(OpenCharacterMastery);
        }

        if (masteryCloseButton != null)
        {
            masteryCloseButton.onClick.RemoveListener(CloseCharacterMastery);
            masteryCloseButton.onClick.AddListener(CloseCharacterMastery);
        }

        if (PlayfabManager.Instance != null)
            PlayfabManager.Instance.OnCubeCoinsChanged += HandleCoinsChanged;

        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.OnCharacterMasteryChanged += HandleMasteryChanged;
    }

    private void OnDisable()
    {
        if (toggleShop != null)
            toggleShop.action.performed -= ShopTriggered;

        if (purchaseButton != null)
            purchaseButton.onClick.RemoveListener(PurchaseSelected);

        if (masteryButton != null)
            masteryButton.onClick.RemoveListener(OpenCharacterMastery);

        if (masteryCloseButton != null)
            masteryCloseButton.onClick.RemoveListener(CloseCharacterMastery);

        if (PlayfabManager.Instance != null)
            PlayfabManager.Instance.OnCubeCoinsChanged -= HandleCoinsChanged;

        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.OnCharacterMasteryChanged -= HandleMasteryChanged;
    }

    private bool purchaseInFlight;

    private void Start()
    {
        AutoBindMasteryUI();

        if (shop != null)
            shop.SetActive(false);
        if (CharDetails != null)
            CharDetails.SetActive(false);
        if (characterMasteryModal != null)
            characterMasteryModal.SetActive(false);
        RefreshShopFromInventory();

        // Force-close the shop when a round begins so it can never remain open mid-round.
        if (roundManager != null)
            roundManager.OnRoundStart += ForceCloseShop;
    }

    private void AutoBindMasteryUI()
    {
        if (shop == null)
            return;

        if (masteryButton == null && CharDetails != null)
        {
            Transform mTr = CharDetails.transform.Find("Mastery");
            if (mTr != null)
                masteryButton = mTr.GetComponent<Button>();
        }

        if (characterMasteryModal == null)
        {
            Transform cmTr = shop.transform.Find("CharacterMastery");
            if (cmTr != null)
                characterMasteryModal = cmTr.gameObject;
        }

        if (characterMasteryModal != null)
        {
            Transform bg = characterMasteryModal.transform.Find("BG");
            if (bg != null)
            {
                if (masteryCharImage == null)
                    masteryCharImage = bg.Find("Image")?.GetComponent<Image>();
                if (masteryTitleText == null)
                    masteryTitleText = bg.Find("Title")?.GetComponent<TMP_Text>();
                if (masteryTypeText == null)
                    masteryTypeText = bg.Find("Type")?.GetComponent<TMP_Text>();
                if (masteryNameText == null)
                    masteryNameText = bg.Find("Name")?.GetComponent<TMP_Text>();
                if (masteryCloseButton == null)
                    masteryCloseButton = bg.Find("Close")?.GetComponent<Button>();

                Transform ml = bg.Find("Mastery Level");
                if (ml != null)
                {
                    if (masteryLevelSlider == null)
                        masteryLevelSlider = ml.Find("Slider")?.GetComponent<Slider>();
                    if (masteryLevelValueText == null)
                    {
                        Transform valTr = ml.Find("Value");
                        if (valTr != null)
                        {
                            masteryLevelValueText = valTr.GetComponent<TMP_Text>();
                            // Ensure SliderValueText does not overwrite "LVL ___" with a raw float
                            SliderValueText svt = valTr.GetComponent<SliderValueText>();
                            if (svt != null)
                                svt.enabled = false;
                        }
                    }
                    if (masteryXpText == null)
                        masteryXpText = ml.Find("XP")?.GetComponent<TMP_Text>();
                }

                if (masterySkinsRow == null)
                    masterySkinsRow = bg.Find("Mastery Skins") as RectTransform;

                if (masterySkinsRow != null)
                {
                    if (masterySkinsLabel == null)
                        masterySkinsLabel = masterySkinsRow.Find("Label")?.GetComponent<TMP_Text>();
                    if (masterySkinButtonContainer == null)
                        masterySkinButtonContainer = masterySkinsRow.Find("Buttons");
                }
            }
        }

        EnsureMasterySkinButtonsBound();
    }

    private void EnsureMasterySkinButtonsBound()
    {
        if (masterySkinButtonsBound || masterySkinButtonContainer == null)
            return;

        masterySkinButtons.Clear();
        for (int i = 0; i < masterySkinButtonContainer.childCount; i++)
        {
            Button button = masterySkinButtonContainer.GetChild(i).GetComponent<Button>();
            if (button == null)
                continue;

            int tier = masterySkinButtons.Count;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnMasterySkinSelected(tier));
            masterySkinButtons.Add(button);
        }

        masterySkinButtonsBound = masterySkinButtons.Count > 0;
    }

    public void OnMasterySkinSelected(int tier)
    {
        if (selectedCharacter == null)
            return;

        ProgressionManager prog = ProgressionManager.EnsureExists();
        if (prog == null)
            return;

        int highestUnlocked = prog.GetCharacterMasteryCosmeticTier(selectedCharacter._Name);
        if (tier > highestUnlocked)
            return;

        prog.SetSelectedMasteryCosmeticTier(selectedCharacter._Name, tier);
        RefreshCharacterMasteryModal();
    }

    private void RefreshMasterySkinButtons(int level)
    {
        EnsureMasterySkinButtonsBound();
        if (selectedCharacter == null)
            return;

        ProgressionManager prog = ProgressionManager.EnsureExists();
        int activeTier = prog != null
            ? prog.GetActiveMasteryCosmeticTier(selectedCharacter._Name)
            : 0;

        for (int i = 0; i < masterySkinButtons.Count; i++)
        {
            Button button = masterySkinButtons[i];
            if (button == null)
                continue;

            int tier = i;
            int requiredLevel = tier == 0 ? 0 : MasteryCosmeticUnlock.GetRequiredLevel(tier);
            bool unlocked = tier == 0 || level >= requiredLevel;
            bool isSelected = unlocked && tier == activeTier;

            button.interactable = unlocked;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                if (tier == 0)
                    label.text = LocalizationManager.Get("shop.mastery_skin_original", "Original");
                else
                    label.text = unlocked ? requiredLevel.ToString() : $"LVL {requiredLevel}";

                label.color = isSelected
                    ? new Color(0.06f, 0.06f, 0.06f, 1f)
                    : new Color(1f, 1f, 1f, unlocked ? 1f : 0.35f);
            }

            Image background = button.targetGraphic as Image;
            if (background != null)
            {
                background.color = isSelected
                    ? new Color(1f, 0.78f, 0.22f, 1f)
                    : unlocked
                        ? new Color(0.16f, 0.16f, 0.16f, 1f)
                        : new Color(0.1f, 0.1f, 0.1f, 0.85f);
            }
        }

        if (masterySkinsLabel != null)
        {
            MasteryCosmeticUnlock activeCosmetic =
                activeTier > 0 && selectedCharacter.character != null
                    ? MasteryCosmeticUnlock.GetFor(selectedCharacter.character, activeTier)
                    : null;

            string activeName =
                activeCosmetic != null && !string.IsNullOrEmpty(activeCosmetic.DisplayName)
                    ? activeCosmetic.DisplayName
                    : LocalizationManager.Get("shop.mastery_skin_original", "Original");

            masterySkinsLabel.text = LocalizationManager.Format(
                "shop.mastery_skins_fmt",
                "MASTERY SKINS: {0}",
                activeName.ToUpperInvariant()
            );
        }
    }

    private void HandleMasteryChanged(string charName, int level, int curXp, int reqXp)
    {
        if (selectedCharacter != null && string.Equals(selectedCharacter._Name, charName, StringComparison.OrdinalIgnoreCase))
        {
            UpdateMasteryButtonLabel();
            if (characterMasteryModal != null && characterMasteryModal.activeInHierarchy)
            {
                RefreshCharacterMasteryModal();
            }
        }
    }

    public void OpenCharacterMastery()
    {
        if (selectedCharacter == null)
            return;

        AutoBindMasteryUI();
        if (characterMasteryModal == null)
            return;

        RefreshCharacterMasteryModal();
        characterMasteryModal.SetActive(true);

        if (masteryCloseButton != null)
            StartCoroutine(SelectDelayed(masteryCloseButton.gameObject));
    }

    public void CloseCharacterMastery()
    {
        if (characterMasteryModal != null)
            characterMasteryModal.SetActive(false);

        if (masteryButton != null && masteryButton.gameObject.activeInHierarchy)
            StartCoroutine(SelectDelayed(masteryButton.gameObject));
    }

    private void RefreshCharacterMasteryModal()
    {
        if (selectedCharacter == null)
            return;

        ProgressionManager prog = ProgressionManager.EnsureExists();
        int level = prog != null ? prog.GetCharacterMasteryLevel(selectedCharacter._Name) : 1;
        int curXp = prog != null ? prog.GetCharacterMasteryXP(selectedCharacter._Name) : 0;
        int reqXp = prog != null ? prog.GetCharacterMasteryXPRequired(selectedCharacter._Name) : 250;

        if (masteryCharImage != null)
            masteryCharImage.sprite = selectedCharacter.image;
        if (masteryTitleText != null)
            masteryTitleText.text = LocalizationManager.Get("shop.mastery", "Mastery");
        if (masteryNameText != null)
            masteryNameText.text = LocalizationManager.GetCharacterName(selectedCharacter);

        if (masteryTypeText != null)
        {
            if (selectedCharacter is KillerType)
            {
                masteryTypeText.text = LocalizationManager.Get("shop.role_killer", "KILLER");
                masteryTypeText.color = new Color(1f, 0.2f, 0.2f, 1f);
            }
            else
            {
                masteryTypeText.text = LocalizationManager.Get("shop.role_survivor", "SURVIVOR");
                masteryTypeText.color = new Color(0.25f, 1f, 0.4f, 1f);
            }
        }

        if (masteryLevelValueText != null)
            masteryLevelValueText.text = $"LVL {level}";

        if (level >= ProgressionManager.MaxMasteryLevel)
        {
            if (masteryXpText != null)
                masteryXpText.text = LocalizationManager.Get("shop.max_level", "MAX LEVEL (100)");
            if (masteryLevelSlider != null)
            {
                masteryLevelSlider.minValue = 0f;
                masteryLevelSlider.maxValue = 1f;
                masteryLevelSlider.value = 1f;
            }
        }
        else
        {
            if (masteryXpText != null)
                masteryXpText.text = $"{curXp} / {reqXp} XP";
            if (masteryLevelSlider != null)
            {
                masteryLevelSlider.minValue = 0f;
                masteryLevelSlider.maxValue = Mathf.Max(1, reqXp);
                masteryLevelSlider.value = curXp;
            }
        }

        RefreshMasterySkinButtons(level);
    }

    private void UpdateMasteryButtonLabel()
    {
        if (masteryButton == null || selectedCharacter == null)
            return;

        TMP_Text btnText = masteryButton.GetComponentInChildren<TMP_Text>(true);
        if (btnText != null)
        {
            int lvl = ProgressionManager.Instance != null
                ? ProgressionManager.Instance.GetCharacterMasteryLevel(selectedCharacter._Name)
                : 1;
            btnText.text = LocalizationManager.Format("shop.mastery_lvl", "Mastery (LVL {0})", lvl);
        }
    }



    private void OnDestroy()
    {
        if (roundManager != null)
            roundManager.OnRoundStart -= ForceCloseShop;
    }

    /// <summary>
    /// Closes the shop immediately and restores gameplay cursor state.
    /// Called when a round begins to ensure the shop is never open mid-round.
    /// </summary>
    private void ForceCloseShop()
    {
        if (shop != null && shop.activeInHierarchy)
        {
            shop.SetActive(false);
            CursorManager.SetState(CursorManager.CursorState.Gameplay);
        }
    }

    private void HandleCoinsChanged(int _)
    {
        UpdatePurchaseButtonState();
    }

    private void ShopTriggered(InputAction.CallbackContext context)
    {
        if (shop == null)
            return;

        // Shop is only accessible during intermission, not during active rounds.
        if (roundManager != null && roundManager.roundActive)
            return;

        bool newState = !shop.activeInHierarchy;
        shop.SetActive(newState);

        if (newState)
        {
            RefreshShopFromInventory();
            StartCoroutine(SelectDelayed(uiSelectionActive));
        }
        else
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        CursorManager.SetState(
            newState ? CursorManager.CursorState.UI : CursorManager.CursorState.Gameplay
        );
    }

    private IEnumerator SelectDelayed(GameObject target)
    {
        yield return null;
        if (target != null && target.activeInHierarchy && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    private void RefreshShopFromInventory()
    {
        PlayFabClientAPI.GetUserInventory(
            new GetUserInventoryRequest(),
            inv =>
            {
                ownedItemIds = new HashSet<string>(inv.Inventory.Select(i => i.ItemId));
                BuildShopUI();
            },
            error =>
            {
                Debug.LogError("[Shop] Failed to get inventory: " + error.GenerateErrorReport());
                ownedItemIds.Clear();
                BuildShopUI();
            }
        );
    }

    private void BuildShopUI()
    {
        // --- DEBUG SUMMARY ---
        Debug.Log(
            $"[Shop] BuildShopUI()"
                + $" | killers={(killers == null ? -1 : killers.Length)}"
                + $" | survivors={(survivors == null ? -1 : survivors.Length)}"
                + $" | shopSurvivorContent={(shopSurvivorContent ? shopSurvivorContent.name : "NULL")}"
                + $" | ownedSurvivorContent={(ownedSurvivorContent ? ownedSurvivorContent.name : "NULL")}"
        );

        ClearChildren(shopKillerContent?.transform);
        ClearChildren(ownedKillerContent?.transform);
        ClearChildren(shopSurvivorContent?.transform);
        ClearChildren(ownedSurvivorContent?.transform);

        selectedCharacter = null;
        if (CharDetails != null)
            CharDetails.SetActive(false);

        int spawnedKillers = 0;
        int spawnedSurvivors = 0;

        // Killers
        if (killers != null)
        {
            foreach (var killer in killers)
            {
                if (killer == null)
                    continue;

                bool isOwned =
                    !string.IsNullOrEmpty(killer._PlayFabItemID)
                    && ownedItemIds.Contains(killer._PlayFabItemID);
                Transform parent = (isOwned ? ownedKillerContent : shopKillerContent)?.transform;
                if (parent == null)
                {
                    Debug.LogWarning(
                        "[Shop] Killer parent is null (check shopKillerContent/ownedKillerContent assignments)"
                    );
                    continue;
                }

                var entry = Instantiate(characterPrefab, parent);
                if (!TryWireCharacterEntry(entry, killer))
                    Destroy(entry);
                else
                    spawnedKillers++;
            }
        }

        // Survivors
        if (survivors != null)
        {
            foreach (var survivor in survivors)
            {
                if (survivor == null)
                    continue;

                bool isOwned =
                    !string.IsNullOrEmpty(survivor._PlayFabItemID)
                    && ownedItemIds.Contains(survivor._PlayFabItemID);
                Transform parent = (
                    isOwned ? ownedSurvivorContent : shopSurvivorContent
                )?.transform;

                if (parent == null)
                {
                    Debug.LogWarning(
                        "[Shop] Survivor parent is null. You likely didn't assign shopSurvivorContent and/or ownedSurvivorContent in the inspector."
                    );
                    continue;
                }

                var entry = Instantiate(characterPrefab, parent);
                if (!TryWireCharacterEntry(entry, survivor))
                    Destroy(entry);
                else
                    spawnedSurvivors++;
            }
        }

        Debug.Log(
            $"[Shop] Spawned entries -> Killers:{spawnedKillers} Survivors:{spawnedSurvivors}"
        );

        // Default active tab
        ApplyDefaultTab();

        UpdatePurchaseButtonState();
    }

    private void ApplyDefaultTab()
    {
        switch (defaultTab)
        {
            case DefaultTab.ShopKillers:
                SetActiveContent(shopKillerContent);
                break;
            case DefaultTab.OwnedKillers:
                SetActiveContent(ownedKillerContent);
                break;
            case DefaultTab.ShopSurvivors:
                SetActiveContent(shopSurvivorContent);
                break;
            case DefaultTab.OwnedSurvivors:
                SetActiveContent(ownedSurvivorContent);
                break;
        }
    }

    private bool TryWireCharacterEntry(GameObject entry, CubePlayerBase data)
    {
        if (entry == null || data == null)
            return false;

        var button = entry.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[Shop] Entry prefab missing Button", entry);
            return false;
        }

        var template = entry.GetComponent<CharacterTemplate>();
        if (template == null)
        {
            Debug.LogError("[Shop] Character entry prefab missing CharacterTemplate", entry);
            return false;
        }

        template.AssociatedCharacter = data;
        button.onClick.AddListener(() => OnCharacterClicked(template));
        return true;
    }

    private void ClearChildren(Transform t)
    {
        if (t == null)
            return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    // --- TAB VISIBILITY ---

    private void SetActiveContent(GameObject target)
    {
        GameObject[] all =
        {
            shopKillerContent,
            ownedKillerContent,
            shopSurvivorContent,
            ownedSurvivorContent,
        };

        // Disable all
        foreach (var go in all)
            if (go != null)
                go.SetActive(false);

        if (target == null)
            return;

        // Ensure all parents are active (common issue: parent panel/scrollview disabled)
        EnsureParentsActive(target.transform);

        // Enable target
        target.SetActive(true);
    }

    private void EnsureParentsActive(Transform t)
    {
        // Only ensure up to the shop root, NOT beyond
        var cur = t;
        while (cur != null && cur != shop.transform)
        {
            if (!cur.gameObject.activeSelf)
                cur.gameObject.SetActive(true);

            cur = cur.parent;
        }
    }

    public void ShowShopKillers() => SetActiveContent(shopKillerContent);

    public void ShowOwnedKillers() => SetActiveContent(ownedKillerContent);

    public void ShowShopSurvivors() => SetActiveContent(shopSurvivorContent);

    public void ShowOwnedSurvivors() => SetActiveContent(ownedSurvivorContent);

    // --- SELECTION ---

    /// <summary>
    /// Equips the given character on the local player's BasePlayer component and
    /// persists the selection via BasePlayer.EquipKiller / EquipSurvivor so it is
    /// remembered across rounds and sessions.
    /// </summary>
    private void EquipCharacter(CubePlayerBase character)
    {
        if (character == null)
            return;

        // Find the local player's BasePlayer — use PhotonLauncher's cached reference.
        GameObject localPlayerGO = PhotonLauncher.LocalPlayerInstance;
        if (localPlayerGO == null)
        {
            // Pre-game (Menu scene) — just save to PlayerPrefs directly via BasePlayer helpers
            // by temporarily creating a throwaway instance to call the static-equivalent logic.
            if (character is KillerType killer)
            {
                PlayerPrefs.SetString("EquippedKillerName", killer._Name);
                PlayerPrefs.Save();
                Debug.Log(
                    $"[Shop] Saved killer selection (no player in scene yet): {killer._Name}"
                );
            }
            else if (character is SurvivorType survivor)
            {
                PlayerPrefs.SetString("EquippedSurvivorName", survivor._Name);
                PlayerPrefs.Save();
                Debug.Log(
                    $"[Shop] Saved survivor selection (no player in scene yet): {survivor._Name}"
                );
            }
            return;
        }

        BasePlayer basePlayer = localPlayerGO.GetComponent<BasePlayer>();
        if (basePlayer == null)
            return;

        if (character is KillerType k)
        {
            basePlayer.EquipKiller(k);
            CUBEStatusText.FadeOutAndDestroy(
                CUBEStatusText.CreateText($"Equipped <color=red>{k._Name}</color> as Killer"),
                2f
            );
        }
        else if (character is SurvivorType s)
        {
            basePlayer.EquipSurvivor(s);
            CUBEStatusText.FadeOutAndDestroy(
                CUBEStatusText.CreateText($"Equipped <color=green>{s._Name}</color> as Survivor"),
                2f
            );
        }
    }

    private void OnCharacterClicked(CharacterTemplate template)
    {
        if (template == null || template.AssociatedCharacter == null)
            return;

        selectedCharacter = template.AssociatedCharacter as CubePlayerBase;

        if (selectedCharacter == null)
        {
            Debug.LogWarning("[Shop] AssociatedCharacter is not a CubePlayerBase");
            return;
        }

        if (CharDetails != null)
            CharDetails.SetActive(true);

        if (masteryButton != null)
        {
            masteryButton.gameObject.SetActive(true);
            UpdateMasteryButtonLabel();
        }

        if (Name != null)
            Name.text = LocalizationManager.GetCharacterName(selectedCharacter);
        if (Description != null)
            Description.text = LocalizationManager.GetCharacterDescription(selectedCharacter);
        if (image != null)
            image.sprite = selectedCharacter.image;

        if (Difficulty != null)
            Difficulty.text = LocalizationManager.Format("shop.difficulty_fmt", "{0}/5 Difficulty", selectedCharacter.Difficulty);
        if (Cost != null)
            Cost.text = LocalizationManager.Format("shop.cost_fmt", "{0} Cubes", selectedCharacter.Cost);
        if (Health != null)
            Health.text = LocalizationManager.Format("shop.hp_fmt", "{0} HP", selectedCharacter.Health);
        if (Speed != null)
            Speed.text = LocalizationManager.Format("shop.speed_fmt", "{0} Speed", selectedCharacter.Speed);
        if (Stamina != null)
            Stamina.text = LocalizationManager.Format("shop.stamina_fmt", "{0} Stamina", selectedCharacter.Stamina);


        // If the player owns this character, equip it immediately on click so the
        // selection persists into the next round without needing a separate Equip button.
        bool isOwned =
            !string.IsNullOrEmpty(selectedCharacter._PlayFabItemID)
            && ownedItemIds.Contains(selectedCharacter._PlayFabItemID);
        if (isOwned)
        {
            EquipCharacter(selectedCharacter);
        }

        UpdatePurchaseButtonState();
    }

    private void UpdatePurchaseButtonState()
    {
        if (purchaseButton == null)
            return;

        if (selectedCharacter == null)
        {
            purchaseButton.interactable = false;
            return;
        }

        string itemId = selectedCharacter._PlayFabItemID;
        int cost = selectedCharacter.Cost;

        bool owned = !string.IsNullOrEmpty(itemId) && ownedItemIds.Contains(itemId);

        bool canAfford = true;
        if (PlayfabManager.Instance != null)
            canAfford = PlayfabManager.Instance.CachedCC >= cost;

        purchaseButton.interactable = !owned && canAfford;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(purchaseButton.gameObject);
    }

    // --- PURCHASING (unchanged logic) ---

    public void PurchaseSelected()
    {
        if (purchaseInFlight)
        {
            Debug.Log("[Shop] Purchase ignored: request already in flight");
            return;
        }
        if (selectedCharacter == null)
        {
            Debug.LogWarning("[Shop] No selection");
            return;
        }

        string itemId = selectedCharacter._PlayFabItemID;
        int cost = selectedCharacter.Cost;

        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogError("[Shop] Selected item has no PlayFab ItemId");
            return;
        }

        if (ownedItemIds.Contains(itemId))
        {
            Debug.Log("[Shop] Already owned: " + itemId);
            UpdatePurchaseButtonState();
            return;
        }

        if (PlayfabManager.Instance != null && PlayfabManager.Instance.CachedCC < cost)
        {
            Debug.Log("[Shop] Not enough CC to buy: " + itemId);
            UpdatePurchaseButtonState();
            return;
        }

        purchaseInFlight = true;

        if (purchaseButton != null)
            purchaseButton.interactable = false;

        var request = new PurchaseItemRequest
        {
            CatalogVersion = catalogVersion,
            ItemId = itemId,
            Price = cost,
            VirtualCurrency = virtualCurrencyCode,
        };

        PlayFabClientAPI.PurchaseItem(
            request,
            result => OnPurchaseSuccess(result, itemId),
            error => OnPurchaseError(error, itemId)
        );
    }

    private void OnPurchaseSuccess(PurchaseItemResult result, string itemId)
    {
        purchaseInFlight = false;
        {
            Debug.Log("[Shop] Purchase successful: " + itemId);
            CUBEStatusText.FadeOutAndDestroy(
                CUBEStatusText.CreateText("Successfully purchased!"),
                1f
            );

            ownedItemIds.Add(itemId);

            if (PlayfabManager.Instance != null)
                PlayfabManager.Instance.RefreshCubeCoins();

            if (selectedCharacter != null)
                OnCharacterPurchased?.Invoke(selectedCharacter);

            RefreshShopFromInventory();
        }
    }


    private void OnPurchaseError(PlayFabError error, string itemId)
    {
        purchaseInFlight = false;
        {
            Debug.LogError(
                "[Shop] Purchase failed (" + itemId + "): " + error.GenerateErrorReport()
            );
            Debug.Log("[Shop] PlayFab error code: " + error.Error);

            CUBEStatusText.FadeOutAndDestroy(
                CUBEStatusText.CreateText("Purchase failed: " + error.ErrorMessage),
                1f
            );
            UpdatePurchaseButtonState();
        }
    }
}
