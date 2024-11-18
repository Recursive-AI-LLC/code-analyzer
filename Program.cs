using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeAnalysis
{
    [JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(CodeStatistics))]
    internal partial class CodeAnalysisJsonContext : JsonSerializerContext
    {
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please provide a directory path to analyze.");
                return;
            }

            string directoryPath = args[0];
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Directory not found: {directoryPath}");
                return;
            }

            bool verbose = args.Length > 1 && args[1].Contains("verbose", StringComparison.OrdinalIgnoreCase);

            try
            {
                var analyzer = new CodeAnalyzer(directoryPath, verbose);
                var stats = await analyzer.AnalyzeDirectoryAsync();
                
                // Output the results using source-generated JSON serialization
                string jsonOutput = JsonSerializer.Serialize(stats, CodeAnalysisJsonContext.Default.CodeStatistics);
                string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "code_analysis_results.json");
                await File.WriteAllTextAsync(outputPath, jsonOutput);
                
                Console.WriteLine($"Analysis complete. Results written to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during analysis: {ex.Message}");
            }
        }
    }
}