using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace windows_terminal_launcher {
  public class WindowsTerminalConfiguration {
    public string defaultProfile { get; set; }
    public WindowsTerminalConfigurationProfiles profiles { get; set; }
    [JsonExtensionData]
    private IDictionary<string, JToken> _extraStuff;
  }  
  public class WindowsTerminalConfigurationProfiles {
    public WindowsTerminalConfigurationProfile defaults { get; set; }
    public WindowsTerminalConfigurationProfile[] list { get; set; }
    [JsonExtensionData]
    private IDictionary<string, JToken> _extraStuff;
  }
  public class WindowsTerminalConfigurationProfile {
    public string guid { get; set; }
    public string name { get; set; }
    public string icon { get; set; }
    public string startingDirectory { get; set; }
    public bool hidden { get; set; }
    [JsonExtensionData]
    private IDictionary<string, JToken> _extraStuff;
  }

}
