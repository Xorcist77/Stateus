using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Configuration;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace Stateus {

  class Program {

    [DllImport("user32.dll")]
    public static extern Int32 GetAsyncKeyState(Int32 i);

    [DllImport("user32.dll")]
    static extern Int32 MapVirtualKey(UInt32 uCode, UInt32 uMapType);

    static String HandleUnknown(Object val) {
      String str = ((String)val).Trim();
      return (String.IsNullOrEmpty(str)) ? "Unknown" : str;
    }

    enum LogMode {
      All,
      Console,
      LogFile,
    }

    static String MemTypeMap(Int32 memType) {
      switch (memType) {
        case 1: return "Other";
        case 2: return "DRAM";
        case 3: return "DRAM (Synchronous)";
        case 4: return "DRAM (Cache)";
        case 5: return "EDO";
        case 6: return "EDRAM";
        case 7: return "VRAM";
        case 8: return "SRAM";
        case 9: return "RAM";
        case 10: return "ROM";
        case 11: return "FLASH";
        case 12: return "EEPROM";
        case 13: return "FEPROM";
        case 14: return "EPROM";
        case 15: return "CDRAM";
        case 16: return "3DRAM";
        case 17: return "SDRAM";
        case 18: return "SGRAM";
        case 19: return "RDRAM";
        case 20: return "DDR";
        case 21: return "DDR2";
        case 22: return "DDR2 FB-DIMM";
        case 23: return "DDR2—FB";
        case 24: return "DDR3";
        case 25: return "FBD2";
        case 26: return "DDR4";
        default: return $"Unknown({memType})";
      }
    }

    static String KeyCodeMap(Keys keyCode, Boolean showCode) {
      String keyFace = keyCode.ToString();
      switch (keyCode) {
        case Keys.Capital: keyFace = "CapsLock"; break;
        case Keys.D0: keyFace = "0"; break;
        case Keys.D1: keyFace = "1"; break;
        case Keys.D2: keyFace = "2"; break;
        case Keys.D3: keyFace = "3"; break;
        case Keys.D4: keyFace = "4"; break;
        case Keys.D5: keyFace = "5"; break;
        case Keys.D6: keyFace = "6"; break;
        case Keys.D7: keyFace = "7"; break;
        case Keys.D8: keyFace = "8"; break;
        case Keys.D9: keyFace = "9"; break;
        case Keys.LControlKey: keyFace = "LCtrl"; break;
        case Keys.LMenu: keyFace = "LAlt"; break;
        case Keys.LShiftKey: keyFace = "LShift"; break;
        case Keys.PageUp: keyFace = "PgUp"; break;
        case Keys.PageDown: keyFace = "PgDn"; break;
        case Keys.RControlKey: keyFace = "RCtrl"; break;
        case Keys.RMenu: keyFace = "RAlt"; break;
        case Keys.RShiftKey: keyFace = "RShift"; break;
      }
      if (keyFace.StartsWith("Oem")) {
        keyFace = Convert.ToChar(MapVirtualKey((UInt32)keyCode, 2)).ToString();
      }
      if (showCode) {
        keyFace += $"_{(UInt32)keyCode:D3}";
      }
      return keyFace;
    }


    public class AppOptions {
      public Int32 PollingRate { get; set; }
      public Boolean DisplayInfo { get; set; }
      public String LogfilePath { get; set; }
    }

    public class AppOptionsBinder : BinderBase<AppOptions> {
      private readonly Option<Int32> _pollingRateOption;
      private readonly Option<String> _displayInfoOption;
      private readonly Option<String> _logfilePathOption;

      public AppOptionsBinder(Option<Int32> pollingRateOption, Option<String> displayInfoOption, Option<String> logfilePathOption) {
        _pollingRateOption = pollingRateOption;
        _displayInfoOption = displayInfoOption;
        _logfilePathOption = logfilePathOption;
      }

      protected override AppOptions GetBoundValue(System.CommandLine.Binding.BindingContext bindingContext) {
        return new AppOptions {
          PollingRate = bindingContext.ParseResult.GetValueForOption(_pollingRateOption),
          DisplayInfo = Boolean.Parse(bindingContext.ParseResult.GetValueForOption(_displayInfoOption)),
          LogfilePath = Path.GetFullPath(bindingContext.ParseResult.GetValueForOption(_logfilePathOption))
        };
      }
    }

    static async Task<Int32> Main(String[] args) {
      Option<Int32> pollingRateOption = new Option<Int32>(
        aliases: new[] { "-r", "--Polling-Rate" },
        description: "Millisecond interval at which devices are polled",
        isDefault: true,
        parseArgument: result => {
          Int32 pr;
          String err = "Polling-Rate must be an integer from 1-1000";
          if (!result.Tokens.Any()) { //No value passed to option (or option not specified)
            if (!Int32.TryParse(ConfigurationManager.AppSettings["Polling-Rate"], out pr)) {
              pr = 5;
            }
          } else {
            if (!Int32.TryParse(result.Tokens.Single().Value, out pr)) {
              result.ErrorMessage = err;
            }
          }
          if (pr < 1 || pr > 1000) {
            result.ErrorMessage = err;
          }
          return pr;
        }
      );
      pollingRateOption.ArgumentHelpName = "1-1000";
      pollingRateOption.Arity = ArgumentArity.ExactlyOne;

      Option<String> displayInfoOption = new Option<String>(
        aliases: new[] { "-i", "--Display-Info" },
        description: "Flag to show or hide hardware information header",
        isDefault: true,
        parseArgument: result => {
          Boolean di;
          String err = "Display-Info must be True or False";
          if (!result.Tokens.Any()) {
            if (!Boolean.TryParse(ConfigurationManager.AppSettings["Display-Info"], out di)) {
              di = true;
            }
          } else {
            if (!Boolean.TryParse(result.Tokens.Single().Value, out di)) {
              result.ErrorMessage = err;
            }
          }
          return di.ToString();
        }
      );
      displayInfoOption.ArgumentHelpName = "True|False";
      displayInfoOption.Arity = ArgumentArity.ExactlyOne;

      Option<String> logfilePathOption = new Option<String>(
        aliases: new[] { "-p", "--Logfile-Path" },
        description: "Path to folder where log files will be generated",
        isDefault: true,
        parseArgument: result => {
          String lf = null;
          String err = "Logfile-Path must be a valid storage location";
          if (!result.Tokens.Any()) {  
            lf = ConfigurationManager.AppSettings["Logfile-Path"];
            if (lf == null) {
              lf = Path.Combine(Environment.CurrentDirectory, "logs");
            }
          } else {
            try {
              lf = result.Tokens.Single().Value;
            }
            catch {
              result.ErrorMessage = err;
            }
          }
        //Handles for Complete, Relative, and UNC path validation
          if (!Regex.IsMatch (lf, @"^(?:(?:[A-Za-z]:|\\\\[a-z0-9_.$●-]+\\[a-z0-9_.$●-]+)\\|\\?[^\\/:*?""<>|\r\n]+\\?)(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]*$")) {
            result.ErrorMessage = err;
          } else {
            lf = Path.GetFullPath(lf);
          }
          return lf;
        }
      );
      logfilePathOption.ArgumentHelpName = "Folder";
      logfilePathOption.Arity = ArgumentArity.ExactlyOne;

      RootCommand rootCommand = new RootCommand("Simple Windows Keyboard & Mouse \"State Monitor\" with Logging");
      rootCommand.AddOption(pollingRateOption);
      rootCommand.AddOption(displayInfoOption);
      rootCommand.AddOption(logfilePathOption);
      rootCommand.SetHandler((appOptions) => {
        Run(appOptions);
      }, new AppOptionsBinder(pollingRateOption, displayInfoOption, logfilePathOption));

      Parser parser = new CommandLineBuilder(rootCommand)
        .UseDefaults()
        .UseHelp(ctx => {
          ctx.HelpBuilder.CustomizeSymbol(pollingRateOption, secondColumnText: $"{pollingRateOption.Description} [default: 5]");
          ctx.HelpBuilder.CustomizeSymbol(displayInfoOption, secondColumnText: $"{displayInfoOption.Description} [default: True]");
          ctx.HelpBuilder.CustomizeSymbol(logfilePathOption, secondColumnText: $@"{logfilePathOption.Description} [default: .\logs");
        }).Build();

      return await parser.InvokeAsync(args);
    }

    static void Run(AppOptions appOptions) {
    //Read Assembly Info
      String AppName = Assembly.GetEntryAssembly().GetName().Name;
      String AppVers = Assembly.GetEntryAssembly().GetName().Version.ToString();

      //General Logging Scheme
      LogMode logMode = LogMode.All;

    //Startup Notification
      Console.Title = $"{AppName} v{AppVers.Substring(0, AppVers.LastIndexOf('.'))}";
      String FileName = $@"{appOptions.LogfilePath}\{AppName}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log";
      String initString = $"{Console.Title} - Starting...{Environment.NewLine}{Environment.NewLine}[POLLING RATE] - {appOptions.PollingRate}ms";
      Log(FileName,$"{initString}{Environment.NewLine}{Environment.NewLine}", LogMode.Console);
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName));
        Log(FileName, $"{initString}{Environment.NewLine}{Environment.NewLine}", LogMode.LogFile);
        Log(FileName, $"[LOGFILE PATH] - Initializating Log File...{Environment.NewLine}  {FileName}{Environment.NewLine}{Environment.NewLine}", LogMode.Console);
      }
      catch (Exception ex) {
        Log(FileName, $"[LOGFILE PATH] - Initializating Log File...{Environment.NewLine}  ERR: {ex.Message}{Environment.NewLine}{Environment.NewLine}", LogMode.Console);
        logMode = LogMode.Console;
      }

      //System Information
      if (appOptions.DisplayInfo) {
        String info = String.Empty;
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_BaseBoard")) {
          foreach (ManagementObject obj in searcher.Get()) {
            info += $"  MBD: {HandleUnknown(obj["Manufacturer"])} - {HandleUnknown(obj["Product"])}{Environment.NewLine}";
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem")) {
          foreach (ManagementObject obj in searcher.Get()) {
            info += $"  OPS: {HandleUnknown(obj["Caption"])} - {HandleUnknown(obj["OSArchitecture"])} (v{HandleUnknown(obj["Version"])}){Environment.NewLine}";
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Processor")) {
          foreach (ManagementObject obj in searcher.Get()) {
            info += $"  CPU: {HandleUnknown(obj["Name"])}{Environment.NewLine}";
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory")) {
          foreach (ManagementObject obj in searcher.Get()) {
            try {
              info += $"  RAM: {HandleUnknown(obj["BankLabel"])} - {HandleUnknown(obj["Manufacturer"])} - {HandleUnknown(obj["PartNumber"])} {MemTypeMap(Convert.ToInt32(obj["SMBIOSMemoryType"]))} ({Convert.ToInt64(obj["Capacity"]) / (1024 * 1024 * 1024) + "GB"}) - {obj["Speed"]}MHz{Environment.NewLine}";
            }
            catch { //SMBIOSMemoryType unavailable, fall back on MemoryType
              info += $"  RAM: {HandleUnknown(obj["BankLabel"])} - {HandleUnknown(obj["Manufacturer"])} - {HandleUnknown(obj["PartNumber"])} {MemTypeMap(Convert.ToInt32(obj["MemoryType"]))} ({Convert.ToInt64(obj["Capacity"]) / (1024 * 1024 * 1024) + "GB"}) - {obj["Speed"]}MHz{Environment.NewLine}";
            }
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_VideoController")) {
          foreach (ManagementObject obj in searcher.Get()) {
            if (Convert.ToInt32(obj["Availability"]) == 3) {
              info += $"  VID: {HandleUnknown(obj["Name"])}{Environment.NewLine}";
            }
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_SoundDevice")) {
          foreach (ManagementObject obj in searcher.Get()) {
            if (Convert.ToInt32(obj["StatusInfo"]) == 3) {
              info += $"  SND: {HandleUnknown(obj["Name"])}{Environment.NewLine}";
            }
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_USBController")) {
          foreach (ManagementObject obj in searcher.Get()) {
            info += $"  USB: {HandleUnknown(obj["Name"])} {Environment.NewLine}";
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Keyboard")) {
          foreach (ManagementObject obj in searcher.Get()) {
            info += $"  KEY: {HandleUnknown(obj["Name"])} - {HandleUnknown(obj["Description"])}{Environment.NewLine}";
          }
        }
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_PointingDevice")) {
          foreach (ManagementObject obj in searcher.Get()) {
            info += $"  PTR: {HandleUnknown(obj["Name"])} - {HandleUnknown(obj["Description"])}{Environment.NewLine}";
          }
        }
        Log(FileName, $"[SYSTEM SPECS] - Querying Hardware Devices...{Environment.NewLine}{info}{Environment.NewLine}", logMode);
      }

    //Data Initialization
      List<Keys> currInputState = new List<Keys>();
      List<Keys> lastInputState = new List<Keys>();
      List<KeyValuePair<Keys, String>> diffInputState = new List<KeyValuePair<Keys, String>>();
      IEnumerable<Keys> scanCodes = Enum.GetValues(typeof(Keys)).Cast<Keys>().Except(new Keys[] { Keys.Menu, Keys.ControlKey, Keys.ShiftKey });

    //Start main polling loop
      String pollString = $"[RECORD INPUT] - Monitoring Input Devices...{Environment.NewLine}";
      Log(FileName, $"{pollString}", logMode);
      while (true) {
      //Sleep based on polling rate
        Thread.Sleep(appOptions.PollingRate);

      //Read current input state
        foreach (Keys k in scanCodes) {
          Int32 keyVal = (Int32)k;
          Int64 keyState = GetAsyncKeyState(keyVal);
          if (keyState != 0) {
            if (!currInputState.Contains(k)) {
              currInputState.Add(k);
            }
          } else {
            currInputState.Remove(k);
          }
        }

      //Remove previously released inputs
        foreach (KeyValuePair<Keys, String> kvp in diffInputState.ToList()) {
          if (!lastInputState.Contains(kvp.Key)) {
            diffInputState.RemoveAll(x => x.Key == kvp.Key);
          }
        }

      //Record newly released inputs
        foreach (Keys k in lastInputState) {
          if (diffInputState.Exists(x => x.Key == k) && !currInputState.Contains(k)) {
            Int32 index = diffInputState.FindIndex(x => x.Key == k);
            diffInputState.RemoveAll(x => x.Key == k);
            diffInputState.Insert(index, new KeyValuePair<Keys, String>(k, "↑"));
          }
        }

      //Record newly pressed inputs and previously pressed "held" inputs
        foreach (Keys k in currInputState) {
          if (diffInputState.Exists(x => x.Key == k)) {
            Int32 index = diffInputState.FindIndex(x => x.Key == k);
            diffInputState.RemoveAll(x => x.Key == k);
            diffInputState.Insert(index, new KeyValuePair<Keys, String>(k, "|"));
          } else {
            if (!lastInputState.Contains(k)) {
              diffInputState.Add(new KeyValuePair<Keys, String>(k, "↓"));
            }
          }
        }

      //If the state has changed, display it to the screen and record it to a log file
        if (!Enumerable.SequenceEqual(currInputState.OrderBy(e => e), lastInputState.OrderBy(e => e))) {
          IEnumerable<String> keyStates = diffInputState.Select(x => $"{KeyCodeMap(x.Key, false)}{x.Value}");
          String stateString = $"  {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:ffff")}  {String.Join(" ", keyStates)}";
          Log(FileName, $"{stateString}{Environment.NewLine}", logMode);
          lastInputState = currInputState.ToList();
        }

      }

    }

    static void Log(String file, String line, LogMode mode = LogMode.All) {
      switch (mode) {
        case LogMode.All:
          Console.Write(line);
          File.AppendAllText(file, line);
          break;
        case LogMode.Console:
          Console.Write(line);
          break;
        case LogMode.LogFile:
          File.AppendAllText(file, line);
          break;
      }
    }
  }

}
