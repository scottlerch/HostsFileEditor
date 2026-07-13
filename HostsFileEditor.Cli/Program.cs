using HostsFileEditor.CommandLine;

// hfe.exe — a console-subsystem launcher for the shared command line (issue #2). Because it is a
// console app (unlike the GUI-subsystem HostsFileEditor.exe), cmd/PowerShell WAIT for it to exit, so
// it can be used directly in a sequential script without `start /wait`. It runs the exact same Core
// CLI as the GUI editions' headless mode, so behavior and exit codes are identical.
return HostsCli.Run(args, Console.Out, Console.Error);
