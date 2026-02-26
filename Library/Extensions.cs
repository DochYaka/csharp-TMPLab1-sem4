using Library.Components;
using Library.Exceptions;
using Library.Headers;
using Library.Records;
using System.Runtime.Intrinsics.X86;

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

        public static T? GetPrevRecordByPtr<T>(this Header<T> header, int ptr) where T : Record<T>
        {
            if (header.FirstRecord != null)
            {
                if (header.FirstRecordPtr == ptr)
                    throw new FirstComponentInListException();
                for (var tmpRecord = header.FirstRecord; tmpRecord.NextRecord != null; tmpRecord = tmpRecord.NextRecord)
                {
                    if (tmpRecord.NextRecordPtr == ptr)
                        return tmpRecord;
                }
            }
            return null;
        }

        public static T? GetPrevRecord<T>(this Header<T> header, T record) where T : Record<T>
        {
            return header.GetPrevRecordByPtr(header.GetRecordPtr(record));
        }

        public static int GetRecordPtr<T>(this Header<T> header, T record) where T : Record<T>
        {
            if (header.FirstRecord != null)
            {
                if (ReferenceEquals(header.FirstRecord, record))
                    return header.FirstRecordPtr;
                for (var tmpRecord = header.FirstRecord; tmpRecord.NextRecord != null; tmpRecord = tmpRecord.NextRecord)
                {
                    if (ReferenceEquals(tmpRecord.NextRecord, record))
                        return tmpRecord.NextRecordPtr;
                }
            }

            return -1;
        }

        public static void EnumerateRecords<T>(this Header<T> header, Action<T> action) where T : Record<T>
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

        public static T? FindRecord<T>(this Header<T> header, Func<T, bool> condition) where T : Record<T>
        {
            if (header.FirstRecord != null)
            {
                var tmp = header.FirstRecord;
                while (tmp != null)
                {
                    if (condition.Invoke(tmp))
                        return tmp;
                    tmp = tmp.NextRecord;
                }
            }
            return null;
        }
    }

    public static class ComponentRecordListExtensions
    {
        public static MyComponent? GetCompByPtr(this ComponentHeader header, int ptr)
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
        /// <param name="compName">Название компонента</param>
        /// <returns>Если запись с компонентом найдена, то возвращает запись, иначе null</returns>
        public static ComponentRecord? GetCompRecByName(this ComponentHeader header, string compName)
        {
            if (header.FirstRecord != null)
            {
                var tmp = header.FirstRecord;
                while (tmp != null)
                {
                    if (tmp.DataArea.ComponentName == compName)
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

            header.EnumerateRecords(tmp);

            return res;
        }

        public static void EnumerateAllCompSpecs(this ComponentRecord record, Action<SpecificationRecord> action)
        {
            if (record.SpecificationRecord == null)
                return;
            EnumerateAllCompSpecs(record.SpecificationRecord, action);
        }

        private static void EnumerateAllCompSpecs(SpecificationRecord record, Action<SpecificationRecord> action)
        {
            while (record != null)
            {
                action.Invoke(record);

                if (record.ComponentRecord!.SpecificationRecord != null)
                    EnumerateAllCompSpecs(record.ComponentRecord.SpecificationRecord, action);

                record = record.SpecificationNext;
            }
        }

        public static bool? EnumerateAllCompSpecsWithCondition(this ComponentRecord record, Func<SpecificationRecord, bool> action)
        {
            if (record.SpecificationRecord == null)
                return null;
            return EnumerateAllCompSpecsWithCondition(record.SpecificationRecord, action);
        }

        private static bool? EnumerateAllCompSpecsWithCondition(SpecificationRecord record, Func<SpecificationRecord, bool> action)
        {
            while (record != null)
            {
                if (action.Invoke(record))
                    return true;

                if (record.ComponentRecord!.SpecificationRecord != null)
                    return EnumerateAllCompSpecsWithCondition(record.ComponentRecord.SpecificationRecord, action);

                record = record.SpecificationNext;
            }
            return false;
        }

        public static ComponentRecord? GetCompWithSpec(this ComponentHeader header, SpecificationRecord specificationRecord)
        {
            return header.FindRecord(record =>
            {
                return ReferenceEquals(record.SpecificationRecord, specificationRecord);
            });
        }
    }

    public static class SpecificationRecordListExtensions
    {
        public static void EnumerateSpecRecords(this SpecificationHeader header, Action<SpecificationRecord> action)
        {
            header.EnumerateRecords(record => {
                var tmp = record;
                while (tmp != null)
                {
                    action.Invoke(tmp);
                    tmp = tmp.SpecificationNext;
                }
            });
        }

        private static SpecificationRecord? FindFirstSpecRecord(this SpecificationHeader header, SpecificationRecord record)
        {
            if (header.GetRecordPtr(record) != -1)
                return record;

            if (header == null)
                return null;

            if (header.FirstRecord == null)
                return null;

            var tmp = header.FirstRecord;

            while (tmp != null)
            {
                var tmpSpec = tmp;
                while (tmpSpec!.SpecificationNext != null)
                {
                    if (ReferenceEquals(tmpSpec.SpecificationNext, record))
                        return tmp;

                    tmpSpec = tmp.SpecificationNext;
                }
                tmp = tmp.NextRecord;
            }

            return null;

        }

        public static SpecificationRecord? GetPrevSpec(this SpecificationHeader header, SpecificationRecord record)
        {
            if (header.GetRecordPtr(record) != -1)
                throw new FirstComponentInListException();

            var tmp = header.FindFirstSpecRecord(record);
            if (tmp == null)
                return null;

            while (tmp.SpecificationNext != null)
            {
                if (ReferenceEquals(tmp.SpecificationNext, record))
                    return tmp;
                tmp = tmp.SpecificationNext;
            }

            return null;
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