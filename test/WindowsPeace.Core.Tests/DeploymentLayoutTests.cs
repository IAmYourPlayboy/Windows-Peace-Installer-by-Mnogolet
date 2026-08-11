using System;
using System.IO;
using System.Text.RegularExpressions;
using WindowsPeace.Core.Selection;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Значения по умолчанию продублированы в коде и в схеме рецепта.
/// Дублирование допущено осознанно — шаг А не читает рецепт, — но расхождение
/// должно ломать сборку, а не всплывать через полгода на чужой машине.
/// </summary>
public class DeploymentLayoutTests
{
    private static string SchemaText()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WindowsPeace.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, "contract", "recipe.schema.json"));
    }

    private static int DefaultOf(string property)
    {
        var pattern = "\"" + property + "\"\\s*:\\s*\\{[^}]*?\"default\"\\s*:\\s*(\\d+)";
        var match = Regex.Match(SchemaText(), pattern, RegexOptions.Singleline);
        Assert.True(match.Success, $"В схеме не найдено значение по умолчанию для {property}");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Размер_EFI_совпадает_со_схемой() => Assert.Equal(DefaultOf("espMB"), DeploymentLayout.Default.EspMb);

    [Fact]
    public void Размер_MSR_совпадает_со_схемой() => Assert.Equal(DefaultOf("msrMB"), DeploymentLayout.Default.MsrMb);

    [Fact]
    public void Размер_раздела_восстановления_совпадает_со_схемой()
        => Assert.Equal(DefaultOf("recoveryMB"), DeploymentLayout.Default.RecoveryMb);

    [Fact]
    public void Минимальный_размер_раздела_совпадает_со_схемой()
    {
        var match = Regex.Match(SchemaText(), "\"windowsSizeGB\"\\s*:\\s*\\{[^}]*?\"minimum\"\\s*:\\s*(\\d+)", RegexOptions.Singleline);
        Assert.True(match.Success);

        var minimumGib = ulong.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(SelectionRules.MinimumWindowsPartitionBytes, minimumGib * 1024UL * 1024UL * 1024UL);
    }
}
