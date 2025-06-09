// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Windows.ApplicationModel.Resources;

namespace ComicReader.Common;

class StringResourceProvider
{
    private static readonly ResourceLoader sResourceLoader = new();

    public static string AboutCopyright => GetResourceString("AboutCopyright");
    public static string AddToFavorites => GetResourceString("AddToFavorites");
    public static string AllComics => GetResourceString("AllComics");
    public static string AllComicsIn => GetResourceString("AllComicsIn");
    public static string AllHidden => GetResourceString("AllHidden");
    public static string AllMatchedResults => GetResourceString("AllMatchedResults");
    public static string AppDisplayName => GetResourceString("AppDisplayName");
    public static string Calculating => GetResourceString("Calculating");
    public static string Cancel => GetResourceString("Cancel");
    public static string ClearCacheDetail => GetResourceString("ClearCacheDetail");
    public static string CompletionState => GetResourceString("CompletionState");
    public static string ContributionRunBeforeLink => GetResourceString("ContributionRunBeforeLink");
    public static string ContributionRunAfterLink => GetResourceString("ContributionRunAfterLink");
    public static string DebugModeWarning => GetResourceString("DebugModeWarning");
    public static string Default => GetResourceString("Default");
    public static string DefaultTags => GetResourceString("DefaultTags");
    public static string FilteredBy => GetResourceString("FilteredBy");
    public static string Finished => GetResourceString("Finished");
    public static string FinishPercentage => GetResourceString("FinishPercentage");
    public static string LastReadTime => GetResourceString("LastReadTime");
    public static string NewFolder => GetResourceString("NewFolder");
    public static string NewTab => GetResourceString("NewTab");
    public static string NoRating => GetResourceString("NoRating");
    public static string NoResults => GetResourceString("NoResults");
    public static string Proceed => GetResourceString("Proceed");
    public static string Progress => GetResourceString("Progress");
    public static string Rating => GetResourceString("Rating");
    public static string ReaderStatusError => GetResourceString("ReaderStatusError");
    public static string ReaderStatusLoading => GetResourceString("ReaderStatusLoading");
    public static string Reading => GetResourceString("Reading");
    public static string RemoveFromFavorites => GetResourceString("RemoveFromFavorites");
    public static string SearchResults => GetResourceString("SearchResults");
    public static string SearchResultsOf => GetResourceString("SearchResultsOf");
    public static string Settings => GetResourceString("Settings");
    public static string Tag => GetResourceString("Tag");
    public static string Title => GetResourceString("Title");
    public static string TotalComics => GetResourceString("TotalComics");
    public static string Ungrouped => GetResourceString("Ungrouped");
    public static string Unread => GetResourceString("Unread");
    public static string Untitled => GetResourceString("Untitled");
    public static string Warning => GetResourceString("Warning");

    private static string GetResourceString(string resource)
    {
        return sResourceLoader.GetString(resource) ?? "?";
    }

    private StringResourceProvider() { }
}
