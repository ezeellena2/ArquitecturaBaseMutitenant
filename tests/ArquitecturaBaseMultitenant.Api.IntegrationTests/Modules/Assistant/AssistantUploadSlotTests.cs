using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

// ADR 0064 §2 and §4: before the body of an upload is read, the quota (429 as always, nothing kept), then one slot of the person
// among all copies, with no queue, then one of the copy, and the body has its time.
[Collection(ApiTestGroup.Name)]
public sealed class AssistantUploadSlotTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=");
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(20);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_the_daily_quota_or_the_monthly_limit_the_upload_gets_429_and_nothing_is_kept()
    {
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false);
        var conversation = await session.ConversationAsync();
        await SpendTodayAsync(session);

        using (var daily = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, Guid.NewGuid(), Png))
        {
            using var problem = await AssertWaitAsync(daily, AssistantErrors.LimitDailyReachedCode);
            Assert.Equal(1, problem.RootElement.GetProperty("dailyLimit").GetInt32());
            Assert.True(problem.RootElement.GetProperty("retryAfter").GetInt32() > 0);
            Assert.True(problem.RootElement.TryGetProperty("dailyResetAtUtc", out _));
        }

        await LimitsAsync(session, dailyLimit: 50, budget: 0m);
        using (var monthly = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, Guid.NewGuid(), Png))
            (await AssertWaitAsync(monthly, AssistantErrors.LimitMonthlyReachedCode)).Dispose();

        // Nothing was read into the store nor written for the conversation.
        Assert.Empty(await session.Host.Services.GetRequiredService<IFileStoreMaintenance>()
            .ListUnconfirmedAsync("assistant", session.Host.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(1), Ct));
        using var scope = session.Host.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(session.TenantId!.Value);
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantAttachment>()
            .AnyAsync(row => row.ConversationId == conversation.Id, Ct));

        // With quota again, the same upload is read and kept.
        await LimitsAsync(session, dailyLimit: 50, budget: 1000m);
        using var accepted = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, Guid.NewGuid(), Png);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task A_second_upload_of_the_same_person_gets_429_at_once_another_person_still_uploads_and_the_key_replays_its_file()
    {
        using var slots = new ObservedSlots();
        await using var session = await AssistantTurnTestSession.CreateAsync(workerEnabled: false, configure: slots.Install);
        var conversation = await session.ConversationAsync();
        using var held = new HeldUpload(conversation.Version);
        var firstKey = Guid.NewGuid();
        var first = SendAsync(session.Client, session.Path, conversation.Id, firstKey, held);
        await slots.WaitTakenAsync();

        // The copy has free slots, but the person has one, with no queue.
        using (var second = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, Guid.NewGuid(), Png).WaitAsync(Soon, Ct))
            (await AssertWaitAsync(second, AssistantErrors.AttachmentUploadBusyCode, retryAfter: 1)).Dispose();

        using var other = AssistantTurnTestSession.NewClient(session.Host);
        await AccountJourney.RegisterAsync(session.Host, other, Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(other, session.Path + "/notice");
        var otherConversation = await ConversationAsync(other, session.Path);
        using (var another = await UploadAsync(other, session.Path, otherConversation.Id, otherConversation.Version, Guid.NewGuid(), Png).WaitAsync(Soon, Ct))
            Assert.Equal(HttpStatusCode.Created, another.StatusCode);

        // The held body arrives: the upload ends as always and gives its slots back.
        held.Release();
        using var firstResponse = await first.WaitAsync(Soon, Ct);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var body = await firstResponse.Content.ReadAsStringAsync(Ct);

        // The same key with the same file replays it; with another file, 422.
        using (var replay = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, firstKey, Png))
        {
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal(body, await replay.Content.ReadAsStringAsync(Ct));
        }
        using (var changed = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, firstKey, [.. Png, 0]))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode);
            using var problem = JsonDocument.Parse(await changed.Content.ReadAsStringAsync(Ct));
            Assert.Equal("Request.IdempotencyKeyReused", problem.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task With_the_copy_s_only_slot_taken_another_person_gets_429_queue_full_and_a_body_that_does_not_arrive_gets_400()
    {
        using var slots = new ObservedSlots();
        await using var session = await AssistantTurnTestSession.CreateAsync(workerEnabled: false, configure: builder =>
        {
            builder.UseSetting("Assistant:Uploads:ConcurrencyLimit", "1");
            builder.UseSetting("Assistant:Uploads:QueueLimit", "0");
            builder.UseSetting("Assistant:Uploads:BodyTimeout", "00:00:03");
            slots.Install(builder);
        });
        var conversation = await session.ConversationAsync();
        using var other = AssistantTurnTestSession.NewClient(session.Host);
        await AccountJourney.RegisterAsync(session.Host, other, Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(other, session.Path + "/notice");
        var otherConversation = await ConversationAsync(other, session.Path);
        using var held = new HeldUpload(conversation.Version);
        var sent = System.Diagnostics.Stopwatch.StartNew();
        var stalled = SendAsync(session.Client, session.Path, conversation.Id, Guid.NewGuid(), held);
        await slots.WaitTakenAsync();

        using (var refused = await UploadAsync(other, session.Path, otherConversation.Id, otherConversation.Version, Guid.NewGuid(), Png).WaitAsync(Soon, Ct))
            (await AssertWaitAsync(refused, AssistantErrors.AttachmentUploadQueueFullCode, retryAfter: 1)).Dispose();

        // The held body never arrives: its time ends with 400 UploadTimedOut and both slots go back, long before the 20 s
        // of this wait. The in-memory server, like Kestrel once its drain gives up, cuts a request whose body is still
        // arriving when the response ends, so the client may see that cut instead of the 400.
        try
        {
            using var timedOut = await stalled.WaitAsync(Soon, Ct);
            Assert.Equal(HttpStatusCode.BadRequest, timedOut.StatusCode);
            using var problem = JsonDocument.Parse(await timedOut.Content.ReadAsStringAsync(Ct));
            Assert.Equal(AssistantErrors.AttachmentUploadTimedOutCode, problem.RootElement.GetProperty("code").GetString());
        }
        catch (OperationCanceledException) when (!Ct.IsCancellationRequested)
        {
            // Cut by the server on its time; the slots are checked below.
        }
        Assert.True(sent.Elapsed >= TimeSpan.FromSeconds(2), "The body keeps its slots until its time ends.");
        using var accepted = await UploadAsync(other, session.Path, otherConversation.Id, otherConversation.Version, Guid.NewGuid(), Png).WaitAsync(Soon, Ct);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using var again = await UploadAsync(session.Client, session.Path, conversation.Id, conversation.Version, Guid.NewGuid(), Png).WaitAsync(Soon, Ct);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    private static async Task<JsonDocument> AssertWaitAsync(HttpResponseMessage response, string code, int? retryAfter = null)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        var seconds = problem.RootElement.GetProperty("retryAfter").GetInt32();
        Assert.Equal(TimeSpan.FromSeconds(seconds), response.Headers.RetryAfter?.Delta);
        if (retryAfter is { } expected) Assert.Equal(expected, seconds);
        return problem;
    }

    private static async Task SpendTodayAsync(AssistantTurnTestSession session)
    {
        await LimitsAsync(session, dailyLimit: 1, budget: 1000m);
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
        {
            var today = session.QuotaDay(services);
            var quota = AssistantPlatformKeyDailyQuota.Create(session.Account.UserId, today.OrganizationId, today.Day);
            quota.Reserve();
            quota.Consume();
            services.GetRequiredService<ApplicationDbContext>().Add(quota);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }

    private static async Task LimitsAsync(AssistantTurnTestSession session, int dailyLimit, decimal budget)
    {
        using var scope = session.Host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var settings = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformSettings>().SingleAsync(ct);
            settings.Update(settings.DefaultModelId, settings.AllowedModelIds, settings.Effort, dailyLimit,
                new Money(budget, CurrencyCode.Create("USD").Value), true, 30);
            services.GetRequiredService<ISecurityEventRepository>().Add(Domain.Auditing.SecurityEvent.ForPlatformSettings(
                Domain.Auditing.AuditActorKind.System, Guid.Empty, "Assistant upload quota test",
                services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }

    private static async Task<Application.Modules.Assistant.Models.AssistantConversationSummaryResponse> ConversationAsync(HttpClient client, string path)
    {
        using var request = AssistantTurnTestSession.Request(HttpMethod.Post, path + "/conversations", "{}", Guid.NewGuid());
        using var response = await client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonSerializer.Deserialize<Application.Modules.Assistant.Models.AssistantConversationSummaryResponse>(
            await response.Content.ReadAsStringAsync(Ct), AssistantTurnTestSession.Json)!;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, Guid conversation, uint version, Guid key, byte[] bytes)
    {
        var content = new MultipartFormDataContent("assistant-upload-slot-boundary");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "foto.png");
        content.Add(new StringContent(System.Xml.XmlConvert.ToString(version)), "conversationVersion");
        return SendAsync(client, path, conversation, key, content);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, Guid conversation, Guid key, HttpContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{path}/conversations/{conversation:D}/attachments") { Content = content };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request, Ct);
    }

    // The real slots in Redis, observed: each slot taken is counted, so the test knows an upload holds its slots.
    private sealed class ObservedSlots : IDisposable
    {
        private readonly SemaphoreSlim _taken = new(0);

        public void Dispose() => _taken.Dispose();

        public void Install(IWebHostBuilder builder) => builder.ConfigureTestServices(services =>
            services.AddSingleton<IExclusiveSlots>(provider => new Observer(ActivatorUtilities.CreateInstance<RedisExclusiveSlots>(provider), _taken)));

        public Task WaitTakenAsync() => _taken.WaitAsync(Soon, Ct).ContinueWith(task => Assert.True(task.Result), TaskScheduler.Default);

        private sealed class Observer(IExclusiveSlots inner, SemaphoreSlim taken) : IExclusiveSlots
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
        private const string Boundary = "held-upload";
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
