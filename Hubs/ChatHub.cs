using Microsoft.AspNetCore.Components;
using System.Net.NetworkInformation;
using System;

using System.Security.Cryptography;
using CasperArmy_Chat.Shared;
using System.Globalization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System.Security.Cryptography;

using Microsoft.AspNetCore.SignalR;
using Org.BouncyCastle.Utilities;
using CasperArmy_Chat.Services;
using CasperArmy_Chat.Entities;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using CasperArmy_Chat.Data;
using CasperArmy_Chat.Shared.Cryptography.CasperNetwork;
using CasperArmy_Chat.Shared.Cryptography;
using System.ComponentModel;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Utilities.Encoders;
using System.Text;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Cms;
using Newtonsoft.Json.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Org.BouncyCastle.OpenSsl;

namespace CasperArmy_Chat.Hubs
{
  public class ChatHub : Hub
  {
    private IDataService _dataService;
    // private DataContext _context;
    HashService hashService = new HashService();
    SigningService signingService = new SigningService();


    private string PrivateKey = "";
    private string PublicKey = "";

    public ChatHub(IDataService dataService)
    {
      _dataService = dataService;

      GenerateServerSecret();
    }

    private void GenerateServerSecret()
    {
      // Receiving random bytes using a cryptographically strong algorithm in the amount allowed for the algorithm to use as a key
      var oid = Org.BouncyCastle.Asn1.X9.X962NamedCurves.GetOid("prime256v1");
      SecureRandom random = new SecureRandom();

      ECKeyPairGenerator generator = new ECKeyPairGenerator("ECDSA");
      generator.Init(new ECKeyGenerationParameters(oid, random));
      AsymmetricCipherKeyPair? keyPair = generator.GenerateKeyPair();
      PrivateKey = Convert.ToBase64String(PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private).GetEncoded());
      PublicKey = Convert.ToBase64String(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.Public).GetEncoded());
    }

    public async Task SendMessage(int groupId, int userId, string iv, string capsule)
    {
      // TODO: change sgn into Base64 encoding
      Group group = await _dataService.GetGroup(groupId);
      Dictionary<string, string> capsuleDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(capsule);
      string enc = capsuleDict["enc"];
      string sgn = capsuleDict["sgn"];
      string publicKey = await _dataService.GetUserPublicKey(userId);
      byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(enc));
      // Verify signature.
      if (hashService.GetAlgorithm(publicKey) == SignAlgorithmEnum.ed25519)
      {
        if (!signingService.VerifyEd25519Signature(publicKey, Convert.ToHexString(hash), sgn))
        {
          throw new Exception("Wrong signature");
        }
      }
      else if (hashService.GetAlgorithm(publicKey) == SignAlgorithmEnum.secp256k1)
      {
        if (!signingService.VerifySecp256k1Signature(publicKey, Convert.ToHexString(hash), sgn))
        {
          throw new Exception("Wrong signature");
        }
      }
      else
      {
        throw new Exception("Unknown key type");
      }
      Aes AES = Aes.Create();
      AES.Mode = CipherMode.CBC;
      AES.Padding = PaddingMode.PKCS7;
      AES.BlockSize = 128;
      AES.KeySize = 256;
      AES.Key = Convert.FromBase64String(group.SharedKey);
      AES.IV = Convert.FromBase64String(iv);
      string payload = Encoding.UTF8.GetString(AES.DecryptCbc(Convert.FromBase64String(enc), AES.IV));
      Dictionary<string, string> payloadDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(payload);
      int gid = int.Parse(payloadDict["gid"]);
      int uid = int.Parse(payloadDict["uid"]);
      string pTx = payloadDict["pTx"];
      if (gid != groupId || userId != uid) throw new Exception("Group id or user id dismatch");
      AES.GenerateIV();
      AES.GenerateKey();
      string cipher = Convert.ToBase64String(AES.EncryptCbc(Encoding.UTF8.GetBytes(pTx), AES.IV));
      Entities.Message msg = new Entities.Message();
      msg.GroupId = groupId;
      msg.UserId = userId;
      msg.Cipher = cipher;
      msg.OnetimeKey = Convert.ToBase64String(AES.Key);
      msg.IV = Convert.ToBase64String(AES.IV);
      await _dataService.AddMessage(msg);
      await Clients.Group(group.Name).SendAsync("ReceiveMessage", groupId, userId, iv, capsule);
    }

    public async Task HandShake(int userId)
    {
      Group[] groups = await _dataService.GetUserGroups(userId);
      for (int i = 0; i < groups.Length; i++)
      {
        await Groups.AddToGroupAsync(Context.ConnectionId, groups[i].Id.ToString());
      }
      await Clients.Caller.SendAsync("HandShake", PublicKey);
      // await Clients.Groups(Array.ConvertAll<GromsgDTO.srcData.Lengthup, string>(groups, g => g.Id.ToString())).SendAsync("UserJoin", userId);
    }
  }
}
