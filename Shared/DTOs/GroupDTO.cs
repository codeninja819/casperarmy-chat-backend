using System.ComponentModel.DataAnnotations;

namespace CasperArmy_Chat.Shared.DTOs
{
    public class GroupCreateDTO
    {
        [Required]
        public string name { get; set; }

        [Required]
        public bool isPublic { get; set; }
    }
}
