using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class GedEndpoints
{
    public static void MapGedEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        var group = app.MapGroup("/api/v1/ged");

        group.MapGet("/documents", async (HttpContext http, RaqmiDbContext? db, string? siteId, string? moduleCode, string? search) =>
        {
            if (runtime.DemoMode)
                return Results.Json(new { items = DemoBusinessStore.ListDocuments(siteId, EndpointAuth.GetUserId(http) ?? string.Empty, EndpointAuth.GetRoleCode(http)) }, JsonDefaults.Options);

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var query = db.FileObjects.Where(x => x.TenantId == tenantId);
                if (!string.IsNullOrWhiteSpace(moduleCode)) query = query.Where(x => x.ModuleCode == moduleCode);
                if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.OriginalName.ToLower().Contains(search.ToLower()));
                var items = await query.OrderByDescending(x => x.UploadedAt).ToListAsync();
                return Results.Json(new { items }, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("ged");

        group.MapPost("/documents", async (HttpContext http, RaqmiDbContext? db, AuditService? audit, IConfiguration config) =>
        {
            var form = await http.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest(new { error = "Fichier requis" });
            var siteId = form["siteId"].FirstOrDefault();

            if (runtime.DemoMode)
            {
                var created = DemoBusinessStore.CreateDocument(file.FileName, file.ContentType, file.Length, siteId, EndpointAuth.GetUserId(http));
                return Results.Json(new { id = created.Id, originalName = created.OriginalName, mimeType = created.MimeType, sizeBytes = created.SizeBytes, uploadedAt = created.UploadedAt, moduleCode = created.ModuleCode }, JsonDefaults.Options);
            }

            if (runtime.UseDatabase && db is not null && audit is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var dataDir = config["RAQMI_DATA_DIR"] ?? "./data";
                var tenantDir = Path.Combine(dataDir, "files", tenantId);
                Directory.CreateDirectory(tenantDir);
                var storedName = $"{Guid.NewGuid()}_{file.FileName}";
                var fullPath = Path.Combine(tenantDir, storedName);
                await using (var stream = File.Create(fullPath)) await file.CopyToAsync(stream);

                var fileObject = new FileObject
                {
                    TenantId = tenantId,
                    SiteId = siteId,
                    StorageDriver = "LOCAL",
                    Path = fullPath,
                    OriginalName = file.FileName,
                    MimeType = file.ContentType,
                    SizeBytes = file.Length,
                    ModuleCode = form["moduleCode"].FirstOrDefault() ?? "ged",
                    UploadedBy = EndpointAuth.GetUserId(http),
                };
                db.FileObjects.Add(fileObject);
                await db.SaveChangesAsync();
                await audit.LogAsync(tenantId, EndpointAuth.GetUserId(http), "upload", "ged", "FileObject", fileObject.Id, $"Document uploadé: {file.FileName}");
                return Results.Json(fileObject, JsonDefaults.Options);
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("ged");

        group.MapDelete("/documents/{id}", async (HttpContext http, RaqmiDbContext? db, string id) =>
        {
            if (runtime.DemoMode)
                return DemoBusinessStore.DeleteDocument(id) ? Results.NoContent() : Results.NotFound();

            if (runtime.UseDatabase && db is not null)
            {
                var tenantId = EndpointAuth.GetTenantId(http);
                var doc = await db.FileObjects.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
                if (doc is null) return Results.NotFound();
                if (File.Exists(doc.Path)) File.Delete(doc.Path);
                db.FileObjects.Remove(doc);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }

            return Results.StatusCode(501);
        }).RequireRaqmiModule("ged");
    }
}
