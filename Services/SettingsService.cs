using Microsoft.EntityFrameworkCore;
using PrintingBooksPortal.Data;
using PrintingBooksPortal.Models;

namespace PrintingBooksPortal.Services;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;

    public SettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsWatermarkEnabledAsync()
    {
        try
        {
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == SystemSettingKeys.WatermarkEnabled);

            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = SystemSettingKeys.WatermarkEnabled,
                    ValueBool = true
                };
                _db.SystemSettings.Add(setting);
                await _db.SaveChangesAsync();
            }

            return setting.ValueBool;
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            await EnsureTableCreatedAsync();
            var setting = new SystemSetting
            {
                Key = SystemSettingKeys.WatermarkEnabled,
                ValueBool = true
            };
            _db.SystemSettings.Add(setting);
            await _db.SaveChangesAsync();
            return true;
        }
    }

    public async Task SetWatermarkEnabledAsync(bool enabled)
    {
        try
        {
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == SystemSettingKeys.WatermarkEnabled);

            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = SystemSettingKeys.WatermarkEnabled,
                    ValueBool = enabled
                };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.ValueBool = enabled;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            await EnsureTableCreatedAsync();
            var setting = new SystemSetting
            {
                Key = SystemSettingKeys.WatermarkEnabled,
                ValueBool = enabled
            };
            _db.SystemSettings.Add(setting);
            await _db.SaveChangesAsync();
        }
    }

    private static bool IsTableMissing(Exception ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("Invalid object name")
            || msg.Contains("no such table")
            || msg.Contains("does not exist")
            || msg.Contains("Table '")
            || msg.Contains("not found");
    }

    private async Task EnsureTableCreatedAsync()
    {
        // For SQL Server
        const string sqlServer = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemSettings')
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Key] nvarchar(100) NOT NULL,
        [ValueBool] bit NOT NULL DEFAULT 1
    );
    CREATE UNIQUE INDEX [IX_SystemSettings_Key] ON [SystemSettings] ([Key]);
END";

        // For SQLite
        const string sqlite = @"
CREATE TABLE IF NOT EXISTS [SystemSettings] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_SystemSettings] PRIMARY KEY AUTOINCREMENT,
    [Key] TEXT NOT NULL,
    [ValueBool] INTEGER NOT NULL DEFAULT 1
);
CREATE UNIQUE INDEX IF NOT EXISTS [IX_SystemSettings_Key] ON [SystemSettings] ([Key]);";

        try
        {
            // Try SQL Server syntax first
            await _db.Database.ExecuteSqlRawAsync(sqlServer);
        }
        catch
        {
            // Fallback to SQLite syntax
            await _db.Database.ExecuteSqlRawAsync(sqlite);
        }
    }
}
