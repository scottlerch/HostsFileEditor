## Download

Release binaries can be downloaded from [GitHub](https://github.com/scottlerch/HostsFileEditor/releases).
 * [Download Latest Installer](https://github.com/scottlerch/HostsFileEditor/releases/download/v1.2.0/HostsFileEditorSetup-1.2.0.msi)
 * [Download Latest Portable](https://github.com/scottlerch/HostsFileEditor/releases/download/v1.2.0/HostsFileEditor-1.2.0.zip)

## Features
 * Cut, copy, paste, duplicate, enable, disable and move one or more entries at a time
 * Filter and sort when there are a large number of host entries
 * Enable and disable entire hostsfile from application or tray
 * Archive and restore various hostsfile configurations when switching between environments
 * Automatically ping endpoints to check availability

![Main screen](https://cloud.githubusercontent.com/assets/1789883/24075121/a68ddcc8-0bd2-11e7-9eed-c53d02a08930.png)  
*main editor screen with optional archive visible on right*

![Tray](https://cloud.githubusercontent.com/assets/1789883/24075122/a98c7628-0bd2-11e7-845d-0e52a5e2cc7d.png)  
*tray icon with context menu*

### Usage Notes

By default the application closes to the tray. To exit completely you must select Exit from the File menu or tray context menu. Only one instance of the application is allowed at a time. If you try to open it again it will just activate the previously running instance.

When selecting rows to move, delete, copy, or cut be sure to select the entire row using the row header cell. If no entire rows are selected, cut, copy, paste, and delete apply individually to the selected cells.

Using the filter and sort while editing is quirky. The filter and sort are applied once a cell is edited so your cell may change positions or disappear depending on the current sort and filter.

## Build

Requires .NET 9.0 or later. To build the installer you must have [Windows SDK](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) with `makeappx.exe` and `signtool.exe` commands.

To build the application, use the .NET CLI:

```bash
# Build for Debug (includes debugging symbols)
dotnet build -c Debug

# Build for Release (optimized)
dotnet build -c Release

# Build and publish (creates deployable package)
dotnet publish -c Release

# Build and publish with binary logging (recommended for troubleshooting)
dotnet publish -c Release -bl:logs/publish.binlog

# Clean project build artifacts, bin directory, and logs directory
dotnet clean
```

### Build Outputs

- Built files are automatically copied to the `.\bin` directory after publishing
- Binary build logs can be generated using the `-bl` flag (e.g., `dotnet build -bl:logs/build-Release.binlog`)
- The build process automatically creates necessary directories (`bin`, `logs`)

You can view binary logs using:
- Visual Studio: File → Open → build log file (.binlog)
- MSBuild Structured Log Viewer: Download from https://msbuildlog.com/

## License
 
[GNU General Public](https://www.gnu.org/licenses/)

_Equin.ApplicationFramework.BindingListView_ is by Andrew Davey and license
terms can be found at <http://blw.sourceforge.net/>.

Icons are from the _Open Icon Library_ and their license and terms can be found at <http://openiconlibrary.sourceforge.net/>.
