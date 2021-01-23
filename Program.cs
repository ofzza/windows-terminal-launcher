using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace windows_terminal_launcher {

  [HelpOption(
    ShortName = "h",
    LongName = "help",
    Description = "Easy way of adding and removing Windows Explorer context menu entries for launching Windows Terminal profiles"
  )]
  class Program {

    #region Constructor(s)

    /// <summary>
    /// Program entry point
    /// </summary>
    /// <param name="args">Runtime arguments</param>
    static void Main (string[] args) => CommandLineApplication.Execute<Program>(args);

    #endregion

    #region Arguments

    [Option(
      CommandOptionType.SingleOrNoValue,
      ShortName = "d",
      LongName = "directory",
      Description = "Directory path to be started in"
    )]
    public (bool hasValue, string value) directory { get; }

    [Option(
      CommandOptionType.MultipleValue,
      ShortName = "p",
      LongName = "profile",
      Description = "Profile to start with, or when used with --install profiles to install context-menu shortcuts for"
    )]
    public string[] profile { get; }

    [Option(
      CommandOptionType.NoValue,
      ShortName = "i",
      LongName = "install",
      Description = "Installs context-menu shortcuts for all profiles, or profiles specified using --profile"
    )]
    public bool install { get; }

    [Option(
      CommandOptionType.SingleOrNoValue,
      ShortName = "f",
      LongName = "format",
      Description = "Format context-menu shortcuts' names using '%P' as profile name placeholder, for example: 'Open %P Terminal Here'"
    )]
    public (bool hasValue, string value) format { get; }

    [Option(
      CommandOptionType.NoValue,
      ShortName = "u",
      LongName = "uninstall",
      Description = "Uninstalls context-menu shortcuts for all profiles"
    )]
    public bool uninstall { get; }

    #endregion

    #region Properties

    private RegistryKey registryRootKey = Registry.CurrentUser;
    private string[] registryPaths = new string[] {
      @"Software\Classes\*\shell",
      @"Software\Classes\Drive\shell",
      @"Software\Classes\Directory\shell",
      @"Software\Classes\Directory\Background\shell"
    };

    #endregion

    #region Entry point

    private void OnExecute () {

      // Check arguments
      if (this.directory.hasValue && (this.uninstall || this.install)) {
        throw new Exception("--directory can not be used when running --install or --uninstall!");
      }
      if (this.format.hasValue && !this.install) {
        throw new Exception("--format can only be used when running --install!");
      }

      // Check and run action
      if (this.uninstall) {
        // Uninstall context menu shortcuts
        this.UninstallContextMenuShortcuts();
      }
      if (this.install) {
        // Install context menu shortcuts
        this.InstallContextMenuShortcuts();
      }
      if (!this.uninstall && !this.install) {
        // Start windows terminal
        this.StartWindowsTerminal();
      }

    }

    #endregion 

    #region Action implementation
    
    private void InstallContextMenuShortcuts () {
      // Uninstall previous shortcuts (if any exist)
      this.UninstallContextMenuShortcuts();
      // (Re)Install shortcuts
      this.ExecuteWithConfiguration((config, path) => {

        // For each profile ...
        foreach (string keyroot in this.registryPaths) {
          foreach (WindowsTerminalConfigurationProfile profile in config.profiles.list) {
            bool matchedName = (Array.Find<string>(this.profile, (name) => (name == profile.name)) != null);
            bool matchedGuid = (Array.Find<string>(this.profile, (name) => (name == profile.guid)) != null);
            // ... if not hidden no one profile selected or profile matches by name or GUID
            if ((!profile.hidden && (this.profile.Length == 0)) || matchedName || matchedGuid) {
              // ... write to registry
              string format = String.Format((this.format.hasValue ? this.format.value.Replace("%P", "{0}").Replace("%p", "{0}") : "Open {0} Terminal Here"), profile.name);
              string keypath = String.Format(@"{0}\{1}", keyroot, format);
              RegistryKey key = this.registryRootKey.CreateSubKey(keypath);
              string commandpath = String.Format(@"{0}\command", keypath);
              RegistryKey command = this.registryRootKey.CreateSubKey(commandpath);
              string wtlpath = String.Format(@"{0}\windows-terminal-launcher", keypath);
              RegistryKey wtl = this.registryRootKey.CreateSubKey(wtlpath);

              command.SetValue("", String.Format("wt -p \"{0}\" -d \"%V\"", profile.name)); // Run directly
              //command.SetValue("", String.Format("{0} --profile=\"{1}\" --directory=\"%V\"", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, profile.name)); // Run using WindowsTerminalCOnfiguration.exe as proxy
              command.SetValue("Position", @"Bottom");
              command.SetValue("Extended", @"");

              if (profile.icon != null) { key.SetValue("icon", profile.icon); }
              command.Close();
              key.Close();
            }
          }
        }

      });
    }
        
    private void UninstallContextMenuShortcuts () {
      this.ExecuteWithConfiguration((config, path) => {

        // For each registry root path
        foreach (string keyroot in this.registryPaths) {
          // ... for each profile ...
          foreach (WindowsTerminalConfigurationProfile profile in config.profiles.list) {
            // ... if not hidden
            if (!profile.hidden) {
              // ... clear from registry
              string keyspath = String.Format(keyroot);
              RegistryKey key = this.registryRootKey.OpenSubKey(keyspath);
              if (key != null) {
                foreach (string keyname in key.GetSubKeyNames()) {
                  string keypath = String.Format(@"{0}\{1}", keyroot, keyname);
                  string wtlpath = String.Format(@"{0}\windows-terminal-launcher", keypath, keyname);
                  RegistryKey wtl = this.registryRootKey.OpenSubKey(wtlpath);
                  if (wtl != null) {
                    this.registryRootKey.DeleteSubKeyTree(keypath);
                  }
                }
              }
            }
          }
        }

      });
    }

    private void StartWindowsTerminal () {
      this.ExecuteWithConfiguration((config, path) => {

        // Start terminal process
        try {
          // Check if passed working directory
          string workingDir = Environment.CurrentDirectory;
          if (this.directory.hasValue) {
            string escapedDirectoryValue = (this.directory.value.EndsWith("\"") ? this.directory.value.Substring(0, this.directory.value.Length - 1) : this.directory.value);
            FileAttributes attr = File.GetAttributes(escapedDirectoryValue);
            workingDir = ((attr & FileAttributes.Directory) == FileAttributes.Directory ? escapedDirectoryValue : Path.GetDirectoryName(escapedDirectoryValue));
          }
          // Start process ...
          var process = new Process();
          process.StartInfo.FileName = "wt.exe";
          process.StartInfo.Arguments = String.Format(
            "{0} {1}",
            String.Format("-d \"{0}\"", workingDir),
            ((this.profile.Length > 0) ? String.Format("-p \"{0}\"", this.profile[0]) : "")
          );
          process.StartInfo.WorkingDirectory = workingDir;
          process.Start();

        } catch (Exception ex) {
          Console.WriteLine("Failed running Windwos Terminal - is it installed on your system?");
        }

      });
    }

    private void ExecuteWithConfiguration (Action<WindowsTerminalConfiguration, string> action) {

      // Check configuration
      string path = (new List<string>(Directory.EnumerateDirectories(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"))))
        .Find(p => p.Contains(@"\Microsoft.WindowsTerminal"));
      if (path == null) {
        Console.WriteLine("Couldn't find Windows Terminal configuration file!");
        return;
      }
      string configPath = Path.Combine(path, @"LocalState\settings.json");
      string configBackupPath = Path.Combine(path, @"LocalState\settings.json.bak");
      if (!File.Exists(configPath)) {
        Console.WriteLine("Couldn't find Windows Terminal configuration file!");
        return;
      }

      // Read and backup configuration
      string configRaw;
      WindowsTerminalConfiguration config = null;
      // Read until an unmodified version of config is read
      configRaw = File.ReadAllText(configPath);
      config = JsonConvert.DeserializeObject<WindowsTerminalConfiguration>(configRaw);
      // Write backup file
      if (!File.Exists(configBackupPath)) { File.WriteAllText(configBackupPath, configRaw); }

      // Execute action
      try { action(config, configPath); } catch (Exception ex) { }

    }

    #endregion

  }
}
