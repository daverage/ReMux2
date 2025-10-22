using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ReMux2
{
    public partial class ProcessCompletedDialog : Window
    {
        public string OutputFilePath { get; set; } = string.Empty;

        public ProcessCompletedDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ProcessCompletedDialog(string outputFilePath) : this()
        {
            OutputFilePath = outputFilePath;
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(OutputFilePath) && File.Exists(OutputFilePath))
                {
                    // Open Windows Explorer and select the file
                    string argument = $"/select, \"{OutputFilePath}\"";
                    Process.Start("explorer.exe", argument);
                }
                else if (!string.IsNullOrEmpty(OutputFilePath))
                {
                    // If file doesn't exist, try to open the directory
                    string? directory = Path.GetDirectoryName(OutputFilePath);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    {
                        Process.Start("explorer.exe", directory);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("The output file or directory could not be found.", 
                                      "File Not Found", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Warning);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("No output file path available.", 
                                  "No File Path", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open folder: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}