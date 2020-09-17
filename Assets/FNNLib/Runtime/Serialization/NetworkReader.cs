using System;
using System.IO;
using FNNLib.Reflection;
using UnityEditor;
using UnityEngine;

namespace FNNLib.Serialization {
    /// <summary>
    /// Network reader will read data from a buffer into any type.
    /// Used for deserializing packets.
    /// </summary>
    public class NetworkReader {
        /// <summary>
        /// The buffer we are reading from.
        /// </summary>
        internal ArraySegment<byte> buffer;

        /// <summary>
        /// Reader position
        /// </summary>
        public int position;

        /// <summary>
        /// The length of the data buffer.
        /// </summary>
        public int length => buffer.Count;

        public NetworkReader(byte[] buffer) {
            this.buffer = new ArraySegment<byte>(buffer);
        }

        public NetworkReader(ArraySegment<byte> buffer) {
            this.buffer = buffer;
        }

        #region Primitive Reads

        /// <summary>
        /// Read a single byte from the stream.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="EndOfStreamException"></exception>
        public byte ReadByte() {
            if (position + 1 > buffer.Count)
                throw new EndOfStreamException("ReadByte out of range!");
            return buffer.Array[buffer.Offset + position++];
        }

        public bool ReadBool() {
            if (position + 1 > buffer.Count)
                throw new EndOfStreamException("ReadBool out of range!");
            return buffer.Array[buffer.Offset + position++] == 1;
        }

        public byte[] ReadBytes(int count) {
            var data = ReadBytesSegment(count);
            var returnBuffer = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, returnBuffer, 0, data.Count);
            return returnBuffer;
        }

        public ArraySegment<byte> ReadBytesSegment(int count) {
            if (position + count > buffer.Count) {
                throw new EndOfStreamException("ReadBytesSegment can't read " + count +
                                               " bytes because there is not enough data in the stream!");
            }

            var result = new ArraySegment<byte>(buffer.Array, buffer.Offset + position, count);
            position += count;
            return result;
        }

        public ushort ReadUInt16() {
            if (position + 2 > buffer.Count)
                throw new EndOfStreamException("ReadUInt16 out of range!");
            ushort value = 0;
            value |= buffer.Array[buffer.Offset + position++];
            value |= (ushort) (buffer.Array[buffer.Offset + position++] << 8);
            return value;
        }

        public short ReadInt16() => (short) ReadUInt16();

        public uint ReadUInt32() {
            if (position + 4 > buffer.Count)
                throw new EndOfStreamException("ReadUInt32 out of range!");
            uint value = 0;
            value |= buffer.Array[buffer.Offset + position++];
            value |= ((uint) buffer.Array[buffer.Offset + position++] << 8);
            value |= ((uint) buffer.Array[buffer.Offset + position++] << 16);
            value |= ((uint) buffer.Array[buffer.Offset + position++] << 24);
            return value;
        }

        public int ReadInt32() => (int) ReadUInt32();

