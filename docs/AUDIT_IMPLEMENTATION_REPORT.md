# CUBE Audit — Implementation Report

**Baseline commit:** `c01043e78ec028f15aa9f0a1ae7f1bfc76c86e71` (`fix stuff`)
**Engine:** Unity `7000.0.0a10` (URP 17.7.0, PUN 2)
**Date:** 2026-11-07
**Scope:** Phases 1–5 of the approved audit plan
**Changes:** Uncommitted working-tree changes, ready for review

---

## Status Overview

| Phase | Scope | Status |
| :--- | :--- | :--- |
| 1 | Isolated critical correctness fixes | **Complete** |
| 2 | Health authority, damage modifiers, death reporting | **Complete** |
| 3 | Late-join state, map sync, idempotent scene setup | **Complete** |
| 4 | Chase audio and volume isolation | **Complete** |
| 5 | Performance, release hardening, documentation | **Complete** |
| — | Unity CLI compile verification | **Passing** |
| — | Multiplayer runtime verification | **Not performed** (see checklist) |

---

## Phase 1 — Critical Correctness

### Damage, healing, and status text

- **`Assets/Scripts/BasePlayerStats.cs`**
  - `Heal` now ignores dead players, so a heal cannot revive a corpse or race the respawn flow.
  - `GrantMalice`, `RemoveMalice`, and `AdjustMalice` update `CUBEStatusText` only when `photonView.IsMine`, preventing remote clients from overwriting the local status label.
  - Malice logging uses `Owner?.NickName` instead of dereferencing a possibly null owner.

- **`Assets/Resources/ScriptableObjects/Survivors/The Healer/Abilites/Ability Scripts/HealCircleFunction.cs`**
  - Skips players with `health <= 0` or missing `BasePlayerStats`.

### Networking and round state

- **`Assets/Scripts/ServerManager.cs`**
  - Purges the buffered `AdjustMalice` RPC with `RemoveBufferedRPCs(pv.ViewID, "AdjustMalice")` on round transitions.

- **`Assets/Scripts/RoundManager.cs`**
  - `AdjustMalice` is sent with `RpcTarget.All` so every client sees the same UI value.
  - The end-of-round `+1` malice grant now applies only to players still alive.
  - Added a `roundEnding` guard so `EndRound` can run once per round; the flag resets in `StartRound`, `StartIntermission`, and `SyncState`.

### Visuals, customization, and UI

- **`Assets/Scripts/PlayerCustomizer.cs`**
  - `RPC_SyncIntermissionColor` is `Others` instead of `OthersBuffered`; a stale color cannot be replayed to a later joiner.
  - Added null guards for `character`, `bodyMaterial`, and `accessories`.

- **`Assets/Scripts/PhotonLauncher.cs`**
  - `Resources.Load<Material>("PlayerMat")` is null-safe; fallback color is `FFFFFF`.

- **`Assets/Scripts/PlayerSizer.cs`**
  - Null-safe `Camera.main`, `camRefs.GetPlayer()`, and `rigObj` access in `OnEnable`, `OnRoundStart`, and `OnIntermissionStart`.

- **`Assets/Scripts/CursorManager.cs`**
  - Replaced the unguarded `Camera.main.transform.parent.parent` chain with `SetCameraInputEnabled(bool)`, which resolves `CameraInput` through the camera hierarchy, `PhotonLauncher.LocalPlayerInstance`, then `FindFirstObjectByType<CameraInput>()`.

- **`Assets/Scripts/PauseMenu.cs`**
  - Removed the stray `using NUnit.Framework;`.
  - Quit is always interactable in the Editor; added null guards for `pauseMenu`, `pauseMenuAnimator`, and `loader`.

- **`Assets/Scripts/PlayerUIHandler.cs`**
  - Cached health/stamina `Slider` and `TMP_Text` references.
  - Achievement subscription now happens once through `TrySubscribeToAchievements()` with an `achievementSubscribed` flag, instead of removing and adding handlers every frame.

- **`Assets/Scripts/Achievements/Rewards/{CosmeticReward,XpReward,CubeCoinsReward}.cs`**
  - Removed invalid `[CreateAssetMenu]` attributes from non-`ScriptableObject` classes (three console warnings eliminated).

