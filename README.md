# CodeAnalysis
A C# tool for analyzing code files in a directory and generating comprehensive statistics about the codebase.

This tool was created by RecursiveAI's "The Bobs" AI Agent. Check out [RecursiveAI](https://recursiveai.net) to learn more about our advanced AI development capabilities and how we can help with your software development needs!

## Features

- Analyzes all text-based code files in a directory and its subdirectories
- Generates detailed statistics about file sizes, line counts, and character counts
- Groups statistics by file extension
- Excludes common non-code files and directories
- Optional verbose mode for detailed per-file statistics
- Outputs results in pretty-formatted JSON

## Usage

```bash
# Basic analysis
CodeAnalysis.exe "path/to/directory"

# Detailed analysis with per-file information
CodeAnalysis.exe "path/to/directory" verbose
```

The tool will generate a JSON file named `code_analysis_results.json` in the current directory.

## Statistics Generated

- Number of files (total and by extension)
- Number of text files
- Line counts:
  - Total lines
  - Lines by file extension
  - Blank lines
  - Non-blank lines
- Character counts:
  - Total characters
  - Average characters per file (total and by extension)
  - Average characters per line (total and by extension)
  - Maximum characters per file (total and by extension)
  - Maximum characters per line (total and by extension)
- Line counts:
  - Average lines per file (total and by extension)
  - Maximum lines per file (total and by extension)

In verbose mode, additional information is included:
- Detailed file information grouped by extension
- For each file:
  - Relative path
  - Number of lines
  - Number of characters
  - Maximum characters per line
- Files are sorted by:
  1. Maximum characters per line (descending)
  2. Number of lines (descending)
  3. Total characters (descending)

## Excluded Files and Directories

### Excluded Directories
- Version Control: `.git`
- Package Directories: `node_modules`, `packages`
- Build Outputs: `bin`, `obj`, `dist`, `.dist`
- Environment Directories: `env`, `.env`, `venv`, `.venv`
- IDE Directories: `.vs`
- Debug/Release Directories: `Debug`, `Release`

### Excluded File Types
- Binary and Package Files:
  - `.exe`, `.dll`, `.pdb`, `.cache`, `.suo`, `.user`, `.lock`
  - `.bin`, `.obj`, `.zip`, `.tar`, `.gz`, `.7z`, `.rar`
- Image Files:
  - `.jpg`, `.jpeg`, `.png`, `.gif`, `.ico`, `.svg`
- Document Files:
  - `.doc`, `.docx`, `.xls`, `.xlsx`, `.pdf`
- Database Files:
  - `.db`, `.sqlite`, `.mdf`, `.ldf`
- Web Assets:
  - `.min.js`, `.min.css`
  - Single-line JS and CSS files
- Font Files:
  - `.woff`, `.woff2`, `.ttf`, `.eot`, `.otf`
- Auto-generated Files:
  - `.designer.cs`, `.generated.cs`, `.g.cs`, `.g.i.cs`
  - Entity Framework migration files (pattern: `YYYYMMDDHHMMSS_Description.cs`)
- Resource Files:
  - `.resources`, `.resx`

## Example Output

```json
{
  "totalFiles": 150,
  "totalTextFiles": 145,
  "filesByExtension": {
    ".cs": 80,
    ".js": 30,
    ".html": 20,
    ".css": 15
  },
  "totalLines": 15000,
  "linesByExtension": {
    ".cs": 8000,
    ".js": 4000,
    ".html": 2000,
    ".css": 1000
  },
  // ... additional statistics ...
  
  // Only included in verbose mode:
  "detailedFilesByExtension": [
    {
      "extension": ".cs",
      "files": [
        {
          "relativePath": "src/Controllers/UserController.cs",
          "lines": 250,
          "characters": 8500,
          "maxLineLength": 120
        },
        // ... more files ...
      ]
    },
    // ... more extensions ...
  ]
}
```

## Notes

- All statistics grouped by extension are sorted by value in descending order
- File paths in verbose mode are relative to the input directory
- Binary files and known non-text files are automatically excluded
- The tool uses parallel processing for better performance with large directories