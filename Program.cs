using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Stateus {

  class Program {

    [DllImport("user32.dll")]
    public static extern Int32 GetAsyncKeyState(Int32 i);

    [DllImport("user32.dll")]
    static extern Int32 MapVirtualKey(UInt32 uCode, UInt32 uMapType);

    static String MemTypeMap(Int32 memType) {
      switch (memType) {
        case  1: return "Other";
        case  2: return "DRAM";
        case  3: return "DRAM (Synchronous)";
        case  4: return "DRAM (Cache)";
        case  5: return "EDO";
        case  6: return "EDRAM";
        case  7: return "VRAM";
        case  8: return "SRAM";
        case  9: return "RAM";
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

    static void Main(String[] args) {
    //Process Config Options
      Int32 PollingRate;
      if (Int32.TryParse(ConfigurationManager.AppSettings["PollingRate.ms"], out PollingRate)) {
        if (PollingRate <= 0) {
          PollingRate = 5;
        }
      } else {
        PollingRate = 5;
      }

    //Read Assembly Info
      String AppName = Assembly.GetEntryAssembly().GetName().Name;
      String AppVers = Assembly.GetEntryAssembly().GetName().Version.ToString();

    //Startup Notification
      Console.Title = $"{AppName} v{AppVers.Substring(0, AppVers.LastIndexOf('.'))}";
      String FileName = $@"{AppDomain.CurrentDomain.BaseDirectory}\{AppName}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log";
      String initString = $"{Console.Title} - Starting...";
      Console.WriteLine(initString + Environment.NewLine);
      File.AppendAllText(FileName, initString + Environment.NewLine + Environment.NewLine);

    //System Information
      String info = String.Empty;
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_BaseBoard")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"  Motherboard: {obj["Manufacturer"]} - {obj["Product"]}{Environment.NewLine}";
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"  Environment: {obj["Caption"]} - {obj["OSArchitecture"]} (v{obj["Version"]}){Environment.NewLine}";
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Processor")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"    Processor: {obj["Name"]}{Environment.NewLine}";
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"       Memory: {obj["Manufacturer"]} - {obj["PartNumber"]} {MemTypeMap(Convert.ToInt32(obj["SMBIOSMemoryType"]))} ({Convert.ToInt64(obj["Capacity"]) / (1024 * 1024 * 1024) + "GB"}) - {obj["Speed"]}MHz{Environment.NewLine}";
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_VideoController")) {
        foreach (ManagementObject obj in searcher.Get()) {
          if (Convert.ToInt32(obj["Availability"]) == 3) {
            info += $"   Video Card: {obj["Name"]}{Environment.NewLine}";
          }
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_SoundDevice")) {
        foreach (ManagementObject obj in searcher.Get()) {
          if (Convert.ToInt32(obj["StatusInfo"]) == 3) {
            info += $"   Sound Card: {obj["Name"]}{Environment.NewLine}";
          }
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_USBController")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"          USB: {obj["Name"]} {Environment.NewLine}";
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_Keyboard")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"     Keyboard: {obj["Name"]} - {obj["Description"]}{Environment.NewLine}";
        }
      }
      using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_PointingDevice")) {
        foreach (ManagementObject obj in searcher.Get()) {
          info += $"      Pointer: {obj["Name"]} - {obj["Description"]}{Environment.NewLine}";
        }
      }

      Console.Write(info);
      File.AppendAllText(FileName, info);

    //Data Initialization
      Console.WriteLine();
      File.AppendAllText(FileName, Environment.NewLine);
      List<Keys> currInputState = new List<Keys>();
      List<Keys> lastInputState = new List<Keys>();
      List<KeyValuePair<Keys, String>> diffInputState = new List<KeyValuePair<Keys, String>>();
      IEnumerable<Keys> scanCodes = Enum.GetValues(typeof(Keys)).Cast<Keys>().Except(new Keys[] { Keys.Menu, Keys.ControlKey, Keys.ShiftKey });

    //Start main polling loop
      String pollString = $"Started! Monitoring {scanCodes.Count()} possible scan codes... (polling rate = {PollingRate}ms)";
      Console.WriteLine(pollString + Environment.NewLine);
      File.AppendAllText(FileName, pollString + Environment.NewLine + Environment.NewLine);
      while (true) {
      //Sleep based on polling rate
        Thread.Sleep(PollingRate);

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
          Console.WriteLine(stateString);
          File.AppendAllText(FileName, stateString + Environment.NewLine);
          lastInputState = currInputState.ToList();
        }

      }

    }

  }

}
