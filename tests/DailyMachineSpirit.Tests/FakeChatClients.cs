using DailyMachineSpirit.Functions.Generation;
using Microsoft.Extensions.AI;

namespace DailyMachineSpirit.Tests;

/// <summary>
/// Models that give the answers a test lines up, one per request, in order: answer text, an exception to throw, or a
/// function to run first (for something that happens while the model is "thinking"). Records every request.
/// </summary>
public sealed class FakeChatClients : IChatClients
{
    private readonly Dictionary<string, Queue<Func<Task<string>>>> answers = [];

    public List<(string Model, List<ChatMessage> Messages)> Requests { get; } = [];

    public static string Answer(string title, string text, string hereticalTruth)
        => System.Text.Json.JsonSerializer.Serialize(new { title, text, hereticalTruth });

    public FakeChatClients Answers(string model, params string[] texts)
        => Then(model, texts.Select<string, Func<Task<string>>>(text => () => Task.FromResult(text)));

    public FakeChatClients Fails(string model, int times)
        => Then(model, Enumerable.Range(1, times).Select<int, Func<Task<string>>>(
            _ => () => throw new HttpRequestException("The model is overloaded.")));

    public FakeChatClients Then(string model, IEnumerable<Func<Task<string>>> next)
    {
        if (!answers.TryGetValue(model, out var queue))
            answers[model] = queue = new Queue<Func<Task<string>>>();
        foreach (var answer in next)
            queue.Enqueue(answer);
        return this;
    }

    public IChatClient For(string model) => new Client(this, model);

    private sealed class Client(FakeChatClients owner, string model) : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owner.Requests.Add((model, messages.ToList()));
            if (!owner.answers.TryGetValue(model, out var queue) || queue.Count == 0)
                throw new InvalidOperationException($"The test gave {model} no answer for request {owner.Requests.Count}.");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, await queue.Dequeue()()));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey) => null;

        public void Dispose()
        {
        }
    }
}
