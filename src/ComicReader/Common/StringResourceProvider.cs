// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Windows.ApplicationModel.Resources;

namespace ComicReader.Common;

public class StringResourceProvider
{
    private static readonly ResourceLoader sResourceLoader = new();

    public static StringResourceProvider Instance = new();

    public string AboutCopyright => GetResourceString("AboutCopyright");
    public string AddFolder => GetResourceString("AddFolder");
    public string AddToFavorites => GetResourceString("AddToFavorites");
    public string AllComics => GetResourceString("AllComics");
    public string AllComicsIn => GetResourceString("AllComicsIn");
    public string AllHidden => GetResourceString("AllHidden");
    public string AllMatchedResults => GetResourceString("AllMatchedResults");
    public string ApplyOnNextLaunch => GetResourceString("ApplyOnNextLaunch");
    public string AppDisplayName => GetResourceString("AppDisplayName");
    public string Ascending => GetResourceString("Ascending");
    public string Calculating => GetResourceString("Calculating");
    public string Cancel => GetResourceString("Cancel");
    public string ClearCacheDetail => GetResourceString("ClearCacheDetail");
    public string CompletionState => GetResourceString("CompletionState");
    public string ContributionRunBeforeLink => GetResourceString("ContributionRunBeforeLink");
    public string ContributionRunAfterLink => GetResourceString("ContributionRunAfterLink");
    public string DebugModeWarning => GetResourceString("DebugModeWarning");
    public string Default => GetResourceString("Default");
    public string DefaultTags => GetResourceString("DefaultTags");
    public string Descending => GetResourceString("Descending");
    public string ExpressionAnd => GetResourceString("ExpressionAnd");
    public string ExpressionIn => GetResourceString("ExpressionIn");
    public string ExpressionNot => GetResourceString("ExpressionNot");
    public string ExpressionOr => GetResourceString("ExpressionOr");
    public string ExpressionValid => GetResourceString("ExpressionValid");
    public string ExpressionInvalid => GetResourceString("ExpressionInvalid");
    public string FilteredBy => GetResourceString("FilteredBy");
    public string FilterSettings => GetResourceString("FilterSettings");
    public string Finished => GetResourceString("Finished");
    public string FinishPercentage => GetResourceString("FinishPercentage");
    public string Group => GetResourceString("Group");
    public string LastReadTime => GetResourceString("LastReadTime");
    public string NewFolder => GetResourceString("NewFolder");
    public string NewTab => GetResourceString("NewTab");
    public string NoRating => GetResourceString("NoRating");
    public string NoResults => GetResourceString("NoResults");
    public string Proceed => GetResourceString("Proceed");
    public string Progress => GetResourceString("Progress");
    public string Rating => GetResourceString("Rating");
    public string ReaderStatusError => GetResourceString("ReaderStatusError");
    public string ReaderStatusLoading => GetResourceString("ReaderStatusLoading");
    public string Reading => GetResourceString("Reading");
    public string RemoveFromFavorites => GetResourceString("RemoveFromFavorites");
    public string SearchResults => GetResourceString("SearchResults");
    public string SearchResultsOf => GetResourceString("SearchResultsOf");
    public string Settings => GetResourceString("Settings");
    public string Sort => GetResourceString("Sort");
    public string Tag => GetResourceString("Tag");
    public string Title => GetResourceString("Title");
    public string Title1 => GetResourceString("Title1String");
    public string Title2 => GetResourceString("Title2String");
    public string TotalComics => GetResourceString("TotalComics");
    public string Ungrouped => GetResourceString("Ungrouped");
    public string Unread => GetResourceString("Unread");
    public string Untitled => GetResourceString("Untitled");
    public string UseSystemLanguage => GetResourceString("UseSystemLanguage");
    public string ViewType => GetResourceString("ViewType");
    public string ViewTypeLarge => GetResourceString("ViewTypeLarge");
    public string ViewTypeMedium => GetResourceString("ViewTypeMedium");
    public string Warning => GetResourceString("Warning");

    private StringResourceProvider() { }

    private static string GetResourceString(string resource)
    {
        return sResourceLoader.GetString(resource) ?? "?";
    }
}
