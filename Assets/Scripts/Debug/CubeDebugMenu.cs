using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Comprehensive in-game debug menu built with IMGUI (OnGUI).
/// Allows testing roles, characters, health, status effects, and world/physics parameters on the fly.
/// Attached to the persistent 'CUBE Debug' GameObject.
/// Toggle with F2 or Backquote (~).
/// </summary>
public class CubeDebugMenu : MonoBehaviour
{
    private bool isOpen = false;
    private Rect windowRect = new Rect(25, 25, 620, 680);
    private int selectedTab = 0;
    private readonly string[] tabNames = {
        "Status Effects",
        "Player & Targets",
        "Roles & Abilities",
        "Match & Round",
        "Visuals & Audio",
        "Physics & Sandbox"
    };

    private Vector2 scrollPos;
    private bool prevCursorVisible;
    private CursorLockMode prevCursorLockMode;

    // Custom Status Effect Builder State
    private enum EffectTargetScope { LocalPlayer, SelectedTarget, AllPlayers }
    private EffectTargetScope effectTargetScope = EffectTargetScope.LocalPlayer;
    private StatusEffectType customType = StatusEffectType.Speed;
    private int customTier = 1;
    private float customDuration = 5f;
    private bool useCustomMult = false;
    private float customMultiplier = 1.5f;

    // Cheats State
    public bool godMode = false;
    public bool infiniteStamina = false;
    private float customSpeedHack = 1f;
    private float customTimeScale = 1f;
    private bool zeroGravity = false;
    private float playerScale = 1f;
    private bool noclipMode = false;

    // Remote Target Selection
    private int selectedPlayerIndex = 0;

    // Volume Override Controls
    private Volume cachedVolume;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;
    private ColorAdjustments colorAdjustments;
    private FilmGrain filmGrain;
    private bool volumeCached = false;

    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle subHeaderStyle;
    private bool stylesInitialized = false;

    private void Awake()
    {
        // Ensure this GameObject persists across scenes
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Toggle menu with F2 or Backquote (tilde ~)
        bool togglePressed = false;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f2Key.wasPressedThisFrame || Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.BackQuote))
        {
            togglePressed = true;
        }
