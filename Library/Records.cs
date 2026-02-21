using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace Library
{
    public abstract class Record
    {
        protected const int _recordPtrSize = 4;
        protected const int _deletionBitSize = 1;
        protected const int _nextRecordPtrSize = 4;

        public Record? NextRecord { get; set; }

        public int RecordPtr { get; protected set; }
        public bool IsDeleted { get; set; } // 0 - активно, 1 - удалено
        public int NextRecordPtr { get; set; } // Указатель на следующую запись

        public Record()
        {
            RecordPtr = GetHashCode();
            IsDeleted = false;
            NextRecordPtr = -1;
        }

        public virtual int GetTotalSize()
        {
            return _recordPtrSize + _deletionBitSize + _nextRecordPtrSize;
        }

        public virtual byte[] ToBytes()
        {
            byte[] buffer = new byte[GetTotalSize()];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(RecordPtr), 0, buffer, offset, _recordPtrSize);
            offset += _recordPtrSize;

            buffer[offset] = BitConverter.GetBytes(IsDeleted)[0];
            offset += _deletionBitSize;

            Array.Copy(BitConverter.GetBytes(NextRecordPtr), 0, buffer, offset, _nextRecordPtrSize);
            offset += _nextRecordPtrSize;

            return buffer;
        }
    }

    public abstract class Header
    {
        protected const int _firstRecordPtrSize = 4;
        protected const int _freeAreaPtrSize = 4;

        public virtual int TotalSize
        {
            get => _firstRecordPtrSize + _freeAreaPtrSize;
        }

        public Record? FirstRecord { get; set; }

        public int FirstRecordPtr { get; set; }
        public int FreeAreaPtr { get; set; }

        public Header() 
        {
            FirstRecordPtr = -1;
            FreeAreaPtr = -1;
        }

        public virtual byte[] ToBytes()
        {
            byte[] buffer = new byte[TotalSize];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(FirstRecordPtr), 0, buffer, offset, _firstRecordPtrSize);
            offset += _firstRecordPtrSize;

            Array.Copy(BitConverter.GetBytes(FreeAreaPtr), 0, buffer, offset, _freeAreaPtrSize);
            offset += _freeAreaPtrSize;

            return buffer;
        }
    }

    /// <summary>
    /// Структура заголовка файла списка изделий
    /// </summary>
    public class ComponentListHeader : Header 
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

        public ComponentListHeader(ushort dataRecordLength, string specFilename)
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
            byte[] buffer = new byte[TotalSize];
            int offset = 0;

            Array.Copy(Signature, 0, buffer, offset, _signatureSize);
            offset += _signatureSize;

            Array.Copy(BitConverter.GetBytes(DataRecordLength), 0, buffer, offset, _recordLengthSize);
            offset += _recordLengthSize;

            byte[] nameBytes = Encoding.UTF8.GetBytes(SpecFilename);
            Array.Copy(nameBytes, 0, buffer, offset, Math.Min(nameBytes.Length, _specFilenameSize));
            offset += _specFilenameSize;

            buffer = base.ToBytes().Concat(buffer).ToArray();

            if(FirstRecord != null)
                buffer = buffer.Concat(FirstRecord.ToBytes()).ToArray();

            return buffer;
        }

        //Десериализация
        public static ComponentListHeader FromBytes(byte[] buffer, int startIndex = 0)
        {
            int offset = startIndex;

            // Signature
            var signature = new byte[_signatureSize];
            Array.Copy(buffer, offset, signature, 0, _signatureSize);
            offset += _signatureSize;

            // DataRecordLength
            var dataRecordLength = BitConverter.ToUInt16(buffer, offset);
            offset += _recordLengthSize;

            // FirstRecordPtr
            var firstRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _firstRecordPtrSize;

            // FreeAreaPtr
            var freeAreaPtr = BitConverter.ToInt32(buffer, offset);
            offset += _freeAreaPtrSize;

            // SpecFileName
            var specFilename = Encoding.UTF8.GetString(buffer, offset, _specFilenameSize);
            offset += _specFilenameSize;

            return new ComponentListHeader(dataRecordLength, specFilename)
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
    public class ComponentListRecord : Record
    {
        private const int _specificationRecordPtrSize = 4;
        private const int _componentTypeSize = 2;

        public SpecificationRecord? SpecificationRecord { get; set; }

        public MyComponent DataArea { get; private set; } // Компонент

        public int SpecificationRecordPtr { get; set; } // Указатель на первую запись в спецификации

        public ComponentListRecord(MyComponent component) : base()
        {
            SpecificationRecordPtr = -1;

            if (component.ComponentName.Length > ComponentListHeader.DataRecordLength * 2 - _componentTypeSize)
                throw new Exception("Название компонента слишком длинное");
            DataArea = component;
        }

        public override int GetTotalSize()
        {
            return base.GetTotalSize() + _specificationRecordPtrSize + ComponentListHeader.DataRecordLength * 2;
        }

        /// <summary>
        /// Сериализует этот объект и все связанные с ним объекты
        /// </summary>
        public override byte[] ToBytes()
        {
            byte[] buffer = new byte[GetTotalSize()];
            int offset = 0;

            Array.Copy(BitConverter.GetBytes(SpecificationRecordPtr), 0, buffer, offset, _specificationRecordPtrSize);
            offset += _specificationRecordPtrSize;

            Array.Copy(BitConverter.GetBytes(Convert.ToInt16(DataArea.ComponentType)), 0, buffer, offset, _componentTypeSize);
            offset += _componentTypeSize;

            byte[] nameBytes = new byte[ComponentListHeader.DataRecordLength*2 - _componentTypeSize];
            byte[] strBytes = Encoding.Unicode.GetBytes(DataArea.ComponentName);
            Array.Copy(strBytes, nameBytes, strBytes.Length);
            Array.Copy(nameBytes, 0, buffer, offset, nameBytes.Length);
            offset = ComponentListHeader.DataRecordLength * 2 - _componentTypeSize;
            
            buffer = base.ToBytes().Concat(buffer).ToArray();

            if (NextRecord != null)
                buffer = buffer.Concat(NextRecord.ToBytes()).ToArray();

            return buffer;
        }

        //Десериализация
        public static ComponentListRecord FromBytes(byte[] buffer, int startIndex = 0)
        {
            int offset = startIndex;

            var recordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _recordPtrSize;

            // Deleted flag
            bool isDeleted = BitConverter.ToBoolean(buffer, offset);
            offset += _deletionBitSize;

            // FirstComponentPtr
            var specificationRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _specificationRecordPtrSize;

            // NextRecordPtr
            var nextRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _nextRecordPtrSize;

            // DataArea
            ComponentType componentType = (ComponentType)BitConverter.ToInt16(buffer, offset);
            offset += _componentTypeSize;

            string name = Encoding.Unicode.GetString(buffer, offset, ComponentListHeader.DataRecordLength * 2 - _componentTypeSize);
            offset += ComponentListHeader.DataRecordLength * 2 - _componentTypeSize;            

            MyComponent myComponent = new MyComponent(name, componentType);

            return new ComponentListRecord(myComponent)
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
    public class SpecificationHeader : Header
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
    public class SpecificationRecord : Record
    {
        private const int _componentRecordPtrSize = 4;
        private const int _quantitySize = 2;
        private const int _componentPtrSize = 4;

        public ComponentListRecord? ComponentRecord { get; set; }
        public MyComponent[] Components { get; set; }

        public int ComponentRecordPtr { get; set; } // Указатель на запись в списке изделий
        public ushort Quantity { get; set; } // Кратность вхождения
        public int[] ComponentPtrs { get; set; }

        public SpecificationRecord(ushort quantity = 2) : base()
        {
            ComponentRecordPtr = -1;
            Quantity = quantity;
            Components = new MyComponent[quantity];
            ComponentPtrs = new int[quantity];
            Array.Fill(ComponentPtrs, -1);
        }

        public override int GetTotalSize()
        {
            int totalSize = _componentPtrSize * Quantity;
            return totalSize + base.GetTotalSize() + _componentRecordPtrSize + _quantitySize;
        }

        /// <summary>
        /// Сериализует этот объект и все связанные с ним объекты
        /// </summary>
        public override byte[] ToBytes()
        {
            byte[] buffer = new byte[GetTotalSize()];
            int offset = 0;

            // ComponentPtr
            Array.Copy(BitConverter.GetBytes(ComponentRecordPtr), 0, buffer, offset, _componentRecordPtrSize);
            offset += _componentRecordPtrSize;

            // Quantity
            Array.Copy(BitConverter.GetBytes(Quantity), 0, buffer, offset, _quantitySize);
            offset += _quantitySize;

            for (int i = 0; i < ComponentPtrs.Length; i++)
            {
                if (ComponentPtrs[i] == 0)
                    ComponentPtrs[i] = -1;
                Array.Copy(BitConverter.GetBytes(ComponentPtrs[i]), 0, buffer, offset, _componentPtrSize);
                offset += _componentPtrSize;
            }

            buffer = base.ToBytes().Concat(buffer).ToArray();

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

            record.RecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _recordPtrSize;

            // Deleted flag
            record.IsDeleted = BitConverter.ToBoolean(buffer, offset);
            offset += _deletionBitSize;

            // ComponentPtr
            record.ComponentRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _componentRecordPtrSize;

            // Quantity
            record.Quantity = BitConverter.ToUInt16(buffer, offset);
            offset += _quantitySize;

            // NextRecordPtr
            record.NextRecordPtr = BitConverter.ToInt32(buffer, offset);
            offset += _nextRecordPtrSize;

            int i = 0;
            while(offset-startIndex < totalSize)
            {
                record.ComponentPtrs[i] = BitConverter.ToInt32(buffer, offset);
                offset += _componentPtrSize;
                i++;
            }

            return record;
        }

        public void AddComponent(MyComponent myComponent)
        {
            int i = 0;
            while (Components[i] != null)
            {
                if (i == Components.Length - 1)
                    throw new Exception("Достигнут лимит компонентов в спецификации!");
                i++;
            }
            Components[i] = myComponent;
            ComponentPtrs[i] = myComponent.GetHashCode();
        }
    }
}
