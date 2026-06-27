using Raqmi.Shared;

namespace Raqmi.Licensing;

public static class LicensePolicy
{
    public static LicenseEvaluationResult Evaluate(RaqmiLicensePayload license, LicenseEvaluationContext context)
    {
        var commercial = ModuleCatalog.All.Where(m => m.Commercial).Select(m => RaqmiModuleCodes.ToWire(m.Code)).ToList();
        var allowedSet = new HashSet<string>(license.AllowedModules, StringComparer.OrdinalIgnoreCase);
        var blocked = commercial.Where(code => !allowedSet.Contains(code)).ToList();

        if (license.Status != LicenseStatus.Active)
        {
            return Fail($"Licence {license.Status.ToString().ToLowerInvariant()}", commercial, []);
        }

        if (context.Now < license.StartsAt)
        {
            return Fail("Licence pas encore active", commercial, []);
        }

        if (context.Now > license.ExpiresAt)
        {
            return Fail("Licence expirée", blocked, license.AllowedModules, readonlyMode: true);
        }

        if (context.UsersCount > license.Limits.MaxUsers)
        {
            return Fail("Nombre maximum d'utilisateurs dépassé", blocked, license.AllowedModules, readonlyMode: true);
        }

        if (context.SitesCount > license.Limits.MaxSites)
        {
            return Fail("Nombre maximum de sites dépassé", blocked, license.AllowedModules, readonlyMode: true);
        }

        if (context.StorageUsedGb > license.Limits.MaxStorageGb)
        {
            return Fail("Quota de stockage dépassé", blocked, license.AllowedModules, readonlyMode: true);
        }

        if (license.Mode != LicenseMode.Offline && context.LastOnlineCheckAt is { } lastOnline)
        {
            var offlineDays = (context.Now - lastOnline).Days;
            if (offlineDays > license.Limits.OfflineGraceDays)
            {
                return Fail("Délai de tolérance hors ligne dépassé", blocked, license.AllowedModules, readonlyMode: true);
            }
        }

        if (context.RequestedModule is { } requested && !allowedSet.Contains(requested))
        {
            return new LicenseEvaluationResult
            {
                Valid = false,
                ReadonlyMode = false,
                Reason = $"Module non inclus dans la licence: {requested}",
                AllowedModules = license.AllowedModules,
                BlockedModules = blocked,
            };
        }

        return new LicenseEvaluationResult
        {
            Valid = true,
            ReadonlyMode = false,
            AllowedModules = license.AllowedModules,
            BlockedModules = blocked,
        };
    }

    private static LicenseEvaluationResult Fail(
        string reason,
        List<string> blocked,
        List<string> allowed,
        bool readonlyMode = true) =>
        new()
        {
            Valid = false,
            ReadonlyMode = readonlyMode,
            Reason = reason,
            AllowedModules = allowed,
            BlockedModules = blocked,
        };
}
