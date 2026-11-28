using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.Messaging;

/// <summary>
/// ADR 0063: las 24 horas móviles por horas. Cuentan las 24 horas cerradas y la actual; lo de antes no cuenta, y la espera
/// llega hasta que sale de la ventana la hora que deja un lugar libre.
/// </summary>
public sealed class MessageQuotaWindowTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 10, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime ThisHour = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_window_counts_the_current_hour_and_the_24_closed_ones_before_it()
    {
        MessageQuotaHour[] hours =
        [
            new(ThisHour.AddHours(-25), 7), // salió de la ventana
            new(ThisHour.AddHours(-24), 2),
            new(ThisHour.AddHours(-1), 3),
            new(ThisHour, 1),
        ];

        Assert.Equal(ThisHour, MessageQuotaWindow.HourOf(Now));
        Assert.Equal(ThisHour.AddHours(-24), MessageQuotaWindow.FirstHour(Now));
        Assert.Equal(6, MessageQuotaWindow.Count(hours, Now));
        Assert.Null(MessageQuotaWindow.WaitSeconds(hours, 7, Now));
    }

    [Fact]
    public void A_full_window_waits_until_the_oldest_hour_that_frees_a_place_leaves_it()
    {
        MessageQuotaHour[] hours = [new(ThisHour.AddHours(-24), 2), new(ThisHour.AddHours(-3), 3), new(ThisHour, 1)];

        // Lleno con 6: a las 11:00 sale la hora de las 10:00 de ayer y quedan 4.
        Assert.Equal(45 * 60, MessageQuotaWindow.WaitSeconds(hours, 6, Now));
        Assert.Equal(45 * 60, MessageQuotaWindow.WaitSeconds(hours, 5, Now));
        // Con tope 4 hace falta que salgan también las 3 de las 07:00 de hoy: mañana a las 08:00.
        Assert.Equal((21 * 60 + 45) * 60, MessageQuotaWindow.WaitSeconds(hours, 4, Now));
        // Con tope 1 hace falta que salga también la hora actual: mañana a las 11:00.
        Assert.Equal((24 * 60 + 45) * 60, MessageQuotaWindow.WaitSeconds(hours, 1, Now));
    }

    [Fact]
    public void The_window_moves_by_hours_and_the_purge_keeps_what_still_counts()
    {
        MessageQuotaHour[] hours = [new(ThisHour.AddHours(-24), 5)];
        var later = ThisHour.AddHours(1).AddSeconds(1);

        Assert.Equal(1, MessageQuotaWindow.WaitSeconds(hours, 5, ThisHour.AddHours(1).AddSeconds(-1)));
        Assert.Null(MessageQuotaWindow.WaitSeconds(hours, 5, later));
        Assert.True(MessageQuotaWindow.PurgeBefore(Now) > ThisHour.AddHours(-25));
        Assert.True(MessageQuotaWindow.PurgeBefore(Now) < ThisHour.AddHours(-24));
        Assert.Throws<ArgumentException>(() => MessageQuotaWindow.HourOf(DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void A_batch_waits_until_enough_hours_leave_or_a_full_window_when_it_exceeds_the_limit()
    {
        MessageQuotaHour[] hours = [new(ThisHour.AddHours(-24), 2), new(ThisHour.AddHours(-3), 3), new(ThisHour, 1)];

        // Con 6 usados y tope 7: 1 entra; 2 necesitan que salgan los 2 de ayer a las 10:00 (45 min).
        Assert.Null(MessageQuotaWindow.WaitSeconds(hours, 7, Now, amount: 1));
        Assert.Equal(45 * 60, MessageQuotaWindow.WaitSeconds(hours, 7, Now, amount: 2));
        // 4 necesitan que salgan también los 3 de hoy a las 07:00: mañana a las 08:00.
        Assert.Equal((21 * 60 + 45) * 60, MessageQuotaWindow.WaitSeconds(hours, 7, Now, amount: 4));
        // 7 necesitan la ventana vacía: mañana a las 11:00.
        Assert.Equal((24 * 60 + 45) * 60, MessageQuotaWindow.WaitSeconds(hours, 7, Now, amount: 7));
        // Un lote mayor al tope diario entero no entra ni con la ventana vacía: espera la ventana completa sin fallar.
        Assert.Equal((24 * 60 + 45) * 60, MessageQuotaWindow.WaitSeconds(hours, 7, Now, amount: 8));
        Assert.Equal((24 * 60 + 45) * 60, MessageQuotaWindow.WaitSeconds([], 7, Now, amount: 8));
    }
}
