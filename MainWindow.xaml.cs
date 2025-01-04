using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using HtmlAgilityPack;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;

public class MainWindow : Window, IComponentConnector
{
    public class ModpackData
    {
        public List<string> Dependencies { get; set; }

        public ModpackData(List<string> dependencies)
        {
            Dependencies = dependencies;
        }
    }

    internal Button Start;

    internal Button MP_Download;

    internal TextBlock TooltipMPDownload;

    internal Button CloseButton;

    internal ProgressBar ProgressLeiste;

    internal Label Label;

    internal Button InstallGame;

    internal Button FixValheim;

    internal Label MPLokal;

    internal Label MPOnline;

    internal CheckBox Vulkan;

    private bool _contentLoaded;

    public MainWindow()
    {
        InitializeComponent();
        ProgressLeiste.Dispatcher.Invoke(delegate
        {
            ProgressLeiste.Visibility = Visibility.Hidden;
        });
        string text = "";
        try
        {
            using StreamReader streamReader = new StreamReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VulkanSetting.txt"));
            text = streamReader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler beim Lesen der Vulkan-Einstellungen: " + ex.Message);
        }
        if (text == "1")
        {
            Vulkan.IsChecked = true;
        }
        else
        {
            Vulkan.IsChecked = false;
        }
        if (Checkstatus())
        {
            Start.IsEnabled = false;
            FixValheim.Visibility = Visibility.Hidden;
            base.Loaded += MainWindow_Loaded;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await Check_Versions();
    }

    private async Task Check_Versions()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx");
        string localDirectoryPathversion = Path.Combine(path, "versions.json");
        (string, string, string, string[]) tuple = await GetDownloadUrlAndVersionAsync("TeamKoro/Mithrael_Modpack");
        string versionNumber = tuple.Item2;
        string response = tuple.Item3;
        string[] dependencies = tuple.Item4;
        if (!File.Exists(localDirectoryPathversion) && response != null)
        {
            await CreateVersionFile(localDirectoryPathversion, response);
            await UpdateVersionFileOnly(localDirectoryPathversion);
        }
        else if (!File.Exists(localDirectoryPathversion) || response == null)
        {
            Label.Dispatcher.Invoke(delegate
            {
                Label.Content = "Kein Internet oder die Firewall blockiert das Programm!";
            });
            return;
        }
        GetVersionNumberFromJson(localDirectoryPathversion);
        Label.Dispatcher.Invoke(delegate
        {
            MPLokal.Content = "v. " + GetVersionNumberFromJson(localDirectoryPathversion);
            MPOnline.Content = "v. " + versionNumber;
        });
        if (GetVersionNumberFromJson(localDirectoryPathversion) != versionNumber)
        {
            await UpdateVersionFileDependencies(localDirectoryPathversion, dependencies);
            await Start_Download();
            await UpdateVersionFile(localDirectoryPathversion, response);
            Label.Dispatcher.Invoke(delegate
            {
                MPLokal.Content = "v. " + GetVersionNumberFromJson(localDirectoryPathversion);
                MPOnline.Content = "v. " + versionNumber;
            });
            Checkstatus();
        }
        else if (GetVersionNumberFromJson(localDirectoryPathversion) == versionNumber)
        {
            if (GetVersionNumberFromJson(localDirectoryPathversion) == null && versionNumber == null)
            {
                Label.Dispatcher.Invoke(delegate
                {
                    MPLokal.Content = "v. " + GetVersionNumberFromJson(localDirectoryPathversion);
                    MPOnline.Content = "v. " + versionNumber;
                });
                InstallGame.Visibility = Visibility.Hidden;
                FixValheim.Visibility = Visibility.Hidden;
            }
            else
            {
                Label.Dispatcher.Invoke(delegate
                {
                    MPLokal.Content = "v. " + GetVersionNumberFromJson(localDirectoryPathversion);
                    MPOnline.Content = "v. " + versionNumber;
                });
                Checkstatus();
            }
        }
        else if (response == null || versionNumber == null)
        {
            Label.Dispatcher.Invoke(delegate
            {
                Label.Content = "Fehler beim downloaden der Mods.";
            });
        }
    }

