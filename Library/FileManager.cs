using Library.Components;
using Library.Extensions;
using Library.Headers;
using Library.Records;
using System.Collections.Generic;

namespace Library
{
    /// <summary>
    /// Основной класс для управления файлами
    /// </summary>
    public class FileManager : IDisposable
    {
        private static string _path = @$"C:\Users\{Environment.UserName}\Downloads\";
        private const string _compNotFoundExc = "Компонент не найден!";

        private FileStream _compFile;
        private FileStream _specFile;
        private ComponentHeader _compHeader;
        private SpecificationHeader _specHeader;

        private FileManager(FileStream compFile, FileStream specFile, ComponentHeader compHeader, SpecificationHeader specHeader)
        {
            _compFile = compFile;
            _specFile = specFile;
            _compHeader = compHeader;
            _specHeader = specHeader;
        }

        private static void RestoreObjectsFromPtrs(ComponentHeader compHeader, SpecificationHeader specHeader)
        {
            var action = new Action<ComponentRecord>(record =>
            {
                if (record.SpecificationRecordPtr != -1)
                {
                    var tmp = specHeader.GetRecordByPtr(record.SpecificationRecordPtr);
                    if (tmp == null)
                        throw new Exception("Не удалось восстановить ссылки!");
                    record.SpecificationRecord = tmp;
                }
            });
            compHeader.EnumerateRecord(action);

            var action1 = new Action<SpecificationRecord>(record =>
            {
                if (record.ComponentRecordPtr != -1)
                {
                    var tmp = compHeader.GetRecordByPtr(record.ComponentRecordPtr);
                    if (tmp == null)
                        throw new Exception("Не удалось восстановить ссылки!");
                    record.ComponentRecord = tmp;
                }
            });
            specHeader.EnumerateSpecification(action1);
        }

        private void UpdateCompFile()
        {
            var comp = _compHeader.ToBytes();

            _compFile.Seek(0, SeekOrigin.Begin);
            _compFile.Write(comp, 0, comp.Length);
        }

        private void UpdateSpecFile()
        {
            var spec = _specHeader.ToBytes();

            _specFile.Seek(0, SeekOrigin.Begin);
            _specFile.Write(spec, 0, spec.Length);
        }

        private void UpdateFiles()
        {
            UpdateCompFile();
            UpdateSpecFile();
        }

        public static FileManager RestoreFiles(string compFilename, string specFilename, ushort recordLength = 20)
        {
            File.Delete(_path + compFilename);
            File.Delete(_path + specFilename);

            return CreateFiles(compFilename, specFilename, recordLength);
        }

