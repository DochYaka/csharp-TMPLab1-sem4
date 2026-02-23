using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace Library
{
    public abstract class Record
    {
        protected const int _deletionBitSize = 1;
        protected const int _nextRecordPtrSize = 4;

        public int NextRecordPtr { get; set; } // Указатель на следующую запись
        public bool IsDeleted { get; set; } // 0 - активно, 1 - удалено

        public Record()
        {
            NextRecordPtr = -1;
            IsDeleted = false;
        }

        public virtual int GetTotalSize()
        {
            return _deletionBitSize + _nextRecordPtrSize;
        }

        public virtual byte[] ToBytes()
        {
            byte[] buffer = new byte[_deletionBitSize + _nextRecordPtrSize];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(NextRecordPtr), 0, buffer, offset, _nextRecordPtrSize);
            offset += _nextRecordPtrSize;

            buffer[offset] = BitConverter.GetBytes(IsDeleted)[0];
            offset += _deletionBitSize;

            return buffer;
        }
    }

    public abstract class Record<T> : Record where T : Record
    {
        public T? NextRecord { get; set; }
    }

    public abstract class Header
    {
        protected const int _freeAreaPtrSize = 4;
        protected const int _firstRecordPtrSize = 4;

        public virtual int TotalSize
        {
            get => _freeAreaPtrSize + _firstRecordPtrSize;
        }

        public int FirstRecordPtr { get; set; }
        public int FreeAreaPtr { get; set; }

        public Header()
        {
            FreeAreaPtr = -1;
            FirstRecordPtr = -1;
        }

        public virtual byte[] ToBytes()
        {
            byte[] buffer = new byte[_freeAreaPtrSize + _firstRecordPtrSize];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(FirstRecordPtr), 0, buffer, offset, _firstRecordPtrSize);
            offset += _firstRecordPtrSize;

            Array.Copy(BitConverter.GetBytes(FreeAreaPtr), 0, buffer, offset, _freeAreaPtrSize);
            offset += _freeAreaPtrSize;

            return buffer;
        }
    }

    public abstract class Header<T> : Header where T : Record
    {
        public T? FirstRecord { get; set; }
    }

    /// <summary>
    /// Структура заголовка файла списка изделий
    /// </summary>
    public class ComponentHeader : Header<ComponentRecord>
    {
        private const int _signatureSize = 2;
        private const int _recordLengthSize = 2;
        private const int _specFilenameSize = 16;

        public override int TotalSize
        {
            get => base.TotalSize + _signatureSize + _recordLengthSize + _specFilenameSize;
        }

        public byte[] Signature { get; set; } // "PS"
        public static ushort DataRecordLength { get; private set; }
        public char[] SpecFilename { get; set; }

        public ComponentHeader(ushort dataRecordLength, string specFilename)
        {
            Signature = Encoding.ASCII.GetBytes("PS");
            DataRecordLength = dataRecordLength;
            SpecFilename = new char[_specFilenameSize];
            Array.Copy(specFilename.ToCharArray(), SpecFilename, specFilename.Length);
        }

        /// <summary>
        /// Сериализует этот объект и все связанные с ним объекты
        /// </summary>
        public override byte[] ToBytes()
        {
            byte[] buffer = new byte[_signatureSize + _recordLengthSize + _specFilenameSize];
            int offset = 0;

            Array.Copy(Signature, 0, buffer, offset, _signatureSize);
            offset += _signatureSize;

            Array.Copy(BitConverter.GetBytes(DataRecordLength), 0, buffer, offset, _recordLengthSize);
            offset += _recordLengthSize;

            byte[] nameBytes = Encoding.UTF8.GetBytes(SpecFilename);
            Array.Copy(nameBytes, 0, buffer, offset, Math.Min(nameBytes.Length, _specFilenameSize));
            offset += _specFilenameSize;

            buffer = base.ToBytes().Concat(buffer).ToArray();

            if (FirstRecord != null)
                buffer = buffer.Concat(FirstRecord.ToBytes()).ToArray();

            return buffer;
        }

        //Десериализация
        public static ComponentHeader FromBytes(byte[] buffer, int startIndex = 0)
        {
            int offset = startIndex;

            // FirstRecordPtr
            var firstRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _firstRecordPtrSize;

            // FreeAreaPtr
            var freeAreaPtr = BitConverter.ToInt32(buffer, offset);
            offset += _freeAreaPtrSize;

            // Signature
            var signature = new byte[_signatureSize];
            Array.Copy(buffer, offset, signature, 0, _signatureSize);
            offset += _signatureSize;

            // DataRecordLength
            var dataRecordLength = BitConverter.ToUInt16(buffer, offset);
            offset += _recordLengthSize;

            // SpecFileName
            var specFilename = Encoding.UTF8.GetString(buffer, offset, _specFilenameSize);
            offset += _specFilenameSize;

            return new ComponentHeader(dataRecordLength, specFilename)
            {
                Signature = signature,
                FirstRecordPtr = firstRecordPtr,
                FreeAreaPtr = freeAreaPtr
            };
        }
    }

    /// <summary>
    /// Запись файла списка изделий
    /// </summary>
    public class ComponentRecord : Record<ComponentRecord>
    {
        private const int _specificationRecordPtrSize = 4;
        private const int _componentTypeSize = 2;

        public SpecificationRecord? SpecificationRecord { get; set; }

        public MyComponent DataArea { get; private set; } // Компонент

        public int SpecificationRecordPtr { get; set; } // Указатель на первую запись в спецификации

        public ComponentRecord(MyComponent component) : base()
        {
            SpecificationRecordPtr = -1;

            if (component.ComponentName.Length > ComponentHeader.DataRecordLength * 2)
                throw new Exception("Название компонента слишком длинное");
            DataArea = component;
        }

        public override int GetTotalSize()
        {
            return base.GetTotalSize() + _specificationRecordPtrSize + ComponentHeader.DataRecordLength * 2 + _componentTypeSize;
        }

        /// <summary>
        /// Сериализует этот объект и все связанные с ним объекты
        /// </summary>
        public override byte[] ToBytes()
        {
            byte[] buffer = new byte[_specificationRecordPtrSize + _componentTypeSize + ComponentHeader.DataRecordLength * 2];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(SpecificationRecordPtr), 0, buffer, offset, _specificationRecordPtrSize);
            offset += _specificationRecordPtrSize;

            Array.Copy(BitConverter.GetBytes(Convert.ToInt16(DataArea.ComponentType)), 0, buffer, offset, _componentTypeSize);
            offset += _componentTypeSize;

            byte[] nameBytes = new byte[ComponentHeader.DataRecordLength * 2];
            byte[] strBytes = Encoding.Unicode.GetBytes(DataArea.ComponentName);
            Array.Copy(strBytes, nameBytes, strBytes.Length);
            Array.Copy(nameBytes, 0, buffer, offset, nameBytes.Length);
            offset = ComponentHeader.DataRecordLength * 2;

            buffer = base.ToBytes().Concat(buffer).ToArray();

            if (NextRecord != null)
                buffer = buffer.Concat(NextRecord.ToBytes()).ToArray();

            return buffer;
        }

        //Десериализация
        public static ComponentRecord FromBytes(byte[] buffer, int startIndex = 0)
        {
            int offset = startIndex;

            // NextRecordPtr
            var nextRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _nextRecordPtrSize;

            // Deleted flag
            bool isDeleted = BitConverter.ToBoolean(buffer, offset);
            offset += _deletionBitSize;

            // SpecificationRecordPtr
            var specificationRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _specificationRecordPtrSize;

            // DataArea
            ComponentType componentType = (ComponentType)BitConverter.ToInt16(buffer, offset);
            offset += _componentTypeSize;

            string name = Encoding.Unicode.GetString(buffer, offset, ComponentHeader.DataRecordLength * 2).Trim('\0');
            offset += ComponentHeader.DataRecordLength * 2;

            MyComponent myComponent = new MyComponent(name, componentType);

            return new ComponentRecord(myComponent)
            {
                IsDeleted = isDeleted,
                SpecificationRecordPtr = specificationRecordPtr,
                NextRecordPtr = nextRecordPtr
            };
        }
    }

    /// <summary>
    /// Структура заголовка файла спецификаций
    /// </summary>
    public class SpecificationHeader : Header<SpecificationRecord>
    {
        /// <summary>
        /// Сериализует этот объект и все связанные с ним объекты
        /// </summary>
        public override byte[] ToBytes()
        {
            byte[] buffer = base.ToBytes();

            if (FirstRecord != null)
                buffer = buffer.Concat(FirstRecord.ToBytes()).ToArray();

            return buffer;
        }

        public static SpecificationHeader FromBytes(byte[] buffer, int startIndex = 0)
        {
            int offset = startIndex;
            var header = new SpecificationHeader();

            header.FirstRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _firstRecordPtrSize;

            header.FreeAreaPtr = BitConverter.ToInt32(buffer, offset);
            offset += _freeAreaPtrSize;

            return header;
        }
    }

    /// <summary>
    /// Запись файла спецификаций
    /// </summary>
    public class SpecificationRecord : Record<SpecificationRecord>
    {
        private const int _componentRecordPtrSize = 4;
        private const int _quantitySize = 2;
        private const int _specificationNextPtrSize = 4;

        public ComponentRecord? ComponentRecord { get; set; }
        public SpecificationRecord? SpecificationNext { get; set; }

        public int ComponentRecordPtr { get; set; } // Указатель на запись в списке изделий
        public ushort Quantity { get; set; } // Кратность вхождения
        public int SpecificationNextPtr { get; set; }


        public SpecificationRecord(ushort quantity = 2) : base()
        {
            ComponentRecordPtr = -1;
            Quantity = quantity;
            SpecificationNextPtr = -1;
        }

        public override int GetTotalSize()
        {
            return base.GetTotalSize() + _componentRecordPtrSize + _quantitySize + _specificationNextPtrSize;
        }

        /// <summary>
        /// Сериализует этот объект и все связанные с ним объекты
        /// </summary>
        public override byte[] ToBytes()
        {
            byte[] buffer = new byte[_componentRecordPtrSize + _quantitySize + _specificationNextPtrSize];
            int offset = 0;

            // ComponentPtr
            Array.Copy(BitConverter.GetBytes(ComponentRecordPtr), 0, buffer, offset, _componentRecordPtrSize);
            offset += _componentRecordPtrSize;

            // Quantity
            Array.Copy(BitConverter.GetBytes(Quantity), 0, buffer, offset, _quantitySize);
            offset += _quantitySize;

            Array.Copy(BitConverter.GetBytes(SpecificationNextPtr), 0, buffer, offset, _specificationNextPtrSize);
            offset += _specificationNextPtrSize;

            buffer = base.ToBytes().Concat(buffer).ToArray();

            if (SpecificationNext != null)
                buffer = buffer.Concat(SpecificationNext.ToBytes()).ToArray();

            if (NextRecord != null)
                buffer = buffer.Concat(NextRecord.ToBytes()).ToArray();

            return buffer;
        }
        //Десериализация
        public static SpecificationRecord FromBytes(byte[] buffer, int startIndex = 0)
        {
            var record = new SpecificationRecord();
            var totalSize = record.GetTotalSize();
            int offset = startIndex;

            // NextRecordPtr
            record.NextRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _nextRecordPtrSize;

            // Deleted flag
            record.IsDeleted = BitConverter.ToBoolean(buffer, offset);
            offset += _deletionBitSize;

            // ComponentPtr
            record.ComponentRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _componentRecordPtrSize;

            // Quantity
            record.Quantity = BitConverter.ToUInt16(buffer, offset);
            offset += _quantitySize;

            record.SpecificationNextPtr = BitConverter.ToInt32(buffer, offset);
            offset += _specificationNextPtrSize;

            return record;
        }
    }
}