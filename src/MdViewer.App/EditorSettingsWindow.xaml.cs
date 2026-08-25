using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace MdViewer.App;

public partial class EditorSettingsWindow : Window
{
    public EditorSettingsWindow(string? editorPath)
    {
        InitializeComponent();
        EditorPathTextBox.Text = editorPath ?? string.Empty;
        WindowsDefaultOption.IsChecked = editorPath is null;
        CustomEditorOption.IsChecked = editorPath is not null;
        UpdateEditorControls();
    }

    public string? SelectedEditorPath { get; private set; }

    private void OnEditorOptionChanged(object sender, RoutedEventArgs e)
    {
        if (EditorPathTextBox is not null)
        {
            UpdateEditorControls();
        }
    }

    private void UpdateEditorControls()
    {
        var customEditorEnabled = CustomEditorOption.IsChecked == true;
        EditorPathTextBox.IsEnabled = customEditorEnabled;
        BrowseButton.IsEnabled = customEditorEnabled;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose editor executable",
            Filter = "Applications (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            EditorPathTextBox.Text = dialog.FileName;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (WindowsDefaultOption.IsChecked == true)
        {
            SelectedEditorPath = null;
            DialogResult = true;
            return;
        }

        var editorPath = EditorPathTextBox.Text.Trim();
        if (!editorPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(editorPath))
        {
            MessageBox.Show(
                this,
                "Choose an existing .exe file.",
                "Invalid editor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SelectedEditorPath = Path.GetFullPath(editorPath);
        DialogResult = true;
    }
}
