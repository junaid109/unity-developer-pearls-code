using System;
using System.Collections.Generic;

namespace Pearls.Architecture
{
    /// <summary>
    /// A minimal, type-safe Event Bus. Publishers and subscribers never
    /// reference each other directly, only the event type itself.
    /// </summary>
    public class EventBus
    {
        private readonly Dictionary<Type, Delegate> _subscribers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var existing))
            {
                _subscribers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _subscribers[type] = handler;
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var existing)) return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _subscribers.Remove(type);
            else _subscribers[type] = remaining;
        }

        public void Publish<T>(T eventData)
        {
            if (_subscribers.TryGetValue(typeof(T), out var handler))
            {
                // Cast is safe: only Action<T> is ever stored under typeof(T).
                (handler as Action<T>)?.Invoke(eventData);
            }
        }
    }

    public readonly struct PlayerDamagedEvent
    {
        public readonly int Amount;
        public readonly int RemainingHealth;

        public PlayerDamagedEvent(int amount, int remainingHealth)
        {
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }
}