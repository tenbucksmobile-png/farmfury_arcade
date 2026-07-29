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

        /// <summary>How many of the maze's 14 columns should be visible on screen at once — losing
        /// 2 off each side per playtest feedback, once CellSize=2 made "fit the whole board"
        /// framing read as too zoomed-out for a mobile landscape screen.</summary>
        public const int TargetVisibleColumns = 10;

        /// <summary>Those TargetVisibleColumns should fill this fraction of the screen's width; the
        /// remaining width shows GameplayBackdrop art at the edges. ArtWiringBuilder.
        /// WireGameplayBackdrop sizes the backdrop off these same two constants so the two stay
        /// consistent without duplicating the numbers.</summary>
        public const float WidthFillFraction = 0.7f;

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
            ApplyOrthographicSizeForAspect();

            var target = CharacterManager.Instance != null ? CharacterManager.Instance.ActiveCharacterObject : null;
            if (target == null)
            {
                return;
            }

            Vector3 desired = new Vector3(target.transform.position.x, target.transform.position.y, transform.position.z);
            desired = ClampToMazeBounds(desired);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        /// <summary>Derives orthographicSize purely from the camera's own live aspect ratio, so
        /// TargetVisibleColumns always fills WidthFillFraction of the screen width regardless of
        /// the actual device/window aspect. A fixed orthographicSize tuned by eye for one aspect
        /// (e.g. the Unity Editor Game View's often near-square default) looks completely different
        /// once actually run at a true mobile landscape aspect — this makes the framing correct
        /// everywhere instead of guessing a single constant. Recomputed every frame (one division)
        /// rather than once in Awake so a runtime window resize/orientation change stays correct
        /// too. Doesn't depend on maze/level data, only the camera's own aspect, so it's safe to run
        /// before a level has loaded.</summary>
        private void ApplyOrthographicSizeForAspect()
        {
            if (_camera == null || _camera.aspect <= 0f)
            {
                return;
            }

            float targetViewWidth = TargetVisibleColumns * TileMapRenderer.CellSize / WidthFillFraction;
            _camera.orthographicSize = targetViewWidth / (2f * _camera.aspect);
        }

        private Vector3 ClampToMazeBounds(Vector3 desired)
        {
            if (_tileMapRenderer == null || _tileMapRenderer.MazeWidth <= 0 || _tileMapRenderer.MazeHeight <= 0 || _camera == null)
            {
                return desired;
            }

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            float mazeWorldWidth = (_tileMapRenderer.MazeWidth - 1) * TileMapRenderer.CellSize;
            float mazeWorldHeight = (_tileMapRenderer.MazeHeight - 1) * TileMapRenderer.CellSize;

            float minX = halfWidth;
            float maxX = mazeWorldWidth - halfWidth;
            // When the camera's view is wider/taller than the maze itself (e.g. a "fit the whole
            // board" orthographic size), minX > maxX and a plain Clamp would collapse to minX —
            // pinning the camera off to one side instead of centering the board. Center on the
            // maze's own midpoint in that case instead.
            desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : mazeWorldWidth / 2f;

            float minY = halfHeight;
            float maxY = mazeWorldHeight - halfHeight;
            desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : mazeWorldHeight / 2f;
            return desired;
        }
    }
}
