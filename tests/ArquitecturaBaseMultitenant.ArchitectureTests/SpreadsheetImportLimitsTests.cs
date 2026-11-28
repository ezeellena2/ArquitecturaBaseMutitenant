using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Cada tope sale de una sola constante (lista de control 17): el cuerpo multipart de una ruta que recibe una planilla es el
/// tope del archivo más el margen de <see cref="SpreadsheetLimits"/>, así subir el del archivo sube también el que corta Kestrel.
/// </summary>
public sealed class SpreadsheetImportLimitsTests
{
    [Fact]
    public void Multipart_limits_of_the_import_routes_come_from_the_spreadsheet_file_limit()
    {
        var limited = typeof(AccessAttribute).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(method => (Method: method,
                Size: method.GetCustomAttributesData().SingleOrDefault(data => data.AttributeType == typeof(RequestSizeLimitAttribute)),
                Form: method.GetCustomAttribute<RequestFormLimitsAttribute>()))
            .Where(route => route.Size is not null && route.Method.GetCustomAttribute<ConsumesAttribute>()?.ContentTypes
                .Contains("multipart/form-data") == true)
            .ToArray();

        Assert.NotEmpty(limited);
        Assert.All(limited, route =>
        {
            var bytes = Assert.IsType<long>(route.Size!.ConstructorArguments[0].Value);
            Assert.Equal(SpreadsheetLimits.MaxRequestBytes, bytes);
            Assert.Equal(bytes, route.Form?.MultipartBodyLengthLimit);
        });
        Assert.Equal(SpreadsheetLimits.MaxFileBytes, FormCatalogLimits.MaxFileBytes);
        Assert.Equal((long)SpreadsheetLimits.MaxFileBytes + SpreadsheetLimits.MultipartOverheadBytes, SpreadsheetLimits.MaxRequestBytes);
    }
}
