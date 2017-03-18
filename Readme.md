## Download

Release binaries can be downloaded from [GitHub](https://github.com/scottlerch/HostsFileEditor/releases) or [CodePlex](https://hostsfileeditor.codeplex.com/).
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

## Build

Requires Visual Studio 2015 or newer.  To build the installer you must have WiX Toolset 3.7+.

Run `build-release.bat` or `build-debug.bat`. 

The binaries and setup file will be copied to the `bin` directory

## License
 
[GNU General Public](https://www.gnu.org/licenses/)

Equin.ApplicationFramework.BindingListView is by Andrew Davey and license
terms can be found at
<http://blw.sourceforge.net/>.

Icons are from the Open Icon Library and their license and terms can be found at
<http://openiconlibrary.sourceforge.net/>.



