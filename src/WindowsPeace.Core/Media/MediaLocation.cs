using System;
using System.IO;

namespace WindowsPeace.Core.Media;

/// <summary>
/// Найденный носитель Windows Peace. Опознание идёт по наличию файла описи,
/// а не по его содержимому: испорченная опись не делает носитель чужим,
/// и предлагать установку на него всё равно нельзя.
/// </summary>
public sealed class MediaLocation
{
    public MediaLocation(string root)
    {
        Root = root;
        ManifestPath = Path.Combine(root, MediaLayout.ManifestFileName);
    }

    /// <summary>Корень раздела, где лежит опись. Все пути описи считаются отсюда.</summary>
    public string Root { get; }

    public string ManifestPath { get; }

    /// <summary>Полный путь к тому, что опись задаёт своим относительным путём.</summary>
    public string Resolve(string relativePath) => Path.Combine(Root, relativePath);

    /// <summary>Прочитать опись. Все отказы возвращаются исходом, а не исключением.</summary>
    public MediaManifestResult Load(ITextFileReader reader)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (!reader.Exists(ManifestPath))
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged, null,
                "Опись носителя исчезла между поиском и чтением.");
        }

        try
        {
            return MediaManifestReader.Read(reader.ReadAllText(ManifestPath));
        }
        catch (IOException error)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged, null,
                "Опись носителя не читается: " + error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged, null,
                "Доступ к описи носителя закрыт: " + error.Message);
        }
    }
}
