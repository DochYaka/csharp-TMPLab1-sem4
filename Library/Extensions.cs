using Library.Components;
using Library.Headers;
using Library.Records;


namespace Library.Extensions
{
    public static class MyComponentExtentions
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

        public static string ToStr(this ComponentType type)
        {
            return type switch
            {
                ComponentType.Detail => "Деталь",
                ComponentType.Node => "Узел",
                ComponentType.Product => "Изделие",
                _ => throw new ArgumentException("Не существующий тип!")
            };
        }
    }

    public static class RecordListExtensions
    {
        public static T? GetRecordByPtr<T>(this Header<T> header, int ptr) where T : Record<T>
        {
            if (header.FirstRecord != null)
            {
                if (header.FirstRecordPtr == ptr)
                    return header.FirstRecord;
                for (var tmpRecord = header.FirstRecord; tmpRecord.NextRecord != null; tmpRecord = tmpRecord.NextRecord)
                {
                    if (tmpRecord.NextRecordPtr == ptr)
                        return tmpRecord.NextRecord;
                }
            }
            return null;
        }

        public static void EnumerateRecord<T>(this Header<T> header, Action<T> action) where T : Record<T>
        {
            if (header.FirstRecord != null)
            {
                var tmp = header.FirstRecord;
                while (tmp != null)
                {
                    action.Invoke(tmp);
                    tmp = tmp.NextRecord;
                }
            }
        }

        public static void PushRecord<T>(this Header<T> header, T record) where T : Record<T>
        {
            if (header.FirstRecord != null)
            {
                var tmp = header.FirstRecord;
                while (tmp.NextRecord != null)
                {
                    tmp = tmp.NextRecord;
                }
                tmp.NextRecord = record;
                tmp.NextRecordPtr = record.GetHashCode();
            }
            else
            {
                header.FirstRecord = record;
                header.FirstRecordPtr = record.GetHashCode();
            }
        }
    }

    public static class ComponentRecordListExtensions
    {
        public static MyComponent? GetMyCompByPtr(this ComponentHeader header, int ptr)
        {
            if (header.FirstRecord != null)
            {
                var tmp = header.FirstRecord;
                while (tmp != null)
                {
                    if (tmp.DataArea.GetHashCode() == ptr)
                        return tmp.DataArea;

                    tmp = tmp.NextRecord;
                }
            }
            return null;
        }

        public static int GetCompRecPtr(this ComponentHeader header, string compName)
        {
            if (header.FirstRecord != null)
            {
                if (header.FirstRecord.DataArea.ComponentName == compName)
                    return header.FirstRecordPtr;

                var record = header.FirstRecord;
                while (record.NextRecord != null)
                {
                    if (record.NextRecord.DataArea.ComponentName == compName)
                        return record.NextRecordPtr;
                    record = record.NextRecord;
                }
            }
            return -1;
        }

        /// <summary>
        /// Метод ищет запись с названием компонента
        /// </summary>
        /// <param name="name">Название компонента</param>
        /// <returns>Если запись с компонентом найдена, то возвращает запись, иначе null</returns>
        public static ComponentRecord? GetCompRecByName(this ComponentHeader header, string name)
        {
            if (header.FirstRecord != null)
            {
                var tmp = header.FirstRecord;
                while (tmp != null)
                {
                    if (tmp.DataArea.ComponentName == name)
                        return tmp;
                    tmp = tmp.NextRecord;
                }
            }
            return null;
        }

        public static IEnumerable<MyComponent> GetComponents(this ComponentHeader header)
        {
            var res = new List<MyComponent>();

            var tmp = new Action<ComponentRecord>(x =>
            {
                res.Add(x.DataArea);
            });

            header.EnumerateRecord(tmp);

            return res;
        }
    }

    public static class SpecificationRecordListExtensions
    {
        public static void EnumerateSpecification(this SpecificationHeader header, Action<SpecificationRecord> action)
        {
            var action1 = new Action<SpecificationRecord>(record =>
            {
                var tmp = record;
                while (tmp != null)
                {
                    action.Invoke(tmp);
                    tmp = tmp.SpecificationNext;
                }
            });

            header.EnumerateRecord(action1);
        }

        public static void EnumerateAllSpecs(this SpecificationRecord record, Action<SpecificationRecord> action)
        {
            while (record != null)
            {
                action.Invoke(record);

                if (record.ComponentRecord!.SpecificationRecord != null)
                    EnumerateAllSpecs(record.ComponentRecord.SpecificationRecord, action);

                record = record.SpecificationNext;
            }
        }

        public static bool EnumerateAllSpecsWithCondition(this SpecificationRecord record, Func<SpecificationRecord, bool> action)
        {
            while (record != null)
            {
                if (action.Invoke(record))
                    return true;

                if (record.ComponentRecord!.SpecificationRecord != null)
                    return EnumerateAllSpecsWithCondition(record.ComponentRecord.SpecificationRecord, action);

                record = record.SpecificationNext;
            }

            return false;
        }
    }

    public static class ComponentsGraphExtensions
    {
        public static List<List<string>>? AllSpecsToStrings(this ComponentsGraph graph)
        {
            if (graph.Specifications.Count == 0)
                return null;

            List<List<string>> res = new();

            var strings = graph.SpecsToStrings().ToList();
            if (strings.Count != 0)
                res.Add(strings);

            foreach (var item in graph.Specifications)
            {
                var list = item.AllSpecsToStrings();
                if (list != null)
                    res = res.Concat(list).ToList();
            }

            return res;
        }

        public static IEnumerable<string> SpecsToStrings(this ComponentsGraph graph)
        {
            if (graph.Specifications.Count == 0)
                yield break;

            foreach (var item in graph.Specifications)
            {
                yield return item.Value.ComponentName;
            }
        }

        public static void EnumerateComponents(this ComponentsGraph graph, Action<MyComponent, int> action, int depth = 0)
        {
            if (graph.Specifications.Count == 0)
                return;
            depth++;
            foreach (var item in graph.Specifications)
            {
                action.Invoke(item.Value, depth);
                EnumerateComponents(item, action, depth);
            }
        }
    }
}