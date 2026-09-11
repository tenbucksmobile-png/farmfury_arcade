using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using FarmFuryArcade.Data;

namespace FarmFuryArcade.EditorTools
{
    /// <summary>Edit-mode (no Play mode, no manual screenshot) visual preview tool for cosmetic hat
    /// placement — closes the guess/rebuild/screenshot loop CLAUDE.md's hat-positioning history
    /// describes ("no visual Editor access this session... expect to nudge once actually seen").
    ///
    /// Composites each character's own sprite with each hat's matching sprite at that
    /// (character, hat) pair's real CharacterCosmeticRenderer.ResolveHatOffsetAndScale result — the
    /// exact same data path the game uses at runtime, just driven directly from the CosmeticData/
    /// CharacterData assets instead of a live Play session — and renders the result via a temporary
    /// offscreen Camera into one contact-sheet PNG.
    ///
    /// Three poses per character (Front/Left/Right — Up reuses the Down/"Front" sprite the same way
    /// most characters' own art does, so there's no separate Up pose here), stacked as 3 sub-rows
    /// under each hat's row: (hat x pose) rows, character columns. Right reproduces
    /// CharacterAnimator's own mirroring exactly — if a character has no dedicated Right art
    /// (CharacterData.hasDedicatedRightArt == false), its Left sprite is shown flipped, and the hat
    /// flips along with it (plus CosmeticData.mirrorLeftHatForRight for a hat with only Left art on
    /// an otherwise-dedicated-Right-art character, e.g. Ducky/Woolly's baseball cap) — same combined
    /// condition CharacterCosmeticRenderer.LateUpdate uses.
    ///
    /// A small magenta dot marks each cell's local (0,0) — the point hatOffset is measured from — so
    /// a hat sitting too high/low/left/right of it reads directly as "nudge hatOffset this way"
    /// without needing a live render for comparison.
    ///
    /// Safe to re-run after any hatOffset/hatScale/characterHatOverrides edit — every temp
    /// GameObject/Texture/Material is destroyed at the end of RenderAll, nothing is left in the
    /// open scene or saved to disk except the output PNG.</summary>
    public static class CosmeticPreviewRenderer
    {
        private const string CharacterDataFolder = "Assets/_Project/ScriptableObjects/Resources/Characters";
        private const string CosmeticDataFolder = "Assets/_Project/ScriptableObjects/Resources/Cosmetics";
        // NOT under Temp/ — Unity actively clears that folder (e.g. on a domain reload), which
        // silently deleted the first render this tool ever produced before it could be inspected.
        // CosmeticPreviews/ at the project root is untouched by Unity and not under Assets/, so it
        // never gets imported as a game asset either.
        private const string OutputPath = "CosmeticPreviews/cosmetic_preview_sheet.png";

        private const int CellPixelSize = 320;
        private const float OrthoHalfHeight = 1.05f; // world units — fits a ~1-unit-tall character plus a hat above it
        private const float OriginMarkerSize = 0.05f;

        private static readonly CharacterType[] AllCharacters =
        {
            CharacterType.Cluck, CharacterType.Bessie, CharacterType.Percy, CharacterType.Woolly,
            CharacterType.Ducky, CharacterType.Horace, CharacterType.Gerald, CharacterType.Billy,
        };

        // (pose label, walkAnimationFrames/hatFrames index — fixed [Up0,Up1,Down0,Down1,Left0,Left1,
        // Right0,Right1] order — isRight, so Right's body/hat mirroring can be resolved per-cell).
        private static readonly (string label, int frameIndex, bool isRight)[] Poses =
        {
            ("Front", 2, false), // Down0 — also stands in for Up, which most characters have no dedicated art for anyway
            ("Left", 4, false),
            ("Right", 6, true),
        };

        // (row label, CosmeticData asset filename without extension, true if the filename is
        // per-character ("CosmeticData_BaseballCap_{character}") rather than one shared asset).
        // Cowboy Hat moved from a single shared asset to per-character 2026-09-11 (see
        // CosmeticWiringBuilder.WireCowboyHats) — now wired for all 8 characters (Bessie's art
        // landed last, same session).
        private static readonly (string label, string assetName, bool perCharacter)[] HatRows =
        {
            ("Sombrero", "CosmeticData_sombrero_hat", false),
            ("Cowboy Hat", "CosmeticData_CowboyHat_{0}", true),
            ("Baseball Cap", "CosmeticData_BaseballCap_{0}", true),
            ("Chef Hat", "CosmeticData_chef_hat", false),
            ("Crown", "CosmeticData_crown", false),
        };

