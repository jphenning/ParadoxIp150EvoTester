using System;
using System.Globalization;
using System.Text;

namespace ParadoxIp150EvoTester
{
    public static class Extensions
    {
        public static byte GetHighNibble(this byte value)
        {
            return (byte)((value & 0xF0) >> 4);
        }

        public static byte GetLowNibble(this byte value)
        {
            return (byte)(value & 0x0F);
        }

        public static byte[] SubArray(this byte[] data, int startIndex, int length)
        {
            var newData = new byte[length];

            Array.Copy(data, startIndex, newData, 0, length);

            return newData;
        }

        public static byte[] SubArray(this byte[] data, int startIndex)
        {
            return data.SubArray(startIndex, data.Length - startIndex);
        }

        public static byte[] Append(this byte[] data1, byte[] data2)
        {
            byte[] data = new byte[data1.Length + data2.Length];
            Array.Copy(data1, 0, data, 0, data1.Length);
            Array.Copy(data2, 0, data, data1.Length, data2.Length);
            return data;
        }

        public static bool IsBitSet<T>(this T value, byte position) where T : struct, IConvertible
        {
            var intValue = value.ToInt64(CultureInfo.InvariantCulture);
            return (intValue & (1 << position)) != 0;
        }

        public static byte SetBit(this byte value, byte position, bool isBitSet)
        {
            if (isBitSet)
            {
                //left-shift 1, then bitwise OR
                return (byte)(value | (1 << position));
            }

            //left-shift 1, then take complement, then bitwise AND
            return (byte)(value & ~(1 << position));
        }

        public static byte[] ToBytes(this string value, int bytesRequired, byte fillValue)
        {
            if (string.IsNullOrEmpty(value))
                value = string.Empty;
            else if (value.Length > bytesRequired)
                value = value.Substring(0, bytesRequired);

            var asciiBytes = Encoding.ASCII.GetBytes(value);

            var bytes = new byte[bytesRequired];

            Array.Copy(asciiBytes, 0, bytes, 0, asciiBytes.Length);

            var emptyBytes = bytesRequired - asciiBytes.Length;

            if (emptyBytes > 0)
            {
                bytes = bytes.FillRight(bytesRequired - emptyBytes, fillValue);
            }

            return bytes;
        }

        public static byte[] FillRight(this byte[] data, int startIndex, byte padValue)
        {
            if (data == null)
                return null;

            if (startIndex >= data.Length)
                return data;

            var newData = new byte[data.Length];
            Array.Copy(data, 0, newData, 0, data.Length);
            for (var i = startIndex; i < newData.Length; i++)
            {
                newData[i] = padValue;
            }

            return newData;
        }

        public static byte[] PadRight(this byte[] data, int bytesRequired, byte padValue)
        {
            var bytes = new byte[Math.Max(bytesRequired, data.Length)];

            Array.Copy(data, 0, bytes, 0, data.Length);

            var padLength = bytesRequired - data.Length;

            for (var i = bytesRequired - padLength; i < bytesRequired; i++)
            {
                bytes[i] = padValue;
            }

            return bytes;
        }

        public static string GetString(this byte[] data, int index, int length)
        {
            return Encoding.ASCII.GetString(data, index, length).TrimEnd(' ', '\0');
        }

        public static string ToHexString(this byte[] data)
        {
            return data.ToHexString(0, data.Length);
        }

        public static string ToHexString(this byte[] data, int index, int length)
        {
            var sb = new StringBuilder();

            for (int i = index; i < index + length; i++)
            {
                if (sb.Length > 0)
                    sb.Append(" ");

                sb.Append(data[i].ToString("X2"));
            }

            return sb.ToString();
        }

        public static byte GetBits07To00(this uint value)
        {
            return (byte)(value & 0xFF);
        }

        public static byte GetBits15To08(this uint value)
        {
            return (byte)((value & 0xFF00) >> 8);
        }

        public static byte GetBits17To16(this uint value)
        {
            return (byte)((value & 0x30000) >> 16);
        }
    }
}
