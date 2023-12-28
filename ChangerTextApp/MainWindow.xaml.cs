using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChangerTextApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        const string PROCESSING = "Processing...";
        const string START_CHANGE = "Start change";
        const string LINE = "==============================================================";

        CancellationToken? token;
        readonly StringBuilder fileReportBuilder = new();
        readonly StringBuilder directoryReportBuilder = new();
        readonly StringBuilder errorReportBuilder = new();


        public enum MODE
        {
            File,
            Directory
        }

        public MainWindow()
        {
            InitializeComponent();

            CbFilesContent.IsChecked = true;
            BtnChange.Content = START_CHANGE;
        }

        private void BtnSelectDirectoryPath_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog()
            {
                Multiselect = false,
            };

            var dialogResult = openFolderDialog.ShowDialog();
            if (dialogResult == true)
            {
                SetPathOnGui(openFolderDialog.FolderName);
            }
        }

        private void BtnSelectFilePath_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = false,
            };

            var dialogResult = openFileDialog.ShowDialog();

            if (dialogResult == true)
            {
                SetPathOnGui(openFileDialog.FileName);
            }

        }

        private void SetPathOnGui(string path)
        {
            TbPath.Text = path;
            TbPath.Focus();
            TbPath.CaretIndex = TbPath.Text.Length;
        }

        private async void BtnChange_Click(object sender, RoutedEventArgs e)
        {
            var dirOrFilePath = TbPath.Text.Trim();
            if (!System.IO.Directory.Exists(dirOrFilePath) && !System.IO.File.Exists(dirOrFilePath))
            {
                MessageBox.Show("Selected directory or file not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CbDirectoryName.IsChecked != true &&
                CbFilesContent.IsChecked != true &&
                CbFilesName.IsChecked != true)
            {
                MessageBox.Show("No selected any change", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            var mode = MODE.File;
            if (System.IO.Directory.Exists(dirOrFilePath)) mode = MODE.Directory;


            BtnChange.IsEnabled = false;
            BtnChange.Content = PROCESSING;



            token = new CancellationToken();


            ClearReport();

            var path = TbPath.Text;
            var changeFileContent = CbFilesContent.IsChecked ?? false;
            var changeFileName = CbFilesName.IsChecked ?? false;
            var changeDirectoryName = CbDirectoryName.IsChecked ?? false;
            var textFrom = TbTextFromChange.Text;
            var textTo = TbTextToChange.Text;

            await Task.Run(() =>
            {
                ChangeTextProcess(path,
                    changeFileContent,
                    changeFileName,
                    changeDirectoryName,
                    textFrom,
                    textTo,
                    mode,
                    token.Value);
            });



            BtnChange.IsEnabled = true;
            BtnChange.Content = START_CHANGE;

            ShowReport();
        }

        private void ShowReport()
        {
            var reportPath = AppDomain.CurrentDomain.BaseDirectory + "\\" + nameof(ChangerTextApp) + "_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";

            File.WriteAllText(reportPath, fileReportBuilder.ToString() + directoryReportBuilder.ToString() + errorReportBuilder.ToString());
            Process.Start("notepad.exe", reportPath);
        }

        private void ClearReport()
        {
            fileReportBuilder.Clear();
            fileReportBuilder.AppendLine(LINE);
            fileReportBuilder.AppendLine("Line Changed:");

            directoryReportBuilder.Clear();
            directoryReportBuilder.AppendLine(LINE);
            directoryReportBuilder.AppendLine("Folder Changed:");


            errorReportBuilder.Clear();
            errorReportBuilder.AppendLine(LINE);
            errorReportBuilder.AppendLine("Errors:");
        }

        private void ChangeTextProcess(string path,
            bool changeFileContent,
            bool changeFileName,
            bool changeDirectoryName,
            string textFrom,
            string textTo,
            MODE mode,
            CancellationToken cancellationToken)
        {

            maxProgressBar = 100;
            currentProgressBar = 0;

            switch (mode)
            {
                case MODE.File:
                    ChangeFilesProcess(path, changeFileContent, changeFileName, textFrom, textTo, cancellationToken);
                    break;
                case MODE.Directory:
                    ChangeDirectoresProcess(path, changeFileContent, changeFileName, changeDirectoryName, textFrom, textTo, cancellationToken);
                    break;
            }
        }


        private void ChangeFilesProcess(string path,
            bool changeFileContent,
            bool changeFileName,
            string textFrom,
            string textTo,
            CancellationToken cancellationToken)
        {

            if (changeFileContent && !cancellationToken.IsCancellationRequested)
                if (!ChangeContentFile(path, textFrom, textTo, cancellationToken)) return;

            if (changeFileName && !cancellationToken.IsCancellationRequested)
                if (!ChangeFileName(path, textFrom, textTo, cancellationToken)) return;

        }

        private void ChangeDirectoresProcess(string path,
            bool changeFileContent,
            bool changeFileName,
            bool changeDirectoryName,
            string textFrom,
            string textTo,
            CancellationToken cancellationToken)
        {

            var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

            maxProgressBar = directories.Length + files.Length;

            foreach (var file in files)
            {
                currentProgressBar++;
                SetProgressBar();
                ChangeFilesProcess(file, changeFileContent: changeFileContent, changeFileName, textFrom, textTo, cancellationToken);
            }

            if (changeDirectoryName && !cancellationToken.IsCancellationRequested)
            {

                foreach (var directory in directories)
                {
                    currentProgressBar++;
                    SetProgressBar();
                    ChangeDirectoryName(directory, textFrom, textTo, cancellationToken);
                }
            }
            else
            {
                currentProgressBar = maxProgressBar;
                SetProgressBar();
            }
        }

        private bool ChangeContentFile(string filePath, string textFrom, string textTo, CancellationToken cancellationToken)
        {

            var sourceFile = filePath;
            var destFile = filePath + "_tmp";

            try
            {
                IEnumerable<string> lines;

                lines = File.ReadLines(sourceFile);

                using var file = File.Open(destFile, FileMode.OpenOrCreate);
                file.Seek(0, SeekOrigin.End);
                using var stream = new StreamWriter(file);

                foreach (var line in lines)
                {
                    if (line == null) continue;

                    if (line.Contains(textFrom))
                    {
                        var newLine = line.ToString().Replace(textFrom, textTo);
                        stream.WriteLine(newLine);
                        fileReportBuilder.AppendLine($"{sourceFile}:    {line} => {newLine}");
                    }
                    else
                        stream.WriteLine(line);

                    if (cancellationToken.IsCancellationRequested)
                        return false;
                }

                stream.Close();
                file.Close();

                File.Delete(sourceFile);
                File.Move(destFile, sourceFile);

                return true;
            }
            catch (IOException)
            {
                errorReportBuilder.AppendLine($"{sourceFile}:    No permission");
                return true;
            }
            catch (Exception ex)
            {
                errorReportBuilder.AppendLine($"{sourceFile}:    {ex.Message}");
                return false;
            }

        }

        private bool ChangeFileName(string filePath, string textFrom, string textTo, CancellationToken cancellationToken)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Name.Contains(textFrom) && !cancellationToken.IsCancellationRequested)
                {

                    var newFilePath = fileInfo.DirectoryName + "\\" + fileInfo.Name.Replace(textFrom, textTo);
                    fileInfo.MoveTo(newFilePath);
                    fileReportBuilder.AppendLine($"{filePath} => {newFilePath}");
                }

                return true;

            }
            catch (Exception ex)
            {
                errorReportBuilder.AppendLine($"{filePath}:    {ex.Message}");
                return false;
            }
        }

        private bool ChangeDirectoryName(string directoryPath, string textFrom, string textTo, CancellationToken cancellationToken)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(directoryPath);
                if (directoryInfo.Name.Contains(textFrom) && !cancellationToken.IsCancellationRequested)
                {
                    var newDirectoryPath = directoryInfo.Parent?.FullName + "\\" + directoryInfo.Name.Replace(textFrom, textTo);
                    directoryInfo.MoveTo(newDirectoryPath);
                    directoryReportBuilder.AppendLine($"{directoryPath} => {newDirectoryPath}");
                }

                return true;

            }
            catch (Exception ex)
            {
                errorReportBuilder.AppendLine($"{directoryPath}:    {ex.Message}");
                return false;
            }
        }


        int maxProgressBar = 100;
        int currentProgressBar = 0;
        private void SetProgressBar()
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBarChange.Maximum = maxProgressBar;
                if (currentProgressBar > maxProgressBar)
                    ProgressBarChange.Value = maxProgressBar;
                else
                    ProgressBarChange.Value = currentProgressBar;

            });
        }
    }
}