        public ulong ReadUInt64() {
            if (position + 8 > buffer.Count)
                throw new EndOfStreamException("ReadUInt32 out of range!");
            ulong value = 0;
            value |= buffer.Array[buffer.Offset + position++];
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 8);
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 16);
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 24);
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 32);
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 40);
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 48);
            value |= ((ulong) buffer.Array[buffer.Offset + position++] << 56);
            return value;
        }

        public long ReadInt64() => (long) ReadUInt64();

        public ushort ReadPackedUInt16() => (ushort) ReadPackedUInt64();

        public short ReadPackedInt16() {
            // https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
            var value = ReadPackedUInt16();
            return (short) ((value >> 1) ^ -(value & 1));
        }

        public uint ReadPackedUInt32() => (uint) ReadPackedUInt64();

        public int ReadPackedInt32() {
            // https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
            var value = ReadPackedUInt32();
            return (int) ((value >> 1) ^ -(value & 1));
        }

        public ulong ReadPackedUInt64() {
            // https://sqlite.org/src4/doc/trunk/www/varint.wiki
            var a0 = ReadByte();
            if (a0 < 241) {
                return a0;
            }

            var a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248) {
                return 240 + ((a0 - (ulong) 241) << 8) + a1;
            }

            var a2 = ReadByte();
            if (a0 == 249) {
                return 2288 + ((ulong) a1 << 8) + a2;
            }

            var a3 = ReadByte();
            if (a0 == 250) {
                return a1 + (((ulong) a2) << 8) + (((ulong) a3) << 16);
            }

            var a4 = ReadByte();
            if (a0 == 251) {
                return a1 + (((ulong) a2) << 8) + (((ulong) a3) << 16) + (((ulong) a4) << 24);
            }

            var a5 = ReadByte();
            if (a0 == 252) {
                return a1 + (((ulong) a2) << 8) + (((ulong) a3) << 16) + (((ulong) a4) << 24) + (((ulong) a5) << 32);
            }

            var a6 = ReadByte();
            if (a0 == 253) {
                return a1 + (((ulong) a2) << 8) + (((ulong) a3) << 16) + (((ulong) a4) << 24) + (((ulong) a5) << 32) +
                       (((ulong) a6) << 40);
            }

            var a7 = ReadByte();
            if (a0 == 254) {
                return a1 + (((ulong) a2) << 8) + (((ulong) a3) << 16) + (((ulong) a4) << 24) + (((ulong) a5) << 32) +
                       (((ulong) a6) << 40) + (((ulong) a7) << 48);
            }

            var a8 = ReadByte();
            if (a0 == 255) {
                return a1 + (((ulong) a2) << 8) + (((ulong) a3) << 16) + (((ulong) a4) << 24) + (((ulong) a5) << 32) +
                       (((ulong) a6) << 40) + (((ulong) a7) << 48) + (((ulong) a8) << 56);
            }

            throw new IndexOutOfRangeException("Invalid packed int! A0 = " + a0);
        }

        public long ReadPackedInt64() {
            // https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
            var value = ReadPackedUInt64();
            return ((long) (value >> 1) ^ -((long) value & 1));
        }

        #endregion

        #region Reads

        public float ReadSingle() {
            var conversion = new UIntFloat {intValue = ReadUInt32()};
            return conversion.floatValue;
        }

        public double ReadDouble() {
            var conversion = new UIntDouble {longValue = ReadUInt64()};
            return conversion.doubleValue;
        }

        public decimal ReadDecimal() {
            var conversion = new UIntDecimal {longValue1 = ReadUInt64()};
            conversion.longValue2 = ReadUInt64();
            return conversion.decimalValue;
        }

        public string ReadString() {
            // String size
            ushort size = ReadPackedUInt16();
            if (size == 0) return null;

            // Get real size
            var realSize = size - 1;

            // Check max size
            if (realSize >= NetworkWriter.MaxStringLength)
                throw new EndOfStreamException("String sent was too long (" + realSize + "). Maximum is " +
                                               NetworkWriter.MaxStringLength);

            // Get byte data
            var data = ReadBytesSegment(realSize);
            return NetworkWriter.Encoding.GetString(data.Array, data.Offset, data.Count);
        }

        public byte[] ReadBytesWithSize() {
            var segment = ReadSegmentWithSize();
            if (segment == null)
                return null;
            var data = new byte[segment.Value.Count];
            Array.Copy(segment.Value.Array, segment.Value.Offset, data, 0, segment.Value.Count);
            return data;
        }

        public ArraySegment<byte>? ReadSegmentWithSize() {
            var count = ReadPackedUInt32();
            if (count == 0)
                return null;
            var realCount = checked((int) (count - 1u));
            if (position + realCount > buffer.Count)
                throw new EndOfStreamException("ReadSegmentWithSize can't read " + realCount +
                                               " bytes because there is not enough data in the stream!");

            var segment = new ArraySegment<byte>(buffer.Array, buffer.Offset + position, realCount);
            position += realCount;
            return segment;
        }

        public Vector2 ReadVector2() => new Vector2(ReadSingle(), ReadSingle());

        public Vector3 ReadVector3() => new Vector3(ReadSingle(), ReadSingle(), ReadSingle());

        public object ReadPackedObject(Type type) {
            if (type.IsNullable()) {
                var isNull = ReadBool();
                if (isNull)
                    return null;
            }

            if (type.IsArray && type.HasElementType && SerializationSystem.CanSerialize(type.GetElementType())) {
                var size = ReadPackedInt32();
                var array = Array.CreateInstance(type.GetElementType(), size);
                for (var i = 0; i < size; i++)
                    array.SetValue(ReadPackedObject(type.GetElementType()), i);
                return array;
            }
            if (type == typeof(byte))
                return ReadByte();
            if (type == typeof(ushort))
                return ReadPackedUInt16();
            if (type == typeof(short))
                return ReadPackedInt16();
            if (type == typeof(uint))
                return ReadPackedUInt32();
            if (type == typeof(int))
                return ReadPackedInt32();
            if (type == typeof(ulong))
                return ReadPackedUInt64();
            if (type == typeof(long))
                return ReadPackedInt64();
            if (type == typeof(float))
                return ReadSingle();
            if (type == typeof(double))
                return ReadDouble();
            if (type == typeof(decimal))
                return ReadDecimal();
            if (type == typeof(string))
                return ReadString();
            if (type == typeof(bool))
                return ReadBool();
            if (type == typeof(Vector2))
                return ReadVector2();
            if (type == typeof(Vector3))
                return ReadVector3();
            if (typeof(ISerializable).IsAssignableFrom(type)) {
                var instance = Activator.CreateInstance(type);
                ((ISerializable) instance).DeSerialize(this);
                return instance;
            }

            throw new InvalidOperationException("This type cannot be deserialized!");
        }

        #endregion
    }
}