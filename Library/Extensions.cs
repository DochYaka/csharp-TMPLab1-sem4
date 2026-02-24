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
    }
}