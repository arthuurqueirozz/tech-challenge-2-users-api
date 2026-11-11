using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FCG.IntegrationEvents.V1;
using FCG.Users.Api.Configuration;
using Microsoft.Extensions.Options;

namespace FCG.Users.Api.Messaging;

public sealed class SqsUserCreatedPublisher(IAmazonSQS sqs, IOptions<SqsOptions> options)
    : IUserCreatedPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(UserCreatedEvent message, CancellationToken cancellationToken)
    {
        await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = options.Value.QueueUrl,
            MessageBody = JsonSerializer.Serialize(message, JsonOptions)
        }, cancellationToken);
    }
}