- **`Assets/Scripts/PlayfabManager.cs`**
  - `FromHex` no longer throws on malformed input. It trims, accepts `#`, expands six-digit values, and falls back to white with a warning.

### LagSpike rework

- **`Assets/Resources/ScriptableObjects/Survivors/The Coder/Abilites/Ability Scripts/LagSpike.cs`**
  - No longer applies a self-centered slow. It snaps a ground point in front of the caster and starts a trap.

- **`Assets/Resources/ScriptableObjects/Survivors/The Coder/Abilites/Ability Scripts/LagSpikeTrapRunner.cs`** (new)
  - Owner-driven trap: scans every 0.2 s with `Physics.OverlapSphereNonAlloc`, applies `StatusEffectType.LagSpike` only to `PlayerType.Killer`, and broadcasts one `RPC_SpawnTrapVisual` to draw a temporary `LineRenderer` circle.
  - The visual material and renderer are destroyed with the trap.

- **`Assets/Scripts/BasePlayer.cs`**
  - Adds `LagSpikeTrapRunner` to every player in `Awake` so the visual RPC resolves on remotes.

---

## Phase 2 — Health Authority and Death Reporting

The owner's damage math is replicated instead of rewritten, so all copies compute the same result without adding a high-frequency health RPC.

- **`Assets/Scripts/BasePlayerStats.cs`**
  - Added `SetDamageResistance(float)` (owner broadcasts only on change) and `[PunRPC] RPC_SetDamageResistance`.
  - `TakeDamage` detects the transition to `health <= 0`, sends `RPC_ReportDeath` to `RpcTarget.MasterClient`, and guards it with `deathReported`.
  - `[PunRPC] RPC_ReportDeath` invokes `PlayerHitboxManager.CheckKillerWinCondition()` on the master.
  - `deathReported` resets in `ApplyCharacterStats` and `ResetToDefaultStats`.

- **`Assets/Scripts/StatusEffects/StatusEffectManager.cs`**
  - Added `syncedDamageTakenMultiplier` (default `1f`), `BroadcastDamageMultiplier()`, and `[PunRPC] RPC_SyncDamageMultiplier`.
  - `GetDamageTakenMultiplier()` returns the synced value on non-owners, so the victim and attacker agree on modified damage.

- **`Assets/Resources/ScriptableObjects/Survivors/The Healer/Abilites/Ability Scripts/LastStandRunner.cs`**
  - Writes through `myStats.SetDamageResistance(...)` rather than the field, so the value reaches remotes.

- **`Assets/Scripts/PlayerHitboxManager.cs`**
  - `CheckKillerWinCondition()` is now `public` so the death RPC can reach it.

- **`Assets/Scripts/DeathSC.cs`**
  - Added `public bool IsDead => died;`.
  - `Respawn()` yields out on non-owner copies; only the owner drives respawn.

The existing `RPC_OnHit` master-side health check remains as a fallback, so the new death report is additive rather than a replacement.

---

## Phase 3 — Late Join, Map Sync, and Scene Rebuilds

### Pull-based state sync

- **`Assets/Scripts/RoundManager.cs`**
  - `OnPlayerEnteredRoom` now delegates to `SendStateTo(Player)`, which includes `mapIndex`.
  - Added `[PunRPC] RequestState(PhotonMessageInfo)` so a joining client can ask for state **after** its scene has loaded. The master's initial push fires before that client has `SampleScene`, so the push alone was not reliable.
  - Added `HasSyncedState`.
  - `SyncState` now rebuilds the current map locally with `PhotonLauncher.ChooseMap(mapIndex)`.
  - Added `ReapplyStateUntilPlayersSpawn(...)`: re-applies roles/colors every 0.25 s for up to 3 s until every actor in the snapshot has spawned.
  - `OnJoinedRoom` early-returns when `roundActive` or `intermissionActive`, and clears buffered RPCs before bootstrap sends.
  - Added `OnLeftRoom` to clear round flags, timers, and `HasSyncedState`, preventing a stale `roundActive` from suppressing the next lobby.

### Map setup

