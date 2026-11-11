namespace FCG.Users.Api.Configuration;

public sealed class SqsOptions
{
    public const string SectionName = "Sqs";
    public string Region { get; set; } = "us-east-1";
    public string QueueUrl { get; set; } = string.Empty;

    public bool HasValidQueueUrl() =>
        !string.IsNullOrWhiteSpace(Region) &&
        Uri.TryCreate(QueueUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
        uri.Host == $"sqs.{Region}.amazonaws.com" &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment) && uri.Segments.Length == 3;
}
