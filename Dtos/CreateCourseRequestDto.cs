using System.ComponentModel.DataAnnotations;

namespace TmsApi.Dtos;

public class CreateCourseRequestDto
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int MaxCapacity { get; set; }
}