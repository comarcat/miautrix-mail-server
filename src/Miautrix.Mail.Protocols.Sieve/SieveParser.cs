using System.Text.RegularExpressions;
using Miautrix.Mail.Domain;
using Miautrix.Mail.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Miautrix.Mail.Protocols.Sieve;

public sealed record SieveParseResult(bool Success, string? ErrorMessage = null, int? ErrorLine = null, int? ErrorColumn = null);

public sealed class SieveParser
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "fileinto", "envelope", "imap4flags", "vacation", "reject", "ereject", "copy", "mailbox", "body"
    };
    private static readonly HashSet<string> ValidCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "require", "if", "elsif", "else", "fileinto", "redirect", "keep", "discard", "stop",
        "setflag", "addflag", "removeflag", "vacation", "reject", "ereject", "set"
    };

    private static readonly HashSet<string> ValidTests = new(StringComparer.OrdinalIgnoreCase)
    {
        "header", "address", "allof", "anyof", "not", "true", "false", "size", "exists", "envelope", "mailboxexists"
    };

    public SieveParseResult Validate(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return new SieveParseResult(false, "Script cannot be empty", 1);
        }

        var lines = script.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int braceDepth = 0;
        bool inBlockComment = false;
        bool inTextBody = false;
        bool afterTextTerminator = false;

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNum = i + 1;
            var leadingWhitespace = lines[i].Length - lines[i].TrimStart().Length;
            var line = lines[i].Trim();

            if (afterTextTerminator)
            {
                afterTextTerminator = false;
                if (line == ";") continue;
            }

            if (inTextBody)
            {
                if (line == ".") { inTextBody = false; afterTextTerminator = true; }
                continue;
            }

            if (inBlockComment)
            {
                int endIdx = line.IndexOf("*/", StringComparison.Ordinal);
                if (endIdx >= 0)
                {
                    inBlockComment = false;
                    line = line[(endIdx + 2)..].Trim();
                }
                else
                {
                    continue;
                }
            }

            if (line.StartsWith("/*"))
            {
                int endIdx = line.IndexOf("*/", 2, StringComparison.Ordinal);
                if (endIdx >= 0)
                {
                    line = line[(endIdx + 2)..].Trim();
                }
                else
                {
                    inBlockComment = true;
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            // Check for unclosed quotes on single line (unless multiline text)
            int quoteCount = line.Count(c => c == '"');
            if (quoteCount % 2 != 0 && !line.Contains("text:"))
            {
                return new SieveParseResult(false, $"Unterminated string literal on line {lineNum}", lineNum);
            }

            // Track block braces
            braceDepth += line.Count(c => c == '{');
            braceDepth -= line.Count(c => c == '}');
            if (braceDepth < 0)
            {
                return new SieveParseResult(false, $"Unexpected closing brace '}}' on line {lineNum}", lineNum, leadingWhitespace + line.IndexOf('}') + 1);
            }

            // Extract first token
            var tokenMatch = Regex.Match(line, @"^[a-zA-Z_][a-zA-Z0-9_]*");
            if (tokenMatch.Success)
            {
                var token = tokenMatch.Value;
                if (!ValidCommands.Contains(token) && !ValidTests.Contains(token) && !line.StartsWith("}") && !line.StartsWith("{"))
                {
                    return new SieveParseResult(false, $"Unknown Sieve command or token '{token}' on line {lineNum}", lineNum, leadingWhitespace + tokenMatch.Index + 1);
                }

                if (token.Equals("require", StringComparison.OrdinalIgnoreCase))
                {
                    var requireStatement = Regex.Match(line, @"^require\b(?<value>.*?);", RegexOptions.IgnoreCase);
                    var required = requireStatement.Success
                        ? Regex.Matches(requireStatement.Groups["value"].Value, "\"([^\"]+)\"")
                        : Regex.Matches(string.Empty, "\"([^\"]+)\"");
                    if (required.Count == 0)
                    {
                        return new SieveParseResult(false, "require must name at least one supported extension", lineNum, leadingWhitespace + tokenMatch.Index + 1);
                    }

                    foreach (Match extension in required)
                    {
                        if (!SupportedExtensions.Contains(extension.Groups[1].Value))
                        {
                            return new SieveParseResult(false, $"Unknown or unsupported Sieve extension '{extension.Groups[1].Value}'", lineNum, leadingWhitespace + requireStatement.Groups["value"].Index + extension.Index + 1);
                        }
                    }
                }
            }
            else if (!line.StartsWith("{") && !line.StartsWith("}") && !line.StartsWith("/*") && !line.StartsWith("#"))
            {
                return new SieveParseResult(false, $"Syntax error on line {lineNum}", lineNum, leadingWhitespace + 1);
            }

            // Sieve commands not ending in '{' or part of block header must end in ';'
            if (line.Contains("text:", StringComparison.OrdinalIgnoreCase))
            {
                inTextBody = true;
                continue;
            }
            if (!line.EndsWith("{") && !line.EndsWith("}") && !line.EndsWith(";") && !line.StartsWith("if", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("elsif", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("else", StringComparison.OrdinalIgnoreCase))
            {
                return new SieveParseResult(false, $"Missing terminating semicolon ';' on line {lineNum}", lineNum, leadingWhitespace + line.Length + 1);
            }
        }

        if (braceDepth != 0)
        {
            return new SieveParseResult(false, "Unmatched opening block brace '{'", lines.Length);
        }

        return new SieveParseResult(true);
    }
}

public sealed class SieveScriptService
{
    private readonly AppDbContext _dbContext;
    private readonly SieveParser _parser;

    public SieveScriptService(AppDbContext dbContext, SieveParser parser)
    {
        _dbContext = dbContext;
        _parser = parser;
    }

    public async Task<SieveParseResult> SetActiveScriptAsync(
        Guid tenantId,
        Guid mailboxId,
        string scriptName,
        string scriptContent,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate script first before touching database
        var validation = _parser.Validate(scriptContent);
        if (!validation.Success)
        {
            return validation;
        }

        // 2. Fetch existing active script
        var existingActive = await _dbContext.SieveScripts
            .FirstOrDefaultAsync(s => s.MailboxId == mailboxId && s.IsActive, cancellationToken);

        // 3. Atomically activate new script and deactivate old
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (existingActive != null)
            {
                existingActive.IsActive = false;
                existingActive.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var newScript = new SieveScript
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                MailboxId = mailboxId,
                Name = scriptName,
                Content = scriptContent,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.SieveScripts.Add(newScript);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new SieveParseResult(true);
    }

    public async Task<SieveScript?> GetActiveScriptAsync(Guid mailboxId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SieveScripts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MailboxId == mailboxId && s.IsActive, cancellationToken);
    }
}
