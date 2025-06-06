using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using Application = System.Windows.Application;

public class App : Application
{
    [DebuggerNonUserCode]
    [GeneratedCode("PresentationBuildTasks", "8.0.8.0")]
    public void InitializeComponent()
    {
        base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
    }
}
