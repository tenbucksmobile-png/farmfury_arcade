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

        /// <summary>Each maze tile should read on screen at the same size, relative to the screen's
        /// shorter dimension, as a block did in the mobile puzzle-game sizing reference (7/31/26) —
        /// there a block was ~10.5% of the screen's shorter side (that game is portrait, so width;
        /// this game is landscape-locked, so height). Deriving zoom from the shorter dimension keeps
        /// tile size aspect-independent: every device sees the same tile size, and only the number
        /// of columns visible varies with how wide the device is (same "extra width shows
        /// GameplayBackdrop art at the edges" idea the old width-driven formula had, just now
        /// applied to the axis that's actually scarce on a landscape screen showing a maze that's
        /// taller than it is wide).</summary>
        public const float CellScreenHeightFraction = 0.105f;

        /// <summary>Widest landscape aspect ratio worth planning backdrop coverage for (e.g. an
        /// ultra-wide phone). Used only by ArtWiringBuilder.WireGameplayBackdrop to size the
        /// backdrop generously enough that no real device's extra width outruns it.</summary>
        public const float MaxSupportedAspect = 2.4f;

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

        /// <summary>Derives orthographicSize from CellScreenHeightFraction alone — deliberately NOT
        /// from the camera's aspect ratio. Screen height is the axis that's actually scarce for a
        /// 14x16 maze on a landscape screen (the old aspect-driven formula controlled width instead,
        /// which was already close to fully visible even on a narrow-ish landscape aspect, leaving
        /// height uncontrolled and only ~50% of the maze's rows ever on screen at once). Pinning
        /// tile size to screen height means every device shows the same tile size and the same
        /// number of rows; wider devices simply reveal more columns (and more GameplayBackdrop bleed
        /// at the sides) rather than changing zoom. Recomputed every frame in case of a runtime
        /// window resize, though the result doesn't actually depend on the frame's aspect.</summary>
        private void ApplyOrthographicSizeForAspect()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.orthographicSize = TileMapRenderer.CellSize / (2f * CellScreenHeightFraction);
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
