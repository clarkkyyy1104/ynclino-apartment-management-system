using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace YnclinoApartmentManagementSystem.Helpers
{
    public static class ImageUploadHelper
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

        public static bool IsValid(IFormFile file, out string error)
        {
            error = string.Empty;
            if (file.Length == 0) { error = "The selected file is empty."; return false; }
            if (file.Length > MaxBytes) { error = "Image must be 5 MB or smaller."; return false; }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) { error = "Only JPG, PNG, GIF, or WEBP images are allowed."; return false; }
            return true;
        }

        // Saves the file under wwwroot/uploads/{subfolder} and returns the web path (e.g. /uploads/lostfound/xyz.jpg)
        public static async Task<string> SaveAsync(IFormFile file, string subfolder, IWebHostEnvironment env)
        {
            // WebRootPath is null when the wwwroot folder isn't discovered; fall back to the content root
            var webRootPath = string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var folder = Path.Combine(webRootPath, "uploads", subfolder);
            Directory.CreateDirectory(folder);
            var fullPath = Path.Combine(folder, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(stream);
            return $"/uploads/{subfolder}/{fileName}";
        }
    }
}
