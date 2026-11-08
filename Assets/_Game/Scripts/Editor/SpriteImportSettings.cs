using UnityEditor;
using UnityEngine;

namespace SuitMerge.EditorTools
{
    /// <summary>
    /// Ảnh mới thêm vào Assets/_Game/Images/ tự được cài chuẩn sprite UI:
    /// Sprite (Single), Full Rect, không mipmap, Bilinear, Clamp, nén chất lượng cao.
    /// Pixels Per Unit = 200 vì ảnh xuất @2x → "Set Native Size" trên Image cho đúng kích thước Figma.
    /// Chỉ áp dụng LẦN ĐẦU nhập ảnh — chỉnh tay trong Inspector sau đó sẽ được giữ nguyên.
    /// </summary>
    public class SpriteImportSettings : AssetPostprocessor
    {
        private const string ImagesFolder = "Assets/_Game/Images/";
        private const float PixelsPerUnitAt2x = 200f;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ImagesFolder)) return;

            var importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing) return; // đã nhập trước đó → tôn trọng chỉnh tay

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnitAt2x;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            // Nền gradient: không nén để tránh vệt màu (banding)
            importer.textureCompression = assetPath.Contains("/Backgrounds/")
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
