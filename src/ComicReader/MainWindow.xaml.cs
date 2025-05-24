// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Native;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.Helpers.Navigation;
using ComicReader.Views.Main;

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Search;

using WinRT.Interop;

namespace ComicReader;

public sealed partial class MainWindow : Window
{
    //
    // Member variables
    //

    // IMPORTANT: Any object referenced by this class need be dereferenced in OnWindowClosed,
    // or else memory leaks will occur.
    // See http://github.com/microsoft/microsoft-ui-xaml/issues/7282 for more details.

    private MainPage _mainPage;
    private string _url;

    //
    // Constructors
    //

    public MainWindow(string url)
    {
        _url = url;

        InitializeComponent();
        WindowId = App.WindowManager.RegisterWindow(this);
        WindowHandle = WindowNative.GetWindowHandle(this);

        Title = StringResourceProvider.GetResourceString("AppDisplayName");
        ExtendsContentIntoTitleBar = true;
        TrySetAcrylicBackdrop();
    }

    //
    // Public Interfaces
    //

    public int WindowId { get; }
    public IntPtr WindowHandle { get; private set; }

    public void OnFileActivated(FileActivatedEventArgs args)
    {
        _ = OnFileActivatedAsync(args);
    }

    //
    // Event Handlers
    //

    // IMPORTANT: Handle event registration and unregistration in the code-behind file,
    // do not use XAML for this purpose in order to prevent memory leaks.

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        if (_mainPage == null)
        {
            return;
        }

        var placement = new NativeModels.WindowPlacement();
        NativeMethods.GetWindowPlacement(WindowHandle, out placement);
        string serialized = JsonSerializer.Serialize(placement);
        ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_WINDOW_STATES] = serialized;
    }

    private void OnPageFrameLoaded(object sender, RoutedEventArgs e)
    {
        TryRecoverWindowStates();

        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_MAIN)
            .WithParam(RouterConstants.ARG_WINDOW_ID, WindowId.ToString())
            .WithParam(RouterConstants.ARG_URL, _url);
        AppRouter.OpenInFrame(PageFrame, route);
        _mainPage = (MainPage)PageFrame.Content;
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _mainPage.CloseAllTabs();
        PageFrame.Loaded -= OnPageFrameLoaded;
        App.WindowManager.UnregisterWindow(WindowId);

        _mainPage = null;
        _url = null;
        PageFrame.Content = null;
        PageFrame = null;
        WindowHandle = IntPtr.Zero;
    }

    //
    // File Activation
    //

    private async Task OnFileActivatedAsync(FileActivatedEventArgs args)
    {
        ComicModel comic = await GetStartupComic(args);
        if (comic == null)
        {
            return;
        }

        string token = AppModel.PutComicData(comic);
        Route route = Route.Create(RouterConstants.SCHEME_APP + RouterConstants.HOST_READER)
            .WithParam(RouterConstants.ARG_COMIC_TOKEN, token);

        if (_mainPage == null)
        {
            _url = route.Url;
            return;
        }

        _mainPage.OpenInNewTab(route);
    }

    private async Task<ComicModel> GetStartupComic(FileActivatedEventArgs args)
    {
        var target_file = (StorageFile)args.Files[0];

        if (!AppInfoProvider.IsSupportedExternalFileExtension(target_file.FileType))
        {
            return null;
        }

        ComicModel comic = await ComicModel.FromFile(target_file);

        if (comic == null && AppInfoProvider.IsSupportedImageExtension(target_file.FileType))
        {
            string dir = target_file.Path;
            dir = StringUtils.ParentLocationFromLocation(dir);
            comic = await ComicModel.FromLocation(dir, "MainGetStartupComicFromImage");

            if (comic == null)
            {
                StorageFile info_file = null;
                var all_files = new List<StorageFile>();
                var img_files = new List<StorageFile>();
                StorageFileQueryResult neighboring_file_query =
                    args.NeighboringFilesQuery;

                if (neighboring_file_query != null)
                {
                    IReadOnlyList<StorageFile> files = await args.NeighboringFilesQuery.GetFilesAsync();
                    all_files = [.. files];
                }

                if (all_files.Count == 0)
                {
                    foreach (IStorageItem item in args.Files)
                    {
                        if (item is StorageFile file)
                        {
                            all_files.Add(file);
                        }
                    }
                }

                foreach (StorageFile file in all_files)
                {
                    if (file.Name.ToLower().Equals(ComicModel.COMIC_INFO_FILE_NAME))
                    {
                        info_file = file;
                    }
                    else if (AppInfoProvider.IsSupportedImageExtension(file.FileType))
                    {
                        img_files.Add(file);
                    }
                }

                comic = await ComicModel.FromImageFiles(dir, img_files, info_file);
            }
        }

        return comic;
    }

    //
    // Helpers
    //

    private void TrySetAcrylicBackdrop()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            var desktopAcrylicBackdrop = new DesktopAcrylicBackdrop();
            SystemBackdrop = desktopAcrylicBackdrop;
        }
    }

    private void TryRecoverWindowStates()
    {
        object windowStates = ApplicationData.Current.LocalSettings.Values[GlobalConstants.LOCAL_SETTINGS_KEY_WINDOW_STATES];
        if (windowStates is not string windowStatesJson)
        {
            return;
        }

        NativeModels.WindowPlacement windowPlacement;
        try
        {
            windowPlacement = JsonSerializer.Deserialize<NativeModels.WindowPlacement>(windowStatesJson);
        }
        catch (Exception)
        {
            return;
        }

        NativeMethods.SetWindowPlacement(WindowHandle, ref windowPlacement);
    }
}