#endif

        if (togglePressed)
        {
            SetMenuOpen(!isOpen);
        }

        // Apply continuous cheats if active
        if (isOpen || godMode || infiniteStamina)
        {
            ApplyContinuousCheats();
        }
    }

    public void SetMenuOpen(bool open)
    {
        if (isOpen == open) return;
        isOpen = open;

        if (isOpen)
        {
            prevCursorVisible = Cursor.visible;
            prevCursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            CacheVolume();
        }
        else
        {
            Cursor.visible = prevCursorVisible;
            Cursor.lockState = prevCursorLockMode;
        }
    }

    private void ApplyContinuousCheats()
    {
        BasePlayer localPlayer = GetLocalPlayer();
        if (localPlayer == null) return;

        if (godMode)
        {
            BasePlayerStats stats = localPlayer.GetComponent<BasePlayerStats>();
            if (stats != null && stats.health < stats.maxHealth)
            {
                stats.health = stats.maxHealth;
            }
        }

        if (infiniteStamina)
        {
            BasePlayerStats stats = localPlayer.GetComponent<BasePlayerStats>();
            if (stats != null)
            {
                stats.stamina = stats.maxStamina;
            }
        }
    }

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        headerStyle.normal.textColor = new Color(0.2f, 0.9f, 1f);

        subHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        subHeaderStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitStyles();

        // Small toggle watermark in top-left
        if (!isOpen)
        {
            if (GUI.Button(new Rect(10, 10, 150, 24), "CUBE Debug [F2 / ~]"))
            {
                SetMenuOpen(true);
            }
            return;
        }

        Color oldBg = GUI.backgroundColor;
        Color oldColor = GUI.color;
        GUI.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 0.97f);

        windowRect = GUILayout.Window(99912, windowRect, DrawDebugWindow, "⚡ CUBE Debug & Status Effect Sandbox");

        GUI.backgroundColor = oldBg;
        GUI.color = oldColor;
    }

    private void DrawDebugWindow(int windowId)
    {
        GUILayout.Space(2);

        // Header Tabs
        GUILayout.BeginHorizontal();
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(28));
        if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(28)))
        {
            SetMenuOpen(false);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        switch (selectedTab)
        {
            case 0:
                DrawStatusEffectsTab();
                break;
            case 1:
                DrawPlayerAndTargetsTab();
                break;
            case 2:
                DrawRolesAndAbilitiesTab();
                break;
            case 3:
                DrawMatchAndRoundTab();
                break;
            case 4:
                DrawVisualsAndAudioTab();
                break;
            case 5:
                DrawPhysicsAndSandboxTab();
                break;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Scene: {SceneManager.GetActiveScene().name}  |  Ping: {PhotonNetwork.GetPing()}ms  |  Master: {PhotonNetwork.IsMasterClient}", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Close [F2]", GUILayout.Width(90)))
        {
            SetMenuOpen(false);
        }
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, 10000, 26));
    }

    #region Tab 1: Status Effects

    private void DrawStatusEffectsTab()
    {
        StatusEffectManager sem = GetLocalStatusEffectManager();

        GUILayout.Label("Active Status Effects Inspector", headerStyle);

        if (sem == null)
        {
            GUILayout.Box("No local player found. Join a room or wait for character spawn.", boxStyle);
            return;
        }

        // Scope Selector
        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>Target Scope:</b>", GUILayout.Width(95));
        if (GUILayout.Toggle(effectTargetScope == EffectTargetScope.LocalPlayer, "Local Player", "Button"))
            effectTargetScope = EffectTargetScope.LocalPlayer;
        if (GUILayout.Toggle(effectTargetScope == EffectTargetScope.SelectedTarget, "Selected Target", "Button"))
            effectTargetScope = EffectTargetScope.SelectedTarget;
        if (GUILayout.Toggle(effectTargetScope == EffectTargetScope.AllPlayers, "💥 ALL PLAYERS IN ROOM", "Button"))
            effectTargetScope = EffectTargetScope.AllPlayers;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(boxStyle);
        var activeEffects = sem.GetActiveEffects();
        if (activeEffects.Count == 0)
        {
            GUILayout.Label("<color=#888888>No active status effects on local player (all stats baseline)</color>", labelStyle);
        }
        else
        {
            foreach (var effect in activeEffects)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{effect.Icon} <color={effect.ColorHex}><b>{effect.DisplayName}</b></color> ({effect.durationRemaining:F1}s left)", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("✕ Remove", GUILayout.Width(80)))
                {
                    sem.RemoveEffect(effect.type);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.Label($"<b>Net Speed:</b> {sem.GetSpeedMultiplier():F2}x  |  <b>Damage Taken:</b> {sem.GetDamageTakenMultiplier():F2}x  |  <b>Stamina Regen:</b> {sem.GetStaminaRegenMultiplier():F2}x", labelStyle);

            if (GUILayout.Button($"Clear All Effects ({effectTargetScope})", GUILayout.Height(24)))
            {
                ClearEffectsScoped();
            }
        }
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label($"Character-Specific Status Effects (Applying to: <color=yellow>{effectTargetScope}</color>)", headerStyle);

        // Character Categories
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("🎧 <b>The DJ</b> (Sonic & Rhythm Effects):", subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🎵 Groove I (5s)")) ApplyEffectScoped(StatusEffectType.Groove, 1, 5f);
        if (GUILayout.Button("🎵 Groove II (5s)")) ApplyEffectScoped(StatusEffectType.Groove, 2, 5f);
        if (GUILayout.Button("🔊 Equalized II (3s)")) ApplyEffectScoped(StatusEffectType.Equalized, 2, 3f);
        if (GUILayout.Button("🔥 Hype +1 Stack")) ApplyEffectScoped(StatusEffectType.Hype, 1, 6f);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("👾 <b>The Glitch</b> (Visual Distortion & Anomaly):", subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Distortion I (3s)")) ApplyEffectScoped(StatusEffectType.GlitchDistortion, 1, 3f);
        if (GUILayout.Button("Distortion II (4s)")) ApplyEffectScoped(StatusEffectType.GlitchDistortion, 2, 4f);
        if (GUILayout.Button("Distortion III (5s)")) ApplyEffectScoped(StatusEffectType.GlitchDistortion, 3, 5f);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("💻 <b>The Coder</b> (Temporal Code Exploits):", subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("❄️ Compile Freeze (0.75s)")) ApplyEffectScoped(StatusEffectType.Freeze, 1, 0.75f);
        if (GUILayout.Button("⚡ Speed III Boost (3s)")) ApplyEffectScoped(StatusEffectType.Speed, 3, 3f);
        if (GUILayout.Button("📡 Lag Spike II (4s)")) ApplyEffectScoped(StatusEffectType.LagSpike, 2, 4f);
        if (GUILayout.Button("💻 Overclocked (10s)")) ApplyEffectScoped(StatusEffectType.Overclocked, 1, 10f);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("⚔️ <b>The Titan</b> (Heavy Impact & Pressure):", subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("💥 Tremor II (Shockwave 3s)")) ApplyEffectScoped(StatusEffectType.Tremor, 2, 3f);
        if (GUILayout.Button("🌪️ Wind Stagger II (2.5s)")) ApplyEffectScoped(StatusEffectType.WindStagger, 2, 2.5f);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("❤️ <b>The Healer</b> (Restorative & Divine Auras):", subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("💖 Regen I (+5 HP/s 6s)")) ApplyEffectScoped(StatusEffectType.Regeneration, 1, 6f);
        if (GUILayout.Button("💖 Regen II (+10 HP/s 6s)")) ApplyEffectScoped(StatusEffectType.Regeneration, 2, 6f);
        if (GUILayout.Button("✨ Sanctuary II (Last Stand 5s)")) ApplyEffectScoped(StatusEffectType.Sanctuary, 2, 5f);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Core Movement & Combat Modifiers", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("<b>Heavy Legs (Slowness Debuffs):</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Heavy Legs I (5s)")) ApplyEffectScoped(StatusEffectType.HeavyLegs, 1, 5f);
        if (GUILayout.Button("Heavy Legs II (4s)")) ApplyEffectScoped(StatusEffectType.HeavyLegs, 2, 4f);
        if (GUILayout.Button("Heavy Legs III (3s)")) ApplyEffectScoped(StatusEffectType.HeavyLegs, 3, 3f);
        if (GUILayout.Button("Heavy Legs IV (5s)")) ApplyEffectScoped(StatusEffectType.HeavyLegs, 4, 5f);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("<b>Defense & Vulnerability:</b>");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Resistance I (-15% 5s)")) ApplyEffectScoped(StatusEffectType.Resistance, 1, 5f);
        if (GUILayout.Button("Resistance II (-30% 6s)")) ApplyEffectScoped(StatusEffectType.Resistance, 2, 6f);
        if (GUILayout.Button("Vulnerability II (+30% 6s)")) ApplyEffectScoped(StatusEffectType.Vulnerability, 2, 6f);
        if (GUILayout.Button("Stamina Surge (6s)")) ApplyEffectScoped(StatusEffectType.StaminaBoost, 2, 6f);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Custom Status Effect Forge", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Effect Type:", GUILayout.Width(100));
        if (GUILayout.Button($"< {customType} >"))
        {
            var values = (StatusEffectType[])Enum.GetValues(typeof(StatusEffectType));
            int idx = Array.IndexOf(values, customType);
            int next = (idx + 1) % values.Length;
            customType = values[next];
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Tier: {ActiveStatusEffect.ToRomanNumeral(customTier)} ({customTier})", GUILayout.Width(120));
        for (int t = 1; t <= 5; t++)
        {
            if (GUILayout.Toggle(customTier == t, ActiveStatusEffect.ToRomanNumeral(t), "Button", GUILayout.Width(45)))
            {
                customTier = t;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Duration: {customDuration:F1}s", GUILayout.Width(120));
        customDuration = GUILayout.HorizontalSlider(customDuration, 0.5f, 30f);
        GUILayout.EndHorizontal();

        useCustomMult = GUILayout.Toggle(useCustomMult, " Override with Custom Multiplier");
        if (useCustomMult)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Custom Multiplier: {customMultiplier:F2}x", GUILayout.Width(180));
            customMultiplier = GUILayout.HorizontalSlider(customMultiplier, 0.05f, 5f);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        if (GUILayout.Button($"✨ Cast {customType} {ActiveStatusEffect.ToRomanNumeral(customTier)} ({customDuration:F1}s) to {effectTargetScope}", GUILayout.Height(30)))
        {
            ApplyEffectScoped(customType, customTier, customDuration, useCustomMult ? customMultiplier : 0f);
        }
        GUILayout.EndVertical();
    }

    #endregion

    #region Tab 2: Player & Targets

    private void DrawPlayerAndTargetsTab()
    {
        BasePlayer localPlayer = GetLocalPlayer();
        BasePlayerStats stats = localPlayer != null ? localPlayer.GetComponent<BasePlayerStats>() : null;

        GUILayout.Label("Target Player & Remote Actions", headerStyle);

        var allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        var playerNames = new List<string>();
        foreach (var p in allPlayers)
        {
            string tag = p.photonView.IsMine ? " (YOU)" : "";
            playerNames.Add($"[{p.photonView.OwnerActorNr}] {p.photonView.Owner?.NickName ?? "Bot"}{tag} ({p.GetPlayerType()})");
        }

        if (playerNames.Count > 0)
        {
            selectedPlayerIndex = Mathf.Clamp(selectedPlayerIndex, 0, allPlayers.Length - 1);
            GUILayout.BeginVertical(boxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target:", GUILayout.Width(60));
            if (GUILayout.Button($"< {playerNames[selectedPlayerIndex]} >"))
            {
                selectedPlayerIndex = (selectedPlayerIndex + 1) % allPlayers.Length;
            }
            GUILayout.EndHorizontal();

            BasePlayer targetPlayer = allPlayers[selectedPlayerIndex];
            BasePlayerStats targetStats = targetPlayer != null ? targetPlayer.GetComponent<BasePlayerStats>() : null;

            if (targetPlayer != null && targetStats != null)
            {
                GUILayout.Label($"HP: {targetStats.health}/{targetStats.maxHealth}  |  Role: {targetPlayer.GetPlayerType()}  |  Actor: {targetPlayer.photonView.OwnerActorNr}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Deal 20 Dmg"))
                {
                    targetPlayer.photonView.RPC("TakeDamage", targetPlayer.photonView.Owner, 20);
                }
                if (GUILayout.Button("Heal 20 HP"))
                {
                    targetPlayer.photonView.RPC("Heal", targetPlayer.photonView.Owner, 20);
                }
                if (GUILayout.Button("Teleport Target to Me"))
                {
                    if (localPlayer != null)
                    {
                        PlayerTeleportHandler th = targetPlayer.GetComponent<PlayerTeleportHandler>();
                        th?.Teleport(localPlayer.transform.position + Vector3.forward * 2f, Quaternion.identity, true);
                    }
                }
                if (GUILayout.Button("Teleport Me to Target"))
                {
                    if (localPlayer != null)
                    {
                        PlayerTeleportHandler th = localPlayer.GetComponent<PlayerTeleportHandler>();
                        th?.Teleport(targetPlayer.transform.position + Vector3.forward * 2f, Quaternion.identity, true);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Give Slow (Heavy Legs II)"))
                {
                    targetPlayer.photonView.RPC("RPC_ApplyStatusEffect", targetPlayer.photonView.Owner, (byte)StatusEffectType.HeavyLegs, 2, 4f, 0f);
                }
                if (GUILayout.Button("Give Speed (Speed III)"))
                {
                    targetPlayer.photonView.RPC("RPC_ApplyStatusEffect", targetPlayer.photonView.Owner, (byte)StatusEffectType.Speed, 3, 4f, 0f);
                }
                if (GUILayout.Button("Clear Target Effects"))
                {
                    targetPlayer.photonView.RPC("RPC_ClearAllStatusEffects", targetPlayer.photonView.Owner);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        GUILayout.Space(6);
        GUILayout.Label("Local Health & Vitals Control", headerStyle);

        if (stats == null)
        {
            GUILayout.Box("Local player stats not found.", boxStyle);
            return;
        }

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label($"<b>Health:</b> {stats.health} / {stats.maxHealth}   |   <b>Stamina:</b> {stats.stamina} / {stats.maxStamina}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-10 HP")) stats.TakeDamage(10);
        if (GUILayout.Button("+10 HP")) stats.Heal(10);
        if (GUILayout.Button("-25 HP")) stats.TakeDamage(25);
        if (GUILayout.Button("+25 HP")) stats.Heal(25);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Set to 1 HP (Clutch Test)")) stats.health = 1;
        if (GUILayout.Button("Full Heal")) stats.health = stats.maxHealth;
        if (GUILayout.Button("Instant Suicide")) stats.TakeDamage(9999);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        godMode = GUILayout.Toggle(godMode, " 🛡️ God Mode (Invulnerable to Damage & Death)");
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Stamina & Economy", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Refill Stamina")) stats.stamina = stats.maxStamina;
        if (GUILayout.Button("Drain Stamina")) stats.stamina = 0;
        GUILayout.EndHorizontal();

        infiniteStamina = GUILayout.Toggle(infiniteStamina, " ⚡ Infinite Stamina");

        GUILayout.Space(4);
        GUILayout.Label("Cube Coins (CC):");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+100 CC")) PlayfabManager.Instance?.AddCubeCoins(100, "Debug Reward");
        if (GUILayout.Button("+500 CC")) PlayfabManager.Instance?.AddCubeCoins(500, "Debug Reward");
        if (GUILayout.Button("+2000 CC")) PlayfabManager.Instance?.AddCubeCoins(2000, "Debug Reward");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Player Scaling & Noclip Flight", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Player Scale: {playerScale:F2}x", GUILayout.Width(130));
        float newScale = GUILayout.HorizontalSlider(playerScale, 0.25f, 4f);
        if (!Mathf.Approximately(newScale, playerScale))
        {
            playerScale = newScale;
            if (localPlayer != null)
                localPlayer.transform.localScale = Vector3.one * playerScale;
        }
        if (GUILayout.Button("Reset Scale (1x)", GUILayout.Width(110)))
        {
            playerScale = 1f;
            if (localPlayer != null)
                localPlayer.transform.localScale = Vector3.one;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(noclipMode ? "🚀 Disable Noclip Fly" : "🚀 Enable Noclip Fly"))
        {
            noclipMode = !noclipMode;
            if (localPlayer != null)
            {
                Noclipper noclip = localPlayer.GetComponent<Noclipper>();
                if (noclip != null) noclip.noclipping = noclipMode;
            }
        }
        if (GUILayout.Button("Snap Ragdoll to Ground"))
        {
            if (localPlayer != null)
            {
                PlayerTeleportHandler th = localPlayer.GetComponent<PlayerTeleportHandler>();
                th?.Teleport(localPlayer.transform.position, localPlayer.transform.rotation, snapToGround: true);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    #endregion

    #region Tab 3: Roles & Abilities

    private void DrawRolesAndAbilitiesTab()
    {
        BasePlayer localPlayer = GetLocalPlayer();

        GUILayout.Label("Live Role & Hero Swap (Network Synchronized)", headerStyle);

        if (localPlayer == null)
        {
            GUILayout.Box("Local player not found. Spawn into arena to test.", boxStyle);
            return;
        }

        PhotonView pv = localPlayer.photonView;

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label($"<b>Current Role:</b> <color=yellow>{localPlayer.GetPlayerType()}</color>");
        GUILayout.Label($"<b>Equipped Killer:</b> {localPlayer.equippedKiller?._Name ?? "None"}  |  <b>Survivor:</b> {localPlayer.equippedSurvivor?._Name ?? "None"}");
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Instant Swap to Killer:", subHeaderStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("👑 The Glitch (Teleport / Shards)"))
        {
            SwitchCharacter(localPlayer, BasePlayer.PlayerType.Killer, "The Glitch");
        }
        if (GUILayout.Button("⚔️ The Titan (Heavy Shockwave / Wind)"))
        {
            SwitchCharacter(localPlayer, BasePlayer.PlayerType.Killer, "The Titan");
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Instant Swap to Survivor:", subHeaderStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("💻 The Coder (Compile / Lag)"))
        {
            SwitchCharacter(localPlayer, BasePlayer.PlayerType.Survivor, "The Coder");
        }
        if (GUILayout.Button("🎧 The DJ (Music Circle / Equalizer)"))
        {
            SwitchCharacter(localPlayer, BasePlayer.PlayerType.Survivor, "The DJ");
        }
        if (GUILayout.Button("❤️ The Healer (Heal Circle / Sanctuary)"))
        {
            SwitchCharacter(localPlayer, BasePlayer.PlayerType.Survivor, "The Healer");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        if (GUILayout.Button("Reset Role to None (Intermission Lobby Mode)"))
        {
            pv.RPC("SetPlayerType", RpcTarget.AllBuffered, BasePlayer.PlayerType.None);
            pv.RPC("ResetToDefaultStats", RpcTarget.AllBuffered);
        }
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Ability Testing & Spawning", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        PlayerAbilityManager pam = FindFirstObjectByType<PlayerAbilityManager>();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("⚡ Reset All Ability Cooldowns to 0"))
        {
            if (pam != null)
            {
                pam.ResetAllCooldowns();
                CUBEStatusText.CreateText("<color=#00FFCC>⚡ All Ability Cooldowns Ready!</color>");
            }
        }
        if (GUILayout.Button("🔄 Reload Local Abilities & HUD"))
        {
            if (pam != null)
                pam.SendMessage("OnRoundStart", SendMessageOptions.DontRequireReceiver);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label("Spawn Network Ability Prefabs (At Feet / Forward):", subHeaderStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Music Circle"))
        {
            PhotonNetwork.Instantiate("MusicCirclePrefab", localPlayer.transform.position, Quaternion.identity);
        }
        if (GUILayout.Button("Spawn Equalizer Wall"))
        {
            Vector3 spawn = localPlayer.transform.position + localPlayer.transform.forward * 2.5f;
            PhotonNetwork.Instantiate("EqualizerWallPrefab", spawn, localPlayer.transform.rotation);
        }
        if (GUILayout.Button("Spawn Shockwave"))
        {
            PhotonNetwork.Instantiate("ShockwavePrefab", localPlayer.transform.position, Quaternion.Euler(90f, 0f, 0f));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Heal Circle"))
        {
            PhotonNetwork.Instantiate("HealCirclePrefab", localPlayer.transform.position, Quaternion.identity);
        }
        if (GUILayout.Button("Spawn Glitch Shard"))
        {
            Vector3 spawn = localPlayer.transform.position + Vector3.up * 1.5f + localPlayer.transform.forward * 2f;
            PhotonNetwork.Instantiate("GlitchShard", spawn, Quaternion.identity);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void SwitchCharacter(BasePlayer player, BasePlayer.PlayerType role, string characterName)
    {
        PhotonView pv = player.photonView;
        PlayerCustomizer customizer = player.GetComponent<PlayerCustomizer>();
        BasePlayerStats stats = player.GetComponent<BasePlayerStats>();

        pv.RPC("SetPlayerType", RpcTarget.AllBuffered, role);

        if (customizer != null)
        {
            customizer.photonView.RPC("ApplyRoleAndCharacter", RpcTarget.AllBuffered, role, characterName);
        }

        CubePlayerBase charData = role == BasePlayer.PlayerType.Killer
            ? (CubePlayerBase)CharacterDatabase.GetKillerByName(characterName)
            : (CubePlayerBase)CharacterDatabase.GetSurvivorByName(characterName);

        if (charData != null && stats != null)
        {
            stats.photonView.RPC("ApplyCharacterStats", pv.Owner, charData.Health, charData.Speed, charData.Stamina);
        }

        PlayerAbilityManager pam = FindFirstObjectByType<PlayerAbilityManager>();
        if (pam != null && pv.IsMine)
        {
            pam.SendMessage("OnRoundStart", SendMessageOptions.DontRequireReceiver);
        }

        CUBEStatusText.CreateText($"<color=yellow>Debug Switched to {role}: {characterName}</color>");
    }

    #endregion

    #region Tab 4: Match & Round

    private void DrawMatchAndRoundTab()
    {
        RoundManager rm = FindFirstObjectByType<RoundManager>();

        GUILayout.Label("Round Lifecycle & Time Machine", headerStyle);

        if (rm == null)
        {
            GUILayout.Box("No RoundManager found in this scene.", boxStyle);
            return;
        }

        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label($"<b>Round State:</b> {(rm.roundActive ? "<color=#FF4444>ACTIVE ROUND</color>" : "<color=#44FF44>INTERMISSION</color>")}");
        GUILayout.Label($"<b>Clock Timer:</b> {rm.currentTime} seconds remaining");
        GUILayout.Label($"<b>Configured Times:</b> Round = {rm.roundTime}s  |  Intermission = 50s");
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Time Warp Controls (Master Client):", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Jump to 30s (Triggers Endgame Siren & Visuals)"))
        {
            rm.currentTime = 30;
        }
        if (GUILayout.Button("Jump to 5s"))
        {
            rm.currentTime = 5;
        }
        if (GUILayout.Button("Jump to 0s (End)"))
        {
            rm.currentTime = 0;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+30 Seconds")) rm.currentTime += 30;
        if (GUILayout.Button("-30 Seconds")) rm.currentTime = Mathf.Max(1, rm.currentTime - 30);
        if (GUILayout.Button("+60 Seconds")) rm.currentTime += 60;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Start Round Now"))
        {
            rm.photonView.RPC("StartRound", RpcTarget.AllBuffered, PhotonNetwork.Time + rm.roundTime);
        }
        if (GUILayout.Button("Force Intermission Now"))
        {
            rm.photonView.RPC("StartIntermission", RpcTarget.AllBuffered, rm.SyncedIntermissionSpawnPos, rm.SyncedIntermissionSpawnRot, PhotonNetwork.Time + 50);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Malice Karma System", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        BasePlayer localPlayer = GetLocalPlayer();
        BasePlayerStats stats = localPlayer != null ? localPlayer.GetComponent<BasePlayerStats>() : null;
        if (stats != null)
        {
            GUILayout.Label($"Your Current Malice: <b>{stats.malice}</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+5 Malice")) stats.GrantMalice(5);
            if (GUILayout.Button("+20 Malice")) stats.GrantMalice(20);
            if (GUILayout.Button("Reset Malice to 0")) stats.RemoveMalice(stats.malice);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Network Diagnostics & Scene Control", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        if (GUILayout.Button("🧹 Purge Stale Buffered RPCs (Clean Buffer)"))
        {
            PhotonNetwork.RemoveBufferedRPCs();
            if (rm != null)
                PhotonNetwork.RemoveRPCs(rm.photonView);
            CUBEStatusText.CreateText("<color=#44FF44>Cleaned all buffered room RPCs.</color>");
        }

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🔄 Load Menu Scene (Index 0)"))
        {
            SceneManager.LoadScene(0);
        }
        if (GUILayout.Button("🎮 Load Arena Scene (Index 1)"))
        {
            SceneManager.LoadScene(1);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    #endregion

    #region Tab 5: Visuals & Audio

    private void DrawVisualsAndAudioTab()
    {
        GUILayout.Label("URP Post-Processing & Screen Effects", headerStyle);

        CacheVolume();

        if (cachedVolume == null || cachedVolume.profile == null)
        {
            GUILayout.Box("No active URP Volume Profile found.", boxStyle);
        }
        else
        {
            GUILayout.BeginVertical(boxStyle);
            if (chromatic != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Chromatic Aberration: {chromatic.intensity.value:F2}", GUILayout.Width(200));
                chromatic.intensity.value = GUILayout.HorizontalSlider(chromatic.intensity.value, 0f, 1f);
                GUILayout.EndHorizontal();
            }

            if (lensDistortion != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Lens Distortion: {lensDistortion.intensity.value:F2}", GUILayout.Width(200));
                lensDistortion.intensity.value = GUILayout.HorizontalSlider(lensDistortion.intensity.value, -0.6f, 0.6f);
                GUILayout.EndHorizontal();
            }

            if (colorAdjustments != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Saturation: {colorAdjustments.saturation.value:F1}", GUILayout.Width(200));
                colorAdjustments.saturation.value = GUILayout.HorizontalSlider(colorAdjustments.saturation.value, -100f, 100f);
                GUILayout.EndHorizontal();
            }

            if (filmGrain != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Film Grain: {filmGrain.intensity.value:F1}", GUILayout.Width(200));
                filmGrain.intensity.value = GUILayout.HorizontalSlider(filmGrain.intensity.value, 0f, 30f);
                GUILayout.EndHorizontal();
            }

            if (vignette != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Vignette: {vignette.intensity.value:F2}", GUILayout.Width(200));
                vignette.intensity.value = GUILayout.HorizontalSlider(vignette.intensity.value, 0f, 1f);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            if (GUILayout.Button("Reset Post-Processing Defaults"))
            {
                if (chromatic != null) chromatic.intensity.value = 0f;
                if (lensDistortion != null) lensDistortion.intensity.value = 0f;
                if (colorAdjustments != null) colorAdjustments.saturation.value = 0f;
                if (filmGrain != null) filmGrain.intensity.value = 0f;
                if (vignette != null) vignette.intensity.value = 0f;
            }
            GUILayout.EndVertical();
        }

        GUILayout.Space(6);
        GUILayout.Label("Camera Shake Testing (EZCameraShake)", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Light Shake (Punch)"))
        {
            EZCameraShake.CameraShaker.Instance?.ShakeOnce(1.2f, 4f, 0.05f, 0.2f);
        }
        if (GUILayout.Button("Medium Shake (Shockwave)"))
        {
            EZCameraShake.CameraShaker.Instance?.ShakeOnce(3.0f, 5f, 0.08f, 0.7f);
        }
        if (GUILayout.Button("Heavy Shake (Earthquake)"))
        {
            EZCameraShake.CameraShaker.Instance?.ShakeOnce(6.0f, 7f, 0.12f, 1.2f);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Audio & Sound Testing", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        EndgameManager em = FindFirstObjectByType<EndgameManager>();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Play 30s Siren Clip"))
        {
            if (em != null && em.thirtySeconds != null)
                AudioSource.PlayClipAtPoint(em.thirtySeconds, Camera.main.transform.position);
        }
        if (GUILayout.Button("Play Clock Tick SFX"))
        {
            if (em != null && em.tick != null)
                AudioSource.PlayClipAtPoint(em.tick, Camera.main.transform.position);
        }
        if (GUILayout.Button("Play Punch Impact SFX"))
        {
            AudioClip clip = Resources.Load<AudioClip>("SFX/punch_hit");
            if (clip != null) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }
        if (GUILayout.Button("Play Phase Dash Whoosh SFX"))
        {
            AudioClip clip = Resources.Load<AudioClip>("SFX/punch_swing") ?? Resources.Load<AudioClip>("SFX/woosh");
            if (clip != null) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private Vignette vignette;

    private void CacheVolume()
    {
        if (volumeCached && cachedVolume != null) return;

        var volObj = GameObject.Find("Global Volume");
        if (volObj != null)
            cachedVolume = volObj.GetComponent<Volume>();
        else
            cachedVolume = FindFirstObjectByType<Volume>();

        if (cachedVolume != null && cachedVolume.profile != null)
        {
            cachedVolume.profile.TryGet(out chromatic);
            cachedVolume.profile.TryGet(out lensDistortion);
            cachedVolume.profile.TryGet(out colorAdjustments);
            cachedVolume.profile.TryGet(out filmGrain);
            cachedVolume.profile.TryGet(out vignette);
            volumeCached = true;
        }
    }

    #endregion

    #region Tab 6: Physics & Sandbox

    private void DrawPhysicsAndSandboxTab()
    {
        BasePlayer localPlayer = GetLocalPlayer();

        GUILayout.Label("Physics Simulation & Time Dilation", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"TimeScale: {Time.timeScale:F2}x", GUILayout.Width(130));
        if (GUILayout.Button("0.1x Slow-Mo")) { customTimeScale = 0.1f; Time.timeScale = 0.1f; }
        if (GUILayout.Button("0.5x Matrix")) { customTimeScale = 0.5f; Time.timeScale = 0.5f; }
        if (GUILayout.Button("1.0x Normal")) { customTimeScale = 1.0f; Time.timeScale = 1.0f; }
        if (GUILayout.Button("2.0x Fast")) { customTimeScale = 2.0f; Time.timeScale = 2.0f; }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        bool newZeroG = GUILayout.Toggle(zeroGravity, " 🛸 Zero-G Mode (Disable Global Gravity)");
        if (newZeroG != zeroGravity)
        {
            zeroGravity = newZeroG;
            Physics.gravity = zeroGravity ? Vector3.zero : new Vector3(0, -9.81f, 0);
        }
        GUILayout.EndVertical();

        GUILayout.Space(6);
        GUILayout.Label("Safe Ragdoll Teleportation (Joint Preserving):", headerStyle);

        if (localPlayer != null)
        {
            PlayerTeleportHandler teleporter = localPlayer.GetComponent<PlayerTeleportHandler>();
            MovementSC movement = localPlayer.GetComponentInChildren<MovementSC>();

            GUILayout.BeginVertical(boxStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Teleport Center (0, 3, 0)"))
            {
                teleporter?.Teleport(new Vector3(0f, 3f, 0f), Quaternion.identity, snapToGround: true);
            }
            if (GUILayout.Button("Teleport +5m Up"))
            {
                Transform hip = localPlayer.GetComponent<RagdollNetwork>()?.hip;
                Vector3 cur = hip != null ? hip.position : localPlayer.transform.position;
                teleporter?.Teleport(cur + Vector3.up * 5f, Quaternion.identity, snapToGround: false);
            }
            if (GUILayout.Button("Teleport +15m Skyfall"))
            {
                Transform hip = localPlayer.GetComponent<RagdollNetwork>()?.hip;
                Vector3 cur = hip != null ? hip.position : localPlayer.transform.position;
                teleporter?.Teleport(cur + Vector3.up * 15f, Quaternion.identity, snapToGround: false);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Killer Spawns"))
            {
                PhotonLauncher launcher = FindFirstObjectByType<PhotonLauncher>();
                if (launcher != null && launcher.killerSpawns != null && launcher.killerSpawns.Count > 0)
                    teleporter?.Teleport(launcher.killerSpawns[0].position, launcher.killerSpawns[0].rotation, true);
            }
            if (GUILayout.Button("Survivor Spawns"))
            {
                PhotonLauncher launcher = FindFirstObjectByType<PhotonLauncher>();
                if (launcher != null && launcher.survivorSpawns != null && launcher.survivorSpawns.Count > 0)
                    teleporter?.Teleport(launcher.survivorSpawns[0].position, launcher.survivorSpawns[0].rotation, true);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.Label("Ragdoll Joint Disruption & Launch:", headerStyle);

            GUILayout.BeginVertical(boxStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Collapse Ragdoll (control = 0)"))
            {
                Rig rig = localPlayer.GetComponentInChildren<Rig>();
                if (rig != null) rig.control = 0f;
            }
            if (GUILayout.Button("Restore Ragdoll (control = 1)"))
            {
                Rig rig = localPlayer.GetComponentInChildren<Rig>();
                if (rig != null) rig.control = 1f;
            }
            if (GUILayout.Button("Launch Forward Impulse"))
            {
                foreach (Rigidbody rb in localPlayer.GetComponentsInChildren<Rigidbody>())
                {
                    rb.AddForce(Camera.main.transform.forward * 400f, ForceMode.Impulse);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Speed Multiplier Override:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(Mathf.Approximately(customSpeedHack, 1f), "1x Normal", "Button")) customSpeedHack = 1f;
            if (GUILayout.Toggle(Mathf.Approximately(customSpeedHack, 1.5f), "1.5x Fast", "Button")) customSpeedHack = 1.5f;
            if (GUILayout.Toggle(Mathf.Approximately(customSpeedHack, 2.5f), "2.5x Hyper", "Button")) customSpeedHack = 2.5f;
            if (GUILayout.Toggle(Mathf.Approximately(customSpeedHack, 5f), "5x Sonic", "Button")) customSpeedHack = 5f;
            GUILayout.EndHorizontal();

            if (movement != null)
                movement.SpeedModifier = customSpeedHack;

            GUILayout.EndVertical();
        }

        GUILayout.Space(6);
        GUILayout.Label("Performance & Network Engine Metrics", headerStyle);

        GUILayout.BeginVertical(boxStyle);
        float fps = 1.0f / Mathf.Max(0.0001f, Time.smoothDeltaTime);
        long memMb = System.GC.GetTotalMemory(false) / (1024 * 1024);
        GUILayout.Label($"<b>FPS:</b> {fps:F0}   |   <b>Mem:</b> {memMb} MB   |   <b>Ping:</b> {PhotonNetwork.GetPing()}ms");
        GUILayout.Label($"<b>Room:</b> {PhotonNetwork.CurrentRoom?.Name ?? "None"}   |   <b>Players:</b> {PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}/{PhotonNetwork.CurrentRoom?.MaxPlayers ?? 0}   |   <b>Master:</b> {PhotonNetwork.IsMasterClient}");
        GUILayout.EndVertical();
    }

    #endregion

    #region Helpers

    private BasePlayer GetLocalPlayer()
    {
        if (PhotonLauncher.LocalPlayerInstance != null)
        {
            BasePlayer bp = PhotonLauncher.LocalPlayerInstance.GetComponent<BasePlayer>();
            if (bp != null) return bp;
        }

        BasePlayer[] all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            if (p.photonView != null && p.photonView.IsMine)
                return p;
        }
        return null;
    }

    private StatusEffectManager GetLocalStatusEffectManager()
    {
        BasePlayer localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            StatusEffectManager sem = localPlayer.GetComponent<StatusEffectManager>();
            if (sem == null)
            {
                sem = localPlayer.gameObject.AddComponent<StatusEffectManager>();
            }
            return sem;
        }
        return null;
    }

    private void ApplyEffectScoped(StatusEffectType type, int tier, float duration, float customMultiplier = 0f)
    {
        if (effectTargetScope == EffectTargetScope.LocalPlayer)
        {
            StatusEffectManager sem = GetLocalStatusEffectManager();
            sem?.ApplyEffect(type, tier, duration, customMultiplier);
        }
        else if (effectTargetScope == EffectTargetScope.SelectedTarget)
        {
            var all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
            if (all.Length > 0 && selectedPlayerIndex >= 0 && selectedPlayerIndex < all.Length)
            {
                PhotonView pv = all[selectedPlayerIndex]?.photonView;
                if (pv != null)
                {
                    if (pv.IsMine)
                    {
                        GetLocalStatusEffectManager()?.ApplyEffect(type, tier, duration, customMultiplier);
                    }
                    else
                    {
                        pv.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), pv.Owner, (byte)type, tier, duration, customMultiplier);
                    }
                }
            }
        }
        else if (effectTargetScope == EffectTargetScope.AllPlayers)
        {
            PhotonView[] allViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
            foreach (var pv in allViews)
            {
                if (pv.GetComponent<BasePlayer>() != null)
                {
                    pv.RPC(nameof(StatusEffectManager.RPC_ApplyStatusEffect), RpcTarget.All, (byte)type, tier, duration, customMultiplier);
                }
            }
        }
    }

    private void ClearEffectsScoped()
    {
        if (effectTargetScope == EffectTargetScope.LocalPlayer)
        {
            GetLocalStatusEffectManager()?.ClearAllEffects();
        }
        else if (effectTargetScope == EffectTargetScope.SelectedTarget)
        {
            var all = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
            if (all.Length > 0 && selectedPlayerIndex >= 0 && selectedPlayerIndex < all.Length)
            {
                PhotonView pv = all[selectedPlayerIndex]?.photonView;
                if (pv != null)
                {
                    if (pv.IsMine) GetLocalStatusEffectManager()?.ClearAllEffects();
                    else pv.RPC(nameof(StatusEffectManager.RPC_ClearAllStatusEffects), pv.Owner);
                }
            }
        }
        else if (effectTargetScope == EffectTargetScope.AllPlayers)
        {
            PhotonView[] allViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
            foreach (var pv in allViews)
            {
                if (pv.GetComponent<BasePlayer>() != null)
                {
                    pv.RPC(nameof(StatusEffectManager.RPC_ClearAllStatusEffects), RpcTarget.All);
                }
            }
        }
    }

    #endregion
}
