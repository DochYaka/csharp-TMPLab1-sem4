using System.Xml.Linq;

namespace Library
{
    public interface ISpecification;

    public class MyComponent(string name, ComponentType type)
    {
        public ComponentType ComponentType { get; private set; } = type;
        public string ComponentName { get; set; } = name;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in ComponentName)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }
    }

    public enum ComponentType
    {
        Detail, Product, Node
    }

    public static class Extentions
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

        public static string ToString(this ComponentType type)
        {
            return type switch
            {
                ComponentType.Detail => "деталь",
                ComponentType.Node => "узел",
                ComponentType.Product => "изделие",
                _ => throw new ArgumentException("Не существующий тип!")
            };
        }
    }
}