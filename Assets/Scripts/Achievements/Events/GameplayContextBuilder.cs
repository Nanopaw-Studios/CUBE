using System.Collections.Generic;

namespace CUBE.Achievements
{
    /// <summary>
    /// Fluent builder for constructing immutable <see cref="GameplayContext"/> instances.
    /// Provides a clean API for gameplay code without exposing public mutability.
    /// </summary>
    public class GameplayContextBuilder
    {
        public int SourceActorNumber { get; private set; } = -1;
        public int TargetActorNumber { get; private set; } = -1;

        public float NumericValue { get; private set; }
        public float DamageAmount { get; private set; }
        public float HealAmount { get; private set; }
        public float Distance { get; private set; }
        public float Duration { get; private set; }
        public float AirTime { get; private set; }
        public int CoinAmount { get; private set; }
        public int MasteryLevel { get; private set; }
        public int Health { get; private set; }
        public UnityEngine.Vector3 Velocity { get; private set; }
        public float Speed { get; private set; }

        public CubePlayerBase SourceCharacter { get; private set; }
        public CubePlayerBase TargetCharacter { get; private set; }
        public BasePlayer.PlayerType SourceRole { get; private set; } = BasePlayer.PlayerType.Survivor;
        public BasePlayer.PlayerType TargetRole { get; private set; } = BasePlayer.PlayerType.Survivor;

        public bool RoundWon { get; private set; }
        public bool RoundLost { get; private set; }
        public bool Survived { get; private set; }
        public int RoundNumber { get; private set; }

        public bool WasSprinting { get; private set; }
        public bool WasAirborne { get; private set; }
        public bool TookDamage { get; private set; }

        public string AbilityName { get; private set; }
        public string ItemId { get; private set; }
        public string CustomTag { get; private set; }

        public Dictionary<string, object> CustomMetadata { get; private set; }

        public GameplayContextBuilder SetSourceActor(int actorNr) { SourceActorNumber = actorNr; return this; }
        public GameplayContextBuilder SetTargetActor(int actorNr) { TargetActorNumber = actorNr; return this; }

        public GameplayContextBuilder SetNumericValue(float val) { NumericValue = val; return this; }
        public GameplayContextBuilder SetDamage(float val) { DamageAmount = val; return this; }
        public GameplayContextBuilder SetHeal(float val) { HealAmount = val; return this; }
        public GameplayContextBuilder SetDistance(float val) { Distance = val; return this; }
        public GameplayContextBuilder SetDuration(float val) { Duration = val; return this; }
        public GameplayContextBuilder SetAirTime(float val) { AirTime = val; return this; }
        public GameplayContextBuilder SetCoins(int val) { CoinAmount = val; return this; }
        public GameplayContextBuilder SetMastery(int val) { MasteryLevel = val; return this; }
        public GameplayContextBuilder SetHealth(int val) { Health = val; return this; }
        public GameplayContextBuilder SetVelocity(UnityEngine.Vector3 vel) { Velocity = vel; Speed = vel.magnitude; return this; }
        public GameplayContextBuilder SetSpeed(float speed) { Speed = speed; return this; }

        public GameplayContextBuilder SetSourceCharacter(CubePlayerBase character) { SourceCharacter = character; return this; }
        public GameplayContextBuilder SetTargetCharacter(CubePlayerBase character) { TargetCharacter = character; return this; }
        public GameplayContextBuilder SetSourceRole(BasePlayer.PlayerType role) { SourceRole = role; return this; }
        public GameplayContextBuilder SetTargetRole(BasePlayer.PlayerType role) { TargetRole = role; return this; }

        public GameplayContextBuilder SetRoundResult(bool won, bool lost, bool survived, int number = 0)
        {
            RoundWon = won; RoundLost = lost; Survived = survived; RoundNumber = number; return this;
        }

        public GameplayContextBuilder SetFlags(bool sprinting = false, bool airborne = false, bool tookDamage = false)
        {
            WasSprinting = sprinting; WasAirborne = airborne; TookDamage = tookDamage; return this;
        }

        public GameplayContextBuilder SetAbilityName(string name) { AbilityName = name; return this; }
        public GameplayContextBuilder SetItemId(string id) { ItemId = id; return this; }
        public GameplayContextBuilder SetCustomTag(string tag) { CustomTag = tag; return this; }

        public GameplayContextBuilder AddCustomMetadata(string key, object value)
        {
            CustomMetadata ??= new Dictionary<string, object>();
            CustomMetadata[key] = value;
            return this;
        }

        public GameplayContext Build() => new GameplayContext(this);
    }
}