- **`Assets/Scripts/PhotonLauncher.cs`**
  - Added `currentMapIndex` / `CurrentMapIndex`.
  - `ChooseMap` is idempotent for the same index: it re-populates spawns and schedules role teleports instead of destroying and rebuilding the map.
  - Invalid map indices are rejected.
  - The master chooses the map **before** the `LocalPlayerInstance == null` guard, so a master whose player has not spawned still creates the map.
  - Non-masters call `RequestStateAfterSceneLoad` from `Start`.
  - Map selection is guarded by `maps != null && maps.Length > 0`.

### Colors

- **`Assets/Scripts/PlayerCustomizer.cs`**
  - `ForceApplyIntermissionColor(string hex, bool force = false)`.

- **`Assets/Scripts/RoundManager.cs`**
  - Role-less players (spectators / the joining client) get `force: true`; players with a role keep their character skin and only store the hex for intermission.

---

## Phase 4 — Chase Audio and Volume Isolation

- **`Assets/Scripts/ChaseHandler.cs`**
  - Removed `GameObject.Find("Audio Source")` and the arbitrary `FindFirstObjectByType<AudioSource>()` fallback.
  - Creates a dedicated `ChaseMusicSource` child, loops it at `0.65` volume, and plays `CubeCharacterData.layer1`–`layer4` according to `currentMusicLayer`.
  - `StopMusic` stops only that dedicated source; it can no longer silence `MusicManager.roundSource`.
  - `OnIntermissionStart` stops the layer and disables the component; `OnRoundStart` re-evaluates the role.

- **`Assets/Scripts/StatusEffects/StatusEffectManager.cs`**
  - `CacheVolume` now clones the global `VolumeProfile` into a dedicated higher-priority `GameplayEffectVolume` with `weight = 0`.
  - Gameplay effects set weight to `1` only while a sensory effect is active, then back to `0`. This stops gameplay code from mutating the shared project asset and from fighting `EndgameManager`'s ramp.
  - The volume is created only on the local owner; remote copies do not allocate one.

---

## Phase 5 — Performance, Release Hardening, Documentation

### Performance

- **`Assets/Scripts/PlayerAbilityManager.cs`**
  - `ModifyAbilityCooldown` writes to a per-manager `Dictionary<Ability, int>` instead of mutating the shared `Ability` ScriptableObject.
  - All runtime cooldown reads (initial timer, input gate, tick, clamp, remaining, fill) go through `GetCooldown`.
  - Overrides clear when the moveset is rebuilt.

- **`Assets/Scripts/RagdollNetwork.cs`**
  - Receive buffers are sized from `keyBones.Count` and grow if a packet contains more bones.
  - Extra bone entries are consumed so the PUN stream stays aligned (the fixed 15-entry buffer previously desynchronized the rest of the packet).
  - The per-frame interpolation position/rotation arrays are now reused instead of allocated every `Update`.
  - `ApplySnapshot`/interpolation apply only the number of bones actually received.

- **`Assets/Scripts/ServerManager.cs`**
  - Added a static `Instance`.
  - `CachePlayers` resolves `BasePlayer` from the existing tag results and reuses the array; it no longer runs a second scene-wide `FindObjectsByType` every second.
  - `GetCurrentKiller` uses `Instance` when available.
  - `ChaseHandler` uses `ServerManager.Instance` instead of searching every frame.

- **`Assets/Scripts/WorldSpaceCanvasFinder.cs` / `Billboard.cs`**
  - Cache `Camera.main`, guard missing cameras, and stop assigning the canvas camera every frame.

### Null safety and lifecycle

- **`Assets/Scripts/PhotonMenuConnector.cs`**
  - Null-checks `LevelLoader` before loading `SampleScene`.

- **`Assets/Scripts/MusicManager.cs`**
  - Resolves `RoundManager` if unassigned, null-checks both audio sources and clips, and unsubscribes in `OnDestroy`.

- **`Assets/Scripts/ShopManager.cs`**
  - Guards `EventSystem.current` before selecting the purchase button.

### Release hardening

- **`Assets/Scripts/Debug/CubeDebugMenu.cs`**
  - Destroys its GameObject in non-Editor, non-`DEVELOPMENT_BUILD` builds, so the cheat menu cannot ship. Keeping the class compiled avoids a missing-script warning on the saved `CUBE Debug` object.
