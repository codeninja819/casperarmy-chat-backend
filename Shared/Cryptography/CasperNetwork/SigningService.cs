using CasperArmy_Chat.Shared.Cryptography.CasperNetwork;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using Org.BouncyCastle.Utilities.IO.Pem;
using System;
using System.IO;
using System.Text;


// ALL THIS IS FOR CASPER NETWORK

namespace CasperArmy_Chat.Shared.Cryptography.CasperNetwork;

/// <summary>
/// Casper Network currently supports two Digital Signature Algorithms Ed25519 and Secp256k1. These algorithms are responsible for signing deploys on Casper Network.
/// </summary>
public class SigningService
{
    private const string MESSAGE_PREFIX = "Casper Message:\n";

    /// <summary>
    /// Get the Public-Private key pair from pem file
    /// </summary>
    /// <param name="publicKeyFilePath"></param>
    /// <param name="privateKeyFilePath"></param>
    /// <param name="signAlgorithm"></param>
    /// <returns></returns>
    public AsymmetricCipherKeyPair GetKeyPairFromFile(string publicKeyFilePath, string privateKeyFilePath, SignAlgorithmEnum signAlgorithm)
    {
        return GetKeyPair(File.OpenRead(publicKeyFilePath), File.OpenRead(privateKeyFilePath), signAlgorithm);
    }
    /// <summary>
    /// Get the Public-Private key pair from Stream
    /// </summary>
    /// <param name="publicKeyIn"></param>
    /// <param name="privateKeyIn"></param>
    /// <param name="signAlgorithm"></param>
    /// <returns></returns>
    public AsymmetricCipherKeyPair GetKeyPair(Stream publicKeyIn, Stream privateKeyIn, SignAlgorithmEnum signAlgorithm)
    {
        if (signAlgorithm == SignAlgorithmEnum.ed25519)
        {
            byte[] publicBytes = ReadPemToByte(publicKeyIn);
            byte[] secretBytes = ReadPemToByte(privateKeyIn);

            byte[] publicLastBytes = ByteUtil.GetLastNBytes(publicBytes, 32);
            byte[] secretLastBytes = ByteUtil.GetLastNBytes(secretBytes, 32);

            return new AsymmetricCipherKeyPair(new Ed25519PublicKeyParameters(publicLastBytes, 0), new Ed25519PrivateKeyParameters(secretLastBytes, 0));
        }
        else
        {
            var secretPem = ReadPemToObject(privateKeyIn);
            return (AsymmetricCipherKeyPair)secretPem;
        }
    }
    /// <summary>
    /// Generate Key Pair
    /// </summary>
    /// <param name="signAlgorithm"></param>
    /// <returns></returns>
    public AsymmetricCipherKeyPair GenerateKeyPair(SignAlgorithmEnum signAlgorithm)
    {
        if (signAlgorithm == SignAlgorithmEnum.ed25519)
        {
            SecureRandom random = new SecureRandom();
            Ed25519KeyPairGenerator keyPairGenerator = new Ed25519KeyPairGenerator();
            keyPairGenerator.Init(new Ed25519KeyGenerationParameters(random));
            return keyPairGenerator.GenerateKeyPair();
        }
        else if (signAlgorithm == SignAlgorithmEnum.secp256k1)
        {
            var curve = ECNamedCurveTable.GetByName(SignAlgorithmEnum.secp256k1.ToString());
            var domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

            var secureRandom = new SecureRandom();
            var keyParams = new ECKeyGenerationParameters(domainParams, secureRandom);

            var generator = new ECKeyPairGenerator("ECDSA");
            generator.Init(keyParams);
            var keyPair = generator.GenerateKeyPair();

            return keyPair;
        }
        else
        {
            throw new NotSupportedException("Sign Algorithm not supported");
        }
    }

    public bool VerifyEd25519Signature(string publicKey, string message, string signature)
    {
        var messageBytes = Encoding.UTF8.GetBytes(MESSAGE_PREFIX + message);
        var signatureBytes= Hex.Decode(signature);
        Ed25519PublicKeyParameters pbK = new Ed25519PublicKeyParameters(Hex.Decode(publicKey.Remove(0, 2)));
        return VerifySignatureEd25519(pbK, messageBytes, signatureBytes);
    }


    static string BytesToStringConverted(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        {
            using (var streamReader = new StreamReader(stream))
            {
                return streamReader.ReadToEnd();
            }
        }
    }

