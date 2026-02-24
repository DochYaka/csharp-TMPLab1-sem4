using System.Text;
using System.Windows;
using System.IO;
using Microsoft.Win32;
using Library;

namespace Task2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {   
        private FileManager? _fileManager;
        private readonly string _downloadPath;

        public MainWindow()
        {
            InitializeComponent();
            _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Component files (*.dat)|*.dat|All files (*.*)|*.*",
                InitialDirectory = _downloadPath,
                Title = "Выберите файл компонентов"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _fileManager?.Dispose();

                    string fileName = Path.GetFileName(dialog.FileName);
                    _fileManager = FileManager.OpenFiles(fileName);

                    MessageBox.Show($"Файл {fileName} успешно загружен!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка открытия файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ComponentList_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager == null)
            {
                try
                {
                    _fileManager = FileManager.CreateFiles("components.dat", "specs.dat");
                    MessageBox.Show("Создан новый файл components.dat", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var componentWindow = new ComponentWindow(_fileManager) { Owner = this };
            componentWindow.ShowDialog();
        }

        private void SpecificationList_Click(object sender, RoutedEventArgs e)
        {
            if (_fileManager == null)
            {
                MessageBox.Show("Сначала откройте или создайте файл через кнопку 'Компоненты'",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var specificationWindow = new SpecificationWindow(_fileManager) { Owner = this };
            specificationWindow.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            _fileManager?.Dispose();
            base.OnClosed(e);
        }
    }
}