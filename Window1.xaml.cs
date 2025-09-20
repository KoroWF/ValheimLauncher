using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace ValheimLauncher
{

    public partial class DriveSelectionWindow : Window
    {
        public string SelectedDrive { get; private set; }
        private LauncherSettings currentSettings;
        public DriveSelectionWindow(LauncherSettings settings)
        {
            InitializeComponent();
            this.currentSettings = settings;
            LoadDrives();
        }

        private void LoadDrives()
        {
            try
            {
                // 1. Hole das Laufwerk aus dem aktuellen Installationspfad
                string currentDrive = null;
                if (currentSettings != null && !string.IsNullOrEmpty(currentSettings.ValheimInstallPath))
                {
                    currentDrive = Path.GetPathRoot(currentSettings.ValheimInstallPath);
                }

                // 2. Filtere alle physischen, bereiten Laufwerke, die NICHT das aktuelle sind
                var drives = DriveInfo.GetDrives()
                                      .Where(d => d.IsReady &&
                                                  d.DriveType == DriveType.Fixed &&
                                                  !string.Equals(d.Name, currentDrive, StringComparison.OrdinalIgnoreCase));

                // 3. Füge die verbleibenden Laufwerke zur Combobox hinzu
                foreach (var drive in drives)
                {
                    DriveComboBox.Items.Add(drive.Name);
                }

                // 4. Setze die Auswahl auf das erste verfügbare Laufwerk
                if (DriveComboBox.Items.Count > 0)
                {
                    DriveComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Laufwerke: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string oldInstallPath = currentSettings.ValheimInstallPath;

            if (DriveComboBox.SelectedItem != null)
            {
                SelectedDrive = DriveComboBox.SelectedItem.ToString();
                DialogResult = true; // Setzt das Ergebnis auf 'true', damit das aufrufende Fenster weiß, dass es erfolgreich war
                // Dies ist der Pfad zur EXE-Datei deiner Anwendung
                Close();
            }
            else
            {
                MessageBox.Show("Bitte wählen Sie ein Laufwerk aus.", "Auswahl erforderlich", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancleButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Setzt das Ergebnis auf 'false', damit das aufrufende Fenster weiß, dass es abgebrochen wurde
        } 
    }
}
