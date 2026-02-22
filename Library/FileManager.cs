using System;
using System.Xml.Linq;

namespace Library
{
    public static class ByteArrayExtensions
    {
        public static bool IsEmpty(this byte[] array)
        {
            return array == BitConverter.GetBytes(-1);
        }
    }

    /// <summary>
    /// Основной класс для управления файлами
    /// </summary>
    public class FileManager : IDisposable
    {
        private FileStream _compFile;
        private FileStream _specFile;
        private ComponentListHeader _compHeader;
        private SpecificationHeader _specHeader;
        private static string _path = @$"C:\Users\{Environment.UserName}\Downloads\";

        private FileManager(FileStream compFile, FileStream specFile, ComponentListHeader compHeader, SpecificationHeader specHeader)
        {
            _compFile = compFile;
            _specFile = specFile;
            _compHeader = compHeader;
            _specHeader = specHeader;
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

            var compHeader = new ComponentListHeader(recordLength, specFilename);
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
            var compHeader = ComponentListHeader.FromBytes(buffer, offset);
            offset += compHeader.TotalSize;

            //Получаем остальные записи
            if (compHeader.FirstRecordPtr != -1)
            {
                compHeader.FirstRecord = ComponentListRecord.FromBytes(buffer, offset);
                offset += compHeader.FirstRecord.GetTotalSize();

                var tmp = compHeader.FirstRecord;

                while (tmp.NextRecordPtr != -1)
                {
                    tmp.NextRecord = ComponentListRecord.FromBytes(buffer, offset);
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

            //Востанавливаем объекты из указателей
            RestoreObjectsFromPtrs(compHeader, specHeader);

            return new FileManager(compFile, specFile, compHeader, specHeader);
        }

        private static void RestoreObjectsFromPtrs(ComponentListHeader compHeader, SpecificationHeader specHeader)
        {
            if (compHeader.FirstRecord != null)
            {
                for (var tmpRecord = compHeader.FirstRecord; tmpRecord.NextRecord != null; tmpRecord = tmpRecord.NextRecord)
                {
                    if (tmpRecord.SpecificationRecordPtr != -1)
                    {
                        var tmp = FindRecordByPtr(specHeader, tmpRecord.SpecificationRecordPtr);
                        if (tmp == null)
                            throw new Exception("Не удалось восстановить ссылки!");
                        tmpRecord.SpecificationRecord = tmp;
                    }
                }
            }

            if (specHeader.FirstRecord != null)
            {
                for (var tmpRecord = specHeader.FirstRecord; tmpRecord.NextRecord != null; tmpRecord = tmpRecord.NextRecord)
                {
                    if (tmpRecord.ComponentRecordPtr != -1)
                    {
                        var tmp = FindRecordByPtr(compHeader, tmpRecord.ComponentRecordPtr);
                        if (tmp == null)
                            throw new Exception("Не удалось восстановить ссылки!");
                        tmpRecord.ComponentRecord = tmp;

                        foreach (var component in tmpRecord.ComponentPtrs)
                        {
                            if (component == -1)
                                continue;
                            var tmpComp = FindMyCompByPtr(compHeader, component);
                            if (tmpComp == null)
                                throw new Exception("Не удалось восстановить ссылки!");
                            tmpRecord.AddComponent(tmpComp);
                        }
                    }
                }
            }
        }

        private static T? FindRecordByPtr<T>(Header<T> header, int ptr) where T : Record<T>
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


        /// <summary>
        /// Метод ищет запись с названием компонента
        /// </summary>
        /// <param name="name">Название компонента</param>
        /// <returns>Если запись с компонентом найдена, то возвращает запись, иначе null</returns>
        private ComponentListRecord? FindMyCompByName(string name)
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

        private static MyComponent? FindMyCompByPtr(ComponentListHeader header, int ptr)
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

        /// <summary>
        /// Добавление компонента в список изделий
        /// </summary>
        public void AddComponentToComponentList(MyComponent component)
        {
            if (FindMyCompByName(component.ComponentName) != null)
                throw new ArgumentException("Компонент c таким именем уже существует!");

            var record = new ComponentListRecord(component);

            if (component.ComponentType != ComponentType.Detail)
            {
                SpecificationRecord? tmpSpecRec;
                //Если лист спецификаций пустой, добавляем в него пустую спецификацию и записываем ссылку на нее в запись компонента
                if (_specHeader.FirstRecord == null)
                {
                    _specHeader.FirstRecord = new SpecificationRecord()
                    {
                        ComponentRecord = record,
                        ComponentRecordPtr = record.GetHashCode(),
                    };
                    _specHeader.FirstRecordPtr = _specHeader.FirstRecord.GetHashCode();
                    tmpSpecRec = _specHeader.FirstRecord;
                }
                //Иначе ищем пустую спецификацию в листе спецификаций
                else
                {
                    var tmp = _specHeader.FirstRecord;
                    while (tmp.NextRecord != null)
                    {
                        tmp = tmp.NextRecord;
                    }
                    tmp.NextRecord = new SpecificationRecord()
                    {
                        ComponentRecord = record,
                        ComponentRecordPtr = record.GetHashCode(),
                    };
                    tmp.NextRecordPtr = tmp.NextRecord.GetHashCode();
                    tmpSpecRec = tmp.NextRecord;
                }

                record.SpecificationRecord = tmpSpecRec;
                record.SpecificationRecordPtr = tmpSpecRec.GetHashCode();
            }

            //Добавляем компонент в список компонентов
            if (_compHeader.FirstRecord != null)
            {
                var tmp = _compHeader.FirstRecord;
                while (tmp.NextRecord != null)
                {
                    tmp = tmp.NextRecord;
                }
                tmp.NextRecord = record;
                tmp.NextRecordPtr = record.GetHashCode();
            }
            else
            {
                _compHeader.FirstRecord = record;
                _compHeader.FirstRecordPtr = record.GetHashCode();
            }

            UpdateFiles();
        }

        public List<MyComponent> GetAllComponents()
        {
            var list = new List<MyComponent>();

            var tmp = _compHeader.FirstRecord;

            while (tmp != null)
            {
                if (!tmp.IsDeleted)
                    list.Add(tmp.DataArea);

                tmp = tmp.NextRecord;
            }

            return list;
        }

        /// <summary>
        /// Добавление компонента в спецификацию
        /// </summary>
        public void AddComponentToSpecification(string component, string componentAdded)
        {
            var compRec = FindMyCompByName(component);
            if (compRec == null)
                throw new ArgumentException("Компонент не найден!");
            if (compRec.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("Деталь не может иметь спецификацию!");

            var tmpComp = FindMyCompByName(componentAdded);
            if (tmpComp == null)
                throw new Exception("Невозможно добавить не существующее комплектующее!");
            if (tmpComp.DataArea.ComponentType == ComponentType.Product)
                throw new Exception("Нельзя добавить изделие в спецификацию!");
            compRec.SpecificationRecord?.AddComponent(tmpComp.DataArea);

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

        ///// <summary>
        ///// Удаление записи из списка изделий (логическое удаление)
        ///// </summary>
        //public void DeletePartsListRecord(int position)
        //{
        //    _compFile.Seek(position, SeekOrigin.Begin);
        //    _compFile.WriteByte(0xFF); // Устанавливаем бит удаления в -1 (0xFF)
        //}

        ///// <summary>
        ///// Удаление записи из спецификации (логическое удаление)
        ///// </summary>
        //public void DeleteSpecificationRecord(int position)
        //{
        //    _specFile.Seek(position, SeekOrigin.Begin);
        //    _specFile.WriteByte(0xFF); // Устанавливаем бит удаления в -1 (0xFF)
        //}

        ///// <summary>
        ///// Вывод всей структуры на экран
        ///// </summary>
        //public void DisplayStructure()
        //{
        //    Console.WriteLine("=== СТРУКТУРА ДАННЫХ ===\n");

        //    Console.WriteLine("Файл списка изделий:");
        //    Console.WriteLine($"Длина области данных: {_compHeader.DataRecordLength} байт");
        //    Console.WriteLine($"Первый указатель: {_compHeader.FirstRecordPtr}");
        //    Console.WriteLine($"Указатель на свободную область: {_compHeader.FreeAreaPtr}");
        //    Console.WriteLine($"Имя файла спецификаций: {_compHeader.SpecFileName}\n");

        //    // Вывод всех записей списка изделий
        //    int current = _compHeader.FirstRecordPtr;
        //    int index = 1;

        //    while (current != -1)
        //    {
        //        byte[] buffer = new byte[ComponentListRecord.DeletionBitSize +
        //                                ComponentListRecord.SpecificationRecordPtrSize +
        //                                ComponentListRecord.NextRecordPtrSize +
        //                                _compHeader.DataRecordLength];

        //        _compFile.Seek(current, SeekOrigin.Begin);
        //        _compFile.Read(buffer, 0, buffer.Length);

        //        var record = ComponentListRecord.FromBytes(buffer, 0, _compHeader.DataRecordLength);

        //        Console.WriteLine($"Запись {index} (смещение {current}):");
        //        Console.WriteLine($"  Удалена: {(record.IsDeleted == -1 ? "Да" : "Нет")}");
        //        Console.WriteLine($"  Указатель на спецификацию: {record.SpecificationRecordPtr}");
        //        Console.WriteLine($"  Следующая запись: {record.NextRecordPtr}");
        //        Console.WriteLine($"  Наименование: {record.Name}");

        //        // Если есть спецификация, выводим её
        //        if (record.SpecificationRecordPtr != -1 && record.IsDeleted == 0)
        //        {
        //            DisplaySpecification(record.SpecificationRecordPtr, 2);
        //        }

        //        Console.WriteLine();
        //        current = record.NextRecordPtr;
        //        index++;
        //    }

        //    Console.WriteLine("========================\n");
        //}

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

        public void Dispose()
        {
            _compFile?.Dispose();
            _specFile?.Dispose();
        }
    }
}