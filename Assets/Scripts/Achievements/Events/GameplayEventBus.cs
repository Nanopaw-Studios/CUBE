using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Static pub/sub event bus supporting O(1) dictionary-dispatched event handling.
    /// Handles both generic gameplay events and decoupled stat-changed events.
    /// Domain-reload safe via <c>[RuntimeInitializeOnLoadMethod]</c>.
    /// </summary>
    public static class GameplayEventBus
    {
        private static Dictionary<GameplayEventType, Action<GameplayEvent>> _listeners = new();
        private static Action<StatChangedEvent> _statListeners;

        public static event Action<GameplayEvent> OnAnyGameplayEvent;

        public static void Subscribe(GameplayEventType type, Action<GameplayEvent> callback)
        {
            if (callback == null) return;
            if (_listeners.TryGetValue(type, out var existing))
                _listeners[type] = existing + callback;
            else
                _listeners[type] = callback;
        }

        public static void Unsubscribe(GameplayEventType type, Action<GameplayEvent> callback)
        {
            if (callback == null) return;
            if (_listeners.TryGetValue(type, out var existing))
            {
                existing -= callback;
                if (existing == null) _listeners.Remove(type);
                else _listeners[type] = existing;
            }
        }

        public static void SubscribeStatChanged(Action<StatChangedEvent> callback)
        {
            _statListeners += callback;
        }

        public static void UnsubscribeStatChanged(Action<StatChangedEvent> callback)
        {
            _statListeners -= callback;
        }

        public static void Publish(GameplayEvent evt)
        {
            if (_listeners.TryGetValue(evt.Type, out var handler))
            {
                try { handler?.Invoke(evt); }
                catch (Exception ex) { Debug.LogException(ex); }
            }

            try { OnAnyGameplayEvent?.Invoke(evt); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        public static void PublishStatChanged(StatChangedEvent evt)
        {
            try { _statListeners?.Invoke(evt); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _listeners = new Dictionary<GameplayEventType, Action<GameplayEvent>>();
            _statListeners = null;
            OnAnyGameplayEvent = null;
        }
    }
}
