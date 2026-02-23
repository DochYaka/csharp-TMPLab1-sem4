using System;
using System.Xml.Linq;

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
                    var tmp = GetRecordByPtr(specHeader, record.SpecificationRecordPtr);
                    if (tmp == null)
                        throw new Exception("Не удалось восстановить ссылки!");
                    record.SpecificationRecord = tmp;
                }
            });
            EnumerateRecord(compHeader, action);

            var action1 = new Action<SpecificationRecord>(record =>
            {
                if (record.ComponentRecordPtr != -1)
                {
                    var tmp = GetRecordByPtr(compHeader, record.ComponentRecordPtr);
                    if (tmp == null)
                        throw new Exception("Не удалось восстановить ссылки!");
                    record.ComponentRecord = tmp;
                }
            });
            EnumerateRecord(specHeader, action1);
        }

        private static T? GetRecordByPtr<T>(Header<T> header, int ptr) where T : Record<T>
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

        private static MyComponent? GetMyCompByPtr(ComponentHeader header, int ptr)
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

        private static void EnumerateRecord<T>(Header<T> header, Action<T> action) where T : Record<T>
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

        private static void PushRecord<T>(Header<T> header, T record) where T : Record<T>
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

        private int GetRecordPtr(string compName)
        {
            if (_compHeader.FirstRecord != null)
            {
                if (_compHeader.FirstRecord.DataArea.ComponentName == compName)
                    return _compHeader.FirstRecordPtr;

                var record = _compHeader.FirstRecord;
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
        private ComponentRecord? GetCompRecByName(string name)
        {
            if (_compHeader.FirstRecord != null)
            {
                var tmp = _compHeader.FirstRecord;
                while (tmp != null)
                {
                    if (tmp.DataArea.ComponentName == name)
                        return tmp;
                    tmp = tmp.NextRecord;
                }
            }
            return null;
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

                while (tmp.NextRecordPtr != -1)
                {
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
            if (GetCompRecByName(component.ComponentName) != null)
                throw new ArgumentException("Компонент c таким именем уже существует!");

            var record = new ComponentRecord(component);

            //Добавляем компонент в список компонентов
            PushRecord(_compHeader, record);

            UpdateCompFile();
        }

        /// <summary>
        /// Добавление компонента в спецификацию
        /// </summary>
        public void AddComponentToSpecification(string component, string componentAdded)
        {
            var comp = GetCompRecByName(component);
            if (comp == null)
                throw new ArgumentException(_compNotFoundExc);
            if (comp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("Деталь не может иметь спецификацию!");

            var compAdded = GetCompRecByName(componentAdded);
            if (compAdded == null)
                throw new Exception("Невозможно добавить не существующее комплектующее!");
            if (compAdded.DataArea.ComponentType == ComponentType.Product)
                throw new Exception("Нельзя добавить изделие в спецификацию!");

            var spec = new SpecificationRecord()
            {
                ParentComponentRecordPtr = GetRecordPtr(comp.DataArea.ComponentName),
                ComponentRecordPtr = GetRecordPtr(compAdded.DataArea.ComponentName),
                ComponentRecord = compAdded
            };

            PushRecord(_specHeader, spec);

            if (comp.SpecificationRecord == null)
                comp.SpecificationRecord = spec;

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

        /// <summary>
        /// Вывод всей структуры на экран
        /// </summary>
        public void PrintComponents()
        {
            Console.WriteLine($"{"Наименование",-20}Тип");

            var print = new Action<ComponentRecord>(record =>
            {
                Console.WriteLine($"{record.DataArea.ComponentName,-20}{record.DataArea.ComponentType.ToString()}");
            });

            EnumerateRecord(_compHeader, print);
        }

        public void PrintCompWithSpec(string compName)
        {
            var myComp = GetCompRecByName(compName);
            if (myComp == null)
                throw new ArgumentException(_compNotFoundExc);
            if (myComp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("У детали нет спецификации!");

            Console.WriteLine(compName);

            if (myComp.SpecificationRecord == null)
                return;

            List<ComponentRecord> components = new();

            throw new NotImplementedException();
        }

        //private void DisplaySpecification(int specPtr, int indentLevel)
        //{
        //    string indent = new string(' ', indentLevel * 2);
        //    int current = specPtr;

        //    while (current != -1)
        //    {
        //        byte[] buffer = new byte[SpecificationRecord.TotalSize];
        //        _specFile.Seek(current, SeekOrigin.Begin);
        //        _specFile.Read(buffer, 0, SpecificationRecord.TotalSize);

        //        var record = SpecificationRecord.FromBytes(buffer);

        //        if (record.IsDeleted == 0)
        //        {
        //            // Получаем имя компонента из списка изделий
        //            string componentName = "???";
        //            if (record.ComponentRecordPtr != -1)
        //            {
        //                byte[] nameBuffer = new byte[_compHeader.DataRecordLength];
        //                _compFile.Seek(record.ComponentRecordPtr + ComponentListRecord.DeletionBitSize +
        //                               ComponentListRecord.SpecificationRecordPtrSize +
        //                               ComponentListRecord.NextRecordPtrSize, SeekOrigin.Begin);
        //                _compFile.Read(nameBuffer, 0, _compHeader.DataRecordLength);
        //                componentName = Encoding.ASCII.GetString(nameBuffer).TrimEnd();
        //            }

        //            Console.WriteLine($"{indent}Спецификация (смещение {current}):");
        //            Console.WriteLine($"{indent}  Компонент: {componentName} (указ. {record.ComponentRecordPtr})");
        //            Console.WriteLine($"{indent}  Количество: {record.Quantity}");
        //            Console.WriteLine($"{indent}  Следующая: {record.NextRecordPtr}");
        //        }

        //        current = record.NextRecordPtr;
        //    }
        //}

        //private void UpdatePartsHeader()
        //{
        //    _compFile.Seek(0, SeekOrigin.Begin);
        //    _compFile.Write(_compHeader.ToBytes(), 0, ComponentListHeader.TotalSize);
        //}

        //private void UpdateSpecHeader()
        //{
        //    _specFile.Seek(0, SeekOrigin.Begin);
        //    _specFile.Write(_specHeader.ToBytes(), 0, SpecificationHeader.TotalSize);
        //}

        public void Dispose()
        {
            _compFile?.Dispose();
            _specFile?.Dispose();
        }
    }
}