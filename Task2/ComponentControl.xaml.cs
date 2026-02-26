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

                    try
                    {
                        var graph = _fileManager.GetCompWithSpecs(_selectedParent.ComponentName);
                        int currentCount = graph.Specifications.Count;

                        if (currentCount >= 2)
                        {
                            MessageBox.Show($"Достигнут лимит спецификации для компонента '{_selectedParent.ComponentName}' (максимум 2 компонента)",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    catch
                    {
                    }

                    MyComponent? existingComponent = null;
                    try
                    {
                        existingComponent = _fileManager.GetAllComponents()
                            .FirstOrDefault(c => c.ComponentName.Equals(text, StringComparison.OrdinalIgnoreCase));
                    }
                    catch { }

                    MyComponent newComponent;

                    if (existingComponent != null)
                    {
                        newComponent = existingComponent;

                        try
                        {
                            var graph = _fileManager.GetCompWithSpecs(_selectedParent.ComponentName);
                            if (IsComponentInGraph(graph, newComponent.ComponentName))
                            {
                                MessageBox.Show($"Компонент '{text}' уже есть в спецификации!",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        newComponent = new MyComponent(text, type);
                    }

                    try
                    {
                        _fileManager.AddComponentToSpecification(_selectedParent.ComponentName, newComponent.ComponentName);

                        if (existingComponent == null)
                        {
                            _fileManager.AddComponentToComponentList(newComponent);
                            Components.Add(newComponent);
                        }

                        NameTextBox.Clear();
                        TypeComboBox.SelectedItem = null;

                        MessageBox.Show($"Компонент '{text}' добавлен в спецификацию '{_selectedParent.ComponentName}'",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex) when (ex.Message.Contains("Лимит компонентов в спецификации"))
                    {
                        MessageBox.Show($"Не удалось добавить компонент: {ex.Message}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                DataChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsComponentInGraph(ComponentsGraph graph, string componentName)
        {
            if (graph.Value.ComponentName == componentName)
                return true;

            foreach (var spec in graph.Specifications)
            {
                if (IsComponentInGraph(spec, componentName))
                    return true;
            }

            return false;
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