using Library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using static System.Net.Mime.MediaTypeNames;

namespace Task2
{
    /// <summary>
    /// Логика взаимодействия для Components.xaml
    /// </summary>
    public partial class ComponentWindow : Window
    {
        private FileManager _fileManager;

        public ObservableCollection<MyComponent> Components { get; set; }


        public ComponentWindow(FileManager fileManager)
        {
            InitializeComponent();
            _fileManager = fileManager;

            Components = new ObservableCollection<MyComponent>(
                _fileManager.GetAllComponents()
            );

            DataContext = this;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void ComponentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComponentListView.SelectedItem == null)
            {
                EditButton.IsEnabled = false;
                DeleteButton.IsEnabled = false;
            }
            else
            {
                EditButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = NameTextBox.Text;

                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show("Введите наименование", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (TypeComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Выберите тип", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var type = (ComponentType)TypeComboBox.SelectedItem;

                MyComponent component = new(text, type);
                _fileManager.AddComponentToComponentList(component);
                Components.Add(component);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var component = (MyComponent)ComponentListView.SelectedItem;

                if (component == null)
                {
                    MessageBox.Show("Выберите компонент", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var text = NameTextBox.Text;
                component.ComponentName = text;

                Components.Clear();
                foreach (var item in _fileManager.GetAllComponents())
                {
                    Components.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var component = (MyComponent)ComponentListView.SelectedItem;

                if (ComponentListView.SelectedItem == null)
                {
                    MessageBox.Show("Выберите компонент", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Components.Remove(component);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadData()
        {
            TypeComboBox.ItemsSource = Enum.GetValues(typeof(ComponentType));
        }
    }
}
