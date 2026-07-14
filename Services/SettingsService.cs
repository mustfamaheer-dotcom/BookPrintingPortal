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

    public async Task SetWatermarkEnabledAsync(bool enabled)
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
}
