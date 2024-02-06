using System.Text.Json.Serialization;

namespace CasperArmy_Chat.Entities
{
  public class Message
  {
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public string Cipher { get; set; }
    // public DateTime Timestamp { get; set; } // TODO: add timestamp to msg in front

    [JsonIgnore]
    public string OnetimeKey { get; set; }  // TODO: rename this as SecretKey

    public string IV { get; set; }

    public bool Deleted { get; set; } = false;
  }
}
