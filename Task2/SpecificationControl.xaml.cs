using Library;
using Library.Components;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Task2
{
    /// <summary>
    /// Логика взаимодействия для SpecificationWindow.xaml
    /// </summary>
    public partial class SpecificationControl : UserControl
    {
        private FileManager _fileManager;
        private MyComponent? _selectedComponent;
        private TreeViewItem? _selectedTreeNode;

        private MyComponent parentComponent;

        private TreeViewItem? _lastHighlightedItem;

        public SpecificationControl(FileManager fileManager)
        {
            InitializeComponent();

            _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));

            Loaded += (s, e) => BuildTree();
        }

        public void BuildTree(string? searchText = null)
        {
            treeView.Items.Clear();

            if (_fileManager == null) return;

            try
            {
                var allComponents = _fileManager.GetAllComponents().ToList();

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    allComponents = allComponents
                        .Where(c => c.ComponentName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var rootComponents = FindRootComponents(allComponents);

                foreach (var root in rootComponents)
                {
                    try
                    {
                        var graph = _fileManager.GetCompWithSpecs(root.ComponentName);
                        var treeNode = CreateTreeNode(graph);
                        treeView.Items.Add(treeNode);
                    }
                    catch
                    {
                        var node = new TreeViewItem
                        {
                            Header = $"{root.ComponentName} ({root.ComponentType})",
                            Tag = root
                        };
                        treeView.Items.Add(node);
                    }
                }

                ExpandAll(treeView.Items);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка построения дерева: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExpandAll(ItemCollection items)
        {
            foreach (TreeViewItem item in items)
            {
                item.IsExpanded = true;
                ExpandAll(item.Items);
            }
        }

        private List<MyComponent> FindRootComponents(List<MyComponent> allComponents)
        {
            var referencedNames = new HashSet<string>();

            foreach (var comp in allComponents)
            {
                if (_fileManager == null) continue;

                try
                {
                    var graph = _fileManager.GetCompWithSpecs(comp.ComponentName);
                    
                    AddReferencedNames(graph, referencedNames);
                }
                catch
                {
                }
            }

            return allComponents
                .Where(c => !referencedNames.Contains(c.ComponentName))
                .ToList();
        }

        private void AddReferencedNames(ComponentsGraph graph, HashSet<string> names)
        {
            foreach (var spec in graph.Specifications)
            {
                names.Add(spec.Value.ComponentName);
                AddReferencedNames(spec, names);
            }
        }

        private TreeViewItem CreateTreeNode(ComponentsGraph graph)
        {
            var node = new TreeViewItem
            {
                Header = $"{graph.Value.ComponentName} ({graph.Value.ComponentType})",
                Tag = graph.Value
            };

            foreach (var childGraph in graph.Specifications)
            {
                node.Items.Add(CreateTreeNode(childGraph));
            }

            return node;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeView.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is MyComponent component)
            {
                _selectedComponent = component;
                _selectedTreeNode = selectedItem;
            }
            else
            {
                _selectedComponent = null;
                _selectedTreeNode = null;
            }
        }

        private void AddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedComponent == null)
            {
                MessageBox.Show("Выберите компонент, к которому хотите добавить спецификацию", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedComponent.ComponentType == ComponentType.Detail)
            {
                MessageBox.Show($"Компонент '{_selectedComponent.ComponentName}' является деталью и не может иметь спецификацию!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Window
            {
                Title = "Добавление компонента в спецификацию",
                Width = 400,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            stackPanel.Children.Add(new TextBlock { Text = "Наименование компонента:"});
            var nameTextBox = new TextBox {Padding = new Thickness(5) };
            stackPanel.Children.Add(nameTextBox);

            stackPanel.Children.Add(new TextBlock { Text = "Тип компонента:"});
            var typeComboBox = new ComboBox
            {
                Padding = new Thickness(5),
                ItemsSource = new[] { ComponentType.Node, ComponentType.Detail }
            };
            typeComboBox.SelectedIndex = 0;
            stackPanel.Children.Add(typeComboBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            okButton.Click += (s, args) =>
            {
                try
                {
                    var name = nameTextBox.Text?.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        MessageBox.Show("Введите наименование компонента", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var type = (ComponentType)typeComboBox.SelectedItem;

                    var existingComponent = _fileManager.GetAllComponents()
                        .FirstOrDefault(c => c.ComponentName.Equals(name, StringComparison.OrdinalIgnoreCase));

                    MyComponent newComponent;

                    if (existingComponent != null)
                    {
                        newComponent = existingComponent;

                        try
                        {
                            var graph = _fileManager.GetCompWithSpecs(_selectedComponent.ComponentName);
                            if (IsComponentInGraph(graph, newComponent.ComponentName))
                            {
                                MessageBox.Show($"Компонент '{name}' уже есть в спецификации!",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        newComponent = new MyComponent(name, type);
                        _fileManager.AddComponentToComponentList(newComponent);
                    }

                    _fileManager.AddComponentToSpecification(_selectedComponent.ComponentName, newComponent.ComponentName);

                    dialog.DialogResult = true;
                    dialog.Close();

                    BuildTree(SearchTextBox.Text);

                    MessageBox.Show($"Компонент '{name}' добавлен в спецификацию '{_selectedComponent.ComponentName}'",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    DataChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelButton.Click += (s, args) => dialog.DialogResult = false;

            dialog.ShowDialog();
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

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedComponent == null)
            {
                MessageBox.Show("Выберите компонент для изменения", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "Изменение компонента",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"Изменение компонента: {_selectedComponent.ComponentName}",
                FontWeight = FontWeights.Bold
            });

            stackPanel.Children.Add(new TextBlock { Text = "Новое наименование:"});

            var nameTextBox = new TextBox
            {
                Text = _selectedComponent.ComponentName,
                Padding = new Thickness(5)
            };
            stackPanel.Children.Add(nameTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var okButton = new Button
            {
                Content = "Сохранить",
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);

            dialog.Content = stackPanel;

            okButton.Click += (s, args) =>
            {
                try
                {
                    var newName = nameTextBox.Text?.Trim();

                    if (string.IsNullOrEmpty(newName))
                    {
                        MessageBox.Show("Введите наименование компонента", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (newName == _selectedComponent.ComponentName)
                    {
                        dialog.Close();
                        return;
                    }

                    var existingComponent = _fileManager.GetAllComponents()
                        .FirstOrDefault(c => c.ComponentName.Equals(newName, StringComparison.OrdinalIgnoreCase)
                                          && c != _selectedComponent);

                    if (existingComponent != null)
                    {
                        MessageBox.Show($"Компонент с именем '{newName}' уже существует!",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string oldName = _selectedComponent.ComponentName;
                    _selectedComponent.ComponentName = newName;

                    dialog.DialogResult = true;
                    dialog.Close();

                    BuildTree(SearchTextBox.Text);

                    MessageBox.Show($"Компонент '{oldName}' изменён на '{newName}'",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    DataChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при изменении: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelButton.Click += (s, args) => dialog.DialogResult = false;

            dialog.ShowDialog();
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var component = _selectedComponent;

            if (_selectedComponent == null)
            {
                MessageBox.Show("Выберите компонент для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Удалить компонент '{_selectedComponent.ComponentName}'?\n" +
                $"ВНИМАНИЕ: Если на этот компонент есть ссылки в спецификациях, удаление может привести к ошибкам!",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _fileManager.DeleteComponentInSpecification(parentComponent.ComponentName, component.ComponentName);
                _fileManager.Truncate();

                BuildTree();
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            string searchText = SearchTextBox.Text.Trim();

            if (string.IsNullOrEmpty(searchText))
            {
                MessageBox.Show("Введите текст для поиска");
                return;
            }

            if (_lastHighlightedItem != null)
                _lastHighlightedItem.Background = Brushes.Transparent;

            TreeViewItem? foundItem = FindTreeNode(treeView.Items, searchText);

            if (foundItem != null)
            {
                foundItem.Background = Brushes.Blue;

                _lastHighlightedItem = foundItem;
                MessageBox.Show($"Найден: {((MyComponent)foundItem.Tag).ComponentName}");
            }
            else { MessageBox.Show($"Компонент '{searchText}' не найден"); }
        }

        private TreeViewItem? FindTreeNode(ItemCollection items, string searchText)
        {
            foreach (TreeViewItem item in items)
            {
                if (item.Tag is MyComponent comp &&
                    comp.ComponentName.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                    return item;

                if (item.Items.Count > 0)
                {
                    var found = FindTreeNode(item.Items, searchText);
                    if (found != null) return found;
                }
            }
            return null;
        }

        public event EventHandler DataChanged;
    }
}