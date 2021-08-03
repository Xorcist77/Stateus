using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeyState {
  class Program {
    [DllImport("user32.dll")]
    public static extern Int32 GetAsyncKeyState(Int32 i);

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

    //App Initialization
      Console.Title = $"{AppName} v{AppVers.Substring(0, AppVers.LastIndexOf('.'))}";
      String FileName = $@"{AppDomain.CurrentDomain.BaseDirectory}\{AppName}_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log";
      List<Keys> currInputState = new List<Keys>();
      List<Keys> lastInputState = new List<Keys>();
      List<KeyValuePair<Keys, String>> diffInputState = new List<KeyValuePair<Keys, String>>();
      IEnumerable<Keys> scanCodes = Enum.GetValues(typeof(Keys)).Cast<Keys>().Except(new Keys[] { Keys.Menu, Keys.ControlKey, Keys.ShiftKey });

    //Start main polling loop
      String initString = $"{Console.Title} - Started: Monitoring {scanCodes.Count()} possible scan codes... (PollRate = {PollingRate}ms)";
      Console.WriteLine(initString);
      File.AppendAllText(FileName, initString + Environment.NewLine);
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
          IEnumerable<String> keyStates = diffInputState.Select(x => $"{x.Key}{x.Value}");
          String stateString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:ffff") + " " + String.Join(" ", keyStates);
          Console.WriteLine(stateString);
          File.AppendAllText(FileName, stateString + Environment.NewLine);
          lastInputState = currInputState.ToList();
        }
      }

    }

  }
}
