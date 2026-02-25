using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Library;

namespace Task2
{
    public partial class MainWindow : Window
    {
        private FileManager? _fileManager;
        private ComponentControl? _componentControl;
        private SpecificationControl? _specificationControl;
        private readonly string _downloadPath;

        public MainWindow()
        {
            InitializeComponent();
            _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            CreateDefaultFile();
        }

        private void CreateDefaultFile()
        {
            try
            {
                _fileManager?.Dispose();

                CreateControls();

                ComponentList_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void CreateControls()
        {
            if (_fileManager == null) return;

            _componentControl = new ComponentControl(_fileManager);
            _specificationControl = new SpecificationControl(_fileManager);

            _componentControl.DataChanged += (s, e) =>
            {
                _specificationControl?.BuildTree();
            };
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                InitialDirectory = _downloadPath
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _fileManager?.Dispose();

                    string fileName = Path.GetFileName(dialog.FileName);
                    _fileManager = FileManager.OpenFiles(fileName);

                    CreateControls();

                    MessageBox.Show($"Файл {fileName} загружен");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }

        private void ComponentList_Click(object sender, RoutedEventArgs e)
        {
            if (_componentControl != null)
            {
                _componentControl.RefreshData();
                ContentArea.Content = _componentControl;
            }
        }

        private void SpecificationList_Click(object sender, RoutedEventArgs e)
        {
            if (_specificationControl != null)
            {
                _specificationControl.BuildTree();
                ContentArea.Content = _specificationControl;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _fileManager?.Dispose();
            base.OnClosed(e);
        }
    }
}