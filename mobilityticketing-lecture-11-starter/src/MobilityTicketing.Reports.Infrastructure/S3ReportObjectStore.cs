using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MobilityTicketing.Reports.Application;

namespace MobilityTicketing.Reports.Infrastructure;

internal sealed class S3ReportObjectStore(IAmazonS3 client) : IReportObjectStore
{
    public async Task<string> UploadAsync(ExportLocation location, byte[] bytes)
    {
        try
        {
            using var body = new MemoryStream(bytes);
            var response = await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = location.Bucket, Key = location.Key, InputStream = body,
                ContentType = "application/x-ndjson", UseChunkEncoding = false
            });
            return response.VersionId ?? "";
        }
        catch (Exception ex) when (ex is AmazonClientException or AmazonServiceException
                                   or HttpRequestException or IOException)
        {
            throw new ObjectStoreException("Object upload failed.", ex);
        }
    }

    public async Task<byte[]> ReadVersionAsync(ExportLocation location, string versionId)
    {
        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = location.Bucket, Key = location.Key, VersionId = versionId
            });
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
        catch (Exception ex) when (ex is AmazonClientException or AmazonServiceException
                                   or HttpRequestException or IOException)
        {
            throw new ObjectStoreException("Object read failed.", ex);
        }
    }
}
