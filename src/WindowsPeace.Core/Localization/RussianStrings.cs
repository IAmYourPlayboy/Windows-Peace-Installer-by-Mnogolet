using System.Collections.Generic;

namespace WindowsPeace.Core.Localization;

public sealed class RussianStrings : ILanguagePack
{
    public Language Language => Language.Russian;

    public IReadOnlyDictionary<string, string> Strings { get; } = new Dictionary<string, string>
    {
        [Keys.Common.Next] = "Далее",
        [Keys.Language.Title] = "Выберите язык",
    };
}
