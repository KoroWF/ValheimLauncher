using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimLauncher
{
    public class LauncherSettings
    {
        public bool VulkanEnabled { get; set; }
        public ModpackState Modpack { get; set; }

        public LauncherSettings() // Konstruktor für Standardwerte
        {
            VulkanEnabled = false;
            Modpack = new ModpackState();
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
