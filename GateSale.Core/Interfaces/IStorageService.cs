namespace GateSale.Core.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<bool> DeleteFileAsync(string fileUrl);
        Task<Stream> GetFileAsync(string fileUrl);
        Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expiry);
    }
} 