        [MenuItem("Farm Fury Arcade/Debug/Render Cosmetic Preview Sheet")]
        public static void RenderAll()
        {
            int cols = AllCharacters.Length;
            int subRowsPerHat = Poses.Length;
            int rows = HatRows.Length * subRowsPerHat;
            int width = CellPixelSize * cols;
            int height = CellPixelSize * rows;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();

            var lightGO = new GameObject("PreviewLight_TEMP");
            var light2D = lightGO.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.intensity = 1f;
            light2D.color = Color.white;

            var cameraGO = new GameObject("PreviewCamera_TEMP");
            var camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthoHalfHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            camera.nearClipPlane = -10f;
            camera.farClipPlane = 10f;
            camera.targetTexture = rt;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var characterGO = new GameObject("PreviewCharacter_TEMP");
            var charRenderer = characterGO.AddComponent<SpriteRenderer>();

            var markerGO = new GameObject("OriginMarker_TEMP");
            markerGO.transform.SetParent(characterGO.transform, false);
            var markerRenderer = markerGO.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = FarmFuryArcade.Utilities.PlaceholderSprite.Get(new Color(1f, 0f, 1f));
            markerRenderer.transform.localScale = Vector3.one * OriginMarkerSize;
            markerRenderer.sortingOrder = 5;

            var hatGO = new GameObject("PreviewHat_TEMP");
            hatGO.transform.SetParent(characterGO.transform, false);
            var hatRenderer = hatGO.AddComponent<SpriteRenderer>();
            hatRenderer.sortingOrder = 1;

            var log = new List<string>();
            int rowIndex = 0;

            foreach (var (hatLabel, assetNameTemplate, perCharacter) in HatRows)
            {
                foreach (var (poseLabel, frameIndex, isRight) in Poses)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        CharacterType character = AllCharacters[col];
                        CharacterData charData = AssetDatabase.LoadAssetAtPath<CharacterData>(
                            $"{CharacterDataFolder}/CharacterData_{character}.asset");

                        bool bodyMirrored = isRight && (charData == null || !charData.hasDedicatedRightArt);
                        charRenderer.sprite = ResolveCharacterSprite(charData, frameIndex);
                        charRenderer.flipX = bodyMirrored;
                        hatRenderer.enabled = false;

                        string assetName = perCharacter ? string.Format(assetNameTemplate, character) : assetNameTemplate;
                        CosmeticData hat = AssetDatabase.LoadAssetAtPath<CosmeticData>($"{CosmeticDataFolder}/{assetName}.asset");

                        string label = $"{hatLabel}/{poseLabel}";
                        if (hat != null && hat.hatFrames != null && hat.hatFrames.Length > frameIndex && hat.hatFrames[frameIndex] != null)
                        {
                            (Vector2 offset, float scale) = ResolveHatOffsetAndScale(hat, character);
                            hatGO.transform.localPosition = offset;
                            hatGO.transform.localScale = Vector3.one * scale;
                            hatRenderer.sprite = hat.hatFrames[frameIndex];
                            hatRenderer.flipX = bodyMirrored || (isRight && hat.mirrorLeftHatForRight);
                            hatRenderer.enabled = true;
                            log.Add($"[{label}] {character}: offset=({offset.x:F2},{offset.y:F2}) scale={scale:F2}");
                        }
                        else
                        {
                            log.Add($"[{label}] {character}: NOT FOUND (asset '{assetName}' missing or has no frame {frameIndex})");
                        }

                        camera.pixelRect = new Rect(col * CellPixelSize, (rows - 1 - rowIndex) * CellPixelSize, CellPixelSize, CellPixelSize);
                        camera.Render();
                    }
                    rowIndex++;
                }
            }

            RenderTexture.active = rt;
            var outputTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            outputTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            outputTex.Apply();
            RenderTexture.active = null;

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, outputTex.EncodeToPNG());

            Object.DestroyImmediate(outputTex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cameraGO);
            Object.DestroyImmediate(lightGO);
            Object.DestroyImmediate(characterGO);

            Debug.Log($"[CosmeticPreviewRenderer] Wrote {cols}x{rows} contact sheet ({width}x{height}px) to {fullPath}");
            Debug.Log("[CosmeticPreviewRenderer] Column order (left->right): " + string.Join(", ", AllCharacters));
            Debug.Log("[CosmeticPreviewRenderer] Row order (top->bottom): one hat block per row-group, each block sub-rowed Front/Left/Right, in this hat order: " + string.Join(", ", HatRowLabels()));
            foreach (var line in log)
            {
                Debug.Log("[CosmeticPreviewRenderer] " + line);
            }
        }

        private static IEnumerable<string> HatRowLabels()
        {
            foreach (var row in HatRows)
            {
                yield return row.label;
            }
        }

        private static Sprite ResolveCharacterSprite(CharacterData data, int frameIndex)
        {
            if (data == null)
            {
                return null;
            }
            if (data.walkAnimationFrames != null && data.walkAnimationFrames.Length > frameIndex && data.walkAnimationFrames[frameIndex] != null)
            {
                return data.walkAnimationFrames[frameIndex];
            }
            return data.portraitSprite;
        }

        /// <summary>Exact same lookup CharacterCosmeticRenderer.ResolveHatOffsetAndScale uses at
        /// runtime — kept as a separate copy here (this is an Editor-only assembly, that method is
        /// private on a MonoBehaviour in the runtime assembly) rather than reflecting into it.</summary>
        private static (Vector2 offset, float scale) ResolveHatOffsetAndScale(CosmeticData hat, CharacterType character)
        {
            if (hat.characterHatOverrides != null)
            {
                foreach (var overrideEntry in hat.characterHatOverrides)
                {
                    if (overrideEntry.character == character)
                    {
                        return (overrideEntry.hatOffset, overrideEntry.hatScale);
                    }
                }
            }
            return (hat.hatOffset, hat.hatScale);
        }
    }
}