    private async Task<bool> Start_Download()
    {
        Start.IsEnabled = false;
        InstallGame.Visibility = Visibility.Hidden;
        FixValheim.Visibility = Visibility.Hidden;
        bool resultFTPFiles = false;
        try
        {
            Label.Content = "Überprüfe Installierte Mods!";
            resultFTPFiles = await Task.Run(() => DownloadModiDirectory()).ConfigureAwait(continueOnCapturedContext: true);
            Start.IsEnabled = true;
            InstallGame.Visibility = Visibility.Hidden;
            FixValheim.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fehlercode: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
        Label.Dispatcher.Invoke(delegate
        {
            Label.Content = "";
        });
        return resultFTPFiles;
    }

    private bool Checkstatus()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "valheim.exe");
        string path2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "valheim_Data", "boot.config");
        bool flag = File.Exists(path);
        bool flag2 = File.Exists(path2);
        Start.IsEnabled = flag && flag2;
        FixValheim.Visibility = ((!(flag && flag2)) ? Visibility.Hidden : Visibility.Visible);
        InstallGame.Visibility = ((flag && flag2) ? Visibility.Hidden : Visibility.Visible);
        MP_Download.Visibility = ((!flag || !flag2) ? Visibility.Hidden : Visibility.Visible);
        if (flag && flag2)
        {
            return true;
        }
        return false;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        await start_process();
    }

    private async Task start_process()
    {
        Start.IsEnabled = false;
        InstallGame.Visibility = Visibility.Hidden;
        FixValheim.Visibility = Visibility.Hidden;
        string steamPath = GetSteamPath();
        if (steamPath != null)
        {
            if (!IsProcessRunning("steam"))
            {
                Process.Start(steamPath).WaitForInputIdle();
            }
            while (!IsProcessRunning("steam"))
            {
                Thread.Sleep(100);
            }
            if (Label != null)
            {
                Label.Dispatcher.Invoke(delegate
                {
                    Label.Content = "Starte das Spiel!";
                });
            }
            if (Vulkan.IsChecked == true)
            {
                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VulkanSetting.txt")))
                {
                    streamWriter.Write("1");
                }
                Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "valheim.exe"), "-force-vulkan");
            }
            else
            {
                using (StreamWriter streamWriter2 = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VulkanSetting.txt")))
                {
                    streamWriter2.Write("0");
                }
                Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "valheim.exe"));
            }
            Close();
        }
        else
        {
            MessageBox.Show("Fehlercode: Kein Steam zum Starten gefunden.");
        }
    }

    private bool IsProcessRunning(string processName)
    {
        return Process.GetProcessesByName(processName).Length != 0;
    }

    private string GetSteamPath()
    {
        string[] array = new string[2] { "HKEY_CURRENT_USER\\Software\\Valve\\Steam", "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam" };
        for (int i = 0; i < array.Length; i++)
        {
            string text = (string)Registry.GetValue(array[i], "SteamPath", null);
            if (!string.IsNullOrEmpty(text))
            {
                return Path.Combine(text, "Steam.exe");
            }
        }
        return null;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task<bool> DownloadModiDirectory()
    {
        ProgressLeiste.Dispatcher.Invoke(delegate
        {
            ProgressLeiste.Visibility = Visibility.Visible;
        });
        string localDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx");
        string sourceFolder = Path.Combine(localDirectoryPath, "plugins", "1AditionalMods");
        string destinationPath = Path.Combine(localDirectoryPath, "1AditionalMods");
        AutoResetEvent deleteCompleted = new AutoResetEvent(initialState: false);
        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(sourceFolder);
            Thread.Sleep(100);
        }
        if (Directory.Exists(sourceFolder))
        {
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath);
            }
            FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(sourceFolder));
            try
            {
                fileSystemWatcher.Deleted += delegate
                {
                    deleteCompleted.Set();
                };
                fileSystemWatcher.EnableRaisingEvents = true;
                Directory.Move(sourceFolder, destinationPath);
                deleteCompleted.WaitOne();
            }
            finally
            {
                ((IDisposable)fileSystemWatcher)?.Dispose();
            }
        }
        IProgress<int> progress = new Progress<int>(delegate (int percentage)
        {
            ProgressLeiste.Dispatcher.Invoke(delegate
            {
                ProgressLeiste.Value = percentage;
            });
        });
        string versionFilePath = Path.Combine(localDirectoryPath, "versions.json");
        DeleteLocalFilesNotInModpackDataList(localDirectoryPath, versionFilePath);
        string versionFilePath2 = Path.Combine(localDirectoryPath, "versions.json");
        await DownloadAndExtractModpackAsync(versionFilePath2, localDirectoryPath, progress);
        await DeleteLocalFilesNotInModpackDataList(localDirectoryPath, versionFilePath2);
        AutoResetEvent moveComplete = new AutoResetEvent(initialState: false);
        if (Directory.Exists(destinationPath))
        {
            FileSystemWatcher fileSystemWatcher2 = new FileSystemWatcher(Path.GetDirectoryName(destinationPath));
            try
            {
                fileSystemWatcher2.Deleted += delegate
                {
                    moveComplete.Set();
                };
                fileSystemWatcher2.EnableRaisingEvents = true;
                Directory.Move(destinationPath, sourceFolder);
                moveComplete.WaitOne();
            }
            finally
            {
                ((IDisposable)fileSystemWatcher2)?.Dispose();
            }
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath);
            }
        }
        ProgressLeiste.Dispatcher.Invoke(delegate
        {
            ProgressLeiste.Visibility = Visibility.Hidden;
        });
        return Directory.Exists(sourceFolder);
    }

    private string GetVersionNumberFromJson(string jsonFilePath)
    {
        try
        {
            dynamic val = JObject.Parse(File.ReadAllText(jsonFilePath));
            return val.latest.version_number;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler beim Lesen der Versionsnummer aus der JSON-Datei: " + ex.Message);
            return null;
        }
    }

    private async Task CreateVersionFile(string versionFilePath, string response)
    {
        try
        {
            string directoryName = Path.GetDirectoryName(versionFilePath);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }
            using StreamWriter streamWriter = File.CreateText(versionFilePath);
            streamWriter.Write(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler beim Erstellen der JSON-Datei: " + ex.Message);
        }
    }

    private async Task UpdateVersionFile(string versionFilePath, string response)
    {
        SaveJsonToFile(versionFilePath, response);
    }

    private async Task UpdateVersionFileDependencies(string versionFilePath, string[] dependencies)
    {
        JObject jObject = JObject.Parse(File.ReadAllText(versionFilePath));
        ((JArray)jObject["latest"]["dependencies"]).ReplaceAll(dependencies);
        string contents = JsonConvert.SerializeObject(jObject, Formatting.Indented);
        File.WriteAllText(versionFilePath, contents);
    }

    private async Task UpdateVersionFileOnly(string versionFilePath)
    {
        try
        {
            string text = "0.0.1";
            JObject jObject = JObject.Parse(File.ReadAllText(versionFilePath));
            JToken jToken = jObject.SelectToken("latest.version_number");
            if (jToken != null)
            {
                jToken.Replace(text);
            }
            else
            {
                jObject["latest"]["version_number"] = text;
            }
            string contents = jObject.ToString(Formatting.Indented);
            File.WriteAllText(versionFilePath, contents);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler beim Aktualisieren der Versionsnummer: " + ex.Message);
        }
    }

    private void SaveJsonToFile(string filePath, string jsonResponse)
    {
        try
        {
            File.WriteAllText(filePath, jsonResponse);
            Console.WriteLine("JSON-Antwort erfolgreich in die Datei gespeichert.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler beim Speichern der JSON-Antwort in die Datei: " + ex.Message);
        }
    }

    private async Task<(string downloadUrl, string versionNumber, string response, string[] dependencies)> GetDownloadUrlAndVersionAsync(string modpackId)
    {
        string address = "https://thunderstore.io/api/experimental/package/" + modpackId;
        try
        {
            WebClient client = new WebClient();
            try
            {
                string text = await client.DownloadStringTaskAsync(address);
                dynamic val = JObject.Parse(text);
                string item = val.latest.download_url;
                string item2 = val.latest.version_number;
                List<string> list = new List<string>();
                foreach (dynamic item4 in val.latest.dependencies)
                {
                    list.Add(item4.ToString());
                }
                string[] item3 = list.ToArray();
                return (downloadUrl: item, versionNumber: item2, response: text, dependencies: item3);
            }
            finally
            {
                ((IDisposable)client)?.Dispose();
            }
        }
        catch (Exception)
        {
            return (downloadUrl: null, versionNumber: null, response: null, dependencies: null);
        }
    }

    private async Task<bool> DownloadAndExtractModpackAsync(string versionFilePath, string localDirectoryPath, IProgress<int> progress)
    {
        _ = 3;
        try
        {
            dynamic val = JObject.Parse(await File.ReadAllTextAsync(versionFilePath));
            string pluginsPath = Path.Combine(localDirectoryPath, "plugins");
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string destinationPath = Path.Combine(baseDirectory, "BepInExPack_Valheim");
            ModpackData modpackData = new ModpackData(val.latest.dependencies.ToObject<List<string>>());
            if (modpackData != null)
            {
                using StreamWriter streamWriter = new StreamWriter(Path.Combine(localDirectoryPath, "Dependencies.txt"));
                streamWriter.WriteLine("Dependencies:");
                foreach (string dependency2 in modpackData.Dependencies)
                {
                    streamWriter.WriteLine(dependency2);
                }
            }
            else
            {
                Label.Dispatcher.Invoke(delegate
                {
                    Label.Content = "Keine Jsondaten zum auslesen!";
                });
            }
            string dependencyName = "";
            string pluginZipDirectory = Path.Combine(localDirectoryPath, "pluginZip");
            Directory.CreateDirectory(pluginZipDirectory);
            using (HttpClient client = new HttpClient())
            {
                TimeSpan timeout = (client.Timeout = TimeSpan.FromSeconds(5000.0));
                client.Timeout = timeout;
                int totalDependencies = modpackData.Dependencies.Count;
                int completedDependencies = 0;
                foreach (string dependency in modpackData.Dependencies)
                {
                    bool downloadSuccessful = false;
                    int currentRetryAttempt = 0;
                    while (!downloadSuccessful && currentRetryAttempt < 100)
                    {
                        try
                        {
                            dependencyName = dependency;
                            string dependencyUrl = "https://thunderstore.io/package/download/" + dependency.Replace("-", "/") + "/";
                            string downloadFilePath = Path.Combine(pluginZipDirectory, dependencyName + ".zip");
                            HttpResponseMessage response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, dependencyUrl));
                            if (response.IsSuccessStatusCode)
                            {
                                long? contentLength = response.Content.Headers.ContentLength;
                                long num = (File.Exists(downloadFilePath) ? new FileInfo(downloadFilePath).Length : 0);
                                Label.Dispatcher.Invoke(delegate
                                {
                                    Label.Content = dependency;
                                });
                                if (num != contentLength)
                                {
                                    using FileStream fileStream = new FileStream(downloadFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                                    response = await client.GetAsync(dependencyUrl);
                                    Console.WriteLine("Datei wird überschrieben...");
                                    await response.Content.CopyToAsync(fileStream);
                                }
                                downloadSuccessful = true;
                            }
                            else
                            {
                                Label.Dispatcher.Invoke(delegate
                                {
                                    Label.Content = $"Fehler beim Herunterladen der Datei: {response.StatusCode}";
                                });
                            }
                        }
                        catch (HttpRequestException)
                        {
                            Label.Dispatcher.Invoke(delegate
                            {
                                Label.Content = "Timeout-Fehler";
                            });
                            currentRetryAttempt++;
                        }
                    }
                    string text = Path.Combine(pluginZipDirectory, dependencyName + ".zip");
                    string text2 = Path.Combine(pluginsPath, dependencyName);
                    baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    Directory.CreateDirectory(text2);
                    if (text.Contains("denikson-BepInExPack_Valheim"))
                    {
                        using IArchive archive = ArchiveFactory.Open(text);
                        foreach (IArchiveEntry entry in archive.Entries)
                        {
                            if (!entry.IsDirectory)
                            {
                                entry.WriteToDirectory(baseDirectory, new ExtractionOptions
                                {
                                    ExtractFullPath = true,
                                    Overwrite = true
                                });
                            }
                        }
                    }
                    else
                    {
                        using IArchive archive2 = ArchiveFactory.Open(text);
                        foreach (IArchiveEntry entry2 in archive2.Entries)
                        {
                            if (!entry2.IsDirectory)
                            {
                                switch (Path.GetExtension(entry2.Key).ToLower())
                                {
                                    case ".dll":
                                    case ".yml":
                                    case ".json":
                                    case "":
                                        entry2.WriteToDirectory(text2, new ExtractionOptions
                                        {
                                            ExtractFullPath = true,
                                            Overwrite = true
                                        });
                                        break;
                                }
                            }
                        }
                    }
                    if (!downloadSuccessful)
                    {
                        Label.Dispatcher.Invoke(delegate
                        {
                            Label.Content = "Download fehlgeschlagen: Timeout beim Herunterladen der Datei " + dependencyName;
                        });
                    }
                    else
                    {
                        completedDependencies++;
                    }
                    int value = (int)((float)completedDependencies / (float)totalDependencies * 100f);
                    progress?.Report(value);
                }
            }
            AutoResetEvent moveComplete = new AutoResetEvent(initialState: false);
            if (Directory.Exists(destinationPath))
            {
                FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(destinationPath));
                try
                {
                    fileSystemWatcher.Deleted += delegate
                    {
                        moveComplete.Set();
                    };
                    fileSystemWatcher.EnableRaisingEvents = true;
                    FileSystem.MoveDirectory(destinationPath, baseDirectory, overwrite: true);
                    moveComplete.WaitOne();
                }
                finally
                {
                    ((IDisposable)fileSystemWatcher)?.Dispose();
                }
            }
            return true;
        }
        catch (Exception ex2)
        {
            MessageBox.Show("Fehler: " + ex2.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Hand);
            return false;
        }
    }

    private async Task DeleteLocalFilesNotInModpackDataList(string localDirectoryPath, string versionFilePath)
    {
        dynamic val = JObject.Parse(File.ReadAllText(versionFilePath));
        string path = Path.Combine(localDirectoryPath, "plugins");
        string path2 = Path.Combine(localDirectoryPath, "pluginZip");
        List<string> list = val.latest.dependencies.ToObject<List<string>>();
        HashSet<string> hashSet = new HashSet<string>(new ModpackData(list).Dependencies);
        if (!Directory.Exists(path2))
        {
            Directory.CreateDirectory(path2);
        }
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        try
        {
            FileSystemInfo[] fileSystemInfos = new DirectoryInfo(path).GetFileSystemInfos();
            foreach (FileSystemInfo fileSystemInfo in fileSystemInfos)
            {
                string name = fileSystemInfo.Name;
                if (!hashSet.Contains(name) && !name.Contains("BepInEx") && !name.Contains("MMHOOK"))
                {
                    if (fileSystemInfo is FileInfo fileInfo)
                    {
                        fileInfo.Delete();
                    }
                    else if (fileSystemInfo is DirectoryInfo directoryInfo)
                    {
                        directoryInfo.Delete(recursive: true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fehlercode: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
        try
        {
            FileInfo[] files = new DirectoryInfo(path2).GetFiles("*", System.IO.SearchOption.TopDirectoryOnly);
            foreach (FileInfo fileInfo2 in files)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileInfo2.Name);
                if (!list.Contains(fileNameWithoutExtension))
                {
                    fileInfo2.Delete();
                }
            }
        }
        catch (Exception ex2)
        {
            MessageBox.Show("Fehlercode: " + ex2.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Hand);
        }
    }

    private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private async void InstallGame_Click(object sender, RoutedEventArgs e)
    {
        InstallGame.Visibility = Visibility.Hidden;
        Label.Content = "Das Hauptspiel wird geladen, das kann einige Zeit in Anspruch nehmen!";
        await Task.Run(async delegate
        {
            await deleteFolder();
            await installer();
        });
        Checkstatus();
        Label.Content = "Das Hauptspiel wurde fertig geladen";
        await Check_Versions();
    }

    private async void FixValheim_Click(object sender, RoutedEventArgs e)
    {
        FixValheim.Visibility = Visibility.Hidden;
        Start.IsEnabled = false;
        Label.Content = "Überprüfe vorhandene Daten auf Fehler!";
        await Task.Run(async delegate
        {
            await deleteFolder();
            await installer();
        });
        Checkstatus();
        Label.Content = "Überprüfung beendet, bereit zum Starten!";
    }

    private async Task deleteFolder()
    {
        string[] obj = new string[5] { "BepInEx/patchers", "BepInEx/config/Azumatt.MinimalUI_Backgrounds", "BepInEx/config/Intermission", "BepInEx/config/Seasonality", "valheim_Data" };
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string[] array = obj;
        foreach (string path in array)
        {
            string text = Path.Combine(baseDirectory, path);
            if (Directory.Exists(text))
            {
                Directory.Delete(text, recursive: true);
                Console.WriteLine("Deleted: " + text);
            }
            else
            {
                Console.WriteLine("Directory not found: " + text);
            }
        }
    }

    private async Task installer()
    {
        string serverUri = "http://37.114.34.12/ValheimWithBepInEx/";
        ProgressLeiste.Dispatcher.Invoke(delegate
        {
            ProgressLeiste.Value = 0.0;
            ProgressLeiste.Visibility = Visibility.Visible;
        });
        foreach (string item in new List<string> { "BepInEx/patchers", "BepInEx/config/Azumatt.MinimalUI_Backgrounds", "BepInEx/config/Intermission", "BepInEx/config/Seasonality", "valheim_Data" })
        {
            string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item);
            if (Directory.Exists(text))
            {
                try
                {
                    Directory.Delete(text, recursive: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Löschen von " + text + ": " + ex.Message);
                }
            }
        }
        try
        {
            using HttpClient httpClient = new HttpClient();
            await DownloadDirectoryAsync(httpClient, serverUri, await GetTotalSizeFromServer(httpClient));
        }
        catch (Exception ex2)
        {
            MessageBox.Show("Fehler beim HTTP-Download: " + ex2.Message);
        }
        ProgressLeiste.Dispatcher.Invoke(delegate
        {
            ProgressLeiste.Visibility = Visibility.Hidden;
        });
    }

    private async Task<long> GetTotalSizeFromServer(HttpClient httpClient)
    {
        try
        {
            string requestUri = "http://37.114.34.12/gesamtgroesse.txt";
            if (long.TryParse(await httpClient.GetStringAsync(requestUri), out var result))
            {
                return result;
            }
            MessageBox.Show("Fehler beim Lesen der Gesamtgröße von der Webdatei.");
            return 0L;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fehler beim Abrufen der Datei gesamtgroesse.txt: " + ex.Message);
            return 0L;
        }
    }

    private async Task DownloadDirectoryAsync(HttpClient httpClient, string serverUri, long totalSize)
    {
        List<Task> downloadTasks = new List<Task>();
        string html = await httpClient.GetStringAsync(serverUri);
        HtmlDocument htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        long downloadedSize = 0L;
        HtmlNodeCollection htmlNodeCollection = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
        if (htmlNodeCollection == null)
        {
            return;
        }
        string basePath = "/ValheimWithBepInEx/";
        string localBasePath = AppDomain.CurrentDomain.BaseDirectory;
        File.Delete(localBasePath + "ValheimWithBepInEx.zip");
        foreach (HtmlNode item in (IEnumerable<HtmlNode>)htmlNodeCollection)
        {
            string innerText = item.InnerText;
            string relativePath = item.GetAttributeValue("href", string.Empty);
            if (innerText.Contains("[To Parent Directory]"))
            {
                continue;
            }
            Uri fullUri = new Uri(new Uri(serverUri), relativePath);
            relativePath = relativePath.Replace(basePath, "").TrimStart('/');
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string localPath = Path.Combine(localBasePath, relativePath);
            if (relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                Directory.CreateDirectory(localPath);
                downloadTasks.Add(DownloadDirectoryAsync(httpClient, fullUri.ToString(), totalSize));
                continue;
            }
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(fullUri, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    long contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
                    long totalBytesRead = 0L;
                    byte[] buffer = new byte[8192];
                    using (FileStream fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true))
                    {
                        using Stream responseStream = await response.Content.ReadAsStreamAsync();
                        while (true)
                        {
                            int num;
                            int bytesRead = (num = await responseStream.ReadAsync(buffer, 0, buffer.Length));
                            if (num <= 0)
                            {
                                break;
                            }
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            ProgressLeiste.Dispatcher.Invoke(delegate
                            {
                                ProgressLeiste.Value = (double)totalBytesRead / (double)contentLength * 100.0;
                            });
                            Label.Dispatcher.Invoke(delegate
                            {
                                Label.Content = Path.GetFileName(localPath) ?? "";
                            });
                        }
                    }
                    downloadedSize += totalBytesRead;
                    if (Path.GetExtension(localPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExtractAndMoveZipAsync(localBasePath);
                    }
                }
                else
                {
                    MessageBox.Show($"Fehler beim Herunterladen von {relativePath}! Local Pfad: {localPath} WebPfad: {fullUri} Fehler Code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Abrufen der Datei " + relativePath + ": " + ex.Message);
            }
        }
    }

    private async Task ExtractAndMoveZipAsync(string zipDirectoryPath)
    {
        try
        {
            string text = Path.Combine(zipDirectoryPath, "ValheimWithBepInEx.zip");
            string text2 = zipDirectoryPath + "ValheimWithBepInExTemp";
            if (!File.Exists(text))
            {
                MessageBox.Show("Die ZIP-Datei " + text + " existiert nicht.");
                return;
            }
            Label.Dispatcher.Invoke(delegate
            {
                Label.Content = "Extrahiere die Zip Datei!";
            });
            Directory.CreateDirectory(text2);
            using (IArchive archive = ArchiveFactory.Open(text))
            {
                foreach (IArchiveEntry entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        string text3 = Path.Combine(text2, entry.Key);
                        Directory.CreateDirectory(Path.GetDirectoryName(text3));
                        entry.WriteToFile(text3, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            Label.Dispatcher.Invoke(delegate
            {
                Label.Content = "Verschiebe die letzten Daten!";
            });
            string[] files = Directory.GetFiles(text2, "*.*", System.IO.SearchOption.AllDirectories);
            foreach (string obj in files)
            {
                string path = obj.Substring(text2.Length + 1);
                string text4 = Path.Combine(zipDirectoryPath, path);
                Directory.CreateDirectory(Path.GetDirectoryName(text4));
                File.Move(obj, text4, overwrite: true);
            }
            Label.Dispatcher.Invoke(delegate
            {
                Label.Content = "Räume auf!";
            });
            Directory.Delete(text2, recursive: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show("Zugriff verweigert. Bitte überprüfen Sie die Berechtigungen für das Zielverzeichnis:  " + ex.Message);
        }
        catch (Exception ex2)
        {
            MessageBox.Show("Fehler beim Entpacken oder Verschieben der ZIP-Datei: " + ex2.Message);
        }
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx");
        string localDirectoryPathversion = Path.Combine(path, "versions.json");
        string[] item = (await GetDownloadUrlAndVersionAsync("TeamKoro/Mithrael_Modpack")).Item4;
        await UpdateVersionFileDependencies(localDirectoryPathversion, item);
        await Start_Download();
    }

    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "8.0.8.0")]
    public void InitializeComponent()
    {
        if (!_contentLoaded)
        {
            _contentLoaded = true;
            Uri resourceLocator = new Uri("/ValheimLauncher;component/mainwindow.xaml", UriKind.Relative);
            Application.LoadComponent(this, resourceLocator);
        }
    }

    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "8.0.8.0")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    void IComponentConnector.Connect(int connectionId, object target)
    {
        switch (connectionId)
        {
            case 1:
                ((Grid)target).AddHandler(Mouse.MouseDownEvent, new MouseButtonEventHandler(Grid_MouseDown));
                break;
            case 2:
                Start = (Button)target;
                Start.Click += Start_Click;
                break;
            case 3:
                MP_Download = (Button)target;
                MP_Download.Click += Button_Click;
                break;
            case 4:
                TooltipMPDownload = (TextBlock)target;
                break;
            case 5:
                CloseButton = (Button)target;
                CloseButton.Click += Close_Click;
                break;
            case 6:
                ProgressLeiste = (ProgressBar)target;
                break;
            case 7:
                Label = (Label)target;
                break;
            case 8:
                InstallGame = (Button)target;
                InstallGame.Click += InstallGame_Click;
                break;
            case 9:
                FixValheim = (Button)target;
                FixValheim.Click += FixValheim_Click;
                break;
            case 10:
                MPLokal = (Label)target;
                break;
            case 11:
                MPOnline = (Label)target;
                break;
            case 12:
                Vulkan = (CheckBox)target;
                break;
            default:
                _contentLoaded = true;
                break;
        }
    }
}
