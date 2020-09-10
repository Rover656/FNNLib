using System;
using System.IO;
using System.Numerics;
using UnityEditor;
using UnityEngine.Experimental.AI;

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

        public byte[] ReadBytes(int count) {
            var data = ReadBytesSegment(count);
            var returnBuffer = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, returnBuffer, 0, data.Count);
            return returnBuffer;
        }

        public ArraySegment<byte> ReadBytesSegment(int count) {
            if (position + count > buffer.Count) {
                throw new EndOfStreamException("ReadBytesSegment can't read " + count + " bytes because there is not enough data in the stream!");
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
            ushort size = ReadUInt16();
            if (size == 0) return null;
            
            // Get real size
            var realSize = size - 1;
            
            // Check max size
            if (realSize >= NetworkWriter.MaxStringLength)
                throw new EndOfStreamException("String sent was too long (" + realSize + "). Maximum is " + NetworkWriter.MaxStringLength);
            
            // Get byte data
            var data = ReadBytesSegment(realSize);
            return NetworkWriter.Encoding.GetString(data.Array, data.Offset, data.Count);
        }

        public byte[] ReadBytesWithSize() {
            var segment = ReadSegmentWithSize();
            if (!segment.HasValue)
                return null;
            var data = new byte[segment.Value.Count];
            Array.Copy(segment.Value.Array, segment.Value.Offset, data, 0, segment.Value.Count);
            return data;
        }

        public ArraySegment<byte>? ReadSegmentWithSize() {
            uint count = ReadUInt32();
            if (count == 0)
                return null;
            var realCount = checked((int) (count - 1u));
            if (position + realCount > buffer.Count)
                throw new EndOfStreamException("ReadSegmentWithSize can't read " + realCount + " bytes because there is not enough data in the stream!");

            var segment = new ArraySegment<byte>(buffer.Array, buffer.Offset + position, realCount);
            position += realCount;
            return segment;
        }

        public Vector2 ReadVector2() => new Vector2(ReadSingle(), ReadSingle());

        public Vector3 ReadVector3() => new Vector3(ReadSingle(), ReadSingle(), ReadSingle());

        #endregion
    }
}