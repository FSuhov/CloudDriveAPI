using Amazon.S3;
using Amazon.S3.Model;
using API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace API.Controllers {

  [ApiController]
  [Route("[controller]")]
  public class BucketController : ControllerBase {
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;

    public BucketController(IConfiguration configuration, IAmazonS3 s3Client) {
      _configuration = configuration;
      _s3Client = s3Client;
    }

    [HttpGet("/list")]
    [Authorize]
    public async Task<IActionResult> GetAllFiles(string userName) {
      var bucketName = _configuration.GetValue<string>("bucket-name");
      var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);
      if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

      var request = new ListObjectsV2Request()
      {
        BucketName = bucketName,
        Prefix = userName
      };
      var result = await _s3Client.ListObjectsV2Async(request);
      var files = result.S3Objects.Select(q => new FileDto { Name = q.Key.Replace(userName+"/", string.Empty), Size = q.Size, Type = GetFileType(q.Key) }).ToList();

      return Ok(files);
    }

    [HttpPost("/upload")]
    [Authorize]
    public async Task<IActionResult> UploadFileAsync(IFormFile file, string? userName) {
      var bucketName = _configuration.GetValue<string>("bucket-name");
      var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);
      if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
      
      var request = new PutObjectRequest()
      {
        BucketName = bucketName,
        Key = string.IsNullOrEmpty(userName) ? file.FileName : $"{userName}/{file.FileName}",
        InputStream = file.OpenReadStream()
      };
      request.Metadata.Add("Content-Type", file.ContentType);
      await _s3Client.PutObjectAsync(request);
      return Ok($"File {userName}/{file.FileName} uploaded to S3 successfully!");
    }

    [HttpDelete("/delete")]
    [Authorize]
    public async Task<IActionResult> DeleteFileAsync(string? userName, string fileName) {
      var bucketName = _configuration.GetValue<string>("bucket-name");
      var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);
      var key = string.IsNullOrEmpty(userName) ? fileName : $"{userName}/{fileName}";
      if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
      await _s3Client.DeleteObjectAsync(bucketName, key);
      return NoContent();
    }

    [HttpGet("/download")]
    [Authorize]
    public async Task<IActionResult> GetFileByKeyAsync(string? userName, string fileName) {
      var bucketName = _configuration.GetValue<string>("bucket-name");
      var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);
      if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
      var key = string.IsNullOrEmpty(userName) ? fileName : $"{userName}/{fileName}";
      var s3Object = await _s3Client.GetObjectAsync(bucketName, key);
      return File(s3Object.ResponseStream, s3Object.Headers.ContentType);
    }

    [HttpGet("/share")]
    [Authorize]
    public async Task<IActionResult> GetPresignedLinkAsync(string? userName, string fileName) {
      var bucketName = _configuration.GetValue<string>("bucket-name");
      var bucketExists = await _s3Client.DoesS3BucketExistAsync(bucketName);
      if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
      var key = string.IsNullOrEmpty(userName) ? fileName : $"{userName}/{fileName}";
      GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
      {
        BucketName = bucketName,
        Key = key,
        Expires = DateTime.Now.AddMinutes(60)
      };

      var path = _s3Client.GetPreSignedURL(request);

      return Ok(path);
    }

    private string GetFileType(string fileName) {
      string pattern = @"(?<=\.)[^.\s]+$";
      Regex rg = new Regex(pattern);
      return rg.Match(fileName).Value;
    }

  }
}
