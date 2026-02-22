using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Library;

namespace Task2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FileManager? _fileManager;


        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ComponentList_Click(object sender, RoutedEventArgs e)
        {
            var fileManager = FileManager.CreateFiles("comp.dat", "spec.dat");

            var componentWindow = new ComponentWindow(fileManager) { Owner = this };
            componentWindow.Show();
        }

        private void SpecificationList_Click(object sender, RoutedEventArgs e)
        {
            var specificationWindow = new SpecificationWindow() { Owner = this };
            specificationWindow.Show();
        }
    }
}