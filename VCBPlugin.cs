using BepInEx;
using BepInEx.Configuration;
using System.Threading;
using System.IO;
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ValheimCharacterBackup
{
    [BepInPlugin("jdodger415.ValheimCharacterBackup", "Valheim Character Backup", "1.3.1")]
    public class VCBPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> CharEnabled;
        public static ConfigEntry<bool> WrldEnabled;
        public static ConfigEntry<int> IntervalChar;
        public static ConfigEntry<int> IntervalWrld;
        public static ConfigEntry<int> BackupCountChar;
        public static ConfigEntry<int> BackupCountWrld;
        public static Thread BackupThreadChar;
        public static Thread BackupThreadWrld;
        public static ConfigEntry<bool> IntervalModEnabled;
        public static ConfigEntry<int> IntervalTime;
        public static float IntervalSeconds;
        public static Harmony harmonyVCB = new Harmony("jdodger415.VCB");

        public void BackupChar(string CurrentTime)
        {
            var CharSourceFolder = Environment.GetEnvironmentVariable("appdata") + "\\..\\LocalLow\\IronGate\\Valheim\\characters";
            var CharDestFolder = Environment.GetEnvironmentVariable("appdata") + "\\..\\LocalLow\\IronGate\\Valheim\\characters.bak\\" + CurrentTime;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CharSourceFolder = Environment.GetEnvironmentVariable("HOME") + "/.config/unity3d/IronGate/Valheim/characters";
                CharDestFolder = Environment.GetEnvironmentVariable("HOME") + "/.config/unity3d/IronGate/Valheim/characters.bak" + CurrentTime;
            }

            try
            {
                Logger.LogWarning("Character Backup Starting...");
                CopyFolder(CharSourceFolder, CharDestFolder);
                Logger.LogWarning("Character Backup Complete!");

                if (BackupCountChar.Value != 0)
                {
                    IOrderedEnumerable<FileSystemInfo> CharBackupDir = new DirectoryInfo(CharSourceFolder + ".bak").GetFileSystemInfos().OrderByDescending(fi => fi.CreationTime);
                    if (CharBackupDir.Count() > BackupCountChar.Value)
                    {
                        Logger.LogWarning("Too many character backups found, deleting oldest...");
                        Directory.Delete(CharBackupDir.Last().FullName, true);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message + "\nUnable to make character backup\nExtra Info;\n\nCharacter;\nSource folder: " + CharSourceFolder + "\nDestination Folder: " + CharDestFolder);
            }
        }

        public void BackupWorkerChar()
        {
            Thread.Sleep(10000);
            while (true)
            {
                Thread.Sleep(IntervalChar.Value * 1000);

                BackupChar(DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            }
        }

        public void BackupWrld(string CurrentTime)
        {
            var WrldSourceFolder = Environment.GetEnvironmentVariable("appdata") + "\\..\\LocalLow\\IronGate\\Valheim\\worlds";
            var WrldDestFolder = Environment.GetEnvironmentVariable("appdata") + "\\..\\LocalLow\\IronGate\\Valheim\\worlds.bak\\" + CurrentTime;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                WrldSourceFolder = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") + "/unity3d/IronGate/Valheim/worlds";
                WrldDestFolder = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") + "/unity3d/IronGate/Valheim/worlds.bak" + CurrentTime;
            }

            try
            {
                Logger.LogWarning("World Backup Starting...");
                CopyFolder(WrldSourceFolder, WrldDestFolder);
                Logger.LogWarning("World Backup Complete!");

                if (BackupCountWrld.Value != 0)
                {
                    IOrderedEnumerable<FileSystemInfo> WrldBackupDir = new DirectoryInfo(WrldSourceFolder + ".bak").GetFileSystemInfos().OrderByDescending(fi => fi.CreationTime);
                    if (WrldBackupDir.Count() > BackupCountWrld.Value)
                    {
                        Logger.LogWarning("Too many world backups found, deleting oldest...");
                        Directory.Delete(WrldBackupDir.Last().FullName, true);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message + "\nUnable to make world backup\nExtra Info;\n\nWorld;\nSource folder: " + WrldSourceFolder + "\nDestination Folder: " + WrldDestFolder);
            }
        }

        public void BackupWorkerWrld()
        {
            Thread.Sleep(10000);
            while (true)
            {
                Thread.Sleep(IntervalWrld.Value * 1000);

                BackupWrld(DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            }
        }

        [HarmonyPrefix]
        public static void UpdateSavingPrefix(ref Game __instance, ref float dt)
        {
            var m_saveTimerField = __instance.GetType().GetField("m_saveTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            var SavePlayerProfileMethod = __instance.GetType().GetMethod("SavePlayerProfile", BindingFlags.NonPublic | BindingFlags.Instance);

            var m_saveTimer = (float)m_saveTimerField.GetValue(__instance);

            m_saveTimerField.SetValue(__instance, m_saveTimer + dt);
            if (!(m_saveTimer <= IntervalSeconds))
            {
                Debug.LogWarning("Saving the game @"+DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy"));
                m_saveTimerField.SetValue(__instance, 0);
                SavePlayerProfileMethod.Invoke(__instance, new object[] { false });
                
                if (ZNet.instance)
                {
                    ZNet.instance.Save(false);
                }
            }

            return;
        }

        [HarmonyPostfix]
        public static void UpdatePostfix()
        {
            if (CharEnabled.Value)
            {
                Debug.LogWarning("VCB: Starting character backup thread...");
                BackupThreadChar.Start();
            }

            if (WrldEnabled.Value)
            {
                Debug.LogWarning("VCB: Starting world backup thread...");
                BackupThreadWrld.Start();
            }

            harmonyVCB.Unpatch(AccessTools.Method(typeof(Game), "Update"), typeof(VCBPlugin).GetMethod("UpdatePostfix"));
        }

        public void Awake()
        {
            Logger.LogWarning("Loading Config...");
            IntervalChar = Config.Bind("Valheim Character Backup", "BackupIntervalChar", 300, "Interval, in seconds, between backups for the characters folder.");
            IntervalWrld = Config.Bind("Valheim World Backup", "BackupIntervalWrld", 300, "Interval, in seconds, between backups for the worlds folder.");
            BackupCountChar = Config.Bind("Valheim Character Backup", "BackupCountChar", 10, "Number of backups to keep for the characters folder. Set to 0 to disable");
            BackupCountWrld = Config.Bind("Valheim World Backup", "BackupCountWrld", 10, "Number of backups to keep for the worlds folder. Set to 0 to disable");
            CharEnabled = Config.Bind("Valheim Character Backup", "CharacterBackupEnabled", true, "Enable backups for the caracter folder (%appdata%\\..\\LocalLow\\IronGate\\Valheim\\characters.bak)");
            WrldEnabled = Config.Bind("Valheim World Backup", "WorldBackupEnabled", true, "Enable backups for the world folder (%appdata%\\..\\LocalLow\\IronGate\\Valheim\\worlds.bak)");
            IntervalModEnabled = Config.Bind("Valheim Save Interval", "IntervalModEnabled", true, "Enable modification of the games character and world save interval.\nThis can and will conflict with Valheim Plus if the save interval is modified.\nSet to false if you use Valheim Plus' save interval modifier OR\nrestore the default 1200s in Valheim Plus' config OR\ndisable the server section in Valheim Plus' config.");
            IntervalTime = Config.Bind("Valheim Save Interval", "IntervalTime", 0, "Interval in seconds to save the character and world files. Set to 0 for automatic calculation");
            Logger.LogWarning("Config Loaded!");

            if (CharEnabled.Value)
            {
                BackupThreadChar = new Thread(BackupWorkerChar);
            }

            if (WrldEnabled.Value)
            {
                BackupThreadWrld = new Thread(BackupWorkerWrld);
            }

            if (CharEnabled.Value || WrldEnabled.Value)
            {
                harmonyVCB.Patch(AccessTools.Method(typeof(Game), "Update"), postfix: new HarmonyMethod(typeof(VCBPlugin), "UpdatePostfix"));
            }

            if (IntervalModEnabled.Value)
            {
                harmonyVCB.Patch(AccessTools.Method(typeof(Game), "UpdateSaving"), prefix: new HarmonyMethod(typeof(VCBPlugin), "UpdateSavingPrefix"));

                if (IntervalTime.Value == 0 && CharEnabled.Value && WrldEnabled.Value)
                {
                    var largest = (IntervalChar.Value > IntervalWrld.Value) ? IntervalChar.Value : IntervalWrld.Value;
                    var num = largest;
                    for (num=largest; num>=0; num--)
                    {
                        if (IntervalChar.Value % num == 0 && IntervalWrld.Value % num == 0)
                        {
                            IntervalSeconds = num;
                            break;
                        }
                    }
                }
                else if (IntervalTime.Value == 0 && CharEnabled.Value)
                {
                    IntervalSeconds = IntervalChar.Value;
                }
                else if (IntervalTime.Value == 0 && WrldEnabled.Value)
                {
                    IntervalSeconds = IntervalWrld.Value;
                }
                else if (IntervalTime.Value == 0)
                {
                    IntervalSeconds = 1200;
                }

                IntervalSeconds *= 2;

                Logger.LogWarning("Save interval is set to "+(IntervalSeconds / 2)+"s");
            }
        }

        public void OnDestroy()
        {
            Logger.LogWarning("Making Final Backups...");

            if (CharEnabled.Value)
            {
                BackupChar(DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Logger.LogWarning("Ending character backup thread *wave*");
                BackupThreadChar.Abort();
            }

            if (WrldEnabled.Value)
            {
                BackupWrld(DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Logger.LogWarning("Ending world backup thread *wave*");
                BackupThreadWrld.Abort();
            }
        }

        static public void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
                Directory.CreateDirectory(destFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                File.Copy(file, Path.Combine(destFolder, Path.GetFileName(file)));
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                CopyFolder(folder, Path.Combine(destFolder, Path.GetFileName(folder)));
            }
        }
    }
}
