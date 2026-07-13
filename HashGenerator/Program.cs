using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

// Mimic Identity's PasswordHasher<TUser> for Identity V4 (.NET 9+)
// We directly use the same PBKDF2 approach

string password = args.Length > 0 ? args[0] : "Admin@123";

var hasher = new PasswordHasher<object>();
var hash = hasher.HashPassword(null, password);
Console.WriteLine(hash);
