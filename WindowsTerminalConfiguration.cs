using System;
using System.Collections.Generic;
using System.Text;

namespace windows_terminal_launcher {
  public class WindowsTerminalConfiguration {
    public WindowsTerminalConfigurationGlobals globals { get; set; }
    public WindowsTerminalConfigurationProfile[] profiles { get; set; }
  }
  public class WindowsTerminalConfigurationGlobals {
    public string defaultProfile { get; set; }
  }
  public class WindowsTerminalConfigurationProfile {
    public string guid { get; set; }
    public string name { get; set; }
    public string icon { get; set; }
    public string startingDirectory { get; set; }
    public bool hidden { get; set; }
  }

}
