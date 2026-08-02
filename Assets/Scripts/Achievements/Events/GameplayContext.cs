using System;
using System.Collections.Generic;

namespace CUBE.Achievements
{
    /// <summary>
    /// Immutable gameplay context container passed alongside <see cref="GameplayEvent"/>.
    /// Designed for complete safety: once created via <see cref="GameplayContextBuilder"/>, it cannot be mutated.
    /// Eliminates subtle pooled-reference mutation bugs while avoiding heap allocations per frame via typed fields.
    /// </summary>
    public sealed class GameplayContext
    {
        public static readonly GameplayContext Empty = new GameplayContextBuilder().Build();

        // ── Player Identification ─────────────────────────────────────
        public int SourceActorNumber { get; }
        public int TargetActorNumber { get; }

        // ── Typed Gameplay Data ───────────────────────────────────────
        public float NumericValue { get; }
        public float DamageAmount { get; }
        public float HealAmount { get; }
        public float Distance { get; }
        public float Duration { get; }
        public float AirTime { get; }
        public int CoinAmount { get; }
        public int MasteryLevel { get; }
        public int Health { get; }

        // ── Character & Role References ───────────────────────────────
        public CubePlayerBase SourceCharacter { get; }
        public CubePlayerBase TargetCharacter { get; }
        public BasePlayer.PlayerType SourceRole { get; }
        public BasePlayer.PlayerType TargetRole { get; }

        // ── Round & Match Context ─────────────────────────────────────
        public bool RoundWon { get; }
        public bool RoundLost { get; }
        public bool Survived { get; }
        public int RoundNumber { get; }

        // ── Boolean Flags ─────────────────────────────────────────────
        public bool WasSprinting { get; }
        public bool WasAirborne { get; }
        public bool TookDamage { get; }

        // ── Identifiers & Custom Key-Value Data ───────────────────────
        public string AbilityName { get; }
        public string ItemId { get; }
        public string CustomTag { get; }

        private readonly Dictionary<string, object> _customMetadata;

        internal GameplayContext(GameplayContextBuilder builder)
        {
            SourceActorNumber = builder.SourceActorNumber;
            TargetActorNumber = builder.TargetActorNumber;
            NumericValue      = builder.NumericValue;
            DamageAmount      = builder.DamageAmount;
            HealAmount        = builder.HealAmount;
            Distance          = builder.Distance;
            Duration          = builder.Duration;
            AirTime           = builder.AirTime;
            CoinAmount        = builder.CoinAmount;
            MasteryLevel      = builder.MasteryLevel;
            Health            = builder.Health;

            SourceCharacter   = builder.SourceCharacter;
            TargetCharacter   = builder.TargetCharacter;
            SourceRole        = builder.SourceRole;
            TargetRole        = builder.TargetRole;

            RoundWon          = builder.RoundWon;
            RoundLost         = builder.RoundLost;
            Survived          = builder.Survived;
            RoundNumber       = builder.RoundNumber;

            WasSprinting      = builder.WasSprinting;
            WasAirborne       = builder.WasAirborne;
            TookDamage        = builder.TookDamage;

            AbilityName       = builder.AbilityName;
            ItemId            = builder.ItemId;
            CustomTag         = builder.CustomTag;

            if (builder.CustomMetadata != null && builder.CustomMetadata.Count > 0)
                _customMetadata = new Dictionary<string, object>(builder.CustomMetadata);
        }

        public bool TryGetCustomValue<T>(string key, out T value)
        {
            if (_customMetadata != null && _customMetadata.TryGetValue(key, out object raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
    }
}
