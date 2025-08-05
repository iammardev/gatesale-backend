using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using GateSale.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace GateSale.Infrastructure.Services
{
    public class S3StorageService : IStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly ILogger<S3StorageService> _logger;

        public S3StorageService(IConfiguration configuration, ILogger<S3StorageService> logger)
        {
            _logger = logger;
            
            var region = configuration["AWS:Region"] ?? "us-east-1";
            _bucketName = configuration["AWS:S3:BucketName"] ?? "gatesale-media";
            
            var credentials = new BasicAWSCredentials(
                configuration["AWS:AccessKey"],
                configuration["AWS:SecretKey"]
            );
            
            _s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(region));
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            try
            {
                // Generate a unique file name to avoid conflicts
                var uniqueFileName = $"{Guid.NewGuid()}-{fileName}";
                
                _logger.LogInformation("S3 upload starting - Bucket: {BucketName}, Key: {Key}, ContentType: {ContentType}", 
                    _bucketName, uniqueFileName, contentType);
                
                // Set up the transfer utility upload request
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = fileStream,
                    Key = uniqueFileName,
                    BucketName = _bucketName,
                    ContentType = contentType,
                };

                // Upload the file
                using var transferUtility = new TransferUtility(_s3Client);
                _logger.LogInformation("Starting S3 transfer for file {FileName}", fileName);
                
                await transferUtility.UploadAsync(uploadRequest);
                _logger.LogInformation("S3 transfer completed successfully for {FileName}", fileName);
                
                // Return the URL of the uploaded file
                var fileUrl = $"https://{_bucketName}.s3.amazonaws.com/{uniqueFileName}";
                _logger.LogInformation("Generated S3 URL: {FileUrl}", fileUrl);
                return fileUrl;
            }
            catch (AmazonS3Exception s3Ex)
            {
                _logger.LogError(s3Ex, "AWS S3 error uploading file to S3: {FileName}, Status: {StatusCode}, Error: {ErrorCode}, Message: {Message}", 
                    fileName, s3Ex.StatusCode, s3Ex.ErrorCode, s3Ex.Message);
                throw new ApplicationException($"S3 error uploading file: {s3Ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to S3: {FileName}", fileName);
                throw new ApplicationException("An error occurred uploading the file.");
            }
        }

        public async Task<bool> DeleteFileAsync(string fileUrl)
        {
            try
            {
                // Extract the key from the URL
                var key = fileUrl.Split($"{_bucketName}.s3.amazonaws.com/").Last();
                
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };
                
                var response = await _s3Client.DeleteObjectAsync(deleteRequest);
                return response.HttpStatusCode == HttpStatusCode.NoContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file from S3: {FileUrl}", fileUrl);
                throw new ApplicationException("An error occurred deleting the file.");
            }
        }

        public async Task<Stream> GetFileAsync(string fileUrl)
        {
            try
            {
                // Extract the key from the URL
                var key = fileUrl.Split($"{_bucketName}.s3.amazonaws.com/").Last();
                
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };
                
                var response = await _s3Client.GetObjectAsync(request);
                return response.ResponseStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting file from S3: {FileUrl}", fileUrl);
                throw new ApplicationException("An error occurred retrieving the file.");
            }
        }

        public async Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expiry)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    Expires = DateTime.UtcNow.Add(expiry)
                };
                
                return await Task.FromResult(_s3Client.GetPreSignedURL(request));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned URL: {Key}", key);
                throw new ApplicationException("An error occurred generating the URL.");
            }
        }
    }
} 