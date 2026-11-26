using ArquitecturaBaseMultitenant.Application.Models.Features;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Features;

/// <summary>
/// La regla de varias filas (ADR 0069): sin filas del módulo vale su valor de entrada; con filas, el módulo está prendido
/// si alguna está vigente. Una prueba vencida no apaga una habilitación manual, y las filas de otro módulo no cuentan.
/// </summary>
public sealed class TenantFeatureRowTests
{
    private const string Key = "fixture";
    private static readonly DateTime NowUtc = new(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Without_rows_of_the_module_uses_its_default(bool defaultEnabled)
    {
        TenantFeatureRow[] rows = [Row("other", true, TenantFeatureSource.Manual, null)];

        Assert.Equal(defaultEnabled, TenantFeatureRow.IsModuleEnabled([], Key, defaultEnabled, NowUtc));
        Assert.Equal(defaultEnabled, TenantFeatureRow.IsModuleEnabled(rows, Key, defaultEnabled, NowUtc));
    }

    [Fact]
    public void A_single_row_decides_over_the_default()
    {
        Assert.True(TenantFeatureRow.IsModuleEnabled([Row(Key, true, TenantFeatureSource.Manual, null)], Key, false, NowUtc));
        Assert.False(TenantFeatureRow.IsModuleEnabled([Row(Key, false, TenantFeatureSource.Manual, null)], Key, true, NowUtc));
    }

    [Fact]
    public void An_expired_trial_does_not_turn_off_a_manual_row()
    {
        TenantFeatureRow[] rows =
        [
            Row(Key, true, TenantFeatureSource.Trial, NowUtc.AddDays(-1)),
            Row(Key, true, TenantFeatureSource.Manual, null),
        ];

        Assert.True(TenantFeatureRow.IsModuleEnabled(rows, Key, false, NowUtc));
    }

    [Fact]
    public void A_manual_row_turned_off_does_not_turn_off_a_current_approved_row()
    {
        TenantFeatureRow[] rows =
        [
            Row(Key, false, TenantFeatureSource.Manual, null),
            Row(Key, true, TenantFeatureSource.Approved, NowUtc.AddDays(30)),
        ];

        Assert.True(TenantFeatureRow.IsModuleEnabled(rows, Key, false, NowUtc));
    }

    [Fact]
    public void An_expired_approved_row_turns_the_module_off_even_when_it_starts_on()
    {
        TenantFeatureRow[] rows = [Row(Key, true, TenantFeatureSource.Approved, NowUtc.AddDays(-1))];

        Assert.False(TenantFeatureRow.IsModuleEnabled(rows, Key, true, NowUtc));
    }

    [Fact]
    public void A_row_expires_exactly_at_its_instant()
    {
        TenantFeatureRow[] rows = [Row(Key, true, TenantFeatureSource.Trial, NowUtc)];

        Assert.True(TenantFeatureRow.IsModuleEnabled(rows, Key, false, NowUtc.AddTicks(-1)));
        Assert.False(TenantFeatureRow.IsModuleEnabled(rows, Key, false, NowUtc));
    }

    [Fact]
    public void A_row_of_another_module_does_not_turn_this_one_on()
    {
        TenantFeatureRow[] rows =
        [
            Row(Key, false, TenantFeatureSource.Manual, null),
            Row(Key + "-other", true, TenantFeatureSource.Subscription, null),
        ];

        Assert.False(TenantFeatureRow.IsModuleEnabled(rows, Key, true, NowUtc));
    }

    private static TenantFeatureRow Row(string key, bool enabled, TenantFeatureSource source, DateTime? expiresAtUtc) =>
        new(key, enabled, source, expiresAtUtc, 1);
}
