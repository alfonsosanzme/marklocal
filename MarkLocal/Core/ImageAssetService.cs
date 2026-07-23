using System;
using System.IO;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class ImageAssetService
{
    private readonly SettingsService _settings;

    public ImageAssetService(SettingsService settings)
    {
        _settings = settings;
    }

    public string BuildRelativePath(string documentDirectory, string fullImagePath)
    {
        string docDir = Path.GetFullPath(documentDirectory);
        string imgPath = Path.GetFullPath(fullImagePath);
        try
        {
            string relative = Path.GetRelativePath(docDir, imgPath);
            return relative.Replace('\\', '/');
        }
        catch
        {
            return imgPath.Replace('\\', '/');
        }
    }

    public string CopyImageToAssets(string documentDirectory, string sourceImagePath)
    {
        if (string.IsNullOrEmpty(documentDirectory))
            throw new InvalidOperationException("El documento debe estar guardado antes de copiar imágenes.");

        string assetsFolderName = string.IsNullOrWhiteSpace(_settings.Settings.AssetsFolderName)
            ? "assets"
            : _settings.Settings.AssetsFolderName;
        string assetsDir = Path.Combine(documentDirectory, assetsFolderName);
        Directory.CreateDirectory(assetsDir);

        string fileName = Path.GetFileName(sourceImagePath);
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string target = Path.Combine(assetsDir, fileName);
        int counter = 1;
        while (File.Exists(target))
        {
            target = Path.Combine(assetsDir, $"{baseName}-{counter}{extension}");
            counter++;
        }
        File.Copy(sourceImagePath, target);
        return target;
    }

    public string BuildMarkdownImageReference(string altText, string urlOrPath)
    {
        string safeUrl = urlOrPath.Replace(" ", "%20");
        string alt = string.IsNullOrWhiteSpace(altText)
            ? Path.GetFileNameWithoutExtension(urlOrPath)
            : altText;
        return $"![{alt}]({safeUrl})";
    }

    public static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico";
    }
}
