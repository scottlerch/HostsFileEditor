Hosts File Editor
-----------------
Edit and manage the hosts file for you Windows PC.  

HostsFileEditor is free software: you can redistribute it and/or modify it 
under the terms of the GNU General Public License as published by the Free 
Software Foundation, either version 2 of the License, or (at your option)
any later version.

HostsFileEditor is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
more details.
 
You should have received a copy of the GNU General Public   License along
with HostsFileEditor. If not, see http://www.gnu.org/licenses/.

Equin.ApplicationFramework.BindingListView is by Andrew Davey and license
terms can be found at
http://blw.sourceforge.net/

Icons are from Open Icon Library and their license and terms can be found at
http://openiconlibrary.sourceforge.net/


Directory Structure
-------------------
 -bin                   : Binaries output from build scripts
 -docs                  : Misc documentation
 -lib                   : 3rd party libraries
 -logs                  : Log files output from build scripts
 +setup                 : Deployment project that generates *.msi
  -NGenCustomAction     : Custom install action to run NGen
 +src                   : Source code for application
  -Controls             : Custom UI controls
  -Extensions           : Misc extension methods
  -Resources            : Icons and text
  -Utilities            : Misc utility classes


Build
-----
-Install Windows SDK for Windows 7 and .NET
-Optionally, install Visual Studio 2010 (required for *.vdproj file)
-Run build-release.bat or build-debug.bat script from Windows SDK 7.1 or 
 Visual Studio 2010 Command Line
-The binaries and setup file will be copied to the bin directory
