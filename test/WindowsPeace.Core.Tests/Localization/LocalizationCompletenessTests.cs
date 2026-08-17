using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WindowsPeace.Core.Localization;
using Xunit;

namespace WindowsPeace.Core.Tests.Localization;

public class LocalizationCompletenessTests
{
    [Fact] public void Оба_набора_несут_одинаковые_ключи()
    {
        var ru = new RussianStrings().Strings.Keys.OrderBy(k => k);
        var en = new EnglishStrings().Strings.Keys.OrderBy(k => k);
        Assert.Equal(ru, en);
    }

    [Fact] public void Каждый_объявленный_ключ_есть_в_обоих_наборах()
    {
        var ru = new RussianStrings().Strings;
        var en = new EnglishStrings().Strings;
        foreach (var key in DeclaredKeys())
        {
            Assert.True(ru.ContainsKey(key), $"нет русского перевода: {key}");
            Assert.True(en.ContainsKey(key), $"нет английского перевода: {key}");
        }
    }

    // Собирает значения всех const string во вложенных классах Keys рефлексией.
    private static IEnumerable<string> DeclaredKeys()
    {
        foreach (var group in typeof(Keys).GetNestedTypes())
            foreach (var field in group.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.IsLiteral && field.FieldType == typeof(string))
                    yield return (string)field.GetRawConstantValue()!;
    }
}