- **`Assets/Scripts/Noclipper.cs`**
  - Disables itself outside the Editor/development builds and null-checks the input action in `OnEnable`/`OnDisable`.

### Economy and documentation

- **`Assets/Scripts/CubeCoinRewardManager.cs`**
  - `MaxDistanceRewardsPerRound` corrected from `10000` segments to `100` segments (100 × 100 m = 10,000 m), matching the existing comment and docs.
  - Ability reward comment corrected from 15 CC to the actual 5 CC.
- **`AGENTS.md`, `docs/ACHIEVEMENTS_AND_EVENTS.md`, `docs/ARCHITECTURE.md`**
  - `GameplayEventBus.RaiseEvent(...)` corrected to `GameplayEventBus.Publish(...)`.
- **`docs/GAME_LOOP_AND_ECONOMY.md`**
  - Ability reward corrected to 5 CC / 100 CC cap.

---

## Verification

### Compile (Unity CLI)

```powershell
& "C:\Users\Dylan\AppData\Local\Unity\bin\unity.exe" cmd clear_console
& "C:\Users\Dylan\AppData\Local\Unity\bin\unity.exe" recompile
& "C:\Users\Dylan\AppData\Local\Unity\bin\unity.exe" cmd console_status
```

**Result after the final Phase 1–5 pass:**

- `unity recompile` exit code `0`.
- No error rows in the recompile TSV (`severity / code / file / line / column / message` header only).
- `console_status`: `compilationFailed:false`, `compiling:false`.
- `git diff --check`: clean.
- The Unity CLI prints its known warning: `This project's com.unity.pipeline uses the old command names. Run unity pipeline upgrade.` This is a tooling warning, not a script error.

The console error buffer still contains the pre-existing editor noise (the unavailable `script recompile_status` editor command and PlayFab editor DLL load messages). No error references a gameplay script changed in this pass.

### Not verified at runtime

No multiplayer play session was run. The following require two or more clients and should be checked before merging:

1. **Late join during a round:** join while the killer is chasing; confirm the joiner receives roles, timer, map, and spectator/intermission color, and does not spawn into an empty map.
2. **Late join during intermission:** confirm the countdown timestamp arrives once and the next round starts normally.
3. **Damage authority:** have the killer hit a survivor with LastStand/status modifiers active; both clients should compute the same health and the master should end the round on the killing blow.
4. **LagSpike:** cast as The Coder; confirm the circle appears on both clients, only the killer is slowed, and the visual disappears after the lifetime.
5. **Chase audio:** confirm the killer hears the layer changes and that round ambience resumes after the chase ends and during intermission.
6. **Map rebuild:** join a room with an existing map; confirm no duplicate map is instantiated and spawn points remain valid.

Known runtime limitations:

- Chase layers depend on `ChaseHandler.data`, which resolves from `GetEquippedKiller()`. The killer's own client is guaranteed to resolve it; a remote client only plays the layer if that character reference is also available there. This is a pre-existing data-replication limitation, not a new failure. The old behavior (silence, plus `StopMusic` killing round music) is fixed.
- `RagdollNetwork` still allocates per-packet snapshot arrays; the per-frame interpolation allocations and the fixed 15-bone stream bug were fixed. A full snapshot pool is a possible follow-up.
- `StatusEffectManager`'s cloned volume is local-owner only by design.

---

## Rollback

The baseline is `c01043e78ec028f15aa9f0a1ae7f1bfc76c86e71`. To discard the implementation:

```powershell
git checkout c01043e -- .
Remove-Item "Assets/Resources/ScriptableObjects/Survivors/The Coder/Abilites/Ability Scripts/LagSpikeTrapRunner.cs"
Remove-Item "Assets/Resources/ScriptableObjects/Survivors/The Coder/Abilites/Ability Scripts/LagSpikeTrapRunner.cs.meta"
```

Or, if the working tree contains nothing else you want to keep:

```powershell
git reset --hard c01043e
git clean -fd "Assets/Resources/ScriptableObjects/Survivors/The Coder/Abilites/Ability Scripts"
```

No commits were created.
