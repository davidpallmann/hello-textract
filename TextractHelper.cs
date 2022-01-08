using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;

namespace hello_textract
{
    public class TextractHelper
    {
        private string _bucketname { get; set; }
        private RegionEndpoint _region { get; set; }
        private AmazonTextractClient _textractClient { get; set; }
        private AmazonS3Client _s3Client { get; set; }

        public TextractHelper(string bucketname, RegionEndpoint region)
        {
            _bucketname = bucketname;
            _region = region;
            _textractClient = new AmazonTextractClient(_region);
            _s3Client = new AmazonS3Client(_region);
        }

        /// <summary>
        /// ID document analysis.
        /// </summary>
        /// <param name="filename"></param>
        public async Task AnalyzeID(string filename)
        {
            Console.WriteLine("Start document ID analysis");

            var bytes = File.ReadAllBytes(filename);

            AnalyzeIDRequest request = new AnalyzeIDRequest()
            {
                DocumentPages = new List<Document> { new Document { Bytes = new MemoryStream(bytes) } }
            };

            var getDetectionResponse3 = await _textractClient.AnalyzeIDAsync(request);

            foreach (var doc in getDetectionResponse3.IdentityDocuments)
            {
                foreach (var field in doc.IdentityDocumentFields)
                {
                    Console.WriteLine($"{field.Type.Text}: {field.ValueDetection.Text}");
                }
            }
        }

        /// <summary>
        /// Analyze document for text detection.
        /// </summary>
        /// <param name="filename">path to local file</param>
        public async Task AnalyzeText(string filename)
        {
            // Upload document to S3.

            var docLocation = await UploadFileToBucket(filename);

            // Start a document text detection job.

            Console.WriteLine("Starting document text detection job");
            var startJobRequest = new StartDocumentTextDetectionRequest { DocumentLocation = docLocation };
            var startJobResponse = await _textractClient.StartDocumentTextDetectionAsync(startJobRequest);   
            Console.WriteLine($"Job ID: {startJobResponse.JobId}");

            // Wait for the job to complete.

            Console.Write("Waiting for job completion");
            var getResultsRequest = new GetDocumentTextDetectionRequest { JobId = startJobResponse.JobId };
            GetDocumentTextDetectionResponse getResultsResponse = null!;
            while ((getResultsResponse = await _textractClient.GetDocumentTextDetectionAsync(getResultsRequest)).JobStatus==JobStatus.IN_PROGRESS)
            {
                Console.Write(".");
                Thread.Sleep(1000);
            }
            Console.WriteLine();

            // Display detected text blocks.

            if (getResultsResponse.JobStatus == JobStatus.SUCCEEDED)
            {
                Console.WriteLine("Detected text blocks:");
                do
                {
                    foreach (var block in getResultsResponse.Blocks)
                    {
                        Console.WriteLine($"Type {block.BlockType}, Text: {block.Text} ({block.Confidence:N0}%)");
                    }

                    if (string.IsNullOrEmpty(getResultsResponse.NextToken)) { break; }

                    getResultsRequest.NextToken = getResultsResponse.NextToken;
                    getResultsResponse = await _textractClient.GetDocumentTextDetectionAsync(getResultsRequest);

                } while (!string.IsNullOrEmpty(getResultsResponse.NextToken));
            }
            else
            {
                Console.WriteLine($"ERROR: job failed - {getResultsResponse.StatusMessage}");
            }
        }

        /// <summary>
        /// Analyze document for table data.
        /// </summary>
        /// <param name="filename">path to local file</param>
        public async Task AnalyzeTable(string filename)
        {
            // Upload document to S3.

            var docLocation = await UploadFileToBucket(filename);

            // Start a document analysis job.

            Console.WriteLine("Starting document analysis job");
            var startJobRequest = new StartDocumentAnalysisRequest { DocumentLocation = docLocation, FeatureTypes = { "TABLES" } };
            var startJobResponse = await _textractClient.StartDocumentAnalysisAsync(startJobRequest);
            Console.WriteLine($"Job ID: {startJobResponse.JobId}");

            // Wait for the job to complete.

            Console.Write("Waiting for job completion");
            var getResultsRequest = new GetDocumentAnalysisRequest { JobId = startJobResponse.JobId };
            GetDocumentAnalysisResponse getResultsResponse = null!;
            while ((getResultsResponse = await _textractClient.GetDocumentAnalysisAsync(getResultsRequest)).JobStatus == JobStatus.IN_PROGRESS)
            {
                Console.Write(".");
                Thread.Sleep(1000);
            }
            Console.WriteLine();

            // Display detected tables.

            if (getResultsResponse.JobStatus == JobStatus.SUCCEEDED)
            {
                do
                {
                    var tables = from table in getResultsResponse.Blocks where table.BlockType == BlockType.TABLE select table;
                    var cells = from cell in getResultsResponse.Blocks where cell.BlockType == BlockType.CELL select cell;

                    Console.WriteLine($"Found {tables.Count()} tables and {cells.Count()} cells");

                    foreach(var cell in cells)
                    {
                        if (cell.ColumnIndex==1)
                        {
                            Console.WriteLine();
                            Console.Write("| ");
                        }
                        foreach(var rel in cell.Relationships)
                        {
                            foreach (var id in rel.Ids)
                            {
                                var cellText = (from text in getResultsResponse.Blocks where text.Id == id select text.Text).FirstOrDefault();
                                Console.Write($"{cellText} ");
                            }
                        }
                        Console.Write("| ");
                    }

                    if (string.IsNullOrEmpty(getResultsResponse.NextToken)) { break; }

                    getResultsRequest.NextToken = getResultsResponse.NextToken;
                    getResultsResponse = await _textractClient.GetDocumentAnalysisAsync(getResultsRequest);

                } while (!string.IsNullOrEmpty(getResultsResponse.NextToken));
            }
            else
            {
                Console.WriteLine($"ERROR: job failed - {getResultsResponse.StatusMessage}");
            }
        }

        /// <summary>
        /// Upload local file to S3 bucket.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>DocumentLocation object, suitable for inclusion in Textract start job requests.</returns>
        private async Task<DocumentLocation> UploadFileToBucket(string filename)
        {
            Console.WriteLine($"Upload {filename} to {_bucketname} bucket");
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketname,
                FilePath = filename,
                Key = Path.GetFileName(filename)
            };
            await _s3Client.PutObjectAsync(putRequest);
            return new DocumentLocation
            {
                S3Object = new Amazon.Textract.Model.S3Object
                {
                    Bucket = _bucketname,
                    Name = putRequest.Key
                }
            };
        }
    }
}
