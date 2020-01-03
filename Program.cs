using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Win32;

// TODO:
// 1) Replace Sleep(...) with detecting when Terminal process has started
// 2) Make sure to write full profiles.json after modifications, not just parts that are modeled
// 3) Make sure profile.json isn't backed up while modified, if multiple isntances are baing run at nearly the same time

namespace windows_terminal_launcher {
  class Program {

    /// <summary>
    /// Program entry point
    /// </summary>
    /// <param name="args">Runtime arguments</param>
    static void Main (string[] args) => CommandLineApplication.Execute<Program>(args);

    [Option(
      CommandOptionType.SingleOrNoValue,
      ShortName = "d",
      LongName = "directory",
      Description = "Directory path to be started in"
    )]
    public (bool hasValue, string value) directory { get; }
    
    [Option(
      CommandOptionType.SingleOrNoValue,
      ShortName = "p",
      LongName = "profile",
      Description = "Profile to start with"
    )]
    public (bool hasValue, string value) profile { get; }

    [Option(
      CommandOptionType.NoValue,
      ShortName = "i",
      LongName = "install",
      Description = "Installs context menu shortcuts"
    )]
    public bool install { get; }

    [Option(
      CommandOptionType.NoValue,
      ShortName = "u",
      LongName = "uninstall",
      Description = "Uninstalls context menu shortcuts"
    )]
    public bool uninstall { get; }

    private RegistryKey registryRootKey = Registry.CurrentUser;
    private string[] registryPaths = new string[] {
      @"Software\Classes\*\shell",
      @"Software\Classes\Drive\shell",
      @"Software\Classes\Directory\shell",
      @"Software\Classes\Directory\Background\shell"
    };

  private void OnExecute () {
      // Check mode
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

    private void InstallContextMenuShortcuts () {
      this.ExecuteWithConfiguration((config, path) => {

        // For each profile ...
        foreach (WindowsTerminalConfigurationProfile profile in config.profiles) {
          // ... if not hidden
          if (!profile.hidden) {
            // ... write to registry
            foreach (string keyroot in this.registryPaths) {
              string keypath = String.Format(@"{0}\$ {1}", keyroot, profile.name);
              RegistryKey key = this.registryRootKey.CreateSubKey(keypath);
              string commandpath = String.Format(@"{0}\command", keypath);
              RegistryKey command = this.registryRootKey.CreateSubKey(commandpath);
              command.SetValue("",          String.Format("{0} --directory=\"%V\" --profile=\"{1}\"", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, profile.name));
              command.SetValue("Position",  @"Bottom");
              command.SetValue("Extended",  @"");
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

        // For each profile ...
        foreach (WindowsTerminalConfigurationProfile profile in config.profiles) {
          // ... if not hidden
          if (!profile.hidden) {
            // ... write to registry
            foreach (string keyroot in this.registryPaths) {
              string keypath = String.Format(@"{0}\$ {1}", keyroot, profile.name);
              if (this.registryRootKey.OpenSubKey(keypath) != null) {
                this.registryRootKey.DeleteSubKeyTree(keypath);
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
        string configUpdatedRaw = JsonSerializer.Serialize(config, new JsonSerializerOptions() {
          WriteIndented = true
        });
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
          // ... wait until process started (TODO: Replace sleep!!!)
          Thread.Sleep(2000);
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
      string configRaw = File.ReadAllText(configPath);
      if (!File.Exists(configBackupPath)) { File.WriteAllText(configBackupPath, configRaw); }
      WindowsTerminalConfiguration config = JsonSerializer.Deserialize<WindowsTerminalConfiguration>(configRaw);

      // Execute action
      try {
        action(config, configPath);
      } catch (Exception ex) { }

      // Revert configuration and clear backup
      File.WriteAllText(configPath, configRaw);
      File.Delete(configBackupPath);

    }

  }
}
