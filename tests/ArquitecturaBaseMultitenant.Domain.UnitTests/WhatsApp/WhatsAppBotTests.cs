using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Domain.UnitTests.WhatsApp;

/// <summary>B3 de la E11: el horario se mide en la hora local del bot y un equipo borrado sale del menú y de los empleados.</summary>
public sealed class WhatsAppBotTests
{
    private static readonly WhatsAppBotHours Monday = new(DayOfWeek.Monday, new(9, 0), new(18, 0));

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(17, 59, true)]
    [InlineData(18, 0, false)]
    [InlineData(8, 59, false)]
    public void Opening_hours_include_the_opening_minute_and_exclude_the_closing_one(int hour, int minute, bool open)
    {
        var bot = WhatsAppBot.Create(Guid.CreateVersion7());
        Assert.True(bot.Update("Hola", "Cerrado", "America/Argentina/Buenos_Aires", [Monday], [], false, false, null).IsSuccess);
        // El 12/10/2026 es lunes; el 13, martes.
        Assert.Equal(open, bot.IsOpenAt(new DateTime(2026, 10, 12, hour, minute, 0)));
        Assert.False(bot.IsOpenAt(new DateTime(2026, 10, 13, hour, minute, 0)));
    }

    [Fact]
    public void A_deleted_team_leaves_the_menu_and_the_employees_option()
    {
        var team = Guid.CreateVersion7();
        var bot = WhatsAppBot.Create(Guid.CreateVersion7());
        bot.Update("Hola", "Cerrado", "America/Argentina/Buenos_Aires", [Monday],
            [new(Guid.CreateVersion7(), "Ventas", WhatsAppBotAction.Team, team, null), new(Guid.CreateVersion7(), "Horarios", WhatsAppBotAction.Reply, null, "De 9 a 18")],
            false, false, team);
        Assert.True(bot.LeaveTeam(team));
        Assert.Equal(["Horarios"], bot.Options.Select(option => option.Label));
        Assert.Null(bot.EmployeesTeamId);
        Assert.False(bot.LeaveTeam(team));
    }

    [Fact]
    public void The_platform_system_option_takes_one_of_the_ten_places()
    {
        var bot = PlatformWhatsAppBot.Create();
        var menu = Enumerable.Range(1, 10).Select(index => new WhatsAppBotOption(Guid.CreateVersion7(), "Opción " + index, WhatsAppBotAction.Reply, null, "Texto")).ToArray();
        Assert.Equal(WhatsAppErrors.BotTooManyOptionsCode, bot.Update("Hola", "Cerrado", "America/Argentina/Buenos_Aires", [Monday], menu).Error.Code);
        Assert.True(bot.Update("Hola", "Cerrado", "America/Argentina/Buenos_Aires", [Monday], menu[..9]).IsSuccess);
    }
}
