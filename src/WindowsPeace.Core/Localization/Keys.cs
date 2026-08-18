namespace WindowsPeace.Core.Localization;

public static class Keys
{
    public static class Common { public const string Next = "common.next"; }

    public static class Language { public const string Title = "language.title"; }

    public static class Shell
    {
        public const string Back = "shell.back";
        public const string Exit = "shell.exit";
    }

    public static class Recipe
    {
        public const string Title = "recipe.title";
        public const string Intro = "recipe.intro";
        public const string ColName = "recipe.col.name";
        public const string ColImage = "recipe.col.image";
        public const string ColSize = "recipe.col.size";
        public const string ColWhat = "recipe.col.what";
        public const string TroubleNotFound = "recipe.trouble.notFound";
        public const string TroubleDamaged = "recipe.trouble.damaged";
        public const string TroubleTooNew = "recipe.trouble.tooNew";
        public const string TroubleNoRecipes = "recipe.trouble.noRecipes";
    }

    public static class Confirm
    {
        public const string Title = "confirm.title";
        public const string Install = "confirm.install";
        public const string WhatLabel = "confirm.whatLabel";
        public const string WhereLabel = "confirm.whereLabel";
        public const string EffectLabel = "confirm.effectLabel";
        public const string Wipe = "confirm.wipe";
        public const string LostChoice = "confirm.lostChoice";
    }

    public static class Progress
    {
        public const string Title = "progress.title";
        public const string Explanation = "progress.explanation";
    }

    public static class Done
    {
        public const string Title = "done.title";
        public const string Explanation = "done.explanation";
    }

    public static class Error
    {
        public const string Title = "error.title";
        public const string Body = "error.body";
    }

    public static class Disk
    {
        public const string Title = "disk.title";
        public const string ColName = "disk.col.name";
        public const string ColSize = "disk.col.size";
        public const string ColFree = "disk.col.free";
        public const string ColType = "disk.col.type";
        public const string ColState = "disk.col.state";
        public const string Refresh = "disk.refresh";
        public const string Cancel = "disk.cancel";
        public const string Create = "disk.create";
        public const string Delete = "disk.delete";
        public const string Format = "disk.format";
        public const string Extend = "disk.extend";
        public const string Details = "disk.details";
        public const string LoadDriver = "disk.loadDriver";
        public const string StatusEnumerating = "disk.status.enumerating";
        public const string StatusInspecting = "disk.status.inspecting";
        public const string StatusLocating = "disk.status.locating";
        public const string ErrorCancelled = "disk.error.cancelled";
        public const string FreeSpace = "disk.freeSpace";
        public const string Partition = "disk.partition";
        public const string PartitionLabel = "disk.partitionLabel";
        public const string NoteMedia = "disk.note.media";
        public const string NoteSystem = "disk.note.system";
        public const string NoteEmpty = "disk.note.empty";
        public const string NotePartitions = "disk.note.partitions";
    }

    public static class PartitionType
    {
        public const string Efi = "parttype.efi";
        public const string Msr = "parttype.msr";
        public const string Recovery = "parttype.recovery";
        public const string Basic = "parttype.basic";
        public const string Unknown = "parttype.unknown";
    }

    public static class Content
    {
        public const string WindowsAndFiles = "content.windowsAndFiles";
        public const string Windows = "content.windows";
        public const string UserFiles = "content.userFiles";

        public static class NotInspected
        {
            public const string Cancelled = "content.notInspected.cancelled";
            public const string Service = "content.notInspected.service";
            public const string NoLetter = "content.notInspected.noLetter";
            public const string Pending = "content.notInspected.pending";
        }
    }

    public static class Size
    {
        public const string Gb = "size.gb";
        public const string Mb = "size.mb";
        public const string LessThanMb = "size.lessThanMb";
    }

    public static class Sel
    {
        public const string DenyMedia = "sel.denyMedia";
        public const string DenySystem = "sel.denySystem";
        public const string DenyOffline = "sel.denyOffline";
        public const string DenyReadOnly = "sel.denyReadOnly";
        public const string DenyService = "sel.denyService";
        public const string DenyUnknownTarget = "sel.denyUnknownTarget";
        public const string TooSmall = "sel.tooSmall";
    }

    public static class Warn
    {
        public const string WindowsOnTarget = "warn.windowsOnTarget";
        public const string UserFilesOnTarget = "warn.userFilesOnTarget";
        public const string PartitionsNotRead = "warn.partitionsNotRead";
        public const string ContentNotInspected = "warn.contentNotInspected";
        public const string WeakIdentity = "warn.weakIdentity";
        public const string OtherWindows = "warn.otherWindows";
    }

    public static class Plan
    {
        public const string Recovery = "plan.recovery";
        public const string SingleTail = "plan.singleTail";
    }

    public static class Layout
    {
        public const string ReadFailed = "layout.readFailed";
    }
}
