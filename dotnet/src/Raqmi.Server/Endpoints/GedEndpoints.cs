using Microsoft.EntityFrameworkCore;
using Raqmi.Data;
using Raqmi.Data.Entities;
using Raqmi.Server.Middleware;

namespace Raqmi.Server.Endpoints;

public static class GedEndpoints
{
    public static void MapGedEndpoints(this WebApplication app, ServerRuntime runtime)
    {
        if (!runtime.UseDatabase) return;

        var group = app.MapGroup("/api/v1/ged");

        group.MapGet("/documents", async (HttpContext http, RaqmiDbContext db, string? moduleCode, string? entityType, string? entityId, string? search) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var query = db.FileObjects.Where(x => x.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(moduleCode)) query = query.Where(x => x.ModuleCode == moduleCode);
            if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(x => x.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(x => x.EntityId == entityId);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.OriginalName.ToLower().Contains(search.ToLower()));
            var items = await query.OrderByDescending(x => x.UploadedAt).ToListAsync();
            return Results.Json(new { items }, JsonDefaults.Options);
        }).RequireRaqmiModule("ged");

        group.MapPost("/documents", async (HttpContext http, RaqmiDbContext db, AuditService audit, IConfiguration config) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var form = await http.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest(new { error = "Fichier requis" });

            var dataDir = config["RAQMI_DATA_DIR"] ?? "./data";
            var tenantDir = Path.Combine(dataDir, "files", tenantId);
            Directory.CreateDirectory(tenantDir);
            var storedName = $"{Guid.NewGuid()}_{file.FileName}";
            var fullPath = Path.Combine(tenantDir, storedName);
            await using (var stream = File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var fileObject = new FileObject
            {
                TenantId = tenantId,
                SiteId = form["siteId"].FirstOrDefault(),
                StorageDriver = "LOCAL",
                Path = fullPath,
                OriginalName = file.FileName,
                MimeType = file.ContentType,
                SizeBytes = file.Length,
                ModuleCode = form["moduleCode"].FirstOrDefault(),
                EntityType = form["entityType"].FirstOrDefault(),
                EntityId = form["entityId"].FirstOrDefault(),
                UploadedBy = EndpointAuth.GetUserId(http),
            };
            db.FileObjects.Add(fileObject);
            await db.SaveChangesAsync();
            await audit.LogAsync(tenantId, EndpointAuth.GetUserId(http), "upload", "ged", "FileObject", fileObject.Id, $"Document uploadé: {file.FileName}");
            return Results.Json(fileObject, JsonDefaults.Options);
        }).RequireRaqmiModule("ged");

        group.MapGet("/documents/{id}/download", async (HttpContext http, RaqmiDbContext db, string id) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var doc = await db.FileObjects.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
            if (doc is null || !File.Exists(doc.Path)) return Results.NotFound();
            return Results.File(doc.Path, doc.MimeType ?? "application/octet-stream", doc.OriginalName);
        }).RequireRaqmiModule("ged");

        group.MapDelete("/documents/{id}", async (HttpContext http, RaqmiDbContext db, string id) =>
        {
            var tenantId = EndpointAuth.GetTenantId(http);
            var doc = await db.FileObjects.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
            if (doc is null) return Results.NotFound();
            if (File.Exists(doc.Path)) File.Delete(doc.Path);
            db.FileObjects.Remove(doc);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireRaqmiModule("ged");
    }
}
