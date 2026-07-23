using System.Text;
using FluentValidation;
using Mems.Application.Auth;
using Mems.Application.Receipts;
using Mems.Application.Workflow;

namespace Mems.Tests.Receipts;

public sealed class ReceiptServiceTests
{
    private static readonly AuthUser Employee = new("ayesha@example.test", "Ayesha", UserRole.Employee, "DEMO-M-002");
    private static readonly AuthUser OtherEmployee = new("other@example.test", "Omar", UserRole.Employee, "DEMO-M-999");
    private static readonly AuthUser Manager = new("manager@example.test", "Bilal", UserRole.LineManager, null);

    // A real 1×1 PNG so the magic-byte sniff passes.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly FakeReceiptRepository _receipts = new();
    private readonly FakeClaimRepository _claims = new();
    private readonly FakePdfRenderer _pdf = new();
    private readonly ReceiptService _service;

    public ReceiptServiceTests()
    {
        _service = new ReceiptService(_receipts, _claims, _pdf);
    }

    // ---- Upload ----

    [Fact]
    public async Task Upload_rejects_non_image_content_types()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Upload(Employee, "notes.txt", "text/plain", TinyPng));
    }

    [Fact]
    public async Task Upload_rejects_oversized_files()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadAsync(Employee, "big.png", "image/png", new MemoryStream(TinyPng), ReceiptService.MaxSizeBytes + 1));
    }

    [Fact]
    public async Task Upload_rejects_bytes_that_do_not_match_the_declared_type()
    {
        var notAnImage = Encoding.UTF8.GetBytes("hello, definitely not a png");
        await Assert.ThrowsAsync<ValidationException>(() => Upload(Employee, "fake.png", "image/png", notAnImage));
    }

    [Fact]
    public async Task Upload_rejects_non_employees()
    {
        await Assert.ThrowsAsync<WorkflowForbiddenException>(() => Upload(Manager, "r.png", "image/png", TinyPng));
    }

    [Fact]
    public async Task Upload_stores_a_valid_image_and_returns_its_id()
    {
        var result = await Upload(Employee, "receipt.png", "image/png", TinyPng);

        Assert.Equal("receipt.png", result.FileName);
        var stored = await _receipts.GetAsync(result.ReceiptId);
        Assert.NotNull(stored);
        Assert.Equal("DEMO-M-002", stored!.UploadedByEmployeeId);
        Assert.Equal(TinyPng, stored.Content);
    }

    // ---- Viewing ----

    [Fact]
    public async Task Owner_and_approvers_can_view_but_other_employees_cannot()
    {
        var id = (await Upload(Employee, "receipt.png", "image/png", TinyPng)).ReceiptId;

        Assert.Equal("image/png", (await _service.GetForViewAsync(id, Employee)).ContentType);
        Assert.Equal("image/png", (await _service.GetForViewAsync(id, Manager)).ContentType);
        await Assert.ThrowsAsync<WorkflowForbiddenException>(() => _service.GetForViewAsync(id, OtherEmployee));
    }

    [Fact]
    public async Task Viewing_a_missing_receipt_is_not_found()
    {
        await Assert.ThrowsAsync<ReceiptNotFoundException>(() => _service.GetForViewAsync(Guid.NewGuid(), Manager));
    }

    // ---- Per-line PDF ----

    [Fact]
    public async Task Employees_cannot_download_the_line_pdf()
    {
        var (claim, lineId) = await SeedClaimWithReceipt();
        await Assert.ThrowsAsync<WorkflowForbiddenException>(() => _service.GetLineReceiptPdfAsync(claim.Id, lineId, Employee));
    }

    [Fact]
    public async Task Approvers_get_a_pdf_with_the_stored_image()
    {
        var (claim, lineId) = await SeedClaimWithReceipt();

        var pdf = await _service.GetLineReceiptPdfAsync(claim.Id, lineId, Manager);

        Assert.Equal(FakePdfRenderer.Marker, pdf.Content);
        Assert.EndsWith("item-1-receipt.pdf", pdf.FileName);
        Assert.NotNull(_pdf.LastModel);
        Assert.Equal(TinyPng, _pdf.LastModel!.Images.Single().Content);
    }

    [Fact]
    public async Task The_pdf_model_carries_every_image_of_the_line_in_order()
    {
        var first = (await Upload(Employee, "one.png", "image/png", TinyPng)).ReceiptId;
        var second = (await Upload(Employee, "two.png", "image/png", TinyPng)).ReceiptId;
        var claim = SeedClaim(first, second);

        await _service.GetLineReceiptPdfAsync(claim.Id, claim.Lines[0].Id, Manager);

        Assert.Equal(new[] { "one.png", "two.png" }, _pdf.LastModel!.Images.Select(i => i.FileName));
    }

    [Fact]
    public async Task A_legacy_line_without_a_stored_image_still_renders()
    {
        var claim = SeedClaim();

        var pdf = await _service.GetLineReceiptPdfAsync(claim.Id, claim.Lines[0].Id, Manager);

        Assert.Equal(FakePdfRenderer.Marker, pdf.Content);
        Assert.Empty(_pdf.LastModel!.Images);
    }

    // ---- helpers / fakes ----

    private Task<ReceiptUploadResponse> Upload(AuthUser actor, string name, string type, byte[] bytes)
        => _service.UploadAsync(actor, name, type, new MemoryStream(bytes), bytes.LongLength);

    private async Task<(ClaimRecord Claim, Guid LineId)> SeedClaimWithReceipt()
    {
        var receiptId = (await Upload(Employee, "receipt.png", "image/png", TinyPng)).ReceiptId;
        var claim = SeedClaim(receiptId);
        return (claim, claim.Lines[0].Id);
    }

    private ClaimRecord SeedClaim(params Guid[] receiptIds)
    {
        var claim = new ClaimRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = "DEMO-M-002",
            Stage = ApprovalStage.LineManager,
            Lines = new List<ClaimLineRecord>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Category = "Opd",
                    ExpenseDate = new DateOnly(2026, 7, 20),
                    ClaimedAmount = 1_000m,
                    CurrentAmount = 1_000m,
                    ReceiptReference = receiptIds.Length > 0 ? "receipt.png" : null,
                    ReceiptIds = receiptIds.ToList(),
                },
            },
        };
        _claims.Store[claim.Id] = claim;
        return claim;
    }

    private sealed class FakeReceiptRepository : IReceiptRepository
    {
        private readonly Dictionary<Guid, ReceiptRecord> _store = new();

        public Task AddAsync(ReceiptRecord receipt, CancellationToken ct = default)
        {
            _store[receipt.Id] = receipt;
            return Task.CompletedTask;
        }

        public Task<ReceiptRecord?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Guid>> GetOwnedExistingIdsAsync(
            IReadOnlyCollection<Guid> ids, string employeeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(
                _store.Values.Where(r => ids.Contains(r.Id) && r.UploadedByEmployeeId == employeeId).Select(r => r.Id).ToList());

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeClaimRepository : IClaimRepository
    {
        public Dictionary<Guid, ClaimRecord> Store { get; } = new();

        public Task<ClaimRecord?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.GetValueOrDefault(id));

        public Task<IReadOnlyList<ClaimRecord>> GetByEmployeeAsync(string employeeId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimRecord>>(new List<ClaimRecord>());

        public Task<IReadOnlyList<ClaimRecord>> GetByStageAsync(ApprovalStage stage, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimRecord>>(new List<ClaimRecord>());

        public Task<IReadOnlyList<ClaimRecord>> GetPostableAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimRecord>>(new List<ClaimRecord>());

        public Task AddAsync(ClaimRecord claim, CancellationToken ct = default)
        {
            Store[claim.Id] = claim;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakePdfRenderer : IReceiptPdfRenderer
    {
        public static readonly byte[] Marker = { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"

        public ReceiptPdfModel? LastModel { get; private set; }

        public byte[] Render(ReceiptPdfModel model)
        {
            LastModel = model;
            return Marker;
        }
    }
}
