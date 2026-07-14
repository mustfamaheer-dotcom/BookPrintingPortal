using System.ComponentModel.DataAnnotations;

namespace PrintingBooksPortal.Models;

public class SystemSetting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    public bool ValueBool { get; set; }
}

public static class SystemSettingKeys
{
    public const string WatermarkEnabled = "WatermarkEnabled";
}
