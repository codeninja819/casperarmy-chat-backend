namespace CasperArmy_Chat.Entities
{
    public class Group
    {
        public int Id { get; set; }

        public string Name { get; set; }//Unique
        public bool IsPublic { get; set; }
        public int AdminId { get; set; }
        
        public string SharedKey { get; set; }
    }
}
