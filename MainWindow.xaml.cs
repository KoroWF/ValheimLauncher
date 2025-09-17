using HtmlAgilityPack;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpCompress.Archives;
using SharpCompress.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.MessageBox;

namespace ValheimLauncher
{
    public partial class MainWindow : Window, IComponentConnector
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public class ModpackData
        {
            public List<string> Dependencies { get; set; }

            public ModpackData(List<string> dependencies)
            {
                Dependencies = dependencies;
            }
        }

        private LauncherSettings currentSettings; // Eine Instanz, um die geladenen Einstellungen zu halten
        private const string SettingsFileName = "launcher_settings.json"; // Name deiner Einstellungsdatei
        public string settingsFilePath;

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    currentSettings = JsonConvert.DeserializeObject<LauncherSettings>(json);
                    if (currentSettings == null)
                    {
                        currentSettings = new LauncherSettings();
                        Console.WriteLine("Launcher-Einstellungen fehlerhaft, verwende Standardwerte.");
                    }
                    // Stelle sicher, dass untergeordnete Objekte nicht null sind nach dem Laden
                    if (currentSettings.Modpack == null)
                    {
                        currentSettings.Modpack = new ModpackState();
                    }
                    if (currentSettings.Modpack.ExpectedModFiles == null) // Wichtig für spätere Nutzung
                    {
                        currentSettings.Modpack.ExpectedModFiles = new List<string>();
                    }
                    if (string.IsNullOrEmpty(currentSettings.ValheimInstallPath) || currentSettings.ValheimInstallPath == "null")
                    {
                        currentSettings.ValheimInstallPath = "notgiven"; // Setze auf "null", wenn leer
                    }
                }
                else
                {
                    currentSettings = new LauncherSettings();
                    Console.WriteLine("Keine Launcher-Einstellungsdatei gefunden, erstelle Standardwerte.");
                    SaveSettings();
                }
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"Fehler beim Deserialisieren der Launcher-Einstellungen: {jsonEx.Message}");
                currentSettings = new LauncherSettings();
                MessageBox.Show("Fehler beim Laden der Launcher-Einstellungen. Standardwerte werden verwendet.", "Einstellungsfehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Allgemeiner Fehler beim Laden der Launcher-Einstellungen: {ex.Message}");
                currentSettings = new LauncherSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(currentSettings, Formatting.Indented);
                File.WriteAllText(settingsFilePath, json);
                Console.WriteLine("Launcher-Einstellungen erfolgreich gespeichert.");
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"Fehler beim Serialisieren der Launcher-Einstellungen: {jsonEx.Message}");
                MessageBox.Show("Fehler beim Speichern der Launcher-Einstellungen (JSON).", "Speicherfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Allgemeiner Fehler beim Speichern der Launcher-Einstellungen: {ex.Message}");
                MessageBox.Show($"Fehler beim Speichern der Launcher-Einstellungen:\n{ex.Message}", "Speicherfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch toggleSwitch = sender as ToggleSwitch;

            if (toggleSwitch.IsOn == true)
            {
                currentSettings.VulkanEnabled = true;
                SaveSettings();
            }
            else
            {
                currentSettings.VulkanEnabled = false;
                SaveSettings();

            }
        }

        public MainWindow()
        {

            InitializeComponent();

            // Initialisiere den Pfad in der Methode
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string launcherFolderPath = Path.Combine(appDataPath, "ValheimImmerndar");

            // Erstelle den Ordner, wenn er nicht existiert
            if (!Directory.Exists(launcherFolderPath))
            {
                Directory.CreateDirectory(launcherFolderPath);
            }

            settingsFilePath = Path.Combine(launcherFolderPath, SettingsFileName);

            LoadSettings();
            VulkanToggleSwitch.IsOn = currentSettings.VulkanEnabled;

            ProgressLeiste.Dispatcher.Invoke(delegate
            {
                ProgressLeiste.Visibility = Visibility.Hidden;
            });

            if (Checkstatus())
            {
                Start.IsEnabled = false;
                FixValheim.Visibility = Visibility.Hidden;
                base.Loaded += MainWindow_Loaded;
            }
        }
        private void SelectValheimFolder()
        {
            // Starte den Code auf dem UI-Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var driveSelectionWindow = new DriveSelectionWindow(currentSettings);
                if (driveSelectionWindow.ShowDialog() == true)
                {
                    string selectedDrive = driveSelectionWindow.SelectedDrive;

                    string installPath = Path.Combine(selectedDrive, "ValheimImmerndar");

                    // Restlicher Code
                    currentSettings.ValheimInstallPath = installPath;
                    if (!Directory.Exists(installPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(installPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Fehler beim Erstellen des Ordners: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        if (driveSelectionWindow.DialogResult == true)
                        {
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            Process.Start(exePath);
                            Close();
                        }
                        else {
                            MessageBox.Show($"Valheim-Mithrael-Pfad gesetzt: {currentSettings.ValheimInstallPath}", "Erfolg");
                        }
                    }
                }
                else
                {
                  
                }
            });
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
            (string onlineDownloadUrl,
             string onlineVersionNumber,
             string rawOnlineApiResponse,
             string[] onlineDependencies) apiData = await GetDownloadUrlAndVersionAsync("ImmernDarNew/ImmernDarNew_Modpack");

            if (string.IsNullOrEmpty(apiData.rawOnlineApiResponse) || apiData.onlineVersionNumber == null)
            {
                Label.Dispatcher.Invoke(delegate
                {
                    Label.Content = "Kein Internet oder Thunderstore API nicht erreichbar!";
                    MPLokal.Content = "v. " + (currentSettings.Modpack?.CurrentLocalVersion ?? "unbekannt");
                    MPOnline.Content = "v. Fehler";
                });
                Checkstatus(); // Buttons basierend auf lokalem Zustand aktualisieren
                return;
            }

            JObject parsedOnlineResponse = null;
            try
            {
                parsedOnlineResponse = JObject.Parse(apiData.rawOnlineApiResponse);
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Fehler beim Parsen der Online-API-Antwort in Check_Versions: " + ex.Message);
                Label.Dispatcher.Invoke(delegate { Label.Content = "Fehlerhafte Antwort von Thunderstore API."; });
                Checkstatus();
                return;
            }

            string detectedOnlineVersion = apiData.onlineVersionNumber;
            string localVersion = currentSettings.Modpack.CurrentLocalVersion;

            Label.Dispatcher.Invoke(delegate
            {
                MPLokal.Content = "v. " + (localVersion ?? "nie installiert");
                MPOnline.Content = "v. " + (detectedOnlineVersion ?? "unbekannt");
            });

            // Speichere immer die zuletzt abgerufene API-Antwort und die erwarteten Dateien/Abhängigkeiten
            currentSettings.Modpack.LastFetchedThunderstoreApiResponse = parsedOnlineResponse;
            currentSettings.Modpack.ExpectedModFiles = new List<string>(apiData.onlineDependencies ?? Array.Empty<string>());
            SaveSettings(); // Änderungen an API-Response und ExpectedModFiles speichern

            if (localVersion != detectedOnlineVersion)
            {
                Label.Dispatcher.Invoke(delegate { Label.Content = $"Neue Version {detectedOnlineVersion} verfügbar. Vorbereitung..."; });

                bool downloadSuccessful = await Start_Download(); // Start_Download ruft intern DownloadModiDirectory auf

                if (downloadSuccessful)
                {
                    currentSettings.Modpack.CurrentLocalVersion = detectedOnlineVersion; // Lokale Version aktualisieren
                                                                                         // LastFetchedThunderstoreApiResponse und ExpectedModFiles sind bereits aktuell
                    SaveSettings();
                    Label.Dispatcher.Invoke(delegate
                    {
                        MPLokal.Content = "v. " + currentSettings.Modpack.CurrentLocalVersion;
                        Label.Content = "Modpack erfolgreich aktualisiert!";
                    });
                }
                else
                {
                    Label.Dispatcher.Invoke(delegate
                    {
                        Label.Content = "Fehler beim Update. Lokale Version bleibt: " + (localVersion ?? "unbekannt");
                    });
                }
            }
            else
            {
                Label.Dispatcher.Invoke(delegate { Label.Content = ""; });
            }
            Checkstatus();
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
            string path = Path.Combine(currentSettings.ValheimInstallPath, "valheim.exe");
            string path2 = Path.Combine(currentSettings.ValheimInstallPath, "valheim_Data", "boot.config");
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
                if (currentSettings.VulkanEnabled == true)
                {
                    Process.Start(Path.Combine(currentSettings.ValheimInstallPath, "valheim.exe"), "-force-vulkan");
                }
                else
                {
                    Process.Start(Path.Combine(currentSettings.ValheimInstallPath, "valheim.exe"), "-force-d3d12");
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

            string bepinexPath = Path.Combine(currentSettings.ValheimInstallPath, "BepInEx");
            // Die Logik für "1ExtraMods" - diese bleibt, da sie spezifisch für deine Struktur ist
            string pluginsPath = Path.Combine(bepinexPath, "plugins");
            string extraModsUserFolderPath = Path.Combine(pluginsPath, "1ExtraMods"); // Der gewünschte Pfad

            if (!Directory.Exists(extraModsUserFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(extraModsUserFolderPath);
                    Console.WriteLine($"Benutzerdefinierter Mod-Ordner wurde erstellt: {extraModsUserFolderPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Erstellen des Ordners {extraModsUserFolderPath}: {ex.Message}");
                    // Hier könntest du überlegen, ob du den Nutzer informierst oder den Prozess abbrichst,
                    // falls dieser Ordner kritisch ist. Fürs Erste reicht eine Konsolenausgabe.
                }
            }

            IProgress<int> progress = new Progress<int>(percentage =>
            {
                ProgressLeiste.Dispatcher.Invoke(() => ProgressLeiste.Value = percentage);
            });

            // Alte Dateien löschen, die nicht mehr im Modpack sind
            // Wichtig: currentSettings.Modpack.ExpectedModFiles enthält Strings wie "Author-ModName-Version"
            // Diese müssen ggf. für den Vergleich mit Ordnernamen in "plugins" angepasst werden.
            await DeleteLocalFilesNotInModpackDataList(bepinexPath, currentSettings.Modpack.ExpectedModFiles);

            // Das eigentliche Herunterladen und Extrahieren der Mods
            bool downloadSuccess = await DownloadAndExtractModpackAsync(bepinexPath, currentSettings.Modpack.ExpectedModFiles, progress);

            // Alte Dateien erneut löschen, falls durch Extraktion temporär was Falsches entstand
            await DeleteLocalFilesNotInModpackDataList(bepinexPath, currentSettings.Modpack.ExpectedModFiles);

            ProgressLeiste.Dispatcher.Invoke(delegate
            {
                ProgressLeiste.Visibility = Visibility.Hidden;
            });
            return downloadSuccess; // Gebe zurück, ob der Haupt-Download-Prozess erfolgreich war
        }

        private async Task<(string downloadUrl, string versionNumber, string response, string[] dependencies)> GetDownloadUrlAndVersionAsync(string modpackId)
        {
            string address = "https://thunderstore.io/api/experimental/package/" + modpackId;
            try
            {
                // httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ValheimLauncher/DeinName/1.0"); // Einmalig im statischen Konstruktor setzen!

                string jsonResponse = await httpClient.GetStringAsync(address); // Nutze die statische Instanz

                JObject val = JObject.Parse(jsonResponse); // Parse hier, um die Daten direkt zu haben
                string determinedDownloadUrl = val.SelectToken("latest.download_url")?.ToString();
                string determinedVersionNumber = val.SelectToken("latest.version_number")?.ToString();

                List<string> dependenciesList = new List<string>();
                JArray depsArray = val.SelectToken("latest.dependencies") as JArray;
                if (depsArray != null)
                {
                    foreach (JToken dep in depsArray)
                    {
                        dependenciesList.Add(dep.ToString());
                    }
                }
                string[] dependenciesArray = dependenciesList.ToArray();

                return (downloadUrl: determinedDownloadUrl,
                        versionNumber: determinedVersionNumber,
                        response: jsonResponse, // Gib den rohen JSON-String als 'response' zurück
                        dependencies: dependenciesArray);
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"HTTP-Fehler in GetDownloadUrlAndVersionAsync für {modpackId}: {httpEx.Message}");
                return (downloadUrl: null, versionNumber: null, response: null, dependencies: null);
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON-Parsing-Fehler in GetDownloadUrlAndVersionAsync für {modpackId}: {jsonEx.Message}");
                return (downloadUrl: null, versionNumber: null, response: null, dependencies: null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Allgemeiner Fehler in GetDownloadUrlAndVersionAsync für {modpackId}: {ex.Message}");
                return (downloadUrl: null, versionNumber: null, response: null, dependencies: null);
            }
        }

        private async Task<bool> DownloadAndExtractModpackAsync(string bepinexPath, List<string> dependenciesToDownload, IProgress<int> progress)
        {
            // httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ValheimLauncher/DeinName/1.0"); // Sollte einmalig im statischen Konstruktor gesetzt werden

            try
            {
                string pluginsPath = Path.Combine(bepinexPath, "plugins");
                string baseDirectory = currentSettings.ValheimInstallPath;
                string pluginZipDirectory = Path.Combine(bepinexPath, "pluginZip");

                Directory.CreateDirectory(pluginsPath);
                Directory.CreateDirectory(pluginZipDirectory);

                if (dependenciesToDownload == null || !dependenciesToDownload.Any())
                {
                    Label.Dispatcher.Invoke(delegate { Label.Content = "Keine Abhängigkeiten zum Herunterladen oder Verarbeiten."; });
                    progress?.Report(100);
                    return true;
                }

                ModpackData modpackData = new ModpackData(dependenciesToDownload);
                int totalDependencies = modpackData.Dependencies.Count;
                int completedSuccessfullyDependencies = 0;
                bool allOverallOperationsSuccessful = true;

                foreach (string dependencyString in modpackData.Dependencies)
                {
                    // Dateinamen-sicherer String, enthält die Version (z.B. Autor_Modname_1.2.3)
                    string dependencyFileNameSafe = dependencyString.Replace("/", "_").Replace(":", "_");
                    string expectedZipFilePath = Path.Combine(pluginZipDirectory, dependencyFileNameSafe + ".zip");

                    bool downloadNeeded = true; // Standardmäßig annehmen, dass Download nötig ist
                    bool currentDependencyProcessedSuccessfully = false; // Flag für diese spezifische Dependency

                    Label.Dispatcher.Invoke(delegate { Label.Content = $"Prüfe: {dependencyString}"; });

                    // URL für den HEAD-Request (um Dateigröße/Existenz online zu prüfen)
                    string dependencyHeadUrl = "https://thunderstore.io/package/download/" + dependencyString.Replace("-", "/") + "/";
                    long onlineFileSize = -1;

                    // --- Online-Informationen abrufen (HEAD Request) ---
                    using (var ctsHead = new CancellationTokenSource(TimeSpan.FromSeconds(30))) // Kurzes Timeout für HEAD
                    {
                        try
                        {
                            HttpResponseMessage headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, dependencyHeadUrl), ctsHead.Token);
                            if (!headResponse.IsSuccessStatusCode && headResponse.StatusCode == HttpStatusCode.NotFound)
                            {
                                // Versuche alternative URL-Struktur
                                Console.WriteLine($"Standard-URL für {dependencyString} (HEAD) nicht gefunden, versuche alternative URL...");
                                string alternativeDependencyUrl = "https://thunderstore.io/package/download/";
                                int firstDashIndex = dependencyString.IndexOf('-');
                                if (firstDashIndex >= 0)
                                {
                                    alternativeDependencyUrl += dependencyString.Substring(0, firstDashIndex + 1) + dependencyString.Substring(firstDashIndex + 1).Replace("-", "/");
                                }
                                else
                                {
                                    alternativeDependencyUrl += dependencyString.Replace("-", "/");
                                }
                                alternativeDependencyUrl += "/";
                                dependencyHeadUrl = alternativeDependencyUrl;
                                headResponse.Dispose(); // Alte Antwort disposen
                                headResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, dependencyHeadUrl), ctsHead.Token);
                            }
                            headResponse.EnsureSuccessStatusCode(); // Wirft Fehler, wenn immer noch nicht erfolgreich
                            onlineFileSize = headResponse.Content.Headers.ContentLength ?? -1;
                            headResponse.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Fehler beim Abrufen der Metadaten für {dependencyString}: {ex.Message}");
                            allOverallOperationsSuccessful = false; // Markiere diese Dependency als problematisch
                                                                    // Fahre mit der nächsten Dependency fort, da wir keine Infos haben, um zu entscheiden.
                                                                    // ODER: hier direkt `continue;` und `allOverallOperationsSuccessful` nicht unbedingt auf false setzen,
                                                                    //       wenn eine fehlgeschlagene Prüfung nicht den ganzen Prozess stoppen soll.
                                                                    //       Fürs Erste: Wenn Metadaten nicht abgerufen werden können, versuchen wir den Download nicht.
                            Label.Dispatcher.Invoke(delegate { Label.Content = $"Fehler bei Metadaten für {dependencyString}."; });
                            int tempProgress = (totalDependencies > 0) ? (int)((float)(completedSuccessfullyDependencies + 1) / totalDependencies * 100f) : 100; // Zähle es als "bearbeitet"
                            progress?.Report(tempProgress);
                            continue; // Nächste Dependency
                        }
                    }
                    // --- Ende Online-Informationen abrufen ---

                    if (File.Exists(expectedZipFilePath))
                    {
                        FileInfo localZipInfo = new FileInfo(expectedZipFilePath);
                        if (localZipInfo.Length == onlineFileSize && onlineFileSize != -1) // Prüfe Dateigröße
                        {
                            Console.WriteLine($"ZIP-Datei {expectedZipFilePath} existiert und Größe stimmt überein. Download übersprungen.");
                            downloadNeeded = false;
                        }
                        else if (onlineFileSize != -1) // Datei existiert, aber Größe stimmt nicht ODER Online-Größe unbekannt
                        {
                            Console.WriteLine($"ZIP-Datei {expectedZipFilePath} existiert, aber Größe weicht ab (Lokal: {localZipInfo.Length}, Online: {onlineFileSize}) oder Online-Größe unbekannt. Erneuter Download.");
                            downloadNeeded = true;
                        }
                        else // onlineFileSize ist -1 (konnte nicht ermittelt werden), Datei existiert aber lokal
                        {
                            Console.WriteLine($"Online-Dateigröße für {expectedZipFilePath} konnte nicht ermittelt werden. Lokale Datei wird verwendet.");
                            downloadNeeded = false;
                        }
                    }
                    // Wenn die Datei nicht existiert, ist downloadNeeded bereits true.

                    if (downloadNeeded)
                    {
                        int currentRetryAttempt = 0;
                        bool actualDownloadSucceeded = false;
                        Label.Dispatcher.Invoke(delegate { Label.Content = $"Download benötigt für: {dependencyString}"; });

                        while (!actualDownloadSucceeded && currentRetryAttempt < 3)
                        {
                            using (var ctsDownload = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                            {
                                try
                                {
                                    // Die dependencyUrl ist die dependencyHeadUrl, die ggf. schon auf die alternative URL korrigiert wurde
                                    Label.Dispatcher.Invoke(delegate { Label.Content = $"Lade herunter ({currentRetryAttempt + 1}/3): {dependencyString}"; });

                                    using (HttpResponseMessage downloadResponse = await httpClient.GetAsync(dependencyHeadUrl, HttpCompletionOption.ResponseHeadersRead, ctsDownload.Token))
                                    {
                                        downloadResponse.EnsureSuccessStatusCode();
                                        using (Stream contentStream = await downloadResponse.Content.ReadAsStreamAsync(ctsDownload.Token))
                                        using (FileStream fileStream = new FileStream(expectedZipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                                        {
                                            await contentStream.CopyToAsync(fileStream, 8192, ctsDownload.Token);
                                        }
                                    }
                                    Console.WriteLine($"{dependencyString} erfolgreich heruntergeladen nach: {expectedZipFilePath}");
                                    actualDownloadSucceeded = true;
                                }
                                catch (OperationCanceledException opEx) { Console.WriteLine($"Timeout Download {dependencyString} (Versuch {currentRetryAttempt + 1}): {opEx.Message}"); currentRetryAttempt++; }
                                catch (HttpRequestException httpEx) { Console.WriteLine($"HTTP Fehler Download {dependencyString} (Versuch {currentRetryAttempt + 1}): {httpEx.Message}"); currentRetryAttempt++; if (currentRetryAttempt < 3) await Task.Delay(2000, ctsDownload.Token); }
                                catch (Exception ex) { Console.WriteLine($"Allg. Fehler Download {dependencyString} (Versuch {currentRetryAttempt + 1}): {ex.Message}"); currentRetryAttempt = 3; MessageBox.Show($"Downloadfehler {dependencyString}:\n{ex.Message}", "Download-Fehler", MessageBoxButton.OK, MessageBoxImage.Error); }
                            }
                        }
                        if (!actualDownloadSucceeded)
                        {
                            allOverallOperationsSuccessful = false; // Download endgültig fehlgeschlagen
                            Label.Dispatcher.Invoke(delegate { Label.Content = $"Finaler Download-Fehler für {dependencyString}."; });
                            // Fahre mit der nächsten Dependency fort, aber markiere den Gesamtprozess als nicht erfolgreich
                            int tempProgress = (totalDependencies > 0) ? (int)((float)(completedSuccessfullyDependencies + 1) / totalDependencies * 100f) : 100; // Inkrementiere für den Fortschrittsbalken
                            progress?.Report(tempProgress);
                            continue;
                        }
                    }

                    // Extraktion (nur wenn ZIP vorhanden ist und als aktuell gilt oder erfolgreich heruntergeladen wurde)
                    if (File.Exists(expectedZipFilePath))
                    {
                        Label.Dispatcher.Invoke(delegate { Label.Content = $"Extrahiere: {dependencyString}"; });
                        string extractionTargetFolder;
                        bool isBepInExPack = dependencyString.Contains("denikson-BepInExPack_Valheim");

                        if (isBepInExPack) { extractionTargetFolder = baseDirectory; }
                        else { extractionTargetFolder = Path.Combine(pluginsPath, dependencyFileNameSafe); }
                        Directory.CreateDirectory(extractionTargetFolder);

                        try
                        {
                            using (IArchive archive = ArchiveFactory.Open(expectedZipFilePath))
                            {
                                foreach (IArchiveEntry entry in archive.Entries)
                                {
                                    if (entry.IsDirectory) continue;
                                    string entryFileName = Path.GetFileName(entry.Key);
                                    if (isBepInExPack && entryFileName.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string targetDllPath = Path.Combine(baseDirectory, "winhttp.dll");
                                        if (!File.Exists(targetDllPath))
                                        {
                                            entry.WriteToFile(targetDllPath, new ExtractionOptions { Overwrite = false });
                                        }
                                        if (isBepInExPack) continue;
                                    }
                                    entry.WriteToDirectory(extractionTargetFolder, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                                }
                            }
                            Console.WriteLine($"{dependencyString} erfolgreich extrahiert.");
                            currentDependencyProcessedSuccessfully = true; // Markiere diese Dependency als erfolgreich verarbeitet
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Fehler beim Extrahieren von {dependencyString} aus {expectedZipFilePath}: {ex.Message}");
                            currentDependencyProcessedSuccessfully = false;
                            allOverallOperationsSuccessful = false;
                            MessageBox.Show($"Fehler beim Extrahieren von {dependencyString}:\n{ex.Message}", "Extraktions-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else // Sollte nicht passieren, wenn Download-Logik korrekt ist
                    {
                        Console.WriteLine($"Kritischer Fehler: ZIP {expectedZipFilePath} nicht gefunden für Extraktion.");
                        currentDependencyProcessedSuccessfully = false;
                        allOverallOperationsSuccessful = false;
                    }

                    if (currentDependencyProcessedSuccessfully)
                    {
                        completedSuccessfullyDependencies++;
                    }

                    int overallProgress = (totalDependencies > 0) ? (int)((float)completedSuccessfullyDependencies / totalDependencies * 100f) : 100;
                    progress?.Report(overallProgress);

                } // Ende foreach Dependency

                string bepInExPackExtractedFolderName = "BepInExPack_Valheim";
                string sourceFolderPathForOverwrite = Path.Combine(baseDirectory, bepInExPackExtractedFolderName);

                if (Directory.Exists(sourceFolderPathForOverwrite))
                {
                    Console.WriteLine($"Starte Überschreibungs- und Aufräumprozess für: {sourceFolderPathForOverwrite}");
                    Label.Dispatcher.Invoke(delegate { Label.Content = $"Installiere BepInEx Kernkomponenten..."; });

                    try
                    {
                        // Spezielle Behandlung für winhttp.dll: Nicht überschreiben, wenn im baseDirectory schon vorhanden
                        string winHttpInSourcePackPath = Path.Combine(sourceFolderPathForOverwrite, "winhttp.dll");
                        string winHttpInFinalBasePath = Path.Combine(baseDirectory, "winhttp.dll");

                        if (File.Exists(winHttpInSourcePackPath) && File.Exists(winHttpInFinalBasePath))
                        {
                            Console.WriteLine("winhttp.dll existiert bereits im Zielverzeichnis. Version aus dem Pack wird nicht kopiert/verschoben.");
                            File.Delete(winHttpInSourcePackPath); // Lösche es aus der Quelle, damit es beim Verschieben nicht stört
                        }

                        // Verschiebe alle Dateien aus dem Quellordner in das baseDirectory
                        // File.Move mit overwrite=true überschreibt existierende Dateien im Ziel.
                        foreach (string filePath in Directory.GetFiles(sourceFolderPathForOverwrite))
                        {
                            string fileName = Path.GetFileName(filePath);
                            string destinationFilePath = Path.Combine(baseDirectory, fileName);
                            Console.WriteLine($"Verschiebe Datei: {filePath} nach {destinationFilePath}");
                            File.Move(filePath, destinationFilePath, true); // true für overwrite
                        }

                        // Verschiebe alle Unterverzeichnisse aus dem Quellordner in das baseDirectory.
                        // FileSystem.MoveDirectory mit overwrite=true führt Inhalte zusammen und überschreibt Dateien.
                        foreach (string directoryPath in Directory.GetDirectories(sourceFolderPathForOverwrite))
                        {
                            string directoryName = new DirectoryInfo(directoryPath).Name;
                            string destinationDirectoryPath = Path.Combine(baseDirectory, directoryName);
                            Console.WriteLine($"Verschiebe Verzeichnis (Inhalte werden gemerged/überschrieben): {directoryPath} nach {destinationDirectoryPath}");
                            Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(directoryPath, destinationDirectoryPath, true); // true für overwrite
                        }

                        // Nachdem alle Inhalte verschoben wurden, lösche den ursprünglichen (jetzt leeren) Quellordner.
                        Console.WriteLine($"Lösche ursprünglichen Quellordner: {sourceFolderPathForOverwrite}");
                        Directory.Delete(sourceFolderPathForOverwrite, false); // false, da er leer sein sollte. Ggf. true, falls doch Reste drin sind.

                        Label.Dispatcher.Invoke(delegate { Label.Content = "BepInEx Kernkomponenten installiert."; });
                    }
                    catch (Exception ex)
                    {

                    }
                }
                else
                {
                    Console.WriteLine($"Der erwartete BepInExPack-Ordner '{sourceFolderPathForOverwrite}' wurde nach dem Entpacken nicht gefunden. Überspringe spezifische Aufräumarbeiten.");
                    // Hier könntest du entscheiden, ob das ein Fehler ist, der allOverallOperationsSuccessful beeinflusst,
                    // wenn du erwartest, dass dieser Ordner immer da sein sollte nach dem Entpacken des BepInEx Packs.
                }

                return allOverallOperationsSuccessful;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Genereller Fehler in DownloadAndExtractModpackAsync: {ex.Message}");
                Label.Dispatcher.Invoke(delegate { Label.Content = "Schwerer Fehler beim Modpack-Download-Prozess."; });
                MessageBox.Show($"Ein schwerer Fehler ist beim Modpack-Download aufgetreten:\n{ex.Message}", "Download-Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

        }
        private async Task DeleteLocalFilesNotInModpackDataList(string bepinexPath, List<string> expectedThunderstoreDependencies)
        {
            string pluginsPath = Path.Combine(bepinexPath, "plugins");
            string pluginZipPath = Path.Combine(bepinexPath, "pluginZip");

            if (expectedThunderstoreDependencies == null) expectedThunderstoreDependencies = new List<string>();

            HashSet<string> filesAndFoldersToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string depString in expectedThunderstoreDependencies)
            {
                filesAndFoldersToKeep.Add(depString.Replace("/", "_").Replace(":", "_") + ".zip");

                string modFolderNameGuess = depString.Replace("/", "_").Replace(":", "_"); // Basisname
                                                                                           // Versuche, eine typische Versionsendung zu entfernen, falls vorhanden (z.B. -1.2.3)
                int lastDash = modFolderNameGuess.LastIndexOf('-');
                if (lastDash > 0 && modFolderNameGuess.Substring(lastDash + 1).Count(c => c == '.') >= 1) // Einfache Heuristik für Version
                {
                    // modFolderNameGuess = modFolderNameGuess.Substring(0, lastDash); // Dies kann zu falsch positiven Ergebnissen führen.
                }
                filesAndFoldersToKeep.Add(modFolderNameGuess); // Füge den geratenen Ordnernamen hinzu
            }
            // Füge essentielle BepInEx-Dateien/Ordner hinzu, die immer behalten werden sollen
            filesAndFoldersToKeep.Add("Valheim.DisplayBepInExInfo.dll"); // Beispiel, anpassen an deine BepInEx Struktur
            filesAndFoldersToKeep.Add("HappyDragoon-DragoonCapes"); // Beispiel, anpassen an deine BepInEx Struktur

            // Aufräumen im pluginsPath
            if (Directory.Exists(pluginsPath))
            {
                try
                {
                    FileSystemInfo[] fileSystemInfos = new DirectoryInfo(pluginsPath).GetFileSystemInfos();
                    foreach (FileSystemInfo fileSystemInfo in fileSystemInfos)
                    {
                        string name = fileSystemInfo.Name;
                        // WICHTIG: Der Vergleich hier muss robust sein!
                        if (!filesAndFoldersToKeep.Contains(name) &&
                            !name.StartsWith("BepInEx.डियो") && // BepInEx core files
                            !name.StartsWith("BepInEx.Preloader.डियो") &&
                            !name.StartsWith("BepInEx.Harmony.डियो") &&
                            !name.Equals("patchers", StringComparison.OrdinalIgnoreCase) &&
                            !name.Equals("config", StringComparison.OrdinalIgnoreCase) &&
                            !name.Equals("core", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("MMHOOK") && // Beispielhafte Ausnahmen
                            !name.Contains("1ExtraMods", StringComparison.OrdinalIgnoreCase) &&
                            !name.Contains("HappyDragoon-DragoonCapes")) // Spezieller Ordner, den du behalten willst
                        {
                            Console.WriteLine($"Lösche verwaiste Datei/Ordner in plugins: {fileSystemInfo.FullName}");
                            if (fileSystemInfo is FileInfo fileInfo) fileInfo.Delete();
                            else if (fileSystemInfo is DirectoryInfo directoryInfo) directoryInfo.Delete(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Aufräumen des Plugin-Ordners: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Aufräumen im pluginZipPath (heruntergeladene ZIPs, die nicht mehr aktuell sind)
            if (Directory.Exists(pluginZipPath))
            {
                try
                {
                    FileInfo[] zipFiles = new DirectoryInfo(pluginZipPath).GetFiles("*.zip", System.IO.SearchOption.TopDirectoryOnly);
                    foreach (FileInfo zipFile in zipFiles)
                    {
                        if (!filesAndFoldersToKeep.Contains(zipFile.Name)) // Vergleicht mit z.B. "Autor-Mod-Version.zip"
                        {
                            Console.WriteLine($"Lösche verwaiste ZIP-Datei: {zipFile.FullName}");
                            zipFile.Delete();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Aufräumen des PluginZip-Ordners: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            await Task.CompletedTask; // Um die async Signatur zu erfüllen, falls keine echten awaits drin sind
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
            Reset.Visibility = Visibility.Hidden;
            Label.Content = "Nachdem der Installationspfad gewählt wurde, beginnt der Download.";

            // 1. Überprüfe, ob der Installationspfad existiert.
            if (currentSettings.ValheimInstallPath == "notgiven")
            {
                // 2. Wenn nicht vorhanden, öffne einen Dialog zur Pfadauswahl.
                SelectValheimFolder();
                SaveSettings(); // Speichere den neu ausgewählten Pfad
            }

            // Wenn der Benutzer den Pfad-Dialog schließt, ist der Pfad null.
            // In diesem Fall die Installation abbrechen.
            if (currentSettings.ValheimInstallPath == "notgiven")
            {
                MessageBox.Show("Installation abgebrochen. Es wurde kein Valheim-Pfad ausgewählt.");
                InstallGame.Visibility = Visibility.Visible;
                Reset.Visibility = Visibility.Visible;
                return;
            }
            else
            {
                await Task.Run(async delegate
                {
                    await deleteFolder();
                    await installer();

                });
                Checkstatus();
                Label.Content = "Das Hauptspiel wurde fertig geladen";
                Reset.Visibility = Visibility.Visible;
                await Check_Versions();
            }
        }

        private async void FixValheim_Click(object sender, RoutedEventArgs e)
        {
            FixValheim.Visibility = Visibility.Hidden;
            Reset.Visibility = Visibility.Hidden;
            Start.IsEnabled = false;
            Label.Content = "Überprüfe vorhandene Daten auf Fehler!";


            // 1. Überprüfe, ob der Installationspfad existiert.
            if (currentSettings.ValheimInstallPath == "notgiven")
            {
                // 2. Wenn nicht vorhanden, öffne einen Dialog zur Pfadauswahl.
                SelectValheimFolder();
                SaveSettings(); // Speichere den neu ausgewählten Pfad
            }

            // Wenn der Benutzer den Pfad-Dialog schließt, ist der Pfad null.
            // In diesem Fall die Installation abbrechen.
            if (currentSettings.ValheimInstallPath == "notgiven")
            {
                MessageBox.Show("Installation abgebrochen. Es wurde kein Valheim-Pfad ausgewählt.");
                FixValheim.Visibility = Visibility.Visible;
                Reset.Visibility = Visibility.Visible;
                Start.IsEnabled = true;
                Label.Content = "Fix abgebrochen! Spiel kann normal gestartet werden.";
                return;
            }
            else { 
                await Task.Run(async delegate
                {
                    await deleteFolder();
                    await installer();
                });
                Reset.Visibility = Visibility.Visible;
                Checkstatus();
                Label.Content = "Überprüfung beendet, bereit zum Starten!";
            }


        }

        private async Task deleteFolder()
        {
            string[] obj = new string[5] { "BepInEx/patchers", "BepInEx/config/Azumatt.MinimalUI_Backgrounds", "BepInEx/config/Intermission", "BepInEx/config/Seasonality", "valheim_Data" };
            string baseDirectory = currentSettings.ValheimInstallPath;
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
            string serverUri = "https://www.immerndar.de/ValheimWithBepInEx/";
            ProgressLeiste.Dispatcher.Invoke(delegate
            {
                ProgressLeiste.Value = 0.0;
                ProgressLeiste.Visibility = Visibility.Visible;
            });
            foreach (string item in new List<string> { "BepInEx/patchers", "BepInEx/config/Azumatt.MinimalUI_Backgrounds", "BepInEx/config/Intermission", "BepInEx/config/Seasonality", "valheim_Data" })
            {
                string text = Path.Combine(currentSettings.ValheimInstallPath, item);
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
                string valheimInstallationPath = currentSettings.ValheimInstallPath;

                BootConfigModifier configModifier = new BootConfigModifier(valheimInstallationPath);
                configModifier.ApplyPerformanceSettings();
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
                string requestUri = "https://www.immerndar.de/gesamtgroesse.txt";
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
            string localBasePath = currentSettings.ValheimInstallPath;
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
                                    Label.Content = "Download: " + Path.GetFileName(localPath) ?? "";
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
                string zipFilePath = Path.Combine(zipDirectoryPath, "ValheimWithBepInEx.zip");
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
                            using (var entryStream = entry.OpenEntryStream())
                            using (var fileStream = new FileStream(text3, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                entryStream.CopyTo(fileStream);
                            }
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
                    string fileName = Path.GetFileName(obj);

                    // Sonderfall: winhttp.dll
                    if (fileName.Equals("winhttp.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        string zielPfad = Path.Combine(zipDirectoryPath, fileName);
                        if (File.Exists(zielPfad))
                        {
                            continue; // Datei existiert schon, also überspringen
                        }
                        // Zielpfad aus dem relativen Pfad rekonstruieren
                        string relativePath = obj.Substring(text2.Length + 1);
                        string winhttpZiel = Path.Combine(zipDirectoryPath, relativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(winhttpZiel));
                        File.Move(obj, winhttpZiel, overwrite: true);
                        continue; // Danach auch skippen, um keine doppelte Aktion zu machen
                    }

                    string path = obj.Substring(text2.Length + 1);
                    string text4 = Path.Combine(zipDirectoryPath, path);

                    if (!Directory.Exists(Path.GetDirectoryName(text4)))
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(text4));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Fehler beim Erstellen des Verzeichnisses {Path.GetDirectoryName(text4)}:\n{ex.Message}");
                            throw;
                        }
                    }

                    try
                    {
                        File.Move(obj, text4, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Fehler beim Verschieben von Datei:\nQuelle: {obj}\nZiel: {text4}\n\nFehlermeldung:\n{ex.Message}");
                        throw;
                    }

                }
                Label.Dispatcher.Invoke(delegate
                {
                    Label.Content = "Räume auf!";
                });
                Directory.Delete(text2, recursive: true);

                if (File.Exists(zipFilePath))
                {
                    try
                    {
                        File.Delete(zipFilePath);
                        Console.WriteLine($"ZIP-Datei {zipFilePath} erfolgreich gelöscht.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Fehler beim Löschen der ZIP-Datei {zipFilePath}: {ex.Message}");
                        // Optional: MessageBox anzeigen, aber vielleicht nicht notwendig, da es ein Aufräumschritt ist.
                    }
                }
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
            // UI-Feedback und Buttons deaktivieren
            Label.Dispatcher.Invoke(delegate { Label.Content = "Manuelles Update wird gestartet...\nRufe Modpack-Informationen ab..."; });
            if (MP_Download != null) MP_Download.IsEnabled = false;
            if (Start != null) Start.IsEnabled = false;

            // 1. Aktuellste Modpack-Informationen von Thunderstore abrufen
            (string onlineDownloadUrl,
             string onlineVersionNumber,
             string rawOnlineApiResponse,
             string[] onlineDependencies) apiData = await GetDownloadUrlAndVersionAsync("ImmernDarNew/ImmernDarNew_Modpack");

            if (string.IsNullOrEmpty(apiData.onlineVersionNumber))
            {
                Label.Dispatcher.Invoke(delegate { Label.Content = "Fehler: Modpack-Infos von Thunderstore nicht abrufbar."; });
                if (MP_Download != null) MP_Download.IsEnabled = true;
                if (Start != null) Start.IsEnabled = Checkstatus(); // Start-Button basierend auf lokalem Zustand
                return;
            }

            JObject parsedOnlineResponse = null;
            try
            {
                parsedOnlineResponse = JObject.Parse(apiData.rawOnlineApiResponse);
            }
            catch (JsonException ex)
            {
                Console.WriteLine("Fehler beim Parsen der API-Antwort (manuelles Update): " + ex.Message);
                Label.Dispatcher.Invoke(delegate { Label.Content = "Fehlerhafte API-Antwort von Thunderstore."; });
                if (MP_Download != null) MP_Download.IsEnabled = true;
                if (Start != null) Start.IsEnabled = Checkstatus();
                return;
            }

            // 2. currentSettings mit den neuen Online-Informationen aktualisieren
            //    Die CurrentLocalVersion wird hier noch NICHT geändert, erst nach erfolgreichem Download.
            currentSettings.Modpack.LastFetchedThunderstoreApiResponse = parsedOnlineResponse;
            currentSettings.Modpack.ExpectedModFiles = new List<string>(apiData.onlineDependencies ?? Array.Empty<string>());
            SaveSettings(); // Speichere die frischen API-Daten und erwarteten Mods

            Label.Dispatcher.Invoke(delegate
            {
                if (MPOnline != null) MPOnline.Content = "v. " + (apiData.onlineVersionNumber ?? "unbekannt");
                Label.Content = "Neueste Modpack-Informationen abgerufen.\nStarte Download und Installation...";
            });

            // 3. Download- und Installationsprozess anstoßen
            //    Start_Download() sollte jetzt die Informationen aus currentSettings.Modpack.ExpectedModFiles verwenden,
            //    die wir gerade aktualisiert haben.
            bool downloadSuccessful = await Start_Download();

            if (downloadSuccessful)
            {
                // Nach erfolgreichem Download und Installation: Aktualisiere die lokal gespeicherte Version
                currentSettings.Modpack.CurrentLocalVersion = apiData.onlineVersionNumber;
                SaveSettings(); // Speichere die neue lokale Versionsnummer
                Label.Dispatcher.Invoke(delegate
                {
                    if (MPLokal != null) MPLokal.Content = "v. " + currentSettings.Modpack.CurrentLocalVersion;
                    Label.Content = "Modpack manuell erfolgreich aktualisiert!";
                });
            }
            else
            {
                Label.Dispatcher.Invoke(delegate
                {
                    Label.Content = "Fehler beim manuellen Update des Modpacks.\nDie vorherige Version bleibt aktiv.";
                });
            }

            // Buttons wieder aktivieren und Status prüfen
            if (MP_Download != null) MP_Download.IsEnabled = true;
            Checkstatus(); // Aktualisiert den Zustand von Start-Button etc.
        }

        private void ChangeInstallPath(object sender, RoutedEventArgs e)
        {

            // 2. Wenn nicht vorhanden, öffne einen Dialog zur Pfadauswahl.
            SelectValheimFolder();
            SaveSettings(); // Speichere den neu ausgewählten Pfad



        }

        private void OpenInstallPath(object sender, RoutedEventArgs e)
        {
            string folderPath = currentSettings.ValheimInstallPath; // Replace with your folder path
            Process.Start("explorer.exe", folderPath);
        }

    }
}