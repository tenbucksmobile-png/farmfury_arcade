using System;
using UnityEngine;
using UnityEngine.UI;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.UI
{
    /// <summary>
    /// One tappable tile in the Level Select scroll grid. A single shared prefab, instantiated once
    /// per level slot by LevelSelectController — like CharacterSelectCard, navigation/hint-panel
    /// behaviour is injected as callbacks at Initialise time rather than the tile holding its own
    /// scene references, since the prefab has no access to scene-specific objects (the gameplay
    /// screen, the shared LockedHintPanel) until an instance actually exists in the scene.
    /// </summary>
    public class LevelTileController : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image tileBackground;
        [SerializeField] private Sprite spriteLocked;
        [SerializeField] private Sprite spriteUnlocked;
        [SerializeField] private Sprite sprite1Star;
        [SerializeField] private Sprite sprite2Stars;
        [SerializeField] private Sprite sprite3Stars;

        /// <summary>0-indexed, matching LevelData.levelNumber/GameManager.LoadLevel throughout the
        /// rest of the project — the UI-facing "Level N" text is LevelIndex + 1.</summary>
        public int LevelIndex { get; private set; }

        private Action<int> _onPlayRequested;
        private Action<int> _onLockedTapped;

        /// <summary>onPlayRequested/onLockedTapped are owned by LevelSelectController (it knows the
        /// gameplay screen reference and the shared LockedHintPanel instance — this tile doesn't
        /// need to). Called once per tile right after Instantiate.</summary>
        public void Initialise(int levelIndex, Action<int> onPlayRequested, Action<int> onLockedTapped)
        {
            LevelIndex = levelIndex;
            _onPlayRequested = onPlayRequested;
            _onLockedTapped = onLockedTapped;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnTileClicked);

            UpdateVisualState();
        }

        /// <summary>Re-reads UnlockProgression/save data and refreshes the tile's sprite — called by
        /// Initialise, and again by LevelSelectController whenever the screen re-opens (e.g. after
        /// completing a level) so newly-earned stars/unlocks show up without needing to rebuild the
        /// whole grid. There used to be a separate LockedIcon overlay here too, but
        /// LevelTile_Locked.png already bakes the padlock into the tile background art itself — the
        /// overlay was a leftover placeholder square (never wired to any sprite) sitting on top of
        /// the correctly-rendering background, which is what actually caused the "black tiles"
        /// bug (confirmed via LevelSelectTest's runtime diagnostic: sprite/colour/size on
        /// tileBackground were all correct — lockedIcon was the opaque unwired square on top). A
        /// level-number text overlay was removed the same way — the "?" already baked into
        /// LevelTile_unlocked-notplayed.png made a redundant "1" read as visual clutter.</summary>
        public void UpdateVisualState()
        {
            bool unlocked = UnlockProgression.IsLevelUnlocked(LevelIndex);
            int stars = UnlockProgression.GetStarsForLevel(LevelIndex);

            if (tileBackground == null)
            {
                return;
            }

            if (!unlocked)
            {
                SetBackground(spriteLocked);
                return;
            }

            switch (stars)
            {
                case 1:
                    SetBackground(sprite1Star, fallback: spriteUnlocked);
                    break;
                case 2:
                    SetBackground(sprite2Stars, fallback: spriteUnlocked);
                    break;
                case >= 3:
                    SetBackground(sprite3Stars, fallback: spriteUnlocked);
                    break;
                default:
                    SetBackground(spriteUnlocked);
                    break;
            }
        }

        /// <summary>Falls back to spriteUnlocked when a star-tier sprite is missing — matches the
        /// spec's own note that LevelTile_1Stars.png "may not exist yet"; still shows a level's
        /// unlocked/playable state correctly even if art for its exact star tier hasn't landed.
        /// If BOTH are null (real art not wired at all yet — Phase5ProjectBuilder builds this
        /// prefab with every state sprite field empty; ArtWiringBuilder fills them in separately),
        /// leaves tileBackground.sprite untouched rather than clearing it to null, so the tile
        /// keeps showing its plain placeholder colour instead of going blank.</summary>
        private void SetBackground(Sprite sprite, Sprite fallback = null)
        {
            var resolved = sprite != null ? sprite : fallback;
            if (resolved != null)
            {
                tileBackground.sprite = resolved;
            }
        }

        private void OnTileClicked()
        {
            if (UnlockProgression.IsLevelUnlocked(LevelIndex))
            {
                _onPlayRequested?.Invoke(LevelIndex);
            }
            else
            {
                _onLockedTapped?.Invoke(LevelIndex);
            }
        }
    }
}
