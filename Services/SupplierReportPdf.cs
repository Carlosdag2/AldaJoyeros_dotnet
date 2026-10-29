using System.Globalization;
using System.Text.Json;
using AldaJoyeros.Services.Munreco;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AldaJoyeros.Services;

public static class SupplierReportPdf
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
    private sealed record Detail(string Reference, string Action, string Description);
    private sealed record Section(string Title, List<Detail> Rows);

    public static async Task<byte[]> GenerateAsync(string provider, string id, string trigger, string state,
        DateTime created, DateTime? ended, string path, CancellationToken ct = default)
    {
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        JsonDocument? comparison = null;
        try
        {
            var comparisonPath = Path.Combine(Path.GetDirectoryName(path)!, "comparacion.json");
            if (Path.GetFileNameWithoutExtension(path) == "resultado" && File.Exists(comparisonPath))
                comparison = JsonDocument.Parse(await File.ReadAllTextAsync(comparisonPath, ct));
            ct.ThrowIfCancellationRequested();
            return await Task.Run(() => Generate(provider, id, trigger, state, created, ended,
                Path.GetFileNameWithoutExtension(path), report.RootElement, comparison?.RootElement), ct);
        }
        finally { comparison?.Dispose(); }
    }

    private static JsonElement Field(JsonElement node, string name) => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) ? value : default;
    private static string Text(JsonElement node, string name) => Field(node, name) is var value && value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : "";
    private static IEnumerable<JsonElement> Items(JsonElement node) => node.ValueKind == JsonValueKind.Array ? node.EnumerateArray() : [];
    private static string Number(JsonElement value) => value.TryGetInt64(out var number) ? number.ToString("N0", Spanish) : value.ToString();
    private static string Money(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount.ToString("C2", Spanish) : value;
    private static string ActionName(string action) => action switch
    {
        "nuevo" => "Nuevo producto", "reposicion" or "repuesto" => "Reposición", "agotado" => "Sin existencias",
        "retirado" => "Fuera del catálogo", "modificado" or "datos_o_pvp_modificados" => "Ficha modificada",
        "sin_cambios" => "Sin cambios", _ => "Revisar producto"
    };

    private static byte[] Generate(string provider, string id, string trigger, string state, DateTime created,
        DateTime? ended, string type, JsonElement report, JsonElement? comparison)
    {
        var preview = type == "comparacion";
        var failed = type == "error" || state is "failed" or "interrupted" or "needs_login";
        var title = type switch { "comparacion" => "Comprobación de cambios", "revision" => "Productos para revisar", "error" => "Incidencia en la actualización", "verificacion-mensual" => "Verificación del catálogo", _ => "Resultado de la actualización" };
        var stateLabel = state switch { "completed" => "Actualización completada", "previewed" => "Comprobación completada", "needs_login" => "Es necesario renovar el acceso", "interrupted" => "Proceso interrumpido", "failed" => "Proceso detenido", _ => "Consultar el estado en el panel" };
        var origin = trigger switch { "weekly" => "Programación semanal", "monthly" => "Programación mensual", "preview_upload" => "Comprobación de Excel subido", "manual_upload" => "Actualización desde Excel", "preview" => "Comprobación manual", _ => "Actualización manual" };
        var summary = new List<(string Label, string Value)>();
        void Metric(JsonElement node, string key, string label)
        {
            var value = Field(node, key);
            if (value.ValueKind == JsonValueKind.Number) summary.Add((label, Number(value)));
        }
        foreach (var (key, label) in new[] {
            ("products", "Referencias consultadas"), ("added", "Productos nuevos"), ("restocked", "Reposiciones"),
            ("soldOut", "Productos sin existencias"), ("removed", "Retirados del catálogo del proveedor"),
            ("modified", "Fichas con cambios"), ("needsReview", "Productos que requieren revisión"),
            ("collisions", "Referencias coincidentes pendientes"), ("verifiedProducts", "Productos verificados"),
            ("totalProducts", "Productos totales en la tienda"), ("supplierProducts", "Productos vinculados al proveedor"),
            ("previousProductsPreserved", "Productos anteriores conservados"),
            ("linkedImages", "Imágenes enlazadas"), ("imageDownloadFailures", "Imágenes que no se pudieron descargar")
        }) Metric(report, key, label);
        var visibility = Field(report, "visibility");
        foreach (var (key, label) in new[] { ("hidden", "Productos ocultados"), ("shown", "Productos activados"), ("manualPreserved", "Decisiones manuales conservadas"), ("originalCatalogPreserved", "Productos originales protegidos") }) Metric(visibility, key, label);

        var sections = new List<Section>();
        var changes = new List<Detail>();
        var source = comparison ?? report;
        var unchanged = 0;
        foreach (var item in Items(Field(source, "changes")))
        {
            var action = Text(item, "action"); if (action.Length == 0) action = Text(item, "type");
            if (action == "sin_cambios") { unchanged++; continue; }
            var notes = new List<string>();
            var stock = Field(item, "stock"); if (stock.ValueKind == JsonValueKind.Undefined) stock = Field(item, "quantity");
            if (stock.ValueKind == JsonValueKind.Number) notes.Add("Existencias del proveedor: " + Number(stock));
            if (Field(item, "review").ValueKind == JsonValueKind.True) notes.Add("Requiere revisión antes de publicar");
            if (Text(item, "previousPvp").Length > 0) notes.Add("PVP anterior del proveedor: " + Money(Text(item, "previousPvp")));
            if (Text(item, "currentPriceText").Length > 0) notes.Add("Nuevo precio del proveedor: " + Text(item, "currentPriceText"));
            changes.Add(new(Text(item, "reference"), ActionName(action), string.Join(". ", notes)));
        }
        foreach (var item in Items(Field(source, "retired"))) changes.Add(new(item.ToString(), "Fuera del catálogo", "Referencia ausente en el catálogo consultado"));
        if (unchanged > 0) summary.Add(("Referencias sin cambios", unchanged.ToString("N0", Spanish)));
        if (changes.Count > 0) sections.Add(new(preview ? "Cambios detectados en el proveedor" : "Comparación del catálogo del proveedor", changes));
        var collisions = Items(Field(source, "conflictingReferences")).Select(x => new Detail(x.ToString(), "Coincidencia de referencia", "Se conserva el producto existente; no se fusiona automáticamente.")).ToList();
        if (collisions.Count > 0) sections.Add(new("Coincidencias para revisar", collisions));
        var visibilityRows = Items(Field(visibility, "items")).Select(x => new Detail(Text(x, "reference"),
            Field(x, "hidden").ValueKind == JsonValueKind.True ? "Ocultado en la web" : "Activado en la web",
            Text(x, "reason") switch { "disponible" => "El proveedor vuelve a tener existencias", "sin_existencias" => "El proveedor no tiene existencias", "retirado" => "Ya no está en el catálogo del proveedor", _ => "Decisión automática de disponibilidad" })).ToList();
        if (visibilityRows.Count > 0) sections.Add(new("Visibilidad aplicada en la tienda", visibilityRows));
        if (report.ValueKind == JsonValueKind.Array)
        {
            var reviews = Items(report).Select(x => new Detail(Text(x, "reference"), "Revisar producto",
                string.Join(". ", Items(Field(x, "reasons")).Select(r => r.GetString())))).ToList();
            summary.Add(("Referencias incluidas en este informe", reviews.Count.ToString("N0", Spanish)));
            if (reviews.Count > 0) sections.Add(new("Motivos de revisión", reviews));
        }
        var errors = Items(Field(report, "errors")).Count();
        if (errors > 0) summary.Add(("Incidencias detectadas", errors.ToString("N0", Spanish)));
        var notices = new List<string>();
        if (preview) notices.Add("Esta comprobación no modifica los productos de la tienda. Las cifras describen lo detectado en el proveedor; pulsa Actualizar en el panel para aplicar los cambios.");
        else if (failed) notices.Add("El proceso no terminó correctamente. Puede haber cambios parciales: revisa el catálogo y vuelve a comprobar los cambios antes de reintentar. La importación permite reintentar sin crear productos duplicados.");
        else if (type == "resultado") notices.Add("La actualización ha terminado. Los cambios detectados en el proveedor se distinguen de las acciones de visibilidad aplicadas en la tienda; se respetan las protecciones de productos y los cambios manuales.");
        else if (type == "revision") notices.Add("Comprueba en administración los datos o imágenes indicados. Los productos sin existencias o con datos incompletos pueden permanecer ocultos; se respetan las decisiones manuales.");
        if (Field(report, "existingPricesPreserved").ValueKind == JsonValueKind.True) notices.Add("Se han conservado los precios de venta de los productos existentes, aunque el proveedor haya cambiado su PVP.");
        var failureType = Text(report, "errorType");
        // Older runner reports stored only a sanitized message. Recognize the type,
        // but never print raw exception text, private paths or connection details.
        if (failureType.Length == 0) failureType = System.Text.RegularExpressions.Regex.Match(Text(report,"message"), @"\(([A-Za-z][A-Za-z0-9]*Exception)\)").Groups[1].Value;
        if (failed) notices.Add(state == "needs_login" ? "Renueva el acceso del proveedor desde el panel y vuelve a comprobar el catálogo." : failureType switch {
            "MongoWriteException" or "MongoBulkWriteException" => "El catálogo se pudo leer, pero el proceso se detuvo al guardar las imágenes. Revisa el almacenamiento de imágenes y vuelve a comprobar los cambios antes de reintentar.",
            "MongoConnectionException" or "MongoServerSelectionException" => "No se pudo conectar con el almacenamiento de imágenes. Comprueba la conexión a MongoDB antes de reintentar.",
            "MySqlException" => "La actualización se detuvo al acceder a los productos. Comprueba la conexión y el estado de la base de datos MySQL antes de reintentar.",
            "TimeoutException" or "TaskCanceledException" => "El proveedor no respondió dentro del tiempo disponible. Revisa la conexión y vuelve a intentarlo.",
            "InvalidDataException" => "Los datos recibidos no superaron las comprobaciones de importación. Revisa el archivo o catálogo del proveedor antes de reintentar.",
            _ => "Comprueba el acceso al proveedor y el archivo de entrada. Si vuelve a ocurrir, facilita al soporte el identificador de ejecución que aparece al pie del informe."
        });
        if (type == "verificacion-mensual") notices.Add(errors == 0 ? "La verificación guardada no detectó incidencias en los productos y las imágenes comprobados." : "La verificación detectó incidencias. Solicita una revisión antes de dar por válida la actualización.");
        if (errors > 0 || Field(report, "imageDownloadFailures").TryGetInt32Safe() > 0) notices.Add("Hay incidencias que necesitan revisión. Comprueba las referencias afectadas y sus imágenes antes de publicarlas.");
        var bands = Field(report, "pricingBands");
        if (bands.ValueKind == JsonValueKind.Object)
        {
            string Coefficient(string key) { var v=Text(bands,key);if(v.Length==0)v=Text(bands,key.ToUpperInvariant());return decimal.TryParse(v,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n.ToString("0.####",Spanish):v; }
            notices.Add($"PVP calculado a partir del coste: hasta 100 € × {Coefficient("a")}; más de 100 € y hasta 500 € × {Coefficient("b")}; más de 500 € × {Coefficient("c")}. Los precios protegidos manualmente se conservan.");
        }
        if (Field(source, "offlineSimulation").ValueKind == JsonValueKind.True) notices.Add("SIMULACIÓN: esta ejecución utilizó un catálogo guardado, no una consulta nueva al proveedor.");
        if (!preview && type == "resultado" && comparison == null) notices.Add("No hay un listado de comparación disponible para esta ejecución; se muestra el resumen guardado.");
        if (sections.Count == 0 && !failed) notices.Add(type == "revision" ? "No hay referencias pendientes en este informe de revisión." : "No hay referencias afectadas que detallar en este informe.");

        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(36); page.DefaultTextStyle(x => x.FontSize(10).FontColor("#243b32"));
            page.Header().PaddingBottom(16).Row(row =>
            {
                row.RelativeItem().Column(col => { col.Item().Text("ALDA JOYEROS").FontSize(16).SemiBold().FontColor("#123d2d"); col.Item().Text("Administración del catálogo").FontSize(9).FontColor("#68766d"); });
                row.AutoItem().AlignRight().Text(provider).FontSize(13).SemiBold();
            });
            page.Content().Column(col =>
            {
                col.Spacing(12);
                col.Item().Text(title).FontSize(23).SemiBold().FontColor("#123d2d");
                col.Item().Background("#f1f5f0").Padding(12).Column(meta => {
                    meta.Spacing(4); meta.Item().Text(stateLabel).SemiBold();
                    meta.Item().Text($"Origen: {origin} · Inicio: {MunrecoSchedule.Format(created)}");
                    if (ended.HasValue) meta.Item().Text($"Finalización: {MunrecoSchedule.Format(ended)} (hora de Madrid)");
                });
                foreach (var notice in notices) col.Item().Text(notice).LineHeight(1.3f);
                if (summary.Count > 0) col.Item().Table(table => {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); });
                    table.Header(h => { h.Cell().Element(HeaderCell).Text("Resumen de la ejecución"); h.Cell().Element(HeaderCell).AlignRight().Text("Total"); });
                    foreach (var (label,value) in summary) { table.Cell().Element(BodyCell).Text(label); table.Cell().Element(BodyCell).AlignRight().Text(value).SemiBold(); }
                });
                foreach (var section in sections)
                {
                    var sectionContainer = col.Item();
                    if (section.Rows.Count <= 6 && section.Rows.All(r => r.Description.Length < 200 && r.Reference.Length < 100))
                        sectionContainer = sectionContainer.ShowEntire();
                    sectionContainer.EnsureSpace(100).Column(detail => {
                    detail.Item().PaddingTop(8).Text(section.Title + $" ({section.Rows.Count.ToString("N0", Spanish)})").FontSize(13).SemiBold();
                    detail.Item().PaddingTop(8).Table(table => {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1.2f); c.RelativeColumn(1.3f); c.RelativeColumn(2.5f); });
                        table.Header(h => { h.Cell().Element(HeaderCell).Text("Referencia"); h.Cell().Element(HeaderCell).Text("Cambio / acción"); h.Cell().Element(HeaderCell).Text("Detalle"); });
                        foreach(var item in section.Rows) { table.Cell().Element(BodyCell).Text(item.Reference);table.Cell().Element(BodyCell).Text(item.Action);table.Cell().Element(BodyCell).Text(item.Description.Length>0?item.Description:"Ver ficha del producto"); }
                    });
                    });
                }
            });
            page.Footer().PaddingTop(12).BorderTop(1).BorderColor("#dce4dc").PaddingTop(8).Row(row => {
                row.RelativeItem().Text($"Ejecución: {id}").FontSize(8).FontColor("#68766d");
                row.AutoItem().Text(t => { t.Span("Página ");t.CurrentPageNumber();t.Span(" de ");t.TotalPages(); });
            });
        })).GeneratePdf();
    }

    private static int TryGetInt32Safe(this JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : 0;
    private static IContainer HeaderCell(IContainer container) => container.Background("#123d2d").Padding(8).DefaultTextStyle(x => x.FontColor(Colors.White).SemiBold().FontSize(9));
    private static IContainer BodyCell(IContainer container) => container.BorderBottom(1).BorderColor("#e2e8e0").Padding(8).DefaultTextStyle(x => x.FontSize(9));
}
