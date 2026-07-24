namespace TmsApi.Domain.Entities;

public class Assessment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CourseId { get; set; }
}