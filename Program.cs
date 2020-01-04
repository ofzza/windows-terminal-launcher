using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace windows_terminal_launcher {
  class Program {

    #region Constructor(s)

    /// <summary>
    /// Program entry point
    /// </summary>
    /// <param name="args">Runtime arguments</param>
    static void Main (string[] args) => CommandLineApplication.Execute<Program>(args);

    #endregion

    #region Arguments

    // Won't work 'cos not a console application (just here in case this changes)
    [Option(
      CommandOptionType.SingleOrNoValue,
      Template = "-d|--directory",
      Description = "Directory path to be started in",
      ShowInHelpText = true
    )]
    public (bool hasValue, string value) directory { get; }
    
    [Option(
      CommandOptionType.SingleOrNoValue,
      Template = "-p|--profile",
      Description = "Profile to start with",
      ShowInHelpText = true
    )]
    public (bool hasValue, string value) profile { get; }

    [Option(
      CommandOptionType.NoValue,
      Template = "-i|--install",
      Description = "Installs (shift + right click) context-menu shortcuts for all profiles",
      ShowInHelpText = true
    )]
    public bool install { get; }

    [Option(
      CommandOptionType.SingleOrNoValue,
      Template = "-f|--format",
      Description = "Format of installed (shift + right click) context-menu shortcuts' names using '%P' as profile name placeholder, for example: 'Run %P Here'",
      ShowInHelpText = true
    )]
    public (bool hasValue, string value) format { get; }

    [Option(
      CommandOptionType.NoValue,
      Template = "-u|--uninstall",
      Description = "Uninstalls (shift + right click) context-menu shortcuts for all profiles",
      ShowInHelpText = true
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
      if (this.install && (this.directory.hasValue || this.profile.hasValue)) {
        throw new Exception("--directory and --profile are not allowed when running --install!");
      }
      if (this.uninstall && (this.directory.hasValue || this.profile.hasValue || this.format.hasValue)) {
        throw new Exception("--directory, --profile and --format are not allowed when running --uninstall!");
      }
      if (this.format.hasValue && !this.install) {
        throw new Exception("--format can only be used when running --install!");
      }

      // Check and run action
      if (this.install) {
        // Install context menu shortcuts
        this.InstallContextMenuShortcuts();
      } else if (this.uninstall) {
        // Uninstall context menu shortcuts
        this.UninstallContextMenuShortcuts();
      } else {
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
          foreach (WindowsTerminalConfigurationProfile profile in config.profiles) {
            // ... if not hidden
            if (!profile.hidden) {
              // ... write to registry
              string format = String.Format((this.format.hasValue ? this.format.value.Replace("%P", "{0}").Replace("%p", "{0}") : "{0} Terminal Here"), profile.name);
              string keypath = String.Format(@"{0}\{1}", keyroot, format);
              RegistryKey key = this.registryRootKey.CreateSubKey(keypath);
              string commandpath = String.Format(@"{0}\command", keypath);
              RegistryKey command = this.registryRootKey.CreateSubKey(commandpath);
              string wtlpath = String.Format(@"{0}\windows-terminal-launcher", keypath);
              RegistryKey wtl = this.registryRootKey.CreateSubKey(wtlpath);

              command.SetValue("", String.Format("{0} --directory=\"%V\" --profile=\"{1}\"", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, profile.name));
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
          foreach (WindowsTerminalConfigurationProfile profile in config.profiles) {
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

        // Update configuration's selected profile
        if (this.profile.hasValue) {
          WindowsTerminalConfigurationProfile profile = (new List<WindowsTerminalConfigurationProfile>(config.profiles))
            .Find(p => (p.guid.ToLower() == this.profile.value.ToLower()) || (p.name.ToLower() == this.profile.value.ToLower()));
          if (profile != null) {
            profile.startingDirectory = "%__CD__%";
            config.globals.defaultProfile = profile.guid;
          }
        }

        // Store updated configuration
        string configUpdatedRaw = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, configUpdatedRaw);

        // Start terminal process
        try {
          // Check if passed working directory
          string workingDir = Environment.CurrentDirectory;
          if (this.directory.hasValue) {
            FileAttributes attr = File.GetAttributes(this.directory.value);
            workingDir = ((attr & FileAttributes.Directory) == FileAttributes.Directory ? this.directory.value : Path.GetDirectoryName(this.directory.value));
          }
          // Start process ...
          var process = new Process();
          process.StartInfo.FileName = "wt.exe";
          process.StartInfo.WorkingDirectory = workingDir;
          process.Start();
          // ... wait until process started
          DateTime waitStart = DateTime.Now;
          while (string.IsNullOrEmpty(process.MainWindowTitle) && ((DateTime.Now - waitStart).TotalSeconds < 10)) {
            Thread.Sleep(100);
            process.Refresh();
          }

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
      string configPath = Path.Combine(path, @"LocalState\profiles.json");
      string configBackupPath = Path.Combine(path, @"LocalState\profiles.json.bak");
      if (!File.Exists(configPath)) {
        Console.WriteLine("Couldn't find Windows Terminal configuration file!");
        return;
      }

      // Check if backup configuration left from last time
      if (File.Exists(configBackupPath)) {
        File.WriteAllText(configPath, File.ReadAllText(configBackupPath));
        File.Delete(configBackupPath);
      }

      // Read and backup configuration
      string configRaw;
      WindowsTerminalConfiguration config = null;
      // Read until an unmodified version of config is read
      do {
        // If already read, wait a bit
        if (config != null) { Thread.Sleep(100); }
        // Read config
        configRaw = File.ReadAllText(configPath);
        config = JsonConvert.DeserializeObject<WindowsTerminalConfiguration>(configRaw);
      } while (config.wintermRunnerModified);
      // Write backup file
      if (!File.Exists(configBackupPath)) { File.WriteAllText(configBackupPath, configRaw); }

      // Execute action
      try { action(config, configPath); } catch (Exception ex) { }

      // Revert configuration and clear backup
      File.WriteAllText(configPath, configRaw);
      File.Delete(configBackupPath);

    }

    #endregion

  }
}
