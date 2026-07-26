using UnityEngine;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Generic MonoBehaviour singleton. Instance is expected to already exist in the
    /// scene (on the GameManagers GameObject) rather than being lazily spawned.</summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
