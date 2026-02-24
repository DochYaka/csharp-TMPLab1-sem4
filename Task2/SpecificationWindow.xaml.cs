using Library;
using Library.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Task2
{
    /// <summary>
    /// Логика взаимодействия для SpecificationWindow.xaml
    /// </summary>
    public partial class SpecificationWindow : Window
    {
        private FileManager _fileManager;

        public SpecificationWindow(FileManager fileManager)
        {
            InitializeComponent();

            _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));

            // Сразу строим дерево при открытии
            Loaded += (s, e) => BuildTree();
        }

        // Построить дерево
        private void BuildTree()
        {
            treeView.Items.Clear();

            if (_fileManager == null) return;

            try
            {
                var allComponents = _fileManager.GetAllComponents().ToList();

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
                            Header = $"{root.ComponentName} ({root.ComponentType.ToString()})",
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
                Header = $"{graph.Value.ComponentName} ({graph.Value.ComponentType.ToString()})",
                Tag = graph.Value
            };

            foreach (var childGraph in graph.Specifications)
            {
                node.Items.Add(CreateTreeNode(childGraph));
            }

            return node;
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _fileManager.Dispose();
                _fileManager = FileManager.CreateFiles("components.dat", "specs.dat");
                _fileManager.Test();

                BuildTree();

                MessageBox.Show("Тестовые данные созданы!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}