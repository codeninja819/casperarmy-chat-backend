
using BCrypt.Net;
using CasperArmy_Chat.Entities;
using CasperArmy_Chat.Data;
using Org.BouncyCastle.Crypto;
using Microsoft.EntityFrameworkCore;
using CasperArmy_Chat.Shared.DTOs;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using CasperArmy_Chat.Controllers;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.X9;

namespace CasperArmy_Chat.Services
{
  public interface IDataService
  {
    public User GetUserwithKey(string userKey);
    public IEnumerable<User> GetUsers();
    public Group CreateGroup(int userId, GroupCreateDTO group);
    public Task<Group?> GetGroup(int groupId);
    public IEnumerable<Message> GetGroupMessages(int groupId);
    public IEnumerable<int> GetGroupUserIds(int groupId);
    public Task<Group[]> GetUserGroups(int userId);
    public string InviteUsers(int groupId, int adminId, int[] userIds);

    public Task<string> GetUserPublicKey(int userId);
    public Task<string> GetGroupSecret(int groupId);

    public Task<int> AddMessage(Message msg);
  }
  public class DataService : IDataService
  {

    private DataContext _context;
    private IConfiguration _configuration;
    public DataService(DataContext context, IConfiguration configuration)
    {
      _context = context;
      _configuration = configuration;
    }

    public User GetUserwithKey(string userKey)
    {
      var user = _context.Users.SingleOrDefault(x => x.PublicKey == userKey);
      if (user == null)
      {
        // save user
        user = new User();
        user.PublicKey = userKey;
        user.AccountHash = new CasperArmy_Chat.Shared.Cryptography.CasperNetwork.HashService().GetAccountHash(userKey);
        _context.Users.Add(user);
        _context.SaveChanges();
        user = _context.Users.First(x => x.PublicKey == userKey);
      }
      return user;
    }

    public IEnumerable<User> GetUsers()
    {
      return _context.Users;
    }

    public Group CreateGroup(int userId, GroupCreateDTO newGroup)
    {
      // validate
      if (_context.Groups.Any(x => x.Name == newGroup.name))
        return null;

      // save user
      Group group = new Group();
      group.Name = newGroup.name;
      group.AdminId = userId;
      group.SharedKey = GenerateGroupSecret();
      group.IsPublic = newGroup.isPublic;
      _context.Groups.Add(group);
      _context.SaveChanges();
      Group created = _context.Groups.Single(x => x.Name == newGroup.name);
      Join join = new Join();
      join.UserId = userId;
      join.GroupId = created.Id;
      _context.Joins.Add(join);
      _context.SaveChanges();
      return created;
    }