        public static FileManager CreateFiles(string compFilename, string specFilename, ushort recordLength = 20)
        {
            var compFile = new FileStream(_path + compFilename, FileMode.Create, FileAccess.ReadWrite);

            var compHeader = new ComponentHeader(recordLength, specFilename);
            compFile.Write(compHeader.ToBytes(), 0, compHeader.TotalSize);

            var specFile = new FileStream(_path + specFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            var specHeader = new SpecificationHeader();
            specFile.Write(specHeader.ToBytes(), 0, specHeader.TotalSize);

            return new FileManager(compFile, specFile, compHeader, specHeader);
        }

        public static FileManager OpenFiles(string compFilename)
        {
            //Десериализуем файл списка изделий
            var compFile = new FileStream(_path + compFilename, FileMode.Open, FileAccess.ReadWrite);

            byte[] buffer = new byte[compFile.Length];
            int offset = 0;
            compFile.Read(buffer, 0, buffer.Length);

            //Получаем заголовок файла
            var compHeader = ComponentHeader.FromBytes(buffer, offset);
            offset += compHeader.TotalSize;

            //Получаем остальные записи
            if (compHeader.FirstRecordPtr != -1)
            {
                compHeader.FirstRecord = ComponentRecord.FromBytes(buffer, offset);
                offset += compHeader.FirstRecord.GetTotalSize();

                var tmp = compHeader.FirstRecord;

                while (tmp.NextRecordPtr != -1)
                {
                    tmp.NextRecord = ComponentRecord.FromBytes(buffer, offset);
                    offset += tmp.NextRecord.GetTotalSize();
                    tmp = tmp.NextRecord;
                }
            }

            //Десериализуем файл спецификаций
            var specFilename = new string(compHeader.SpecFilename).Trim('\0');
            var specFile = new FileStream(_path + specFilename, FileMode.Open, FileAccess.ReadWrite);

            buffer = new byte[specFile.Length];
            offset = 0;
            specFile.Read(buffer, 0, buffer.Length);

            //Получаем заголовок файла
            var specHeader = SpecificationHeader.FromBytes(buffer, offset);
            offset += specHeader.TotalSize;

            //Получаем остальные записи
            if (specHeader.FirstRecordPtr != -1)
            {
                specHeader.FirstRecord = SpecificationRecord.FromBytes(buffer, offset);
                offset += specHeader.FirstRecord.GetTotalSize();

                var tmp = specHeader.FirstRecord;

                while (tmp.NextRecordPtr != -1 || tmp.SpecificationNextPtr != -1)
                {
                    var tmpSpec = tmp;
                    while (tmpSpec.SpecificationNextPtr != -1)
                    {
                        tmpSpec.SpecificationNext = SpecificationRecord.FromBytes(buffer, offset);
                        offset += tmpSpec.SpecificationNext.GetTotalSize();
                        tmpSpec = tmpSpec.SpecificationNext;
                    }
                    if (tmp.NextRecordPtr == -1)
                        break;

                    tmp.NextRecord = SpecificationRecord.FromBytes(buffer, offset);
                    offset += tmp.NextRecord.GetTotalSize();
                    tmp = tmp.NextRecord;
                }
            }
            try
            {
                //Востанавливаем объекты из указателей
                RestoreObjectsFromPtrs(compHeader, specHeader);
                return new FileManager(compFile, specFile, compHeader, specHeader);
            }
            catch
            {
                compFile.Close();
                specFile.Close();
                throw;
            }
        }

        /// <summary>
        /// Добавление компонента в список изделий
        /// </summary>
        public void AddComponentToComponentList(MyComponent component)
        {
            if (ComponentRecordListExtensions.GetCompRecByName(_compHeader, component.ComponentName) != null)
                throw new ArgumentException("Компонент c таким именем уже существует!");

            var record = new ComponentRecord(component);

            //Добавляем компонент в список компонентов
            _compHeader.PushRecord(record);

            UpdateCompFile();
        }

        /// <summary>
        /// Добавление компонента в спецификацию
        /// </summary>
        public void AddComponentToSpecification(string component, string componentAdded)
        {
            var comp = _compHeader.GetCompRecByName(component);
            if (comp == null)
                throw new ArgumentException(_compNotFoundExc);
            if (comp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("Деталь не может иметь спецификацию!");

            var compAdded = _compHeader.GetCompRecByName(componentAdded);
            if (compAdded == null)
                throw new Exception("Невозможно добавить не существующее комплектующее!");
            if (compAdded.DataArea.ComponentType == ComponentType.Product)
                throw new Exception("Нельзя добавить изделие в спецификацию!");

            var spec = new SpecificationRecord()
            {
                ComponentRecordPtr = _compHeader.GetCompRecPtr(compAdded.DataArea.ComponentName),
                ComponentRecord = compAdded
            };

            if (comp.SpecificationRecord == null)
            {
                _specHeader.PushRecord(spec);
                comp.SpecificationRecord = spec;
            }
            else
            {
                var tmp = comp.SpecificationRecord.SpecificationNext;
                while (tmp != null)
                {
                    tmp = tmp.SpecificationNext;
                }

                comp.SpecificationRecord.SpecificationNextPtr = spec.GetHashCode();
                comp.SpecificationRecord.SpecificationNext = spec;
            }

            UpdateFiles();
        }

        public void Test()
        {
            MyComponent myComponent = new("Изделие1", ComponentType.Product);
            MyComponent myComponent1 = new("Узел1", ComponentType.Node);
            MyComponent myComponent2 = new("Узел2", ComponentType.Node);
            MyComponent myComponent3 = new("Деталь1", ComponentType.Detail);
            MyComponent myComponent4 = new("Деталь2", ComponentType.Detail);
            AddComponentToComponentList(myComponent);
            AddComponentToComponentList(myComponent1);
            AddComponentToComponentList(myComponent2);
            AddComponentToComponentList(myComponent3);
            AddComponentToComponentList(myComponent4);
            AddComponentToSpecification(myComponent.ComponentName, myComponent1.ComponentName);
            AddComponentToSpecification(myComponent.ComponentName, myComponent3.ComponentName);
            AddComponentToSpecification(myComponent1.ComponentName, myComponent2.ComponentName);
            AddComponentToSpecification(myComponent1.ComponentName, myComponent4.ComponentName);
        }

        public IEnumerable<MyComponent> GetAllComponents()
        {
            return ComponentRecordListExtensions.GetComponents(_compHeader);
        }

        public ComponentsGraph GetCompWithSpecs(string compName)
        {
            var myComp = _compHeader.GetCompRecByName(compName);
            if (myComp == null)
                throw new ArgumentException(_compNotFoundExc);
            if (myComp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("У детали нет спецификации!");

            ComponentsGraph specification = new(myComp.DataArea);

            if (myComp.SpecificationRecord == null)
                return specification;

            FindAllSpecs(myComp.SpecificationRecord, specification);

            return specification;
        }

        private void FindAllSpecs(SpecificationRecord record, ComponentsGraph graph)
        {
            while (record != null)
            {
                var tmp = new ComponentsGraph(record.ComponentRecord!.DataArea);
                graph.Specifications.Add(tmp);

                if (record.ComponentRecord.SpecificationRecord != null)
                    FindAllSpecs(record.ComponentRecord.SpecificationRecord, tmp);

                record = record.SpecificationNext;
            }
        }

        public void Dispose()
        {
            _compFile?.Dispose();
            _specFile?.Dispose();
        }
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

        public List<List<string>>? AllSpecsToStrings()
        {
            if (Specifications.Count == 0)
                return null;

            List<List<string>> res = new();

            var strings = SpecsToStrings().ToList();
            if (strings.Count != 0)
                res.Add(strings);

            foreach (var item in Specifications)
            {
                var list = item.AllSpecsToStrings();
                if (list != null)
                    res = res.Concat(list).ToList();
            }

            return res;
        }

        public IEnumerable<string> SpecsToStrings()
        {
            if (Specifications.Count == 0)
                yield break;

            foreach (var item in Specifications)
            {
                yield return item.Value.ComponentName;
            }
        }

        public void EnumerateComponents(ComponentsGraph graph, Action<MyComponent> action)
        {
            throw new NotImplementedException();

            if (Specifications.Count == 0)
                return;

            foreach (var item in Specifications)
            {
                action.Invoke(item.Value);
                EnumerateComponents(item, action);
            }
        }
    }
}