    public bool VerifySecp256k1Signature(string publicKey, string message, string signature)
    {
        bool returnedValue = false;

        var messageBytes = Encoding.UTF8.GetBytes(MESSAGE_PREFIX + message);

        var curve = ECNamedCurveTable.GetByName("secp256k1");
        var domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

        Org.BouncyCastle.Math.EC.ECPoint q = curve.Curve.DecodePoint(Hex.Decode(publicKey.Remove(0, 2)));
        ECPublicKeyParameters pk = new ECPublicKeyParameters(q, domainParams);

        var signer = SignerUtilities.GetSigner("SHA-256withPLAIN-ECDSA");
        signer.Init(forSigning: false, pk);
        try
        {
            signer.BlockUpdate(messageBytes, 0, messageBytes.Length);
            returnedValue = signer.VerifySignature(Hex.Decode(signature));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return returnedValue;
    }

    /// <summary>
    /// Generate Signature with Ed25519 Algo
    /// </summary>
    /// <param name="privateKey"></param>
    /// <param name="toSign"></param>
    /// <returns></returns>
    public byte[] GetSignatureEd25519(AsymmetricKeyParameter privateKey, byte[] toSign)
    {
        Ed25519Signer signer = new Ed25519Signer();
        signer.Init(true, privateKey);
        signer.BlockUpdate(toSign, 0, toSign.Length);
        return signer.GenerateSignature();
    }
    /// <summary>
    /// Verify Signature with Ed25519 Algo
    /// </summary>
    /// <param name="publicKeyParameters"></param>
    /// <param name="message"></param>
    /// <param name="signed"></param>
    /// <returns></returns>
    public bool VerifySignatureEd25519(AsymmetricKeyParameter publicKeyParameters, byte[] message, byte[] signed)
    {
        Ed25519Signer verifier = new Ed25519Signer();
        verifier.Init(false, publicKeyParameters);
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signed);
    }

    /// <summary>
    /// Generate Signature with Secp256k1 Algo
    /// </summary>
    /// <param name="privateKey"></param>
    /// <param name="toSign"></param>
    /// <returns></returns>
    public byte[] GetSignatureSecp256k1(AsymmetricKeyParameter privateKey, byte[] toSign)
    {
        var signer = SignerUtilities.GetSigner("SHA-256withPLAIN-ECDSA");
        signer.Init(true, privateKey);
        signer.BlockUpdate(toSign, 0, toSign.Length);
        var signature = signer.GenerateSignature();
        return signature;
    }
    /// <summary>
    /// Verify Signature with Secp256k1 Algo
    /// </summary>
    /// <param name="publicKeyParameters"></param>
    /// <param name="message"></param>
    /// <param name="signed"></param>
    /// <returns></returns>
    public bool VerifySignatureSecp256k1(AsymmetricKeyParameter publicKeyParameters, byte[] message, byte[] signed)
    {
        var signer = SignerUtilities.GetSigner("SHA-256withPLAIN-ECDSA");
        signer.Init(false, publicKeyParameters);
        signer.BlockUpdate(message, 0, message.Length);
        var signature = signer.VerifySignature(signed);
        return signature;
    }
    /// <summary>
    /// Convert Private Key To Pem
    /// </summary>
    /// <param name="privateKey"></param>
    /// <returns></returns>
    public string ConvertPrivateKeyToPem(AsymmetricKeyParameter privateKey)
    {
        using (var stringWriter = new StringWriter())
        {
            var pemWriter = new Org.BouncyCastle.OpenSsl.PemWriter(stringWriter);
            pemWriter.WriteObject(privateKey);
            pemWriter.Writer.Flush();
            return stringWriter.ToString();
        }
    }
    /// <summary>
    /// Convert Public Key To Pem
    /// </summary>
    /// <param name="publicKey"></param>
    /// <returns></returns>
    public string ConvertPublicKeyToPem(AsymmetricKeyParameter publicKey)
    {
        using (var stringWriter = new StringWriter())
        {
            Org.BouncyCastle.OpenSsl.PemWriter pemWriter = new Org.BouncyCastle.OpenSsl.PemWriter(stringWriter);
            pemWriter.WriteObject(publicKey);
            pemWriter.Writer.Flush();
            return stringWriter.ToString();
        }
    }

    private byte[] ReadPemToByte(Stream keyStream)
    {
        TextReader textReader = new StreamReader(keyStream);
        var pemReader = new Org.BouncyCastle.OpenSsl.PemReader(textReader);
        var pemObject = pemReader.ReadPemObject();
        return pemObject.Content;
    }

    private object ReadPemToObject(Stream keyStream)
    {
        TextReader textReader = new StreamReader(keyStream);
        var pemReader = new Org.BouncyCastle.OpenSsl.PemReader(textReader);
        var pemObject = pemReader.ReadObject();
        return pemObject;
    }

    private PemObject ReadPemToPemObject(Stream keyStream)
    {
        TextReader textReader = new StreamReader(keyStream);
        var pemReader = new Org.BouncyCastle.OpenSsl.PemReader(textReader);
        var pemObject = pemReader.ReadPemObject();
        return pemObject;
    }


}
