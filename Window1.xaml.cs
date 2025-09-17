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

namespace ValheimLauncher
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
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
            // Alle verfügbaren, physischen Laufwerke abrufen
            try
            {
                var drives = DriveInfo.GetDrives()
                                     .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

                foreach (var drive in drives)
                {
                    DriveComboBox.Items.Add(drive.Name);
                }

                if (DriveComboBox.Items.Count > 0)
                {
                    DriveComboBox.SelectedIndex = 0; // Wähle das erste Laufwerk vor
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Laufwerke: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                // Das Fenster kann geschlossen werden, wenn keine Laufwerke gefunden werden.
                this.Close();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string oldInstallPath = currentSettings.ValheimInstallPath;

            // Überprüfe, ob der alte Ordner existiert, und lösche ihn.
            if (!string.IsNullOrEmpty(oldInstallPath) && Directory.Exists(oldInstallPath))
            {
                try
                {
                    // Lösche den Ordner rekursiv (mit allen Inhalten).
                    Directory.Delete(oldInstallPath, true);
                }
                catch (Exception ex)
                {
                    // Behandle mögliche Fehler beim Löschen und zeige eine Meldung an.
                    MessageBox.Show($"Fehler beim Löschen des alten Ordners: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Optional: Wenn das Löschen fehlschlägt, den Vorgang abbrechen.
                    return;
                }
            }

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
