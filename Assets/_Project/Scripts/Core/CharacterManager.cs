using System;
using System.Collections;
using UnityEngine;
using FarmFuryArcade.Data;
using FarmFuryArcade.Gameplay;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Core
{
    /// <summary>
    /// Owns character spawning and swapping. Only one character GameObject exists at a time — a
    /// swap destroys the current one and instantiates the new prefab at the same grid cell/facing,
    /// which is why GridMovement/AbilityBase (both of which subscribe directly to InputController's
    /// static events) stay safe: there's never more than one live subscriber. Every robot's
    /// "player" reference (RobotBase.playerMovement) is a live lookup through
    /// ActiveCharacterObject rather than a cached reference, for the same reason.
    /// </summary>
    public class CharacterManager : Singleton<CharacterManager>
    {
        [SerializeField] private Transform characterParent;
        [SerializeField] private GameObject cluckPrefab;
        [SerializeField] private GameObject bessiePrefab;
        [SerializeField] private GameObject percyPrefab;
        [SerializeField] private GameObject woollyPrefab;
        [SerializeField] private GameObject duckyPrefab;
        [SerializeField] private GameObject horacePrefab;
        [SerializeField] private GameObject geraldPrefab;
        [SerializeField] private GameObject billyPrefab;

        private const float FadeInSeconds = 0.3f;

        public event Action<CharacterType, CharacterType> OnCharacterChanged;

        public CharacterType ActiveCharacter { get; private set; }
        public GameObject ActiveCharacterObject { get; private set; }

        public GameObject SpawnInitialCharacter(CharacterType type, Vector2Int gridPosition)
        {
            ActiveCharacter = type;
            ActiveCharacterObject = SpawnCharacterObject(type, gridPosition, Direction.None);
            GameManager.Instance?.SelectCharacter(type);
            ComboSystem.Instance?.RegisterInitialCharacter(type);
            return ActiveCharacterObject;
        }

        /// <summary>Unlock-gated only. Coin affordability never actually blocks a swap — the cost
        /// is 1 coin normally but 0 when the player has none (see CharacterSwapUI), so there's no
        /// "can't afford it" outcome to check for here.</summary>
        public bool CanSwapTo(CharacterType type)
        {
            return SaveManager.Instance != null && SaveManager.Instance.IsCharacterUnlocked(type);
        }

        public bool SwapCharacter(CharacterType newType)
        {
            if (ActiveCharacterObject == null || newType == ActiveCharacter || !CanSwapTo(newType))
            {
                return false;
            }

            var movement = ActiveCharacterObject.GetComponent<GridMovement>();
            Vector2Int gridPos = movement.CurrentGridPosition;
            Direction facing = movement.CurrentDirection;

            CharacterType previous = ActiveCharacter;
            Destroy(ActiveCharacterObject);

            ActiveCharacter = newType;
            ActiveCharacterObject = SpawnCharacterObject(newType, gridPos, facing);
            GameManager.Instance?.SelectCharacter(newType);

            OnCharacterChanged?.Invoke(previous, newType);
            ComboSystem.Instance?.RegisterCharacterSwap(previous, newType);
            return true;
        }

        public void ClearActiveCharacter()
        {
            if (ActiveCharacterObject != null)
            {
                Destroy(ActiveCharacterObject);
                ActiveCharacterObject = null;
            }
        }

        private GameObject SpawnCharacterObject(CharacterType type, Vector2Int gridPosition, Direction facing)
        {
            GameObject prefab = GetPrefabFor(type);
            if (prefab == null)
            {
                Debug.LogError($"[CharacterManager] No prefab assigned for {type}.");
                return null;
            }

            var tileMap = FindFirstObjectByType<TileMapRenderer>();
            Vector3 worldPos = tileMap.GridToWorld(gridPosition);
            var go = Instantiate(prefab, worldPos, Quaternion.identity, characterParent);

            var data = DataManager.Instance.GetCharacterData(type);
            if (data == null)
            {
                // Audit finding C8.1 — CharacterBase.Initialize already early-outs gracefully on a
                // null CharacterData, so this isn't a crash risk; without this log it was a silent
                // degrade (no speed/animation/ability setup applied) with zero signal to notice.
                Debug.LogWarning($"[CharacterManager] No CharacterData found for {type} — spawning uninitialized.");
            }
            go.GetComponent<CharacterBase>()?.Initialize(data);

            var movement = go.GetComponent<GridMovement>();
            if (movement != null && facing != Direction.None)
            {
                movement.QueueInputDirection(facing);
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                StartCoroutine(FadeIn(sr, FadeInSeconds));
            }

            return go;
        }

        /// <summary>Runs on CharacterManager, not the character itself, so a rapid second swap
        /// (destroying this character before the fade finishes) doesn't auto-stop it — every
        /// access must null-check sr first (Unity's == override detects the destroyed object).</summary>
        private static IEnumerator FadeIn(SpriteRenderer sr, float duration)
        {
            if (sr == null)
            {
                yield break;
            }

            Color c = sr.color;
            c.a = 0f;
            sr.color = c;

            float t = 0f;
            while (t < duration)
            {
                if (sr == null)
                {
                    yield break;
                }

                t += Time.deltaTime;
                c.a = Mathf.Clamp01(t / duration);
                sr.color = c;
                yield return null;
            }

            if (sr != null)
            {
                c.a = 1f;
                sr.color = c;
            }
        }

        private GameObject GetPrefabFor(CharacterType type)
        {
            return type switch
            {
                CharacterType.Cluck => cluckPrefab,
                CharacterType.Bessie => bessiePrefab,
                CharacterType.Percy => percyPrefab,
                CharacterType.Woolly => woollyPrefab,
                CharacterType.Ducky => duckyPrefab,
                CharacterType.Horace => horacePrefab,
                CharacterType.Gerald => geraldPrefab,
                CharacterType.Billy => billyPrefab,
                _ => null
            };
        }
    }
}
