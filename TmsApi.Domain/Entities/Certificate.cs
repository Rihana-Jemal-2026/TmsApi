namespace TmsApi.Domain.Entities;

public class Certificate
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}