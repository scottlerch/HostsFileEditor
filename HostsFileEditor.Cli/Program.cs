using HostsFileEditor.CommandLine;

// hfe.exe — a console-subsystem launcher for the shared command line (issue #2). Because it is a
// console app (unlike the GUI-subsystem HostsFileEditor.exe), cmd/PowerShell WAIT for it to exit, so
// it can be used directly in a sequential script without `start /wait`. It runs the exact same Core
// CLI as the GUI editions' headless mode, so behavior and exit codes are identical.
//
// No args → help (exit 0): unlike the GUI exes (where no args opens the window), a bare `hfe` has
// nothing sensible to do but explain itself — and the Store package's visible "Hosts File Editor CLI"
// entry launches exactly that way, so a Start-menu click shows usage instead of a usage ERROR.
return HostsCli.Run(args.Length == 0 ? ["help"] : args, Console.Out, Console.Error);
