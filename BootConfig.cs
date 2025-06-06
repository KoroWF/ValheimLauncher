using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ValheimLauncher
{
    public class BootConfigModifier
    {
        public readonly string bootConfigPath;

        // Konstruktor, der den Pfad zur boot.config erstellt
        public BootConfigModifier(string valheimBasePath)
        {
            // Path.Combine ist ideal, da es systemunabhängig (Windows/Linux) funktioniert
            string valheimDataPath = Path.Combine(valheimBasePath, "valheim_Data");
            bootConfigPath = Path.Combine(valheimDataPath, "boot.config");
        }

        public void ApplyPerformanceSettings()
        {
            // Hier alle gewünschten Einstellungen definieren, die am Ende in der Datei stehen sollen.
            var desiredSettings = new Dictionary<string, string>
            {
                { "gfx-enable-gfx-jobs", "1" },
                { "gfx-enable-native-gfx-jobs", "1" },
                { "gc-max-time-slice", "11" },
                { "vr-enabled", "0" },
                { "scripting-runtime-version", "latest" }
            };

            try
            {
                // 1. Anzahl der logischen Prozessoren sicher abfragen
                int logicalProcessors = Environment.ProcessorCount;

                // 2. Optimale Worker-Anzahl berechnen (N-1 Regel, aber mindestens 1)
                int workerCount = Math.Max(1, logicalProcessors - 1);

                Console.WriteLine($"System hat {logicalProcessors} logische Prozessoren. Setze Worker-Threads auf {workerCount}.");

                // 3. Dynamische Worker-Anzahl zu den gewünschten Einstellungen hinzufügen
                desiredSettings["job-worker-maximum-count"] = workerCount.ToString();
                desiredSettings["job-worker-count"] = workerCount.ToString();
            }
            catch (Exception ex)
            {
                // Fängt seltene Fehler ab, falls die Prozessoranzahl nicht ermittelt werden kann
                Console.WriteLine($"Konnte CPU-Daten nicht ermitteln, verwende Standardwerte. Fehler: {ex.Message}");
            }

            // 4. BootConfig bearbeiten oder erstellen
            try
            {
                // Wenn die boot.config nicht existiert, erstellen wir sie mit den gewünschten Einstellungen.
                if (!File.Exists(bootConfigPath))
                {
                    Console.WriteLine("boot.config nicht gefunden! Erstelle neue Datei...");
                    // Schreibe nur die gewünschten Einstellungen in die neue Datei
                    File.WriteAllLines(bootConfigPath, desiredSettings.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    Console.WriteLine("Neue boot.config wurde erfolgreich erstellt.");
                    return; // Beende die Methode hier
                }

                // Lese die bestehende boot.config
                var lines = File.ReadAllLines(bootConfigPath).ToList();
                var existingSettings = new Dictionary<string, string>();
                bool needsUpdate = false;

                // Erstelle ein Dictionary aus den vorhandenen Zeilen für einen einfacheren Zugriff
                var linesToKeep = new List<string>();
                foreach (var line in lines)
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        existingSettings[parts[0].Trim()] = parts[1].Trim();
                    }
                    else
                    {
                        linesToKeep.Add(line); // Behalte leere oder kommentarzeilen bei
                    }
                }

                // Vergleiche und aktualisiere die Einstellungen
                foreach (var setting in desiredSettings)
                {
                    // Prüfen, ob die Einstellung existiert und ob der Wert korrekt ist.
                    if (!existingSettings.ContainsKey(setting.Key) || existingSettings[setting.Key] != setting.Value)
                    {
                        existingSettings[setting.Key] = setting.Value;
                        needsUpdate = true;
                    }
                }

                // Schreibe die Datei nur, wenn es Änderungen gab.
                if (needsUpdate)
                {
                    Console.WriteLine("Aktualisiere boot.config...");
                    // Füge die (potenziell modifizierten) Einstellungen zu den Zeilen hinzu, die wir behalten wollen
                    linesToKeep.AddRange(existingSettings.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    File.WriteAllLines(bootConfigPath, linesToKeep);
                    Console.WriteLine("boot.config wurde erfolgreich aktualisiert.");
                }
                else
                {
                    Console.WriteLine("boot.config ist bereits korrekt konfiguriert.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Bearbeiten der boot.config: {ex.Message}");
                // Optional kannst du die Exception weiterwerfen, um den Startvorgang abzubrechen
                // throw;
            }
        }
    }
}