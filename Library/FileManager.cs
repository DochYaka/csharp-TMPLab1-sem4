using Library.Components;
using Library.Exceptions;
using Library.Headers;
using Library.Records;
using Library.Extensions;
using System.ComponentModel;

namespace Library
{
    /// <summary>
    /// Основной класс для управления файлами
    /// </summary>
    public class FileManager : IDisposable
    {
        private static string _path = @$"C:\Users\{Environment.UserName}\Downloads\";
        private const string _compNotFoundExc = "Компонент не найден!";
        private const string _compDeletedExc = "Компонент уже помечен на удаление!";

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
            compHeader.EnumerateRecords(action);

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
            specHeader.EnumerateSpecRecords(action1);
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
            if (_compHeader.GetCompRecByName(component.ComponentName) != null)
                throw new ArgumentException("Компонент c таким именем уже существует!");

            var record = new ComponentRecord(component);

            //Добавляем компонент в список компонентов
            _compHeader.PushRecord(record);

            UpdateCompFile();
        }

        /// <summary>
        /// Добавление компонента в спецификацию
        /// </summary>
        public void AddComponentToSpecification(string parentComponent, string componentAdded)
        {
            if (parentComponent == componentAdded)
                throw new Exception("Нельзя добавить в спецификацию этот же компонент!");
            var comp = _compHeader.GetCompRecByName(parentComponent);
            if (comp == null)
                throw new ArgumentException($"{parentComponent}: {_compNotFoundExc}");
            if (comp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("Деталь не может иметь спецификацию!");

            var compAdded = _compHeader.GetCompRecByName(componentAdded);
            if (compAdded == null)
                throw new Exception($"{componentAdded}: {_compNotFoundExc}");

            if (comp.SpecificationRecord != null)
            {
                comp.EnumerateAllCompSpecs(rec =>
                {
                    if (rec.ComponentRecord!.DataArea.ComponentName == componentAdded)
                        throw new Exception("Нельзя добавить в спецификацию уже добавленный компонент!");
                });
            }

            if (compAdded.SpecificationRecord != null)
            {
                compAdded.EnumerateAllCompSpecs(rec =>
                {
                    if (rec.ComponentRecord!.DataArea.ComponentName == parentComponent)
                        throw new Exception("Нельзя добавить в спецификацию родительский компонент!");
                });
            }

            var spec = new SpecificationRecord()
            {
                ComponentRecordPtr = _compHeader.GetCompRecPtr(compAdded.DataArea.ComponentName),
                ComponentRecord = compAdded
            };

            if (comp.SpecificationRecord == null)
            {
                _specHeader.PushRecord(spec);
                comp.SpecificationRecord = spec;
                comp.SpecificationRecordPtr = spec.GetHashCode();
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

        public void DeleteComponent(string component)
        {
            var comp = _compHeader.GetCompRecByName(component);
            if (comp == null)
                throw new ArgumentException(_compNotFoundExc);
            if (comp.IsDeleted)
                throw new ArgumentException(_compDeletedExc);

            comp.IsDeleted = true;

            _specHeader.EnumerateSpecRecords(rec =>
            {
                if (!rec.IsDeleted && rec.ComponentRecord!.DataArea.ComponentName == component)
                {
                    comp.IsDeleted = false;
                    throw new ArgumentException("Компонент присутствует в спецификациях других компонентов!");
                }
            });

            UpdateCompFile();
        }

        public void DeleteComponentInSpecification(string parentComponent, string componentDeleted)
        {
            var parentComp = _compHeader.GetCompRecByName(parentComponent);
            if (parentComp == null)
                throw new ArgumentException($"{parentComponent}: {_compNotFoundExc}");
            if (parentComp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("У детали не может быть спецификации!");
            if (parentComp.SpecificationRecord == null)
                throw new Exception("У компонента отсутствует спецификация!");

            var compDeleted = _compHeader.GetCompRecByName(componentDeleted);
            if (compDeleted == null)
                throw new ArgumentException($"{componentDeleted}: {_compNotFoundExc}");

            var condition = parentComp.EnumerateAllCompSpecsWithCondition(rec =>
            {
                if (rec.ComponentRecord!.DataArea.ComponentName == componentDeleted)
                {
                    if (rec.IsDeleted)
                        throw new Exception(_compDeletedExc);
                    rec.IsDeleted = true;
                    return true;
                }
                return false;
            });

            if (condition == null || !(bool)condition)
                throw new Exception("Компонент в спецификации не найден");

            UpdateSpecFile();
        }

        public void RestoreAllComponents()
        {
            var action = new Action<Record>(rec =>
            {
                if (rec.IsDeleted)
                    rec.IsDeleted = false;
            });

            _compHeader.EnumerateRecords(action);
            _specHeader.EnumerateSpecRecords(action);
        }

        public void Truncate()
        {
            _compHeader.EnumerateRecords(rec =>
            {
                if (rec.IsDeleted)
                    TruncateComponent(rec);
            });

            _specHeader.EnumerateSpecRecords(rec =>
            {
                if (rec.IsDeleted)
                    TruncateSpecification(rec);
            });

            UpdateFiles();
        }

        public void TruncateComponent(ComponentRecord component)
        {
            if (!component.IsDeleted)
                throw new Exception("Компонент не помечен на удаление!");

            if (component.SpecificationRecord != null)
                TruncateComponentSpecifications(component.SpecificationRecord);

            try
            {
                var predComp = _compHeader.GetPrevRecord(component);
                if (predComp == null)
                    throw new Exception("Что то пошло не так");
                predComp.NextRecord = component.NextRecord;
                predComp.NextRecordPtr = component.NextRecordPtr;
            }
            catch (FirstComponentInListException)
            {
                _compHeader.FirstRecord = component.NextRecord;
                _compHeader.FirstRecordPtr = component.NextRecordPtr;
            }
            catch
            {
                throw;
            }

            UpdateFiles();
        }

        public void TruncateSpecification(SpecificationRecord specification)
        {
            if (!specification.IsDeleted)
                throw new Exception("Спецификация не помечена на удаление!");
            try
            {
                var predSpec = _specHeader.GetPrevSpec(specification);
                if (predSpec == null)
                    throw new Exception("Что то пошло не так");
                predSpec.SpecificationNext = specification.SpecificationNext;
                predSpec.SpecificationNextPtr = specification.SpecificationNextPtr;
            }
            catch (FirstComponentInListException)
            {
                var comp = _compHeader.GetCompWithSpec(specification);
                if (comp == null)
                    throw new Exception("Что то пошло не так");
                comp.SpecificationRecord = null;
                comp.SpecificationRecordPtr = -1;
                try
                {
                    var predRec = _specHeader.GetPrevRecord(specification);
                    if (predRec == null)
                        throw new Exception("Что то пошло не так");
                    predRec.NextRecord = specification.NextRecord;
                    predRec.NextRecordPtr = specification.NextRecordPtr;
                }
                catch
                {
                    _specHeader.FirstRecord = specification.NextRecord;
                    _specHeader.FirstRecordPtr = specification.NextRecordPtr;
                }
            }
            catch
            {
                throw;
            }

            UpdateFiles();
        }

        private void TruncateComponentSpecifications(SpecificationRecord? record)
        {
            if (record == null)
                return;

            try
            {
                var predRec = _specHeader.GetPrevRecordByPtr(_specHeader.GetRecordPtr(record));
                if (predRec == null)
                    throw new Exception("Что то пошло не так");
                predRec.NextRecord = record.NextRecord;
                predRec.NextRecordPtr = record.NextRecordPtr;
            }
            catch (FirstComponentInListException)
            {
                _specHeader.FirstRecord = record.NextRecord;
                _specHeader.FirstRecordPtr = record.NextRecordPtr;
            }
            catch
            {
                throw;
            }

            while (record != null)
            {
                TruncateComponentSpecifications(record.ComponentRecord!.SpecificationRecord);
                record = record.SpecificationNext;
            }

            UpdateSpecFile();
        }

        public void Test()
        {
            MyComponent myComponent = new("Изделие1", ComponentType.Product);
            MyComponent myComponent1 = new("Узел1", ComponentType.Node);
            MyComponent myComponent2 = new("Узел2", ComponentType.Node);
            MyComponent myComponent3 = new("Деталь1", ComponentType.Detail);
            MyComponent myComponent4 = new("Деталь2", ComponentType.Detail);
            MyComponent myComponent5 = new("Деталь3", ComponentType.Detail);
            AddComponentToComponentList(myComponent);
            AddComponentToComponentList(myComponent1);
            AddComponentToComponentList(myComponent2);
            AddComponentToComponentList(myComponent3);
            AddComponentToComponentList(myComponent4);
            AddComponentToSpecification(myComponent.ComponentName, myComponent1.ComponentName);
            AddComponentToSpecification(myComponent.ComponentName, myComponent3.ComponentName);
            AddComponentToSpecification(myComponent1.ComponentName, myComponent2.ComponentName);
            AddComponentToSpecification(myComponent1.ComponentName, myComponent4.ComponentName);
            AddComponentToComponentList(myComponent5);
            AddComponentToSpecification(myComponent2.ComponentName, myComponent5.ComponentName);

            DeleteComponentInSpecification(myComponent2.ComponentName, myComponent5.ComponentName);
            DeleteComponent(myComponent5.ComponentName);
        }

        public IEnumerable<MyComponent> GetAllComponents()
        {
            return _compHeader.GetComponents();
        }

        public ComponentsGraph GetCompWithSpecs(string component)
        {
            var myComp = _compHeader.GetCompRecByName(component);
            if (myComp == null)
                throw new ArgumentException(_compNotFoundExc);

            if (myComp.DataArea.ComponentType == ComponentType.Detail)
                throw new Exception("У детали нет спецификации!");

            return BuildGraph(myComp);
        }

        private ComponentsGraph BuildGraph(ComponentRecord record)
        {
            var res = new ComponentsGraph(record.DataArea);

            AddAllSpecsToGraph(record.SpecificationRecord, res);

            return res;
        }

        private void AddAllSpecsToGraph(SpecificationRecord? record, ComponentsGraph graph)
        {
            while (record != null)
            {
                var tmp = new ComponentsGraph(record.ComponentRecord!.DataArea);
                graph.Specifications.Add(tmp);

                if (record.ComponentRecord.SpecificationRecord != null)
                    AddAllSpecsToGraph(record.ComponentRecord.SpecificationRecord, tmp);

                record = record.SpecificationNext;
            }
        }

        public void Dispose()
        {
            _compFile?.Dispose();
            _specFile?.Dispose();
        }
    }
}