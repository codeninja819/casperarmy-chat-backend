namespace CasperArmy_Chat.Data
{
    public static class DbInitializer
    {
        public static void Initialize(DataContext context)
        {
            context.Database.EnsureCreated();
            context.SaveChanges();
        }
    }
}
