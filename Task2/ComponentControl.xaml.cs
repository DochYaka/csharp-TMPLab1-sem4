using Library;
using Library.Components;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Task2
{
    /// <summary>
    /// Логика взаимодействия для ComponentControl.xaml
    /// </summary>
    public partial class ComponentControl : UserControl
    {
        private FileManager _fileManager;
        public ObservableCollection<MyComponent> Components { get; set; }

        private MyComponent? _selectedParent;

        public event EventHandler DataChanged;

        public ComponentControl(FileManager fileManager)
        {
            _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));

            Components = new ObservableCollection<MyComponent>(
                _fileManager.GetAllComponents()
            );

            DataContext = this;

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) { LoadData(); }

        private void ComponentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool isSelected = ComponentListView.SelectedItem != null;
            EditButton.IsEnabled = isSelected;
            DeleteButton.IsEnabled = isSelected;

            if (isSelected)
            {
                _selectedParent = (MyComponent)ComponentListView.SelectedItem;

                NameTextBox.Text = _selectedParent.ComponentName;
                TypeComboBox.SelectedItem = _selectedParent.ComponentType;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = NameTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show("Введите наименование компонента для добавления", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (TypeComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Выберите тип компонента", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var type = (ComponentType)TypeComboBox.SelectedItem;

                if (_selectedParent == null)
                {
                    MyComponent newComponent = new(text, type);

                    _fileManager.AddComponentToComponentList(newComponent);
                    Components.Add(newComponent);

                    NameTextBox.Clear();
                    TypeComboBox.SelectedItem = null;

                    MessageBox.Show($"Компонент '{text}' создан и добавлен",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    if (_selectedParent.ComponentType == ComponentType.Detail)
                    {
                        MessageBox.Show($"Компонент '{_selectedParent.ComponentName}' является деталью и не может иметь спецификацию!",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (type == ComponentType.Product)
                    {
                        MessageBox.Show("Нельзя добавить изделие в спецификацию!",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MyComponent newComponent = new(text, type);

                    _fileManager.AddComponentToComponentList(newComponent);
                    Components.Add(newComponent);

                    _fileManager.AddComponentToSpecification(_selectedParent.ComponentName, newComponent.ComponentName);

                    NameTextBox.Clear();
                    TypeComboBox.SelectedItem = null;

                    MessageBox.Show($"Компонент '{text}' создан и добавлен в спецификацию '{_selectedParent.ComponentName}'",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var component = (MyComponent)ComponentListView.SelectedItem;
                if (component == null) return;

                var text = NameTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show("Введите наименование", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string oldName = component.ComponentName;
                component.ComponentName = text;

                ComponentListView.Items.Refresh();

                MessageBox.Show($"Компонент '{oldName}' изменён на '{text}'",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var component = (MyComponent)ComponentListView.SelectedItem;

                if (ComponentListView.SelectedItem == null)
                {
                    MessageBox.Show($"Выберите компонент для удаления", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else
                {
                    _fileManager.DeleteComponent(component.ComponentName);
                    _fileManager.Truncate();

                    RefreshData();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            NameTextBox.Clear();
            TypeComboBox.SelectedItem = null;
            ComponentListView.SelectedItem = null;
            _selectedParent = null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
            MessageBox.Show("Данные обновлены", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void RefreshData()
        {
            Components.Clear();
            foreach (var comp in _fileManager.GetAllComponents())
            {
                Components.Add(comp);
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadData() => TypeComboBox.ItemsSource = Enum.GetValues(typeof(ComponentType));
    }
}