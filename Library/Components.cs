using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace Library
{
    public interface ISpecification;

    public class MyComponent(string name, ComponentType type)
    {
        public ComponentType ComponentType { get; private set; } = type;
        public string ComponentName { get; set; } = name;

        //public ObservableCollection<MyComponent> Children { get; set; }
        //    = new ObservableCollection<MyComponent>();

        public override int GetHashCode()
        {
            return ComponentName.GetHashCode();
        }
    }

    public enum ComponentType
    {
        Detail, Product, Node
    }

    public static class StringExtentions
    {
        public static ComponentType ToComponentType(this string str)
        {
            return str.ToLower() switch
            {
                "деталь" => ComponentType.Detail,
                "узел" => ComponentType.Node,
                "изделие" => ComponentType.Product,
                _ => throw new ArgumentException("Компонент не найден!"),
            };
        }
    }
}