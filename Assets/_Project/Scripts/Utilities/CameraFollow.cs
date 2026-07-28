using UnityEngine;
using FarmFuryArcade.Core;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Attach to Main Camera alongside CameraShake. Follows the active character, read
    /// live via CharacterManager.Instance.ActiveCharacterObject every frame rather than cached —
    /// swapping characters destroys/recreates that GameObject, same convention
    /// RobotBase.playerMovement uses for the same reason. Clamps so the camera never shows past
    /// the maze edges (using TileMapRenderer.MazeWidth/MazeHeight, which are 0 before a level is
    /// loaded — ClampToMazeBounds no-ops in that case).
    ///
    /// Runs in LateUpdate with the default script execution order (0), which CameraShake now
    /// deliberately runs after ([DefaultExecutionOrder(100)]) so its jitter is added on top of the
    /// follow position this script sets each frame, instead of the two components fighting over
    /// transform.position.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.15f;

        private Camera _camera;
        private TileMapRenderer _tileMapRenderer;
        private Vector3 _velocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            // GameManagers is a persistent singleton GameObject (unlike the character, which is
            // destroyed/recreated on every swap) — safe to cache once here.
            var managersGO = GameObject.Find("GameManagers");
            _tileMapRenderer = managersGO != null ? managersGO.GetComponent<TileMapRenderer>() : null;
        }

        private void LateUpdate()
        {
            var target = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
            if (target == null)
            {
                return;
            }

            Vector3 desired = new Vector3(target.transform.position.x, target.transform.position.y, transform.position.z);
            desired = ClampToMazeBounds(desired);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        private Vector3 ClampToMazeBounds(Vector3 desired)
        {
            if (_tileMapRenderer == null || _tileMapRenderer.MazeWidth <= 0 || _tileMapRenderer.MazeHeight <= 0 || _camera == null)
            {
                return desired;
            }

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            float minX = halfWidth;
            float maxX = _tileMapRenderer.MazeWidth - 1 - halfWidth;
            // When the camera's view is wider/taller than the maze itself (e.g. a "fit the whole
            // board" orthographic size), minX > maxX and a plain Clamp would collapse to minX —
            // pinning the camera off to one side instead of centering the board. Center on the
            // maze's own midpoint in that case instead.
            desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : (_tileMapRenderer.MazeWidth - 1) / 2f;

            float minY = halfHeight;
            float maxY = _tileMapRenderer.MazeHeight - 1 - halfHeight;
            desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : (_tileMapRenderer.MazeHeight - 1) / 2f;
            return desired;
        }
    }
}
