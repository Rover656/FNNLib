using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace FNNLib.Serialization {
    // Float conversion helpers
    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntFloat {
        [FieldOffset(0)] public float floatValue;
        [FieldOffset(0)] public uint intValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntDouble {
        [FieldOffset(0)] public double doubleValue;
        [FieldOffset(0)] public ulong longValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct UIntDecimal {
        [FieldOffset(0)] public ulong longValue1;
        [FieldOffset(8)] public ulong longValue2;
        [FieldOffset(0)] public decimal decimalValue;
    }

    public class NetworkWriter {
        /// <summary>
        /// Maximum length of sent strings.
        /// </summary>
        public const int MaxStringLength = 1024 * 32;

        /// <summary>
        /// Writer has its own buffer.
        /// 1500 because normally packets will be smaller than MTU
        /// https://en.wikipedia.org/wiki/Maximum_transmission_unit
        /// </summary>
        private byte[] buffer = new byte[1500];

        /// <summary>
        /// The position of the writer.
        /// </summary>
        private int _position;

        /// <summary>
        /// The length of the data we have written.
        /// This is because the buffer will generally be larger than the data we wrote.
        /// </summary>
        private int _length;

        /// <summary>
        /// Get the length of the internal data.
        /// </summary>
        public int length => _length;

        /// <summary>
        /// String buffer for writing strings.
        /// </summary>
        private readonly byte[] _stringBuffer = new byte[MaxStringLength];

        /// <summary>
        /// Cached encoding for string writing
        /// </summary>
        internal static readonly UTF8Encoding Encoding = new UTF8Encoding(false, true);

        /// <summary>
        /// The position of the writer.
        /// </summary>
        public int Position {
            get => _position;
            set {
                _position = value;
                EnsureLength(value);
            }
        }

        /// <summary>
        /// Reset the writer for another use.
        /// </summary>
        public void Reset() {
            _position = 0;
            _length = 0;
        }

        private void EnsureLength(int requiredLength) {
            if (_length < requiredLength) {
                _length = requiredLength;
                EnsureCapacity(requiredLength);
            }
        }

        private void EnsureCapacity(int requiredCapacity) {
            if (buffer.Length < requiredCapacity) {
                // At least double the size of the buffer. In a pool of writers, this will only be done so many times :)
                Array.Resize(ref buffer, Math.Max(requiredCapacity, buffer.Length * 2));
            }
        }

        #region Get written data

        public byte[] ToArray() {
            var data = new byte[_length];

            // Doesn't write data if something goes wrong.
            Array.ConstrainedCopy(buffer, 0, data, 0, _length);
            return data;
        }

        public ArraySegment<byte> ToArraySegment() {
            return new ArraySegment<byte>(buffer, 0, _length);
        }

        #endregion

        #region Primitive Writes

        public void WriteByte(byte value) {
            EnsureLength(_position + 1);
            buffer[_position++] = value;
        }

        public void WriteBool(bool value) {
            EnsureLength(_position + 1);
            buffer[_position++] = (byte) (value ? 1 : 0);
        }

        public void WriteBytes(byte[] values, int offset, int count) {
            EnsureLength(_position + count);
            Array.ConstrainedCopy(values, offset, buffer, _position, count);
            _position += count;
        }

        public void WriteUInt16(ushort value) {
            EnsureLength(_position + 2);
            buffer[_position++] = (byte) value;
            buffer[_position++] = (byte) (value >> 8);
        }

        public void WriteInt16(short value) => WriteUInt16((ushort) value);

        public void WriteUInt32(uint value) {
            EnsureLength(_position + 4);
            buffer[_position++] = (byte) value;
            buffer[_position++] = (byte) (value >> 8);
            buffer[_position++] = (byte) (value >> 16);
            buffer[_position++] = (byte) (value >> 24);
        }

        public void WriteInt32(int value) => WriteUInt32((uint) value);

        public void WriteUInt64(ulong value) {
            EnsureLength(_position + 8);
            buffer[_position++] = (byte) value;
            buffer[_position++] = (byte) (value >> 8);
            buffer[_position++] = (byte) (value >> 16);
            buffer[_position++] = (byte) (value >> 24);
            buffer[_position++] = (byte) (value >> 32);
            buffer[_position++] = (byte) (value >> 40);
            buffer[_position++] = (byte) (value >> 48);
            buffer[_position++] = (byte) (value >> 56);
        }

        public void WriteInt64(int value) => WriteUInt64((ulong) value);
        
        public void WritePackedUInt16(ushort value) => WritePackedUInt64(value);

        public void WritePackedInt16(short value) => WritePackedInt64(value);

        public void WritePackedUInt32(uint value) => WritePackedUInt64(value);

        public void WritePackedInt32(int value) => WritePackedInt64(value);
        
        public void WritePackedUInt64(ulong value) {
            // https://sqlite.org/src4/doc/trunk/www/varint.wiki
            if (value <= 240) {
                WriteByte((byte) value);
            } else if (value <= 2287) {
                WriteByte((byte) (((value - 240) >> 8) + 241));
                WriteByte((byte) (value - 240));
            } else if (value <= 67823) {
                WriteByte(249);
                WriteByte((byte) ((value - 2288) >> 8));
                WriteByte((byte) (value - 2288));
            } else if (value <= 16777215) {
                WriteByte(250);
                WriteByte((byte) value);
                WriteByte((byte) (value >> 8));
                WriteByte((byte) (value >> 16));
            } else if (value <= 4294967295) {
                WriteByte(251);
                WriteByte((byte) value);
                WriteByte((byte) (value >> 8));
                WriteByte((byte) (value >> 16));
                WriteByte((byte) (value >> 24));
            } else if (value <= 1099511627775) {
                WriteByte(252);
                WriteByte((byte) value);
                WriteByte((byte) (value >> 8));
                WriteByte((byte) (value >> 16));
                WriteByte((byte) (value >> 24));
                WriteByte((byte) (value >> 32));
            } else if (value <= 281474976710655) {
                WriteByte(253);
                WriteByte((byte) value);
                WriteByte((byte) (value >> 8));
                WriteByte((byte) (value >> 16));
                WriteByte((byte) (value >> 24));
                WriteByte((byte) (value >> 32));
                WriteByte((byte) (value >> 40));
            } else if (value <= 72057594037927935) {
                WriteByte(254);
                WriteByte((byte) value);
                WriteByte((byte) (value >> 8));
                WriteByte((byte) (value >> 16));
                WriteByte((byte) (value >> 24));
                WriteByte((byte) (value >> 32));
                WriteByte((byte) (value >> 40));
                WriteByte((byte) (value >> 48));
            } else {
                WriteByte(255);
                WriteByte((byte) value);
                WriteByte((byte) (value >> 8));
                WriteByte((byte) (value >> 16));
                WriteByte((byte) (value >> 24));
                WriteByte((byte) (value >> 32));
                WriteByte((byte) (value >> 40));
                WriteByte((byte) (value >> 48));
                WriteByte((byte) (value >> 56));
            }
        }
        
        public void WritePackedInt64(long value) {
            // https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
            var zigzagged = (ulong) ((value >> 63) ^ (value << 1));
            WritePackedUInt64(zigzagged);
        }

        #endregion

        #region Writes

        public void WriteSingle(float value) {
            var conversion = new UIntFloat {floatValue = value};
            WriteUInt32(conversion.intValue);
        }

        public void WriteDouble(double value) {
            var conversion = new UIntDouble {doubleValue = value};
            WriteUInt64(conversion.longValue);
        }

        public void WriteDecimal(decimal value) {
            var conversion = new UIntDecimal {decimalValue = value};
            WriteUInt64(conversion.longValue1);
            WriteUInt64(conversion.longValue2);
        }

        public void WriteString(string value) {
            // Null strings
            if (value == null) {
                WriteUInt16(0);
            }

            // Write string to buffer.
            int size = Encoding.GetBytes(value, 0, value.Length, _stringBuffer, 0);

            // Check string length
            if (size >= MaxStringLength) {
                throw new
                    ArgumentOutOfRangeException("NetworkWriter.WriteString(string): String too long! String size limit: " +
                                                MaxStringLength);
            }

            // Write size and bytes
            WriteUInt16(checked((ushort) (size + 1)));
            WriteBytes(_stringBuffer, 0, size);
        }

        public void WriteBytesWithSize(byte[] values, int offset, int count) {
            if (values == null) {
                WriteUInt32(0u);
                return;
            }

            WriteSegmentWithSize(new ArraySegment<byte>(values, offset, count));
        }

        public void WriteSegmentWithSize(ArraySegment<byte> segment) {
            if (segment.Array == null) {
                WriteUInt32(0u);
                return;
            }

            WriteUInt32(checked((uint) segment.Count) + 1u);
            WriteBytes(segment.Array, segment.Offset, segment.Count);
        }

        public void WriteVector2(Vector2 value) {
            WriteSingle(value.x);
            WriteSingle(value.y);
        }

        public void WriteVector3(Vector3 value) {
            WriteSingle(value.x);
            WriteSingle(value.y);
            WriteSingle(value.z);
        }

        #endregion
    }
}