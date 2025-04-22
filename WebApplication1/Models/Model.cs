namespace WebApplication1.Models
{
    public class Model
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string IdToken { get; set; }
        public string RealmId { get; set; }
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
