namespace TmsApi.Application.Hubs;

public interface ITmsHubClient
{
    Task ReceiveEnrollmentStatusUpdated(string enrollmentId, string status);
    Task ReceiveTranscriptReady(string studentId);
    Task ReceiveCourseUpdate(string courseId);
    Task ReceiveGradePosted(string gradeId);
}