    public async Task<Group?> GetGroup(int groupId)
    {
      string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
      NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

      DataTable userTable = new DataTable();
      string query = @"SELECT * FROM ""Groups"" WHERE ""Id"" = '" + groupId + "' LIMIT 1";

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
      foreach (DataRow row in userTable.Rows)
      {
        Group group = new Group();
        group.Id = (int)row["Id"];
        group.Name = (string)row["Name"];
        group.IsPublic = (bool)row["IsPublic"];
        group.AdminId = (int)row["AdminId"];
        group.SharedKey = (string)row["SharedKey"];
        return group;
      }
      return null;
      /*var group = _context.Groups.Find(groupId);
      return group;*/
    }
    public async Task<Group[]> GetUserGroups(int userId)
    {
      string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
      NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

      DataTable groupTable = new DataTable();
      DataTable joinTable = new DataTable();
      string query = @"SELECT * FROM ""Groups"" WHERE ""IsPublic"" = TRUE OR ""Id"" IN (SELECT ""GroupId"" FROM ""Joins"" WHERE ""UserId"" = " + userId + ")";

      NpgsqlDataReader myReader;

      await myCon.OpenAsync().ConfigureAwait(false);
      using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
      {
        myReader = await myCommand.ExecuteReaderAsync();
        groupTable.Load(myReader);

        myReader.Close();
      }
      if (myCon.State == ConnectionState.Open)
        await myCon.CloseAsync();

      Group[] groups = new Group[groupTable.Rows.Count];
      int counter = 0;
      foreach (DataRow row in groupTable.Rows)
      {
        groups[counter] = new Group();
        groups[counter].Id = (int)row["Id"];
        groups[counter].Name = (string)row["Name"];
        groups[counter].AdminId = (int)row["AdminId"];
        groups[counter].IsPublic = (bool)row["IsPublic"];
        groups[counter].SharedKey = (string)row["SharedKey"];
        counter++;
      }
      return groups;
      // return _context.Groups.Where(g => g.IsPublic == true || _context.Joins.Any(j => j.UserId == userId && j.GroupId == g.Id));
    }
    public IEnumerable<Message> GetGroupMessages(int groupId)
    {
      Group group = _context.Groups.Single(g => g.Id == groupId);
      Message[] msgs = _context.Messages.Where(m => m.GroupId == groupId).ToArray();
      for (int i = 0; i < msgs.Length; i++)
      {
        Aes AES = Aes.Create();
        AES.Mode = CipherMode.CBC;
        AES.Padding = PaddingMode.PKCS7;
        AES.BlockSize = 128;
        AES.KeySize = 256;
        AES.Key = Convert.FromBase64String(msgs[i].OnetimeKey);
        AES.IV = Convert.FromBase64String(msgs[i].IV);
        byte[] plainText = AES.DecryptCbc(Convert.FromBase64String(msgs[i].Cipher), AES.IV);
        AES.GenerateIV();
        AES.Key = Convert.FromBase64String(group.SharedKey);
        string encrypted = Convert.ToBase64String(AES.EncryptCbc(plainText, AES.IV));
        msgs[i].OnetimeKey = group.SharedKey;
        msgs[i].IV = Convert.ToBase64String(AES.IV);
        msgs[i].Cipher = encrypted;
      }
      return msgs;
    }
    public IEnumerable<int> GetGroupUserIds(int groupId)
    {
      Group group = _context.Groups.Single(g => g.Id == groupId);
      return _context.Joins.Where(j => j.GroupId == groupId).Select(j => j.UserId);
    }
    public string InviteUsers(int groupId, int adminId, int[] userIds)
    {
      Group group = _context.Groups.Single(g => g.Id == groupId);
      if (group.AdminId != adminId)
      {
        return "Only admmin can invite users.";
      }
      for (int i = 0; i < userIds.Length; i++)
      {
        if (_context.Joins.SingleOrDefault(j => j.UserId == userIds[i] && j.GroupId == groupId) != null) continue;
        Join join = new Join();
        join.GroupId = groupId;
        join.UserId = userIds[i];
        _context.Joins.Add(join);
      }
      _context.SaveChanges();
      return "Invite success.";
    }

    public async Task<string> GetUserPublicKey(int userId)
    {
      string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
      NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

      DataTable userTable = new DataTable();
      string query = @"SELECT ""PublicKey"" FROM ""Users"" WHERE ""Id"" = '" + userId + "' LIMIT 1";

      NpgsqlDataReader myReader;

      await myCon.OpenAsync().ConfigureAwait(false);
      using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
      {
        myReader = await myCommand.ExecuteReaderAsync();
        userTable.Load(myReader);

        myReader.Close();
      }
      if (myCon.State == ConnectionState.Open)
        await myCon.CloseAsync();

      foreach (DataRow row in userTable.Rows)
      {
        return (string)row["PublicKey"];
      }
      return "";
    }
    public async Task<string> GetGroupSecret(int groupId)
    {
      string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
      NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);

      DataTable userTable = new DataTable();
      string query = @"SELECT ""SharedKey"" FROM ""Groups"" WHERE ""Id"" = '" + groupId + "' LIMIT 1";

      NpgsqlDataReader myReader;

      await myCon.OpenAsync().ConfigureAwait(false);
      using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
      {
        myReader = await myCommand.ExecuteReaderAsync();
        userTable.Load(myReader);

        myReader.Close();
      }
      if (myCon.State == ConnectionState.Open)
        await myCon.CloseAsync();

      foreach (DataRow row in userTable.Rows)
      {
        return (string)row["SharedKey"];
      }
      return "";
    }

    public async Task<int> AddMessage(Message msg)
    {
      string sqlDataSource = _configuration.GetConnectionString("psqlCasperArmyServer");
      NpgsqlConnection myCon = new NpgsqlConnection(sqlDataSource);
      NpgsqlDataReader myReader;
      DataTable dataTable = new DataTable();

      await myCon.OpenAsync().ConfigureAwait(false);
      string query = @"INSERT INTO ""Messages"" (""GroupId"", ""UserId"", ""Cipher"", ""OnetimeKey"", ""IV"") VALUES (" + msg.GroupId + @", " + msg.UserId + @", '" + msg.Cipher + @"', '" + msg.OnetimeKey + @"', '" + msg.IV + @"') RETURNING ""Id"";";
      await using var command = new NpgsqlCommand(query, myCon);
      myReader = await command.ExecuteReaderAsync();
      dataTable.Load(myReader);

      myReader.Close();
      if (myCon.State == ConnectionState.Open)
        await myCon.CloseAsync();
      return (int)dataTable.Rows[0]["Id"];
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
