# windows-terminal-launcher

Launcher for [Windows Terminal](https://github.com/microsoft/terminal), allows setting working directory and selecting a profile by argument.

## How to use

**To install `shift + right click` context menu shortcuts** to open each of your configured profiles in current directory run:
```cmd
windows-terminal-launcher.exe --install
```

_Context menu shortcuts will use the profile name and profile icon from settings_

**To uninstall `shift + right click` context menu shortcuts** to open each of your configured profiles in current directory run:
```cmd
windows-terminal-launcher.exe --uninstall
```

**To run [Windows Terminal](https://github.com/microsoft/terminal) manually** specifying the working directory and profile run:
```cmd
windows-terminal-launcher.exe [--directory="c:\"] [--profile="Profile name or GUID"]
```
