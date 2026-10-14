using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Una opción del menú del bot: pasa a un equipo o contesta un texto (ADR 0038). La del sistema de Plataforma no se guarda.</summary>
public sealed record WhatsAppBotOption(Guid Id, string Label, WhatsAppBotAction Action, Guid? TeamId, string? Reply);

/// <summary>Un día de atención en la zona del bot: desde la hora de apertura (incluida) hasta la de cierre (excluida).</summary>
public sealed record WhatsAppBotHours(DayOfWeek Day, TimeOnly From, TimeOnly To);

/// <summary>
/// El bot de un número (ADR 0038): bienvenida y fuera de horario, el horario en una zona del catálogo y un menú que WhatsApp
/// muestra como lista de hasta 10 opciones. No se apaga (D9): con el número conectado siempre responde.
/// </summary>
public abstract class WhatsAppBotBase : Entity, IVersioned, IAuditable
{
    private protected List<WhatsAppBotHours> _hours = [];
    private protected List<WhatsAppBotOption> _options = [];

    protected WhatsAppBotBase() { }
    protected WhatsAppBotBase(Guid id) : base(id) { }

    public uint Version { get; private set; }
    public string Welcome { get; private set; } = string.Empty;
    public string OutOfHours { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public IReadOnlyList<WhatsAppBotHours> Hours => _hours.AsReadOnly();
    public IReadOnlyList<WhatsAppBotOption> Options => _options.AsReadOnly();
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    /// <summary>Las opciones que agrega el sistema y ocupan lugar en la lista (Plataforma: «Entrar a mi cuenta»).</summary>
    protected virtual int SystemOptions => 0;

    /// <summary>Reemplaza textos, horario y menú. Las opciones conservan su identificador: es el de la fila de la lista.</summary>
    protected Result Apply(string welcome, string outOfHours, string timeZoneId, IEnumerable<WhatsAppBotHours> days,
        IEnumerable<WhatsAppBotOption> menu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(welcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(outOfHours);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        var nextOptions = menu.ToList();
        if (nextOptions.Count + SystemOptions > WhatsAppFeature.MaxMenuOptions) return Result.Failure(WhatsAppErrors.BotTooManyOptions);
        var nextHours = days.OrderBy(day => ((int)day.Day + 6) % 7).ToList();
        if (welcome.Length > WhatsAppTextLimits.BotText || outOfHours.Length > WhatsAppTextLimits.BotText
            || timeZoneId.Length > TextLimits.ShortName) throw new ArgumentException("A bot text is too long.");
        if (nextHours.Select(day => day.Day).Distinct().Count() != nextHours.Count || nextHours.Any(day => day.From >= day.To))
            throw new ArgumentException("Invalid opening hours.", nameof(days));
        if (nextOptions.Select(option => option.Id).Distinct().Count() != nextOptions.Count || nextOptions.Any(option => !IsValid(option)))
            throw new ArgumentException("Invalid menu option.", nameof(menu));
        Welcome = welcome.Trim(); OutOfHours = outOfHours.Trim(); TimeZoneId = timeZoneId;
        _hours = nextHours; _options = nextOptions;
        return Result.Success();
    }

    /// <summary>Si la hora local, en la zona del bot, cae en un día y horario de atención.</summary>
    public bool IsOpenAt(DateTime local)
    {
        var time = TimeOnly.FromDateTime(local);
        return _hours.Exists(day => day.Day == local.DayOfWeek && time >= day.From && time < day.To);
    }

    public WhatsAppBotOption? FindOption(Guid id) => _options.Find(option => option.Id == id);

    /// <summary>El equipo se borró: sus opciones salen del menú.</summary>
    public virtual bool LeaveTeam(Guid teamId)
    {
        var next = _options.Where(option => option.TeamId != teamId).ToList();
        if (next.Count == _options.Count) return false;
        _options = next;
        return true;
    }

    private static bool IsValid(WhatsAppBotOption option) =>
        option.Id != Guid.Empty && option.Label is { Length: > 0 and <= WhatsAppTextLimits.MenuLabel } && Enum.IsDefined(option.Action)
        && (option.Action == WhatsAppBotAction.Team
            ? option.TeamId is { } team && team != Guid.Empty && option.Reply is null
            : option.TeamId is null && option.Reply is { Length: > 0 and <= WhatsAppTextLimits.BotText });
}
