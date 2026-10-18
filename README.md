# CUBE

**CUBE** is an asymmetric multiplayer physics-ragdoll party/survival game developed by NanoDogs Studios (`LaymGlitched`) in **Unity 6 / 7000.0a10** using the **Universal Render Pipeline (URP)**, **Photon Unity Networking 2 (PUN 2)**, and **PlayFab**.

Players enter chaotic arenas as physics-driven cubes. An adaptive karma system (**Malice**) selects one player as the **Killer**, while the remaining players become **Survivors** fighting to survive timed rounds through agility, physics combat, and unique active abilities.

---

## 🤖 AI Agent & Developer Documentation

If you are an **AI coding assistant** (Antigravity, Cursor, Claude Code, GitHub Copilot) or a developer seeking an instant understanding of the codebase, please start here:

- 📖 **[AGENTS.md](file:///g:/Projects/physicsgamee/AGENTS.md)** — **The Primary Master Briefing**: Architecture overview, non-negotiable invariants (Strictly NO `dotnet build`, safe ragdoll teleportation, RPC buffering rules), directory of all core scripts, and copy-paste recipes for adding characters, abilities, achievements, and combat mechanics.

### Modular Deep-Dive Subsystem Guides

Detailed technical specifications are located in the [`docs/`](file:///g:/Projects/physicsgamee/docs/) directory:

1. **[docs/ARCHITECTURE.md](file:///g:/Projects/physicsgamee/docs/ARCHITECTURE.md)** — Engine settings, scene composition (`Menu.unity` vs `SampleScene.unity`), persistent singletons, player prefab hierarchy, and communication patterns.
2. **[docs/NETWORKING.md](file:///g:/Projects/physicsgamee/docs/NETWORKING.md)** — Photon PUN 2 topology, Master Client authority vs Owning Client simulation, RPC buffering lifecycle, and `RagdollNetwork` state streaming.
3. **[docs/PLAYER_PHYSICS_AND_COMBAT.md](file:///g:/Projects/physicsgamee/docs/PLAYER_PHYSICS_AND_COMBAT.md)** — Active ragdoll mechanics, the `PlayerTeleportHandler` pipeline, melee punch hitboxes, health/damage flow, death, and spectator mode.
4. **[docs/CHARACTERS_AND_ABILITIES.md](file:///g:/Projects/physicsgamee/docs/CHARACTERS_AND_ABILITIES.md)** — ScriptableObject schemas, `CharacterDatabase` dynamic resolution, full roster movesets (Glitch, Titan, Coder, DJ, Healer), and ability input execution.
5. **[docs/GAME_LOOP_AND_ECONOMY.md](file:///g:/Projects/physicsgamee/docs/GAME_LOOP_AND_ECONOMY.md)** — Intermission/Round state machine, Malice killer selection algorithm, Cube Coin (`CC`) reward schedule, and PlayFab shop integration.
6. **[docs/ACHIEVEMENTS_AND_EVENTS.md](file:///g:/Projects/physicsgamee/docs/ACHIEVEMENTS_AND_EVENTS.md)** — Decoupled `GameplayEventBus`, statistics aggregation, requirement trees, and multi-platform adapters (Steamworks / PlayFab).

---

## ⚡ Quick Rules & Invariants

- **Do Not Run `dotnet build`**: Never invoke `dotnet build` or command-line Unity builds unless explicitly directed.
- **Never Modify `transform.position` on Players Directly**: Always call `PlayerTeleportHandler.Teleport(...)` to avoid active ragdoll physics explosions.
- **Master Client Authority**: Only the Master Client controls round transitions, killer selection, and buffered RPC purges.
- **Character Asset Paths**: Custom character ScriptableObjects must live under `Assets/Resources/ScriptableObjects/Killers/{Name}/{Name} Type.asset` or `.../Survivors/{Name}/{Name} Type.asset`.
