using Microsoft.AspNetCore.Mvc;
using CasperArmy_Chat.Entities;
using CasperArmy_Chat.Services;
using CasperArmy_Chat.Shared.DTOs;
using System.Data;
using Npgsql;
using Newtonsoft.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.SignalR;
using CasperArmy_Chat.Hubs;
using CasperArmy_Chat.Shared.Cryptography.CasperNetwork;
using System.Text;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CasperArmy_Chat.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class ChatController : ControllerBase
  {
    private IDataService _dataService;
    private IConfiguration _configuration;
    private IHubContext<ChatHub> _hubContext;

    private HashService hashService = new HashService();
    private SigningService signingService = new SigningService();

    public ChatController(IDataService dataService, IConfiguration configuration, IHubContext<ChatHub> hubContext)
    {
      this._dataService = dataService;
      this._configuration = configuration;
      this._hubContext = hubContext;
    }

    [HttpGet("GetUserId")]
    public async Task<JsonResult> GetUserId(string publicKey)   //017c6ab3be2a2f135fb7a61f2526e35d66a36fc06b14cad72cd73cf415f3289843
    {
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable userTable = new DataTable();
        string query = @"SELECT * FROM ""Users"" WHERE ""PublicKey"" = '" + publicKey + "' LIMIT 1";

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            userTable.Load(myReader);

            if (userTable.Rows.Count == 0)
            {
              string insertQuery = @"INSERT INTO ""Users"" (""PublicKey"", ""AccountHash"") VALUES ('" + publicKey + "', '" + new CasperArmy_Chat.Shared.Cryptography.CasperNetwork.HashService().GetAccountHash(publicKey) + "')";
              string selectQuery = @"SELECT * FROM ""Users"" WHERE ""PublicKey"" = '" + publicKey + "' LIMIT 1";

              await using var batchCmd = new NpgsqlBatch(myCon)
              {
                BatchCommands =
                                {
                                    new(insertQuery),
                                    new(selectQuery)
                                }
              };

              myReader = await batchCmd.ExecuteReaderAsync();
              userTable.Load(myReader);
            }
            myReader.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartObject();
        foreach (DataRow row in userTable.Rows)
        {
          writer.WritePropertyName("id");
          writer.WriteValue(row["Id"]);
          writer.WritePropertyName("publicKey");
          writer.WriteValue(publicKey);
          writer.WritePropertyName("accountHash");
          writer.WriteValue(row["AccountHash"]);
          break;
        }
        writer.WriteEndObject();
        var result = JsonConvert.DeserializeObject<User>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      //return _dataService.GetUserwithKey(publicKey);
    }

    [HttpGet("GetUsers")]
    public async Task<JsonResult> GetUsers()
    {
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable userTable = new DataTable();
        string query = @"SELECT * FROM ""Users""";

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            userTable.Load(myReader);

            myReader.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartArray();
        foreach (DataRow row in userTable.Rows)
        {
          writer.WriteStartObject();
          writer.WritePropertyName("id");
          writer.WriteValue(row["Id"]);
          writer.WritePropertyName("publicKey");
          writer.WriteValue(row["PublicKey"]);
          writer.WritePropertyName("accountHash");
          writer.WriteValue(row["AccountHash"]);
          writer.WriteEndObject();
        }
        writer.WriteEndArray();
        var result = JsonConvert.DeserializeObject<User[]>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      // return _dataService.GetUsers();
    }

    [HttpPost("GroupCreate")]
    public async Task<JsonResult> GroupCreate(int userId, string connId, [FromBody] GroupCreateDTO newGroup)
    {
      int groupId = 0;
      Success success = new Success();
      success.success = true;

      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;

      StringWriter esw = new StringWriter();
      JsonTextWriter eWriter = new JsonTextWriter(esw);
      eWriter.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable groupTable = new DataTable();
        string sharedKey = GenerateGroupSecret();
        string insertQuery = @"INSERT INTO ""Groups"" (""Name"", ""IsPublic"", ""AdminId"", ""SharedKey"") VALUES ('" + newGroup.name + "', " + newGroup.isPublic + ", " + userId + ", '" + sharedKey + "')";
        string selectQuery = @"SELECT * FROM ""Groups"" WHERE ""Name"" = '" + newGroup.name + @"' AND ""IsPublic"" = " + newGroup.isPublic + @" AND ""AdminId"" = " + userId + @" AND ""SharedKey"" = '" + sharedKey + @"' LIMIT 1";

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          await using var batchCmd = new NpgsqlBatch(myCon)
          {
            BatchCommands =
              {
                new(insertQuery),
                new(selectQuery)
              }
          };

          myReader = await batchCmd.ExecuteReaderAsync();
          groupTable.Load(myReader);

          myReader.Close();


          eWriter.WriteStartObject();
          foreach (DataRow row in groupTable.Rows)
          {
            eWriter.WritePropertyName("id");
            eWriter.WriteValue(row["Id"]);
            groupId = (int)row["Id"];
            eWriter.WritePropertyName("name");
            eWriter.WriteValue(row["Name"]);
            eWriter.WritePropertyName("isPublic");
            eWriter.WriteValue(row["IsPublic"]);
            eWriter.WritePropertyName("adminId");
            eWriter.WriteValue(row["AdminId"]);
            eWriter.WritePropertyName("sharedKey");
            eWriter.WriteValue(row["SharedKey"]);
            break;
          }
          // Add user to joins table
          if (!newGroup.isPublic)
          {
            string joinQuery = @"INSERT INTO ""Joins"" (""UserId"", ""GroupId"") VALUES (" + userId + ", " + groupId + ")";
            await using var command = new NpgsqlCommand(joinQuery, myCon);
            await command.ExecuteNonQueryAsync();
          }
          eWriter.WriteEndObject();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }

        if (success.success)
        {
          if (newGroup.isPublic)
          {
            await _hubContext.Clients.All.SendAsync("NewEvent", "CREATE_GROUP", esw.ToString());
          }
          else
          {
            await _hubContext.Groups.AddToGroupAsync(connId, groupId.ToString());
            await _hubContext.Clients.Client(connId).SendAsync("NewEvent", "CREATE_GROUP", esw.ToString());
          }
        }

        writer.WriteStartObject();
        writer.WritePropertyName("success");
        writer.WriteValue(success.success);
        writer.WritePropertyName("error");
        writer.WriteValue(success.error);
        writer.WriteEndObject();
        var result = JsonConvert.DeserializeObject<Success>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      //return _dataService.CreateGroup(userId, newGroup);
    }

    [HttpPost("GroupUpdate")]
    public async Task<JsonResult> GroupUpdate(int userId, int groupId, string newName)
    {
      Success success = new Success();
      success.success = true;

      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable groupTable = new DataTable();
        string sharedKey = GenerateGroupSecret();
        string query = @"UPDATE ""Groups"" SET ""Name"" = '" + newName + @"' WHERE ""Id"" = " + groupId + @" AND ""AdminId"" = " + userId;

        try
        {
          await myCon.OpenAsync().ConfigureAwait(false);

          await using var command = new NpgsqlCommand(query, myCon);
          int affectedRows = await command.ExecuteNonQueryAsync();
          if (affectedRows == 0)
          {
            success.success = false;
            success.error = "You don't have permission to do this operation.";
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
          success.success = false;
          success.error = "Something went wrong.";

        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }

        writer.WriteStartObject();
        writer.WritePropertyName("success");
        writer.WriteValue(success.success);
        writer.WritePropertyName("error");
        writer.WriteValue(success.error);
        writer.WriteEndObject();
        var result = JsonConvert.DeserializeObject<Success>(sw.ToString());

        if (success.success)
        {
          StringWriter eStringWriter = new StringWriter();
          JsonTextWriter eWriter = new JsonTextWriter(eStringWriter);
          eWriter.Formatting = Formatting.Indented;
          eWriter.WriteStartObject();
          eWriter.WritePropertyName("groupId");
          eWriter.WriteValue(groupId);
          eWriter.WritePropertyName("newName");
          eWriter.WriteValue(newName);
          eWriter.WriteEndObject();
          await _hubContext.Clients.Group(groupId.ToString()).SendAsync("NewEvent", "RENAME_GROUP", eStringWriter.ToString());
        }

        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
    }

    [HttpGet("GetUserGroups")]
    public async Task<JsonResult> GetUserGroups(int userId)
    {
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable groupTable = new DataTable();
        DataTable joinTable = new DataTable();
        string query = @"SELECT * FROM ""Groups"" WHERE ""IsPublic"" = TRUE OR ""Id"" IN
                                (SELECT ""GroupId"" FROM ""Joins"" WHERE ""UserId"" = " + userId + ")";

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            groupTable.Load(myReader);

            myReader.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartArray();
        foreach (DataRow row in groupTable.Rows)
        {
          writer.WriteStartObject();
          writer.WritePropertyName("id");
          writer.WriteValue(row["Id"]);
          writer.WritePropertyName("name");
          writer.WriteValue(row["Name"]);
          writer.WritePropertyName("isPublic");
          writer.WriteValue(row["IsPublic"]);
          writer.WritePropertyName("adminId");
          writer.WriteValue(row["AdminId"]);
          writer.WritePropertyName("sharedKey");
          writer.WriteValue(row["SharedKey"]);
          writer.WriteEndObject();
        }
        writer.WriteEndArray();
        var result = JsonConvert.DeserializeObject<Group[]>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      // return _dataService.GetUserGroups(userId);
    }

    [HttpGet("GetGroupMessages")]
    public async Task<JsonResult> GetGroupMessages(int groupId)
    {
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable msgTable = new DataTable();
        string query = @"SELECT * FROM ""Messages"" WHERE ""GroupId"" = '" + groupId + @"' ORDER BY ""Id"" ASC";

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            msgTable.Load(myReader);

            myReader.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartArray();
        foreach (DataRow row in msgTable.Rows)
        {
          string sharedKey = await _dataService.GetGroupSecret(groupId);
          Aes AES = Aes.Create();
          AES.Mode = CipherMode.CBC;
          AES.Padding = PaddingMode.PKCS7;
          AES.BlockSize = 128;
          AES.KeySize = 256;
          AES.Key = Convert.FromBase64String((string)row["OnetimeKey"]);
          AES.IV = Convert.FromBase64String((string)row["IV"]);
          byte[] plainText = AES.DecryptCbc(Convert.FromBase64String((string)row["Cipher"]), AES.IV);
          if ((bool)row["Deleted"] == true)
          {
            plainText = Encoding.UTF8.GetBytes("Deleted Message");
          }
          AES.GenerateIV();
          AES.Key = Convert.FromBase64String(sharedKey);
          string encrypted = Convert.ToBase64String(AES.EncryptCbc(plainText, AES.IV));

          writer.WriteStartObject();
          writer.WritePropertyName("id");
          writer.WriteValue(row["Id"]);
          writer.WritePropertyName("groupId");
          writer.WriteValue(row["GroupId"]);
          writer.WritePropertyName("userId");
          writer.WriteValue(row["UserId"]);
          writer.WritePropertyName("cipher");
          writer.WriteValue(encrypted);
          writer.WritePropertyName("iv");
          writer.WriteValue(Convert.ToBase64String(AES.IV));
          writer.WritePropertyName("deleted");
          writer.WriteValue(row["Deleted"]);
          writer.WriteEndObject();
        }
        writer.WriteEndArray();
        var result = JsonConvert.DeserializeObject<Message[]>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      // return _dataService.GetGroupMessages(groupId);
    }
    [HttpGet("GetGroupUserIds")]
    public async Task<JsonResult> GetGroupUserIds(int groupId)
    {
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable msgTable = new DataTable();
        string query = @"SELECT ""UserId"" FROM ""Joins"" WHERE ""GroupId"" = '" + groupId + "'";

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            msgTable.Load(myReader);

            myReader.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartArray();
        foreach (DataRow row in msgTable.Rows)
        {
          writer.WriteValue(row["UserId"]);
        }
        writer.WriteEndArray();
        var result = JsonConvert.DeserializeObject<int[]>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      // return _dataService.GetGroupUserIds(groupId);
    }
    [HttpPost("InviteUsers")]
    public async Task<JsonResult> InviteUsers(int groupId, int adminId, int[] userIds)
    {
      Success success = new Success();
      success.success = true;

      if (userIds.Length == 0)
        throw new Exception("Given array is empty.");
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);
        try
        {
          NpgsqlDataReader myReader;
          DataTable count = new DataTable();

          string validateQuery = @"SELECT COUNT(""Id"") FROM ""Groups"" WHERE ""Id"" = " + groupId + @" AND ""AdminId"" = " + adminId;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(validateQuery, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            count.Load(myReader);

            myReader.Close();
          }

          if (count.Rows.Count == 0)
          {
            throw new Exception("Not admin");
          }


          string query = @"INSERT INTO ""Joins"" (""UserId"", ""GroupId"") VALUES ";
          for (int i = 0; i < userIds.Length; i++)
          {
            query = query + "(" + userIds[i] + ", " + groupId + "),";
          }
          Console.WriteLine(query.Substring(0, query.Length - 1));
          await using var command = new NpgsqlCommand(query.Substring(0, query.Length - 1), myCon);
          await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
          success.success = false;
          success.error = "Something went wrong.";
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartObject();
        writer.WritePropertyName("success");
        writer.WriteValue(success.success);
        writer.WritePropertyName("error");
        writer.WriteValue(success.error);
        writer.WriteEndObject();

        if (success.success)
        {
          StringWriter esw = new StringWriter();
          JsonTextWriter eWriter = new JsonTextWriter(esw);
          writer.Formatting = Formatting.Indented;
          eWriter.WriteStartObject();
          eWriter.WritePropertyName("groupId");
          eWriter.WriteValue(groupId);
          eWriter.WritePropertyName("userIds");
          eWriter.WriteStartArray();
          for (int i = 0; i < userIds.Length; i++)
            eWriter.WriteValue(userIds[i]);
          eWriter.WriteEndArray();
          eWriter.WriteEndObject();
          await _hubContext.Clients.Group(groupId.ToString()).SendAsync("NewEvent", "JOIN_GROUP", esw.ToString());
          // add users into group, let users know invite
        }

        var result = JsonConvert.DeserializeObject<Success>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
      // return _dataService.InviteUsers(groupId, adminId, userIds);
    }

    [HttpPost("LeaveGroup")]
    public async Task<JsonResult> LeaveGroup(int groupId, int userId)
    {
      Success success = new Success();
      success.success = true;

      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable groupTable = new DataTable();
        string sharedKey = GenerateGroupSecret();
        string query = @"DELETE FROM ""Joins"" WHERE ""GroupId"" = " + groupId + @" AND ""UserId"" = " + userId +
            @" AND (SELECT COUNT (*) FROM ""Groups"" WHERE ""Id"" = " + groupId + @" AND ""AdminId"" = " + userId + @") = 0";

        try
        {
          await myCon.OpenAsync().ConfigureAwait(false);

          await using var command = new NpgsqlCommand(query, myCon);
          int affectedRows = await command.ExecuteNonQueryAsync();
          if (affectedRows == 0)
          {
            success.success = false;
            success.error = "Admins cannot leave group or bad operation.";
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
          success.success = false;
          success.error = "Something went wrong.";
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }

        writer.WriteStartObject();
        writer.WritePropertyName("success");
        writer.WriteValue(success.success);
        writer.WritePropertyName("error");
        writer.WriteValue(success.error);
        writer.WriteEndObject();
        var result = JsonConvert.DeserializeObject<Success>(sw.ToString());

        if (success.success)
        {
          StringWriter esw = new StringWriter();
          JsonTextWriter eWriter = new JsonTextWriter(esw);
          eWriter.Formatting = Formatting.Indented;
          eWriter.WriteStartObject();
          eWriter.WritePropertyName("groupId");
          eWriter.WriteValue(groupId);
          eWriter.WritePropertyName("userId");
          eWriter.WriteValue(userId);
          eWriter.WriteEndObject();
          await _hubContext.Clients.Group(groupId.ToString()).SendAsync("NewEvent", "LEAVE_GROUP", esw.ToString());
        }

        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
    }

    [HttpPost("NewMessage")]
    public async Task<JsonResult> NewMessage([FromBody] NewMessageDTO msgDTO)
    {
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable uploadIdTable = new DataTable();

        if (msgDTO.srcData.Length > 0)
        {

          string insertQuery = @"INSERT INTO ""Uploads"" (""Data"") VALUES ";
          for (int i = 0; i < msgDTO.srcData.Length; i++)
          {
            insertQuery = insertQuery + "('" + msgDTO.srcData[i] + "'),";
          }
          insertQuery = insertQuery.Substring(0, insertQuery.Length - 1);
          string selectQuery = @"SELECT * FROM ( SELECT ""Id"" FROM ""Uploads"" ORDER BY ""Id"" DESC LIMIT " + msgDTO.srcData.Length + @") AS _ ORDER BY ""Id"" ASC;";
          try
          {
            NpgsqlDataReader myReader;

            await myCon.OpenAsync().ConfigureAwait(false);
            await using var batchCmd = new NpgsqlBatch(myCon)
            {
              BatchCommands =
                            {
                                new(insertQuery),
                                new(selectQuery)
                            }
            };

            myReader = await batchCmd.ExecuteReaderAsync();
            uploadIdTable.Load(myReader);

            myReader.Close();
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex);
          }
          finally
          {
            if (myCon.State == ConnectionState.Open)
              await myCon.CloseAsync();
          }
        }


        // TODO: change sgn into Base64 encoding
        Group group = await _dataService.GetGroup(msgDTO.groupId);
        Dictionary<string, string> capsuleDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(msgDTO.capsule);
        string enc = capsuleDict["enc"];
        string sgn = capsuleDict["sgn"];
        string publicKey = await _dataService.GetUserPublicKey(msgDTO.userId);
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
        AES.IV = Convert.FromBase64String(msgDTO.iv);
        string payload = Encoding.UTF8.GetString(AES.DecryptCbc(Convert.FromBase64String(enc), AES.IV));
        Dictionary<string, string> payloadDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(payload);
        int gid = int.Parse(payloadDict["gid"]);
        int uid = int.Parse(payloadDict["uid"]);
        string pTx = payloadDict["pTx"];
        string marker = "<LazyLoadingImage";
        for (int i = 0, pos = 0; i < msgDTO.srcData.Length; i++)
        {
          pos = pTx.IndexOf(marker, pos) + marker.Length + 1;
          pTx = pTx.Insert(pos, @"dataId={""" + uploadIdTable.Rows[i]["Id"] + @"""} ");
        }
        if (gid != msgDTO.groupId || msgDTO.userId != uid) throw new Exception("Group id or user id dismatch");
        string encrypted = Convert.ToBase64String(AES.EncryptCbc(Encoding.UTF8.GetBytes(pTx), AES.IV));
        AES.GenerateIV();
        AES.GenerateKey();
        string cipher = Convert.ToBase64String(AES.EncryptCbc(Encoding.UTF8.GetBytes(pTx), AES.IV));
        Entities.Message msg = new Entities.Message();
        msg.GroupId = msgDTO.groupId;
        msg.UserId = msgDTO.userId;
        msg.Cipher = cipher;
        msg.OnetimeKey = Convert.ToBase64String(AES.Key);
        msg.IV = Convert.ToBase64String(AES.IV);
        int msgId = await _dataService.AddMessage(msg);
        await _hubContext.Clients.Group(group.Id.ToString()).SendAsync("ReceiveMessage", msgDTO.groupId, msgDTO.userId, msgId, msgDTO.iv, encrypted);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex);
        return new JsonResult(ex.ToString());
      }

      var result = JsonConvert.DeserializeObject<Success>(@"{""success"":true}");
      return new JsonResult(result);
    }

    [HttpPost("DeleteMessage")]
    public async Task<JsonResult> DeleteMessage(int userId, int groupId, int msgId)
    {
      Success success = new Success();
      success.success = true;

      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable groupTable = new DataTable();
        string sharedKey = GenerateGroupSecret();
        string query = @"UPDATE ""Messages"" SET ""Deleted"" = TRUE WHERE ""Id"" = " + msgId + @" AND ""GroupId"" = " + groupId + @" AND ""UserId"" = " + userId;

        try
        {
          await myCon.OpenAsync().ConfigureAwait(false);

          await using var command = new NpgsqlCommand(query, myCon);
          int affectedRows = await command.ExecuteNonQueryAsync();
          if (affectedRows == 0)
          {
            success.success = false;
            success.error = "Bad operation.";
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
          success.success = false;
          success.error = "Something went wrong.";
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }

        writer.WriteStartObject();
        writer.WritePropertyName("success");
        writer.WriteValue(success.success);
        writer.WritePropertyName("error");
        writer.WriteValue(success.error);
        writer.WriteEndObject();
        var result = JsonConvert.DeserializeObject<Success>(sw.ToString());

        await _hubContext.Clients.Group(groupId.ToString()).SendAsync("DeleteMessage", groupId, userId, msgId);
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
    }

    [HttpGet("GetUpload")]
    public async Task<JsonResult> GetUpload(int uploadId)
    {
      StringWriter sw = new StringWriter();
      JsonTextWriter writer = new JsonTextWriter(sw);
      writer.Formatting = Formatting.Indented;
      try
      {
        string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
        NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

        DataTable uploadTable = new DataTable();
        string query = @"SELECT * FROM ""Uploads"" WHERE ""Id"" = " + uploadId;

        try
        {
          NpgsqlDataReader myReader;

          await myCon.OpenAsync().ConfigureAwait(false);
          using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
          {
            myReader = await myCommand.ExecuteReaderAsync();
            uploadTable.Load(myReader);

            myReader.Close();
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex);
        }
        finally
        {
          if (myCon.State == ConnectionState.Open)
            await myCon.CloseAsync();
        }
        writer.WriteStartObject();
        foreach (DataRow row in uploadTable.Rows)
        {
          writer.WritePropertyName("id");
          writer.WriteValue((int)row["Id"]);
          writer.WritePropertyName("data");
          writer.WriteValue((string)row["Data"]);
          break;
        }
        writer.WriteEndObject();
        var result = JsonConvert.DeserializeObject<Upload>(sw.ToString());
        return new JsonResult(result);
      }
      catch (Exception ex)
      {
        return new JsonResult(ex.ToString());
      }
    }

    /*[HttpGet("{id}")]
    public string Get(int id)
    {
        return "value";
    }

    // POST api/<ChatController>
    [HttpPost]
    public void Post([FromBody] string value)
    {
    }

    // PUT api/<ChatController>/5
    [HttpPut("{id}")]
    public void Put(int id, [FromBody] string value)
    {
    }

    // DELETE api/<ChatController>/5
    [HttpDelete("{id}")]
    public void Delete(int id)
    {
    }*/

    public class Success
    {
      public bool success { get; set; }
      public string? error { get; set; }
    }

    private static string GenerateGroupSecret()
    {
      Aes AES = Aes.Create();
      AES.KeySize = AES.LegalKeySizes.Max(x => x.MaxSize);
      AES.GenerateKey();
      return Convert.ToBase64String(AES.Key);
    }
  }
}
