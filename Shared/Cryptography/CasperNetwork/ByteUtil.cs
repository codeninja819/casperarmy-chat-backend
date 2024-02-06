using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace CasperArmy_Chat.Shared.Cryptography.CasperNetwork
{
    /// <summary>
    /// This util includes helper methods in binary format 
    /// </summary>
    public class ByteUtil
    {
        /// <summary>
        /// Combine byte arrays
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static byte[] CombineBytes(byte[] first, byte[] second)
        {
            byte[] bytes = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
            Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
            return bytes;
        }
        /// <summary>
        /// String to Byte Array
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] StringToByteArray(string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }
        /// <summary>
        /// Byte Array to Hex
        /// </summary>
        /// <param name="byteArray"></param>
        /// <returns></returns>
        public static string ByteArrayToHex(byte[] byteArray)
        {
            return BitConverter.ToString(byteArray).Replace("-", "").ToLower();
        }

        /// <summary>
        /// Hex to Byte Array
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] HexToByteArray(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "The binary key cannot have an odd number of digits: {0}", hexString));
            }

            byte[] data = new byte[hexString.Length / 2];
            for (int index = 0; index < data.Length; index++)
            {
                string byteValue = hexString.Substring(index * 2, 2);
                data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return data;
        }

        /// <summary>
        /// Get last N Bytes
        /// </summary>
        /// <param name="toTruncate"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static byte[] GetLastNBytes(byte[] toTruncate, int length)
        {
            byte[] secretBytes = new byte[length];
            var start = toTruncate.Length - length;
            Array.Copy(toTruncate, start, secretBytes, 0, length);
            return secretBytes;
        }
        /// <summary>
        /// Remove leading zeros
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static byte[] RemoveLeadingZeros(byte[] bytes)
        {
            Array.Reverse(bytes);
            for (int i = bytes.Length; i > 0; i--)
            {
                if (bytes[i] == 0)
                {
                    Array.Resize(ref bytes, i);
                }
            }
            Array.Reverse(bytes);
            return bytes;
        }
        /// <summary>
        /// Remove trailing zeros
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static byte[] RemoveTrailingZeros(byte[] bytes)
        {
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] == 0)
                {
                    Array.Resize(ref bytes, i);
                }
                else
                {
                    break;
                }
            }
            return bytes;
        }
        /// <summary>
        /// Prefix Option in bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static byte[] PrefixOption(byte[] bytes)
        {

            byte[] optionPrefix = new byte[1];
            if (bytes == null || bytes.
                Length == 0)
            {
                optionPrefix[0] = (byte)PrefixOptionEnum.OptionNone;
            }
            else
            {
                optionPrefix[0] = (byte)PrefixOptionEnum.OptionSome;
            }
            return CombineBytes(optionPrefix, bytes);
        }
        /// <summary>
        /// Serialize to Byte Array
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] SerializeToByteArray(object obj)
        {
            if (obj == null)
            {
                return null;
            }
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }
        /// <summary>
        /// Desirialize from Byte Array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="byteArray"></param>
        /// <returns></returns>
        public static T DeserializeFromByteArray<T>(byte[] byteArray) where T : class
        {
            if (byteArray == null)
            {
                return null;
            }
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(byteArray, 0, byteArray.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = (T)binForm.Deserialize(memStream);
                return obj;
            }
        }
        /// <summary>
        /// Resize Byte Array
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="newSize"></param>
        /// <returns></returns>
        public static byte[] ResizeByteArray(byte[] bytes, int newSize)
        {
            Array.Resize(ref bytes, newSize);
            return bytes;
        }
    }
}
