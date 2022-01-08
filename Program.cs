using Amazon;
using hello_textract;

const string bucketname = @"[bucketname]";
var region = RegionEndpoint.[region];

if (args.Length == 2)
{
    var filename = args[0];
    var analysisType = (args.Length > 1) ? args[1] : "text";

    TextractHelper helper = new TextractHelper(bucketname, region);

    switch (analysisType)
    {
        case "id":
            await helper.AnalyzeID(filename);
            Environment.Exit(1);
            break;
        case "text":
            await helper.AnalyzeText(filename);
            Environment.Exit(1);
            break;
        case "table":
            await helper.AnalyzeTable(filename);
            Environment.Exit(1);
            break;
    }
}

Console.WriteLine("?Invalid parameter - command line format: dotnet run -- <file> text|data|table");
