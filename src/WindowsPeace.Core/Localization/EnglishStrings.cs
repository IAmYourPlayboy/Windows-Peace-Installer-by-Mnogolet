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

        [Keys.Disk.Title] = "Where to install Windows Peace?",
        [Keys.Disk.ColName] = "Name",
        [Keys.Disk.ColSize] = "Size",
        [Keys.Disk.ColFree] = "Free",
        [Keys.Disk.ColType] = "Type",
        [Keys.Disk.ColState] = "State",
        [Keys.Disk.Refresh] = "Refresh",
        [Keys.Disk.Cancel] = "Stop",
        [Keys.Disk.Create] = "Create",
        [Keys.Disk.Delete] = "Delete",
        [Keys.Disk.Format] = "Format",
        [Keys.Disk.Extend] = "Extend",
        [Keys.Disk.Details] = "Details",
        [Keys.Disk.LoadDriver] = "Load driver",
        [Keys.Disk.StatusEnumerating] = "Scanning disks…",
        [Keys.Disk.StatusInspecting] = "Inspecting disk {0} of {1}…",
        [Keys.Disk.StatusLocating] = "Looking for boot media…",
        [Keys.Disk.ErrorCancelled] = "Disk scan stopped. Press \"Refresh\" to try again.",
        [Keys.Disk.FreeSpace] = "Unallocated space",
        [Keys.Disk.Partition] = "Partition {0}",
        [Keys.Disk.PartitionLabel] = "Partition {0}: {1}",
        [Keys.Disk.NoteMedia] = "Boot media - cannot install here",
        [Keys.Disk.NoteSystem] = "The current system runs here",
        [Keys.Disk.NoteEmpty] = "Empty",
        [Keys.Disk.NotePartitions] = "Partitions: {0}",

        [Keys.PartitionType.Efi] = "EFI system",
        [Keys.PartitionType.Msr] = "MSR",
        [Keys.PartitionType.Recovery] = "Recovery",
        [Keys.PartitionType.Basic] = "Basic",
        [Keys.PartitionType.Unknown] = "Unknown",

        [Keys.Content.WindowsAndFiles] = "Windows and user files",
        [Keys.Content.Windows] = "Windows",
        [Keys.Content.UserFiles] = "User files",

        [Keys.Content.NotInspected.Cancelled] = "Inspection cancelled",
        [Keys.Content.NotInspected.Service] = "Service partition; contents are not inspected",
        [Keys.Content.NotInspected.NoLetter] = "The partition has no drive letter",
        [Keys.Content.NotInspected.Pending] = "Contents not inspected yet",

        [Keys.Size.Gb] = "GB",
        [Keys.Size.Mb] = "MB",
        [Keys.Size.LessThanMb] = "less than 1 MB",

        [Keys.Sel.DenyMedia] = "This is the Windows Peace boot media - installation here is impossible",
        [Keys.Sel.DenySystem] = "The current system runs on this disk",
        [Keys.Sel.DenyOffline] = "The disk is offline",
        [Keys.Sel.DenyReadOnly] = "The disk is write-protected",
        [Keys.Sel.DenyService] = "This is a service partition; the system creates it itself",
        [Keys.Sel.DenyUnknownTarget] = "Unknown target kind",
        [Keys.Sel.TooSmall] = "Not enough space: {0} GB short of the minimum 40 GB",

        [Keys.Warn.WindowsOnTarget] = "Windows is installed on the target. It will be deleted permanently.",
        [Keys.Warn.UserFilesOnTarget] = "The target has user files. They will be deleted permanently.",
        [Keys.Warn.PartitionsNotRead] = "This disk's partitions could not be read, so its contents are unknown.",
        [Keys.Warn.ContentNotInspected] = "Some partitions could not be inspected: they have no drive letter.",
        [Keys.Warn.WeakIdentity] = "The disk's serial number could not be read; it cannot be identified reliably.",
        [Keys.Warn.OtherWindows] = "Windows was found on another disk. It may hijack booting.",

        [Keys.Plan.Recovery] = "Recovery",
        [Keys.Plan.SingleTail] = ". Other partitions are unchanged.",

        [Keys.Layout.ReadFailed] = "Could not read the layout, error code {0}",
    };
}
