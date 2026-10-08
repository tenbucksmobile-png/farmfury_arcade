using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmFuryArcade.Core
{
    /// <summary>Web demo (YouTube Playables requirement): the game must be playable at every aspect
    /// ratio from 9:32 to 32:9 and must not lock orientation, but letterboxing/pillarboxing is
    /// allowed. The game and all its screens were designed for landscape, so instead of a separate
    /// portrait layout the whole game (world + UI) is drawn inside a centred box whose aspect is
    /// clamped to [MinAspect, MaxAspect]; outside it a second camera paints plain bars.
    ///
    /// - Main Camera's viewport rect is set to that box every time the window size changes.
    /// - The root UI Canvas is switched from Screen Space Overlay (which always covers the whole
    ///   window) to Screen Space Camera on the Main Camera, so the UI lives inside the same box.
    ///   CameraShake/CameraFollow move the camera, and a camera-space canvas moves with it, so the
    ///   UI stays still on screen.
    ///
    /// Added automatically to Game.unity's Main Camera when that scene loads (no scene edit needed).
    /// </summary>
    public class AspectLetterbox : MonoBehaviour
    {
        /// <summary>16:10 - narrow enough for every landscape screen layout to fit.</summary>
        public const float MinAspect = 1.6f;
        /// <summary>Matches CameraFollow.MaxSupportedAspect, the widest the backdrops were sized for.</summary>
        public const float MaxAspect = 2.4f;

        private static readonly Color BarColor = new Color(0.137f, 0.122f, 0.125f); // #231F20, the web page colour

        private Camera _camera;
        private Camera _barsCamera;
        private int _lastWidth, _lastHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (scene, mode) => AttachIfGameScene();
            AttachIfGameScene();
        }

        private static void AttachIfGameScene()
        {
            var cam = Camera.main;
            var canvasGO = GameObject.Find("Canvas");
            if (cam == null || canvasGO == null || cam.GetComponent<AspectLetterbox>() != null)
            {
                return;
            }
            cam.gameObject.AddComponent<AspectLetterbox>();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            var canvasGO = GameObject.Find("Canvas");
            var canvas = canvasGO != null ? canvasGO.GetComponent<Canvas>() : null;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _camera;
                canvas.planeDistance = 1f;      // in front of the maze (camera sits at z = -10)
                canvas.sortingOrder = 1000;     // above every sprite sorting order in the maze
            }

            var bars = new GameObject("LetterboxBarsCamera");
            DontDestroyOnLoad(bars);
            bars.hideFlags = HideFlags.DontSave;
            _barsCamera = bars.AddComponent<Camera>();
            _barsCamera.clearFlags = CameraClearFlags.SolidColor;
            _barsCamera.backgroundColor = BarColor;
            _barsCamera.cullingMask = 0;
            _barsCamera.depth = _camera.depth - 1;
            _barsCamera.orthographic = true;

            Apply();
        }

        private void OnDestroy()
        {
            if (_barsCamera != null)
            {
                Destroy(_barsCamera.gameObject);
            }
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            if (_lastWidth <= 0 || _lastHeight <= 0)
            {
                return;
            }
            float aspect = (float)_lastWidth / _lastHeight;
            Rect rect = new Rect(0f, 0f, 1f, 1f);
            if (aspect < MinAspect)
            {
                float h = aspect / MinAspect;           // bars above and below
                rect = new Rect(0f, (1f - h) / 2f, 1f, h);
            }
            else if (aspect > MaxAspect)
            {
                float w = MaxAspect / aspect;           // bars left and right
                rect = new Rect((1f - w) / 2f, 0f, w, 1f);
            }
            _camera.rect = rect;
        }
    }
}
