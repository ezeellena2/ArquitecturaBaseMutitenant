using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Resources;
using FluentValidation;

namespace ArquitecturaBaseMultitenant.Application.Common.Validation;

/// <summary>Valida la forma base64url del cursor; la tupla interna se valida en E2 con CursorCodec.</summary>
public abstract class CursorRequestValidator<TRequest> : AbstractValidator<TRequest>
    where TRequest : CursorRequest
{
    protected CursorRequestValidator()
    {
        RuleFor(request => request.Limit)
            .InclusiveBetween(1, CursorRequest.MaxLimit).WithMessage(_ => ValidationTexts.CursorLimitInvalid);

        RuleFor(request => request.After)
            .Must(IsBase64Url).WithMessage(_ => ValidationTexts.CursorInvalid);
    }

    private static bool IsBase64Url(string? cursor) =>
        cursor is null || (cursor.Length > 0 && cursor.Length % 4 != 1 && cursor.All(character =>
            character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'));
}
