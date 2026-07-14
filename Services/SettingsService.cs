using Microsoft.EntityFrameworkCore;
using PrintingBooksPortal.Data;
using PrintingBooksPortal.Models;

namespace PrintingBooksPortal.Services;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;

    private const string DefaultWatermarkText = "LICENSED TO: {shopName}\nUSER: {userName}\nDATE: {date}\nDO NOT DISTRIBUTE";

    public SettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsWatermarkEnabledAsync()
    {
        return await GetBoolSettingAsync(SystemSettingKeys.WatermarkEnabled, true);
    }

    public async Task SetWatermarkEnabledAsync(bool enabled)
    {
        await SetBoolSettingAsync(SystemSettingKeys.WatermarkEnabled, enabled);
    }

    public async Task<string> GetWatermarkTextAsync()
    {
        try
        {
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == SystemSettingKeys.WatermarkText);

            if (setting == null || string.IsNullOrWhiteSpace(setting.ValueString))
            {
                return DefaultWatermarkText;
            }

            return setting.ValueString;
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            await EnsureTableCreatedAsync();
            return DefaultWatermarkText;
        }
    }

    public async Task SetWatermarkTextAsync(string text)
    {
        try
        {
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == SystemSettingKeys.WatermarkText);

            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = SystemSettingKeys.WatermarkText,
                    ValueString = text
                };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.ValueString = text;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            await EnsureTableCreatedAsync();
            var setting = new SystemSetting
            {
                Key = SystemSettingKeys.WatermarkText,
                ValueString = text
            };
            _db.SystemSettings.Add(setting);
            await _db.SaveChangesAsync();
        }
    }

    private async Task<bool> GetBoolSettingAsync(string key, bool defaultValue)
    {
        try
        {
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new SystemSetting { Key = key, ValueBool = defaultValue };
                _db.SystemSettings.Add(setting);
                await _db.SaveChangesAsync();
            }

            return setting.ValueBool;
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            await EnsureTableCreatedAsync();
            var setting = new SystemSetting { Key = key, ValueBool = defaultValue };
            _db.SystemSettings.Add(setting);
            await _db.SaveChangesAsync();
            return defaultValue;
        }
    }

    private async Task SetBoolSettingAsync(string key, bool value)
    {
        try
        {
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
            {
                setting = new SystemSetting { Key = key, ValueBool = value };
                _db.SystemSettings.Add(setting);
            }
            else
            {
                setting.ValueBool = value;
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            await EnsureTableCreatedAsync();
            var setting = new SystemSetting { Key = key, ValueBool = value };
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
        const string sqlServer = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SystemSettings')
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Key] nvarchar(100) NOT NULL,
        [ValueBool] bit NOT NULL DEFAULT 1,
        [ValueString] nvarchar(2000) NULL
    );
    CREATE UNIQUE INDEX [IX_SystemSettings_Key] ON [SystemSettings] ([Key]);
END";

        const string sqlite = @"
CREATE TABLE IF NOT EXISTS [SystemSettings] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_SystemSettings] PRIMARY KEY AUTOINCREMENT,
    [Key] TEXT NOT NULL,
    [ValueBool] INTEGER NOT NULL DEFAULT 1,
    [ValueString] TEXT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS [IX_SystemSettings_Key] ON [SystemSettings] ([Key]);";

        try
        {
            await _db.Database.ExecuteSqlRawAsync(sqlServer);
        }
        catch
        {
            await _db.Database.ExecuteSqlRawAsync(sqlite);
        }
    }
}
