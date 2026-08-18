using System.Collections.Generic;

namespace WindowsPeace.Core.Localization;

public interface ILanguagePack
{
    Language Language { get; }
    IReadOnlyDictionary<string, string> Strings { get; }
}
