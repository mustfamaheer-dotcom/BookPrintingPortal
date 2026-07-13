using Microsoft.AspNetCore.Identity;
using PrintingBooksPortal.Models;

namespace PrintingBooksPortal.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        await db.Database.EnsureCreatedAsync();

        var adminRole = "Admin";
        var shopRole = "Shop";

        if (!await roleManager.RoleExistsAsync(adminRole))
            await roleManager.CreateAsync(new IdentityRole(adminRole));

        if (!await roleManager.RoleExistsAsync(shopRole))
            await roleManager.CreateAsync(new IdentityRole(shopRole));

        var adminEmail = "admin@printingbooks.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, adminRole);
        }

        if (!db.EducationalBoards.Any())
        {
            var boards = new List<EducationalBoard>
            {
                new() { Name = "Cambridge IGCSE", Description = "Cambridge International General Certificate of Secondary Education" },
                new() { Name = "Edexcel International", Description = "Pearson Edexcel International Curriculum" },
                new() { Name = "IB Diploma", Description = "International Baccalaureate Diploma Programme" },
                new() { Name = "National Curriculum", Description = "Local National Educational Board" }
            };
            db.EducationalBoards.AddRange(boards);
            await db.SaveChangesAsync();
        }
    }
}
