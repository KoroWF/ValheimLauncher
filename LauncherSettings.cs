using Newtonsoft.Json.Linq;

namespace ValheimLauncher
{
    public class LauncherSettings
    {
        public bool VulkanEnabled { get; set; }
        public ModpackState Modpack { get; set; }
        public string ValheimInstallPath { get; set; }

        public LauncherSettings() // Konstruktor für Standardwerte
        {
            VulkanEnabled = false;
            Modpack = new ModpackState();
            ValheimInstallPath = "notgiven"; // Standardwert für den Installationspfad
        }
    }

    public class ModpackState
    {
        public string CurrentLocalVersion { get; set; } // Tatsächlich installierte Version
        public JObject LastFetchedThunderstoreApiResponse { get; set; } // Komplette API-Antwort als JObject
        public List<string> ExpectedModFiles { get; set; } // Liste der Abhängigkeits-Strings von der API (z.B. "Author-ModName-Version")

        public ModpackState()
        {
            CurrentLocalVersion = "0.0.0"; // Oder null, um "noch nie installiert" zu signalisieren
            ExpectedModFiles = new List<string>();
            LastFetchedThunderstoreApiResponse = null; // Wird beim ersten API-Abruf gefüllt
        }
    }
}
