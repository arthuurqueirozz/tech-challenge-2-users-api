using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using FCG.IntegrationEvents.V1;
using FCG.Users.Api.Configuration;
using FCG.Users.Api.Messaging;
using FCG.Users.Infrastructure.Persistence;
using FCG.Users.Infrastructure.Persistence.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FCG.Users.Tests.Infrastructure;

public sealed class SqsOutboxTests
{
    private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/fcg-fase3-user-created";

    [Fact]
    public async Task FailedSend_RemainsPending_AndRetryUsesOriginalEventId()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellation);
        using var sqs = new RecordingSqs { Fail = true };
        var clock = new MutableTimeProvider();
        var services = new ServiceCollection();
        services.AddDbContext<UsersDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<IAmazonSQS>(sqs);
        services.AddSingleton(Options.Create(new SqsOptions { QueueUrl = QueueUrl }));
        services.AddScoped<IUserCreatedPublisher, SqsUserCreatedPublisher>();
        await using var provider = services.BuildServiceProvider();
        var original = new UserCreatedEvent(Guid.NewGuid(), clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), "Synthetic", "smoke@example.invalid");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            await db.Database.EnsureCreatedAsync(cancellation);
            await new IntegrationEventOutbox(db).AddAsync(original, cancellation);
            await db.SaveChangesAsync(cancellation);
        }
        using var worker = new OutboxPublisherBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), clock,
            NullLogger<OutboxPublisherBackgroundService>.Instance);

        await worker.PublishPendingMessagesAsync(cancellation);
        await using (var scope = provider.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<UsersDbContext>().IntegrationOutboxMessages.SingleAsync(cancellation);
            Assert.Null(row.ProcessedAtUtc);
            Assert.Equal(1, row.AttemptCount);
            Assert.True(row.NextAttemptAtUtc > clock.GetUtcNow().UtcDateTime);
        }
        // Before the backoff has elapsed, the worker must not attempt another send.
        await worker.PublishPendingMessagesAsync(cancellation);
        Assert.Single(sqs.Requests);
        clock.Now = clock.Now.AddMinutes(1);
        sqs.Fail = false;
        await worker.PublishPendingMessagesAsync(cancellation);
        await worker.PublishPendingMessagesAsync(cancellation);

        Assert.Equal(2, sqs.Requests.Count);
        Assert.All(sqs.Requests, request =>
        {
            Assert.Equal(QueueUrl, request.QueueUrl);
            Assert.Equal(original, JsonSerializer.Deserialize<UserCreatedEvent>(request.MessageBody, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Assert.DoesNotContain("messageType", request.MessageBody);
        });
        await using var finalScope = provider.CreateAsyncScope();
        var completed = await finalScope.ServiceProvider.GetRequiredService<UsersDbContext>().IntegrationOutboxMessages.SingleAsync(cancellation);
        Assert.NotNull(completed.ProcessedAtUtc);
        Assert.Null(completed.LastError);
        Assert.Equal(original.EventId, completed.Id);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("http://sqs.us-east-1.amazonaws.com/123456789012/queue", false)]
    [InlineData("https://sqs.eu-west-1.amazonaws.com/123456789012/queue", false)]
    [InlineData(QueueUrl, true)]
    public void QueueConfiguration_RequiresHttpsInConfiguredRegion(string url, bool valid) =>
        Assert.Equal(valid, new SqsOptions { QueueUrl = url }.HasValidQueueUrl());

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingSqs() : AmazonSQSClient(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
    {
        public bool Fail { get; set; }
        public List<SendMessageRequest> Requests { get; } = [];
        public override Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Fail ? Task.FromException<SendMessageResponse>(new AmazonSQSException("Unavailable"))
                : Task.FromResult(new SendMessageResponse());
        }
    }
}
