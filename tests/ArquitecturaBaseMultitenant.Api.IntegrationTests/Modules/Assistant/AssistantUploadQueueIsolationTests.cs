using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

// ADR 0064 §4 (adversarial review of B12): the copy's queue is shared by organizations, and one never leaves another without a
// place. The personal side is not an organization: its people must not take, between them, every place an organization needs.
[Collection(ApiTestGroup.Name)]
public sealed class AssistantUploadQueueIsolationTests
{
    private const string PersonalPath = "/api/me/assistant";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(20);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task People_of_the_personal_side_do_not_take_every_queue_place_an_organization_still_needs()
    {
        using var slots = new CountedSlots();
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false, configure: builder =>
        {
            builder.UseSetting("Assistant:Uploads:ConcurrencyLimit", "1");
            builder.UseSetting("Assistant:Uploads:QueueLimit", "2");
            builder.UseSetting("Assistant:Uploads:QueuePerOrganization", "1");
            slots.Install(builder);
        });
        var conversation = await session.ConversationAsync();
        var people = new List<(HttpClient Client, AssistantConversationSummaryResponse Conversation)>();
        try
        {
            for (var index = 0; index < 3; index++)
            {
                var client = AssistantTurnTestSession.NewClient(session.Host);
                await AccountJourney.RegisterAsync(session.Host, client, Ct);
                await AssistantExecutionActorTests.AcceptNoticeAsync(client, PersonalPath + "/notice");
                people.Add((client, await ConversationAsync(client, PersonalPath)));
            }

            // One person of the personal side holds the copy's only slot and another waits. The personal side is one part of the
            // queue, not one per personal space: a third person finds its place taken.
            using var held = new HeldUpload(people[0].Conversation.Version);
            var running = SendAsync(people[0].Client, PersonalPath, people[0].Conversation.Id, Guid.NewGuid(), held);
            await slots.WaitTakenAsync();
            await Task.Delay(500, Ct);
            var second = UploadAsync(people[1].Client, PersonalPath, people[1].Conversation.Id, people[1].Conversation.Version);
            await slots.WaitTakenAsync();
            await Task.Delay(500, Ct);
            using (var third = await UploadAsync(people[2].Client, PersonalPath, people[2].Conversation.Id, people[2].Conversation.Version)
                .WaitAsync(Soon, Ct))
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
                Assert.Contains("Assistant.Attachment.UploadQueueFull", await third.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
            }

            // The organization has not used its place in the queue: its upload waits for the slot, it is not refused.
            var business = UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version);
            if (await Task.WhenAny(business, Task.Delay(TimeSpan.FromSeconds(2), Ct)) == business)
            {
                using var early = await business;
                var body = await early.Content.ReadAsStringAsync(Ct);
                Assert.Fail($"The organization's upload was answered at once with {(int)early.StatusCode} {body}: the personal side took every place of the queue.");
            }

            held.Release();
            using var businessResponse = await business.WaitAsync(Soon, Ct);
            Assert.Equal(HttpStatusCode.Created, businessResponse.StatusCode);
            (await running.WaitAsync(Soon, Ct)).Dispose();
            (await second.WaitAsync(Soon, Ct)).Dispose();
        }
        finally
        {
            foreach (var (client, _) in people) client.Dispose();
        }
    }

    private static async Task<AssistantConversationSummaryResponse> ConversationAsync(HttpClient client, string path)
    {
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post, path + "/conversations", "{}", Guid.NewGuid());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonSerializer.Deserialize<AssistantConversationSummaryResponse>(await response.Content.ReadAsStringAsync(Ct), AssistantTurnTestSession.Json)!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, Guid conversation, uint version)
    {
        var content = new MultipartFormDataContent("assistant-upload-isolation-boundary");
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "foto.png");
        content.Add(new StringContent(System.Xml.XmlConvert.ToString(version)), "conversationVersion");
        return SendAsync(client, path, conversation, Guid.NewGuid(), content);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, Guid conversation, Guid key, HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{path}/conversations/{conversation:D}/attachments") { Content = content };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    // The real slots in Redis, counted: the test knows when an upload holds the person's slot.
    private sealed class CountedSlots : IDisposable
    {
        private readonly SemaphoreSlim _taken = new(0);

        public void Dispose() => _taken.Dispose();

        public void Install(IWebHostBuilder builder) => builder.ConfigureTestServices(services =>
            services.AddSingleton<IExclusiveSlots>(provider => new Counter(ActivatorUtilities.CreateInstance<RedisExclusiveSlots>(provider), _taken)));

        public async Task WaitTakenAsync() => Assert.True(await _taken.WaitAsync(Soon, Ct));

        private sealed class Counter(IExclusiveSlots inner, SemaphoreSlim taken) : IExclusiveSlots
        {
            public async Task<IAsyncDisposable?> TryTakeAsync(string scope, Guid holderId, CancellationToken cancellationToken)
            {
                var slot = await inner.TryTakeAsync(scope, holderId, cancellationToken);
                if (slot is not null) taken.Release();
                return slot;
            }
        }
    }

    // A multipart body that sends its version and the start of its picture, and the rest only when the test releases it.
    private sealed class HeldUpload : HttpContent
    {
        private const string Boundary = "held-isolation-upload";
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly uint _version;

        public HeldUpload(uint version)
        {
            _version = version;
            Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={Boundary}");
        }

        public void Release() => _released.TrySetResult();

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var start = $"--{Boundary}\r\nContent-Disposition: form-data; name=\"conversationVersion\"\r\n\r\n{_version}\r\n"
                + $"--{Boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"foto.png\"\r\nContent-Type: image/png\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(start));
            await stream.WriteAsync(Png.AsMemory(0, 8));
            await stream.FlushAsync();
            await _released.Task;
            await stream.WriteAsync(Png.AsMemory(8));
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"\r\n--{Boundary}--\r\n"));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            _released.TrySetResult();
            base.Dispose(disposing);
        }
    }
}
