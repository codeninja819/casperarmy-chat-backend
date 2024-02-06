using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Crypto.Digests;

namespace CasperArmy_Chat.Shared.Cryptography.CasperNetwork
{
    /// <summary>
    /// The hash service utilizes BLAKE2b which is a cryptographic hash function.
    /// </summary>
    public class HashService
    {
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /// <summary>
        /// Get the Account Hash from the Account key
        /// </summary>
        /// <param name="accountKey"></param>
        /// <returns></returns>
        public string GetAccountHash(string accountKey)
        {
            var valueKeyAlgorithm = ByteUtil.CombineBytes(Encoding.UTF8.GetBytes(GetAlgorithm(accountKey).ToString().ToLower()), new byte[1]);

            var valueKey = ByteUtil.CombineBytes(valueKeyAlgorithm, ByteUtil.HexToByteArray(accountKey.Substring(2)));

            var resultHex = GetHashToHexFixedSize(valueKey, 32);

            return resultHex;
        }
        /// <summary>
        /// Get the Hash to hexadecimal value
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="hashSize"></param>
        /// <returns></returns>
        public string GetHashToHexFixedSize(byte[] bytes, int hashSize)
        {
            var resultByte = GetHashToBinaryFixedSize(bytes, hashSize);

            var resultHex = BitConverter.ToString(resultByte).Replace("-", "").ToLower();

            return resultHex;
        }
        /// <summary>
        /// Get Hash To Byte Array
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="hashSize"></param>
        /// <returns></returns>
        public byte[] GetHashToBinaryFixedSize(byte[] bytes, int hashSize)
        {

            var blake2B = new Blake2bDigest(hashSize * 8);
            blake2B.BlockUpdate(bytes, 0, bytes.Length);

            byte[] result = new byte[hashSize];
            blake2B.DoFinal(result, 0);
            return result;
        }
        /// <summary>
        /// Get the Signing Algorithm
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public SignAlgorithmEnum GetAlgorithm(string key)
        {
            string keyStartingWith = key.Substring(0, 2);

            if (key == null || key.Length < 66)
            {
                throw new ArgumentOutOfRangeException("Key size must be equal or greater than 66 chars");
            }
            try
            {
                switch (keyStartingWith)
                {
                    case "01":
                        if (key.Length != 66)
                        {
                            throw new ArgumentOutOfRangeException("Key length must be 66 chars");
                        }
                        return SignAlgorithmEnum.ed25519;
                        break;
                    case "02":
                        if (key.Length != 68)
                        {
                            throw new ArgumentOutOfRangeException("Key length must be 68 chars");
                        }
                        return SignAlgorithmEnum.secp256k1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown key prefix: [%s]", key.Substring(0, 2)));
                }

            }
            catch (Exception)
            {
                throw new ArgumentOutOfRangeException(string.Format("Unknown key prefix: [%s]", key.Substring(0, 2)));
            }
        }
        /// <summary>
        /// Get the Signing Algorithm in bytes
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public byte[] GetAlgorithmBytes(string key)
        {
            string keyStartingWith = key.Substring(0, 2);
            string algoResult = string.Empty;

            if (key == null || key.Length < 66)
            {
                throw new ArgumentOutOfRangeException("Key size must be equal or greater than 66 chars");
            }
            try
            {
                switch (keyStartingWith)
                {
                    case "01":
                        return new byte[] { 1 };
                    case "02":
                        return new byte[] { 2 };
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown key prefix: [%s]", key.Substring(0, 2)));
                }

            }
            catch (Exception)
            {
                throw new ArgumentOutOfRangeException(string.Format("Unknown key prefix: [%s]", key.Substring(0, 2)));
            }
        }

        /// <summary>
        /// Get the Signing Algorithm in Hex
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetAlgorithmHex(string key)
        {
            string keyStartingWith = key.Substring(0, 2);
            string algoResult = string.Empty;

            if (key == null || key.Length < 66)
            {
                throw new ArgumentOutOfRangeException("Key size must be equal or greater than 66 chars");
            }
            try
            {
                switch (keyStartingWith)
                {
                    case "01":
                        return keyStartingWith;
                    case "02":
                        return keyStartingWith;
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unknown key prefix: [%s]", key.Substring(0, 2)));
                }

            }
            catch (Exception)
            {
                throw new ArgumentOutOfRangeException(string.Format("Unknown key prefix: [%s]", key.Substring(0, 2)));
            }
        }

    }
}
