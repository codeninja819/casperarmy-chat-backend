using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CasperArmy_Chat.Shared.DTOs
{
    public class NewMessageDTO
    {
        [Required]
        public int groupId { get; set; }
        [Required]
        public int userId { get; set; }
        [Required]
        public string iv { get; set; }
        [Required]
        public string capsule { get; set; }
        [Required]
        public string[] srcData { get; set; }
    }
}
