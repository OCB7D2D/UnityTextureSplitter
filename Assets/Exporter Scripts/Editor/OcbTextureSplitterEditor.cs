using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityTextureSplitter
{

    [ExecuteInEditMode] [CanEditMultipleObjects]
    [CustomEditor(typeof(OcbTextureSplitter))]
    public class OcbTextureSplitterEditor : Editor
    {

        float uiWidth = 100; // Update when the windows is repainted

        static readonly GUILayoutOption options = GUILayout.Height(EditorGUIUtility.singleLineHeight);

        static readonly string[] textureSplits = new string[] { "1", "2", "3", "4", "5", "6", "7", "8" };

        // Basic function running in the editor
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUI.BeginChangeCheck();

            // Update the UI width only on repaint events
            if (Event.current.type.Equals(EventType.Repaint))
                uiWidth = GUILayoutUtility.GetLastRect().width;

            var script = (OcbTextureSplitter)target;
            string path = AssetDatabase.GetAssetPath(target);

            GUILayout.Space(12);

            script.TextureSource = EditorGUILayout.ObjectField("Input",
                script.TextureSource, typeof(Texture2D), false, options) as Texture2D;

            CheckUncompressedTexture(script.TextureSource);
            CheckReadableTexture(script.TextureSource);

            script.TextureSplitWidth = EditorGUILayout.Popup("Splits Width", script.TextureSplitWidth, textureSplits);
            script.TextureSplitHeight = EditorGUILayout.Popup("Splits Height", script.TextureSplitHeight, textureSplits);

            script.TextureOffsetWidth = EditorGUILayout.IntSlider("Offset Width", script.TextureOffsetWidth, -1024, 1024);
            script.TextureOffsetHeight = EditorGUILayout.IntSlider("Offset Height", script.TextureOffsetHeight, -1024, 1024);

            GUILayout.Space(12);

            var lblBtn = "Create split textures";
            var output = Path.Join(Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path)) + "{0}{1}.png";
            if (GUILayout.Button(lblBtn, GUILayout.Height(48)))
            {
                CreateSplitTexture(script, output);
                // ExportPackedTexture(script, output);
                AssetDatabase.Refresh(); // Refresh first
            }

            // if (texture is Texture2D tex2d)
            // {
            //     GUILayout.Space(20);
            //     Rect rect = EditorGUILayout.GetControlRect(false, uiWidth);
            //     EditorGUI.DrawPreviewTexture(rect, tex2d);
            // }

            GUILayout.Space(20);

            // Note: doesn't support "undo"
            if (EditorGUI.EndChangeCheck())
                script.SetDirty();

        }

        private Color32 GetPixel(Color32[] from,
            int x, int y, int w, int h)
        {
            while (x >= w) x -= w;
            while (y >= h) y -= h;
            while (x < 0) x += w;
            while (y < 0) y += h;
            return from[y * h + x];
        }

        private Color32[] GetPixels(Texture2D src, Color32[] from,
            int off_x, int off_y, int width, int height)
        {
            var w = src.width;
            var h = src.height;
            var rv = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    rv[y * height + x] = GetPixel(
                        from, x + off_x, y + off_y, w, h);
            return rv;
        }

        private void CreateSplitTexture(OcbTextureSplitter script, string output)
        {
            if (script.TextureSource == null) return;
            var src = script.TextureSource;
            var width = src.width / (1 + script.TextureSplitWidth);
            var height = src.height / (1 + script.TextureSplitHeight);
            Texture2D packed = new Texture2D(width, height, TextureFormat.RGBA32, true);
            if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(script.TextureSource)) is TextureImporter importer)) return;
            var from = script.TextureSource.GetPixels32(0);
            for (int w = 0; w <= script.TextureSplitWidth; w++)
            {
                for (int h = 0; h <= script.TextureSplitHeight; h++)
                {
                    var x = w * width + script.TextureOffsetWidth;
                    var y = h * height + script.TextureOffsetHeight;
                    var to = GetPixels(src, from, x, y, width, height);
                    if (importer.textureType == TextureImporterType.NormalMap)
                        to = UnpackNormalPixels(to);
                    packed.SetPixels32(to);
                    packed.Apply(true, false);
                    var bytes = packed.EncodeToPNG();
                    var path = string.Format(output, w, h);
                    File.WriteAllBytes(path, bytes);
                }
            }
            AssetDatabase.Refresh();
            for (int w = 0; w <= script.TextureSplitWidth; w++)
            {
                for (int h = 0; h <= script.TextureSplitWidth; h++)
                {
                    var path = string.Format(output, w, h);
                    if (AssetImporter.GetAtPath(path) is TextureImporter result)
                    {
                        result.textureType = importer.textureType;
                        result.sRGBTexture = importer.sRGBTexture;
                        result.alphaSource = importer.alphaSource;
                        result.ignorePngGamma = importer.ignorePngGamma;
                        result.alphaIsTransparency = importer.alphaIsTransparency;
                        result.wrapMode = importer.wrapMode;
                        result.wrapModeU = importer.wrapModeU;
                        result.wrapModeV = importer.wrapModeV;
                        result.wrapModeW = importer.wrapModeW;
                        result.filterMode = importer.filterMode;
                        result.anisoLevel = importer.anisoLevel;
                        result.SaveAndReimport();
                    }
                }
            }

        }

        // Make sure we can read the texture on the GPU
        private static void CheckReadableTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (texture.isReadable) return;
            if (GUILayout.Button("Fix: Mark readable", GUILayout.Height(24)))
            {
                string path = AssetDatabase.GetAssetPath(texture);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    if (importer.isReadable) return;
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }
        }

        // Make sure we do not compress source textures
        // We only want to compress the final result
        // Otherwise we may do two lossy compressions
        private static void CheckUncompressedTexture(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    if (GUILayout.Button("Fix: Mark uncompressed", GUILayout.Height(24)))
                    {
                        importer.textureCompression =
                            TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        // Normals are stored in the green and alpha channel
        // This is due to normals using BC3 block compression
        // BC3 format stores color data using 5:6:5 color (5 bits red,
        // 6 bits green, 5 bits blue) and alpha data using one byte
        // Normals axis range from -1 to 1 while pixel data from 0 to 1
        // We therefore need to "unpack" those axes to get desired range
        // From there we can compute the remaining z vector value
        // Given that we know normal vectors have a length of 1
        // We then pack this info back into 0 to 1 color range
        public static Color32[] UnpackNormalPixels(Color32[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                pixels[i].r = pixels[i].a;
                //pixels[i].g = pixels[i].g;
                float x = pixels[i].r / 255f * 2 - 1;
                float y = pixels[i].g / 255f * 2 - 1;
                // Get `z` via `1 = sqrt(x^2 + y^2 + z^2)`
                float z = Mathf.Sqrt(1 - x * x - y * y);
                pixels[i].b = (byte)((z * 0.5f + 0.5f) * 255f + 0.5f);
                pixels[i].a = 1;
            }
            return pixels;
        }

        // Somehow normals in Texture2DArray seem to have xy axis switched
        // This has also been verified in the shader as shadows didn't match
        // I assume 7D2D uses old MicroSplat version that had this inverted!?
        public static Color32[] UnpackNormalPixelsSwitched(Color32[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                (pixels[i].g, pixels[i].a) = (pixels[i].a, pixels[i].g);
            }
            return UnpackNormalPixels(pixels);
        }

    }

}