using System;
using UnityEngine;

namespace Pearls.Architecture
{
    // --- THE CONTRACTS (The Clean Part) ---

    public interface IGameService
    {
        GameState CurrentState { get; }
        void ChangeState(GameState newState);
        event Action<GameState> OnStateChanged;
    }

    public interface IPlayerService
    {
        int PlayerScore { get; }
        void AddScore(int amount);
    }

    public enum GameState { Boot, Lobby, Playing, Paused, GameOver }

    // --- THE LEGACY MONOLITH (The Messy Part) ---

    /// <summary>
    /// The "Refactored" GameManager. 
    /// Notice how it implements multiple specific interfaces.
    /// Consumers (UI/Player) should only ever see the Interface, not the Class.
    /// </summary>
    public class LegacyGameManager : MonoBehaviour, IGameService, IPlayerService
    {
        // Internal mess still exists here...
        private int _score;
        private GameState _state;

        public GameState CurrentState => _state;
        public int PlayerScore => _score;

        public event Action<GameState> OnStateChanged;

        public void ChangeState(GameState newState)
        {
            _state = newState;
            Debug.Log($"[GameManager] Logic: Changing state to {newState}");
            OnStateChanged?.Invoke(newState);
        }

        public void AddScore(int amount)
        {
            _score += amount;
            // Legacy code might still be doing something messy here...
            UpdateLegacyScoreboardUI();
        }

        private void UpdateLegacyScoreboardUI() { /* Spaghetti UI code */ }
    }

    // --- THE MODERN CONSUMER (The Success Part) ---

    public class ModernScoreDisplay : MonoBehaviour
    {
        // Dependency Injection target (VContainer/Zenject)
        private IPlayerService _playerService;

        public void Construct(IPlayerService playerService)
        {
            _playerService = playerService;
        }

        void Update()
        {
            if (_playerService != null)
            {
                // UI only knows about IPlayerService. It has NO idea 
                // that a multi-thousand-line GameManager even exists.
                // GetComponent<Text>().text = $"Score: {_playerService.PlayerScore}";
            }
        }
    }
}