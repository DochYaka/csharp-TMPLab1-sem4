using Library.Records;
using System.Text;

namespace Library.Headers
{
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
}