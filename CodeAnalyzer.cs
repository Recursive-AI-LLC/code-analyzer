using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CodeAnalysis
{
    public enum FileCategory
    {
        Source,
        Markup,
        Style,
        Script,
        Data,
        Configuration,
        Documentation,
        Other
    }

    public class FileTypeInfo
    {
        public FileCategory Category { get; set; }
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
    }

    public class ProjectStructureInfo
    {
        public int DirectoryCount { get; set; }
        public int MaxDepth { get; set; }
        public Dictionary<string, int> FilesPerDirectory { get; set; } = new();
        public List<string> LargestDirectories { get; set; } = new();
    }

    public class DistributionStatistics
    {
        public Dictionary<FileCategory, double> FileTypeDistribution { get; set; } = new();
        public Dictionary<string, double> ExtensionDistribution { get; set; } = new();
        public double AverageFilesPerDirectory { get; set; }
        public double CodeToResourceRatio { get; set; }
        public double DocumentationRatio { get; set; }
    }

    public class CodeComplexityStats
    {
        public double AverageFileComplexity { get; set; }
        public Dictionary<string, double> ComplexityByExtension { get; set; } = new();
        public List<string> HighComplexityFiles { get; set; } = new();
    }

    public class CodebaseInsights
    {
        public string[] KeyFindings { get; set; } = Array.Empty<string>();
        public Dictionary<string, string> Recommendations { get; set; } = new();
        public Dictionary<string, double> Metrics { get; set; } = new();
    }
    public class CodeAnalyzer
    {
        private static readonly Dictionary<string, FileTypeInfo> FileTypes = new()
        {
            {
                ".cs", new FileTypeInfo
                {
                    Category = FileCategory.Source,
                    Extensions = new[] { ".cs" },
                    Description = "C# source files"
                }
            },
            {
                ".html", new FileTypeInfo
                {
                    Category = FileCategory.Markup,
                    Extensions = new[] { ".html", ".htm", ".cshtml", ".razor" },
                    Description = "HTML and template files"
                }
            },
            {
                ".css", new FileTypeInfo
                {
                    Category = FileCategory.Style,
                    Extensions = new[] { ".css", ".scss", ".sass", ".less" },
                    Description = "Style sheets"
                }
            },
            {
                ".js", new FileTypeInfo
                {
                    Category = FileCategory.Script,
                    Extensions = new[] { ".js", ".ts", ".jsx", ".tsx" },
                    Description = "JavaScript and TypeScript files"
                }
            },
            {
                ".json", new FileTypeInfo
                {
                    Category = FileCategory.Data,
                    Extensions = new[] { ".json", ".xml", ".yaml", ".yml" },
                    Description = "Data files"
                }
            },
            {
                ".config", new FileTypeInfo
                {
                    Category = FileCategory.Configuration,
                    Extensions = new[] { ".config", ".conf", ".ini", ".env" },
                    Description = "Configuration files"
                }
            },
            {
                ".md", new FileTypeInfo
                {
                    Category = FileCategory.Documentation,
                    Extensions = new[] { ".md", ".txt", ".rst" },
                    Description = "Documentation files"
                }
            }
        };

        private static readonly HashSet<string> ExcludedDirectories = new()
        {
            ".git", "node_modules", "bin", "obj", "dist", ".dist", "env", ".env",
            "venv", ".venv", "packages", ".vs", "Debug", "Release"
        };

        private static readonly HashSet<string> ExcludedFilePatterns = new()
        {
            // Binary and package files
            ".exe", ".dll", ".pdb", ".cache", ".suo", ".user", ".lock",
            ".bin", ".obj", ".zip", ".tar", ".gz", ".7z", ".rar",
            
            // Image files
            ".jpg", ".jpeg", ".png", ".gif", ".ico", ".pdf", ".svg",
            
            // Document files
            ".doc", ".docx", ".xls", ".xlsx", ".pdf",
            
            // Database files
            ".db", ".sqlite", ".mdf", ".ldf",
            
            // Web assets
            ".min.js", ".min.css",
            
            // Font files
            ".woff", ".woff2", ".ttf", ".eot", ".otf",
            
            // Auto-generated files
            ".designer.cs", ".generated.cs", ".g.cs", ".g.i.cs",
            
            // Resource files
            ".resources", ".resx"
        };

        private readonly bool _verboseOutput;
        private readonly string _baseDirectory;

        public CodeAnalyzer(string baseDirectory, bool verboseOutput = false)
        {
            _baseDirectory = Path.GetFullPath(baseDirectory);
            _verboseOutput = verboseOutput;
        }

        public async Task<CodeStatistics> AnalyzeDirectoryAsync()
        {
            var stats = new CodeStatistics();
            var files = GetFilesToAnalyze(_baseDirectory);
            var fileStats = new List<FileStatistics>();
            var projectStructure = new ProjectStructureInfo();

            // Calculate project structure metrics
            var directories = Directory.GetDirectories(_baseDirectory, "*", SearchOption.AllDirectories)
                .Where(dir => !dir.Split(Path.DirectorySeparatorChar)
                    .Any(d => ExcludedDirectories.Contains(d.ToLowerInvariant())))
                .ToList();

            projectStructure.DirectoryCount = directories.Count + 1; // +1 for root directory
            projectStructure.MaxDepth = directories
                .Select(dir => dir.Substring(_baseDirectory.Length)
                    .Count(c => c == Path.DirectorySeparatorChar))
                .DefaultIfEmpty(0)
                .Max();

            var filesPerDir = new Dictionary<string, int>();
            foreach (var dir in directories.Concat(new[] { _baseDirectory }))
            {
                var fileCount = Directory.GetFiles(dir).Length;
                filesPerDir[dir] = fileCount;
            }

            projectStructure.FilesPerDirectory = filesPerDir
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToDictionary(
                    kvp => Path.GetRelativePath(_baseDirectory, kvp.Key),
                    kvp => kvp.Value);

            projectStructure.LargestDirectories = filesPerDir
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => Path.GetRelativePath(_baseDirectory, kvp.Key))
                .ToList();

            // Analyze files in parallel
            await Parallel.ForEachAsync(files, async (file, token) =>
            {
                var fileStat = await AnalyzeFileAsync(file);
                if (fileStat != null)
                {
                    // Set file category based on extension
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    fileStat.Category = FileTypes
                        .FirstOrDefault(ft => ft.Value.Extensions.Contains(ext))
                        .Value?.Category ?? FileCategory.Other;

                    // Calculate complexity metrics
                    await CalculateFileComplexityAsync(fileStat);

                    lock (fileStats)
                    {
                        fileStats.Add(fileStat);
                    }
                }
            });

            // Calculate all statistics
            stats.ProjectStructure = projectStructure;
            stats.CalculateStatistics(fileStats, _verboseOutput, _baseDirectory);
            
            // Calculate distribution statistics
            CalculateDistributionStatistics(stats, fileStats);
            
            // Calculate complexity statistics
            CalculateComplexityStatistics(stats, fileStats);
            
            // Generate insights
            GenerateCodebaseInsights(stats, fileStats);

            return stats;
        }

        private async Task CalculateFileComplexityAsync(FileStatistics fileStat)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(fileStat.FilePath);
                
                // Calculate indentation levels
                fileStat.IndentationLevels = lines
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.TakeWhile(char.IsWhiteSpace).Count() / 4) // Assuming 4 spaces per level
                    .DefaultIfEmpty(0)
                    .Max();

                // Calculate branching depth (simple heuristic)
                var branchingKeywords = new[] { "if", "else", "switch", "case", "for", "foreach", "while", "do" };
                fileStat.BranchingDepth = lines
                    .Count(l => branchingKeywords.Any(kw => l.TrimStart().StartsWith(kw + " ")));

                // Calculate basic complexity score
                fileStat.Complexity = (fileStat.IndentationLevels * 0.3) + 
                                    (fileStat.BranchingDepth * 0.7);

                // Check if it's a test file
                fileStat.IsTest = fileStat.FilePath.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                 fileStat.FilePath.Contains("spec", StringComparison.OrdinalIgnoreCase);

                // Check if it's a generated file
                fileStat.IsGenerated = fileStat.FilePath.Contains(".generated.") ||
                                     fileStat.FilePath.Contains(".g.") ||
                                     lines.Take(5).Any(l => l.Contains("auto-generated", StringComparison.OrdinalIgnoreCase));

                // Add specific metrics
                fileStat.Metrics["CyclomaticComplexity"] = fileStat.BranchingDepth;
                fileStat.Metrics["StructuralComplexity"] = fileStat.IndentationLevels;
                fileStat.Metrics["LinesOfCode"] = fileStat.NonBlankLines;
            }
            catch
            {
                // If complexity calculation fails, set default values
                fileStat.Complexity = 0;
                fileStat.IndentationLevels = 0;
                fileStat.BranchingDepth = 0;
            }
        }

        private void CalculateDistributionStatistics(CodeStatistics stats, List<FileStatistics> fileStats)
        {
            var totalFiles = fileStats.Count;
            if (totalFiles == 0) return;

            // Calculate file type distribution
            stats.Distribution.FileTypeDistribution = fileStats
                .GroupBy(f => f.Category)
                .ToDictionary(
                    g => g.Key,
                    g => (double)g.Count() / totalFiles * 100);

            // Calculate extension distribution
            stats.Distribution.ExtensionDistribution = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(
                    g => g.Key,
                    g => (double)g.Count() / totalFiles * 100);

            // Calculate average files per directory
            stats.Distribution.AverageFilesPerDirectory = (double)totalFiles / stats.ProjectStructure.DirectoryCount;

            // Calculate code to resource ratio (source files vs. other files)
            var sourceFiles = fileStats.Count(f => f.Category == FileCategory.Source);
            var resourceFiles = fileStats.Count(f => f.Category == FileCategory.Style || 
                                                   f.Category == FileCategory.Markup ||
                                                   f.Category == FileCategory.Script);
            stats.Distribution.CodeToResourceRatio = resourceFiles > 0 ? 
                (double)sourceFiles / resourceFiles : 
                sourceFiles;

            // Calculate documentation ratio
            var docFiles = fileStats.Count(f => f.Category == FileCategory.Documentation);
            stats.Distribution.DocumentationRatio = (double)docFiles / totalFiles * 100;
        }

        private void CalculateComplexityStatistics(CodeStatistics stats, List<FileStatistics> fileStats)
        {
            if (!fileStats.Any()) return;

            // Calculate average complexity
            stats.Complexity.AverageFileComplexity = fileStats.Average(f => f.Complexity);

            // Calculate complexity by extension
            stats.Complexity.ComplexityByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(f => f.Complexity));

            // Identify high complexity files
            var complexityThreshold = stats.Complexity.AverageFileComplexity * 2;
            stats.Complexity.HighComplexityFiles = fileStats
                .Where(f => f.Complexity > complexityThreshold)
                .OrderByDescending(f => f.Complexity)
                .Take(10)
                .Select(f => Path.GetRelativePath(_baseDirectory, f.FilePath))
                .ToList();
        }

        private void GenerateCodebaseInsights(CodeStatistics stats, List<FileStatistics> fileStats)
        {
            var insights = new List<string>();
            var recommendations = new Dictionary<string, string>();
            var metrics = new Dictionary<string, double>();

            // Add key findings
            if (stats.Complexity.AverageFileComplexity > 5)
                insights.Add("High average code complexity detected");

            if (stats.Distribution.DocumentationRatio < 10)
                insights.Add("Low documentation coverage");

            var generatedFiles = fileStats.Count(f => f.IsGenerated);
            if (generatedFiles > fileStats.Count * 0.3)
                insights.Add($"High proportion of generated code ({generatedFiles} files)");

            // Add recommendations
            if (stats.Complexity.HighComplexityFiles.Any())
                recommendations["Complexity"] = "Consider refactoring high complexity files";

            if (stats.Distribution.CodeToResourceRatio > 3)
                recommendations["Resources"] = "Consider organizing resources into a dedicated directory";

            if (stats.ProjectStructure.MaxDepth > 7)
                recommendations["Structure"] = "Deep directory structure detected. Consider flattening the hierarchy";

            // Add metrics
            metrics["MaintenabilityIndex"] = 100 - (stats.Complexity.AverageFileComplexity * 10);
            metrics["TestCoverage"] = (double)fileStats.Count(f => f.IsTest) / fileStats.Count * 100;
            metrics["GeneratedCodeRatio"] = (double)generatedFiles / fileStats.Count * 100;

            stats.Insights.KeyFindings = insights.ToArray();
            stats.Insights.Recommendations = recommendations;
            stats.Insights.Metrics = metrics;
        }

        private IEnumerable<string> GetFilesToAnalyze(string directoryPath)
        {
            return Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    var dirName = Path.GetDirectoryName(file);
                    if (dirName == null) return false;

                    // Check if any parent directory is excluded
                    if (dirName.Split(Path.DirectorySeparatorChar)
                        .Any(dir => ExcludedDirectories.Contains(dir.ToLowerInvariant())))
                        return false;

                    // Skip files without extensions
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension)) return false;

                    // Check if file extension is excluded
                    return !ExcludedFilePatterns.Any(pattern =>
                        extension.EndsWith(pattern, StringComparison.OrdinalIgnoreCase));
                });
        }

        private async Task<FileStatistics?> AnalyzeFileAsync(string filePath)
        {
            try
            {
                // Try to read the first few bytes to check if it's a text file
                byte[] buffer = new byte[512];
                using (var stream = File.OpenRead(filePath))
                {
                    int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (!IsTextFile(buffer, bytesRead))
                        return null;
                }

                string[] lines = await File.ReadAllLinesAsync(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var fileName = Path.GetFileName(filePath).ToLowerInvariant();

                // Skip single-line JS/CSS files
                if ((extension == ".js" || extension == ".css") && lines.Length <= 1)
                    return null;

                // Skip migration files (typically have a timestamp prefix)
                if (extension == ".cs" && (
                    Regex.IsMatch(fileName, @"^\d{14}_[a-z0-9_]+\.cs$", RegexOptions.IgnoreCase) || // Migration files
                    fileName.Contains(".designer.") || // Designer files
                    fileName.Contains(".generated.") || // Generated files
                    fileName.EndsWith(".g.cs") || // Generated files
                    fileName.EndsWith(".g.i.cs") // Generated interface files
                ))
                    return null;

                return new FileStatistics
                {
                    FilePath = filePath,
                    Extension = string.IsNullOrEmpty(extension) ? "(no extension)" : extension,
                    TotalLines = lines.Length,
                    BlankLines = lines.Count(line => string.IsNullOrWhiteSpace(line)),
                    NonBlankLines = lines.Count(line => !string.IsNullOrWhiteSpace(line)),
                    TotalCharacters = lines.Sum(line => line.Length),
                    MaxLineLength = lines.Any() ? lines.Max(line => line.Length) : 0,
                    CharactersPerLine = lines.Length > 0 ?
                        lines.Where(l => !string.IsNullOrWhiteSpace(l))
                             .Average(line => line.Length) : 0
                };
            }
            catch (Exception)
            {
                return null; // Skip files that can't be read
            }
        }

        private bool IsTextFile(byte[] buffer, int bytesRead)
        {
            // Check for common binary file signatures
            if (bytesRead >= 2)
            {
                if (buffer[0] == 0xFF && buffer[1] == 0xFE) return true; // UTF-16 LE BOM
                if (buffer[0] == 0xFE && buffer[1] == 0xFF) return true; // UTF-16 BE BOM
                if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF) return true; // UTF-8 BOM
            }

            // Check for binary content
            int nullCount = 0;
            for (int i = 0; i < bytesRead && i < buffer.Length; i++)
            {
                if (buffer[i] == 0)
                    nullCount++;

                // If more than 10% of the content is null bytes, consider it binary
                if (nullCount > bytesRead * 0.1)
                    return false;
            }

            return true;
        }
    }

    public class DetailedFileInfo
    {
        public string RelativePath { get; set; } = "";
        public int Lines { get; set; }
        public int Characters { get; set; }
        public int MaxLineLength { get; set; }
    }

    public class ExtensionFileGroup
    {
        public string Extension { get; set; } = "";
        public List<DetailedFileInfo> Files { get; set; } = new();
    }

    public class CodeStatistics
    {
        // Basic statistics
        public int TotalFiles { get; set; }
        public int TotalTextFiles { get; set; }
        public Dictionary<string, int> FilesByExtension { get; set; } = new();
        public int TotalLines { get; set; }
        public Dictionary<string, int> LinesByExtension { get; set; } = new();
        public int TotalBlankLines { get; set; }
        public int TotalNonBlankLines { get; set; }
        public long TotalCharacters { get; set; }
        public double AverageCharactersPerFile { get; set; }
        public Dictionary<string, double> AverageCharactersPerFileByExtension { get; set; } = new();
        public double AverageLinesPerFile { get; set; }
        public Dictionary<string, double> AverageLinesPerFileByExtension { get; set; } = new();
        public int MaxCharactersPerFile { get; set; }
        public Dictionary<string, int> MaxCharactersPerFileByExtension { get; set; } = new();
        public int MaxLinesPerFile { get; set; }
        public Dictionary<string, int> MaxLinesPerFileByExtension { get; set; } = new();
        public double AverageCharactersPerLine { get; set; }
        public Dictionary<string, double> AverageCharactersPerLineByExtension { get; set; } = new();
        public int MaxCharactersPerLine { get; set; }
        public Dictionary<string, int> MaxCharactersPerLineByExtension { get; set; } = new();

        // Project structure information
        public ProjectStructureInfo ProjectStructure { get; set; } = new();

        // Distribution statistics
        public DistributionStatistics Distribution { get; set; } = new();

        // Code complexity statistics
        public CodeComplexityStats Complexity { get; set; } = new();

        // Codebase insights
        public CodebaseInsights Insights { get; set; } = new();

        [JsonIgnore]
        public string? FileWithMostCharacters { get; set; }
        [JsonIgnore]
        public string? FileWithMostLines { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ExtensionFileGroup>? DetailedFilesByExtension { get; set; }

        public void CalculateStatistics(List<FileStatistics> fileStats, bool includeVerboseOutput = false, string? baseDirectory = null)
        {
            if (!fileStats.Any()) return;

            TotalFiles = fileStats.Count;
            TotalTextFiles = fileStats.Count;

            // Calculate extension-based statistics
            FilesByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Calculate line statistics
            TotalLines = fileStats.Sum(f => f.TotalLines);
            TotalBlankLines = fileStats.Sum(f => f.BlankLines);
            TotalNonBlankLines = fileStats.Sum(f => f.NonBlankLines);

            LinesByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Sum(f => f.TotalLines))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Calculate character statistics
            TotalCharacters = fileStats.Sum(f => f.TotalCharacters);
            AverageCharactersPerFile = fileStats.Average(f => f.TotalCharacters);

            AverageCharactersPerFileByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Average(f => f.TotalCharacters))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Calculate lines per file statistics
            AverageLinesPerFile = fileStats.Average(f => f.TotalLines);

            AverageLinesPerFileByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Average(f => f.TotalLines))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Calculate maximum values
            var fileWithMostChars = fileStats.MaxBy(f => f.TotalCharacters);
            if (fileWithMostChars != null)
            {
                MaxCharactersPerFile = fileWithMostChars.TotalCharacters;
                FileWithMostCharacters = fileWithMostChars.FilePath;
            }

            MaxCharactersPerFileByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Max(f => f.TotalCharacters))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var fileWithMostLines = fileStats.MaxBy(f => f.TotalLines);
            if (fileWithMostLines != null)
            {
                MaxLinesPerFile = fileWithMostLines.TotalLines;
                FileWithMostLines = fileWithMostLines.FilePath;
            }

            MaxLinesPerFileByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Max(f => f.TotalLines))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Calculate characters per line statistics
            AverageCharactersPerLine = TotalNonBlankLines > 0 ?
                (double)TotalCharacters / TotalNonBlankLines : 0;

            AverageCharactersPerLineByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Average(f => f.CharactersPerLine))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            MaxCharactersPerLine = fileStats.Max(f => f.MaxLineLength);

            MaxCharactersPerLineByExtension = fileStats
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.Max(f => f.MaxLineLength))
                .OrderByDescending(kvp => kvp.Value)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Add detailed file information in verbose mode
            if (includeVerboseOutput)
            {
                DetailedFilesByExtension = fileStats
                    .GroupBy(f => f.Extension)
                    .Select(g => new ExtensionFileGroup
                    {
                        Extension = g.Key,
                        Files = g.Select(f => new DetailedFileInfo
                        {
                            RelativePath = baseDirectory != null ? Path.GetRelativePath(baseDirectory, f.FilePath) : f.FilePath,
                            Lines = f.TotalLines,
                            Characters = f.TotalCharacters,
                            MaxLineLength = f.MaxLineLength
                        })
                        .OrderByDescending(f => f.MaxLineLength)  // First sort by max chars per line
                        .ThenByDescending(f => f.Lines)           // Then by number of lines
                        .ThenByDescending(f => f.Characters)      // Finally by total characters
                        .ToList()
                    })
                    .ToList();
            }
        }
    }

    public class FileStatistics
    {
        public string FilePath { get; set; } = "";
        public string Extension { get; set; } = "";
        public FileCategory Category { get; set; } = FileCategory.Other;
        public int TotalLines { get; set; }
        public int BlankLines { get; set; }
        public int NonBlankLines { get; set; }
        public int TotalCharacters { get; set; }
        public int MaxLineLength { get; set; }
        public double CharactersPerLine { get; set; }
        public double Complexity { get; set; }
        public int IndentationLevels { get; set; }
        public int BranchingDepth { get; set; }
        public bool IsGenerated { get; set; }
        public bool IsTest { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
    }
}