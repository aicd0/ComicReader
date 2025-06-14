# Comic Reader UWP
The Comic Reader UWP is a modern Windows app written in C#. The app provided basic functionality for comic reading, along with some common utilities such as file management, searching, tagging, rating, etc.

Notice that though the app is named "UWP", the framework of the app is no longer based on UWP (Windows Universal Platform). It has advanced to WinUI 3 since 1.4.

Comic Reader UWP irregularly ships with new features and bug fixes. You can get the latest version of Comic Reader UWP in the [Microsoft Store](https://www.microsoft.com/store/apps/9NS9FG32DCP5).

![Comic Reader UWP Screenshot](docs/Images/ComicReaderScreenshot.png)

## Getting started
Prerequisites:
- Your computer must be running Windows 10, version 22H2 or newer. Windows 11 is recommended.
- Install the latest version of [Visual Studio](https://developer.microsoft.com/en-us/windows/downloads) (the free community edition is sufficient).
  - Install the ".NET desktop development" and "WinUI application development" workloads.
  - Install the latest Windows 11 SDK.
- Install the [XAML Styler](https://marketplace.visualstudio.com/items?itemName=TeamXavalon.XAMLStyler2022) Visual Studio extension.
- Get the code:
    ```
    git clone https://github.com/aicd0/ComicReader.git
    ```
- Open [ComicReader.sln](src/ComicReader.sln) in Visual Studio to build and run the Comic Reader UWP app.

## Contributing
If Comic Reader UWP is not working properly, you can [submit an issue on GitHub](https://github.com/aicd0/ComicReader/issues/new/choose). If you know how to fix an issue, it is also encouraged to create a [pull request](https://github.com/aicd0/ComicReader/pulls) for it.

## License
Licensed under the [MIT License](./LICENSE).
