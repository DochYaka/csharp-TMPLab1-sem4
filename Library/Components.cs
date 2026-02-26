using System.Xml.Linq;

namespace Library.Components
{
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

    public class ComponentsGraph
    {
        public MyComponent Value { get; set; }

        public List<ComponentsGraph> Specifications { get; set; }

        public ComponentsGraph(MyComponent value)
        {
            Value = value;
            Specifications = new();
        }
    }
}