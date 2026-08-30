using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TmsApi.Tests;

public class CoursesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CoursesApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetCourses_ReturnsOkAndPagedJson()
    {
        // Act — request courses route
        var response = await _client.GetAsync("/api/courses?page=1&pageSize=10");

        // Assert — check HTTP status 200 OK
        response.EnsureSuccessStatusCode();

        // TMS API contract check: PagedResponse<T> with items array
        var page = await response.Content.ReadFromJsonAsync<PagedCoursesJson>();
        Assert.NotNull(page?.Items);
    }

    [Fact]
    public async Task CreateCourse_InvalidCode_ReturnsValidationError()
    {
        // Act — post invalid payload (empty code)
        var response = await _client.PostAsJsonAsync("/api/courses", new
        {
            code = "",
            title = "Intro to TMS Security",
            maxCapacity = 30
        });

        // Assert — validation failure or auth failure returns 400, 401, 403, or 422
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }

    private sealed class PagedCoursesJson
    {
        public List<CourseRowJson> Items { get; set; } = default!;
        public int TotalCount { get; set; }
    }

    private sealed class CourseRowJson
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public int MaxCapacity { get; set; }
        public int EnrollmentCount { get; set; }
    }
}
