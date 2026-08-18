using System;
using System.Collections.Generic;

namespace WindowsPeace.Core.Localization;

public sealed class EnglishStrings : ILanguagePack
{
    public Language Language => Language.English;

    public IReadOnlyDictionary<string, string> Strings { get; } = new Dictionary<string, string>
    {
        [Keys.Common.Next] = "Next",
        [Keys.Language.Title] = "Select language",

        [Keys.Shell.Back] = "Back",
        [Keys.Shell.Exit] = "Exit installer",

        [Keys.Recipe.Title] = "What to install?",
        [Keys.Recipe.Intro] = "Choose what to install. The list shows what is on this media.",
        [Keys.Recipe.ColName] = "Recipe",
        [Keys.Recipe.ColImage] = "Edition",
        [Keys.Recipe.ColSize] = "Size",
        [Keys.Recipe.ColWhat] = "What it is",
        [Keys.Recipe.TroubleNotFound] =
            "Windows Peace media not found: the wizard seems to be running from elsewhere. Nothing to install here.",
        [Keys.Recipe.TroubleDamaged] =
            "The media manifest is damaged and cannot be read. Nothing can be installed from this media - " +
            "it must be rewritten.",
        [Keys.Recipe.TroubleTooNew] =
            "This media was built by a newer version of Windows Peace. It cannot be used - a newer wizard is required.",
        [Keys.Recipe.TroubleNoRecipes] = "The media has no recipes: nothing to install.",

        [Keys.Confirm.Title] = "Review and confirm",
        [Keys.Confirm.Install] = "Install",
        [Keys.Confirm.WhatLabel] = "What we install",
        [Keys.Confirm.WhereLabel] = "Where we install",
        [Keys.Confirm.EffectLabel] = "What will happen",
        [Keys.Confirm.Wipe] =
            "The disk will be repartitioned. Everything on it will be lost permanently.",
        [Keys.Confirm.LostChoice] =
            "The selection was lost: go back and choose what to install and where.",

        [Keys.Progress.Title] = "Installation",
        [Keys.Progress.Explanation] =
            "This is where partitioning, Windows extraction, driver and bootloader install will run. " +
            "It arrives in the next step of the program's development. " +
            "For now the wizard writes nothing to disk.",

        [Keys.Done.Title] = "Done",
        [Keys.Done.Explanation] =
            "This will show the installation result and a restart button. " +
            "It arrives in the next step of the program's development.",

        [Keys.Error.Title] = "Windows Peace",
        [Keys.Error.Body] =
            "Windows Peace could not continue and will now close." + Environment.NewLine + Environment.NewLine +
            "This is ours to sort out, not yours: what happened is recorded in the work log.",
    };
}
