using System.CommandLine;
using System.Text.Json;
using WrapGod.Abstractions.Config;
using WrapGod.Manifest;
using WrapGod.Manifest.Config;

namespace WrapGod.Cli;

internal static class ExplainCommand
{
    public static Command Create()
    {
        var symbolArg = new Argument<string>(
            "symbol",
            "Type or member name to look up (e.g. HttpClient, ILogger.LogInformation)");

        var manifestOption = new Option<FileInfo?>(
            ["--manifest", "-m"],
            "Path to the WrapGod manifest JSON file (auto-detects *.wrapgod.json)");

        var configOption = new Option<FileInfo?>(
            ["--config", "-c"],
            "Path to the WrapGod config JSON file");

        var command = new Command("explain", "Show traceability info for a type or member")
        {
            symbolArg,
            manifestOption,
            configOption
        };

        command.SetHandler(HandleAsync, symbolArg, manifestOption, configOption);
        return command;
    }

    private static async Task HandleAsync(string symbol, FileInfo? manifestFile, FileInfo? configFile)
    {
        Console.WriteLine("WrapGod Explain");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        // Auto-detect manifest if not provided
        if (manifestFile is null)
        {
            var found = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.wrapgod.json");
            if (found.Length == 0)
            {
                Console.Error.WriteLine("No manifest file found. Use --manifest or run from a directory with *.wrapgod.json.");
                return;
            }

            manifestFile = new FileInfo(found[0]);
        }

        if (!manifestFile.Exists)
        {
            Console.Error.WriteLine($"Manifest not found: {manifestFile.FullName}");
            return;
        }

        var json = await File.ReadAllTextAsync(manifestFile.FullName);
        var manifest = JsonSerializer.Deserialize<ApiManifest>(json);

        if (manifest is null)
        {
            Console.Error.WriteLine("Failed to deserialize manifest.");
            return;
        }

        // Load config if provided
        WrapGodConfig? config = null;
        if (configFile is not null && configFile.Exists)
        {
            config = JsonConfigLoader.LoadFromFile(configFile.FullName);
        }
        else
        {
            var defaultConfig = Path.Combine(Directory.GetCurrentDirectory(), "wrapgod.config.json");
            if (File.Exists(defaultConfig))
            {
                config = JsonConfigLoader.LoadFromFile(defaultConfig);
            }
        }

        // Search for the symbol as a type
        var matchedTypes = manifest.Types
            .Where(t => MatchesSymbol(t, symbol))
            .ToList();

        if (matchedTypes.Count == 0)
        {
            // Search for the symbol as a member across all types
            var memberMatches = manifest.Types
                .SelectMany(t => t.Members.Select(m => (Type: t, Member: m)))
                .Where(pair => pair.Member.Name.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
                               $"{pair.Type.Name}.{pair.Member.Name}".Equals(symbol, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (memberMatches.Count == 0)
            {
                Console.WriteLine($"Symbol '{symbol}' not found in manifest.");
                Console.WriteLine();
                Console.WriteLine("Suggestions:");
                Console.WriteLine("  - Check spelling (search is case-insensitive)");
                Console.WriteLine("  - Use the fully qualified name: Namespace.TypeName");
                Console.WriteLine("  - Use TypeName.MemberName for members");
                return;
            }

            foreach (var (type, member) in memberMatches)
            {
                PrintMemberInfo(manifest, type, member, config);
            }

            return;
        }

        foreach (var type in matchedTypes)
        {
            PrintTypeInfo(manifest, type, config);
        }
    }

    private static bool MatchesSymbol(ApiTypeNode type, string symbol)
    {
        return type.Name.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
               type.FullName.Equals(symbol, StringComparison.OrdinalIgnoreCase) ||
               type.StableId.Equals(symbol, StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintTypeInfo(ApiManifest manifest, ApiTypeNode type, WrapGodConfig? config)
    {
        Console.WriteLine($"Type: {type.FullName}");
        Console.WriteLine($"  Kind:       {type.Kind}");
        Console.WriteLine($"  Assembly:   {manifest.Assembly.Name}");
        Console.WriteLine($"  Version:    {manifest.Assembly.Version}");
        Console.WriteLine($"  StableId:   {type.StableId}");
        Console.WriteLine($"  Members:    {type.Members.Count}");

        if (type.IsGenericType)
        {
            Console.WriteLine($"  Generic:    yes (definition={type.IsGenericTypeDefinition})");
        }

        if (type.Presence is not null)
        {
            Console.WriteLine($"  Version presence:");
            if (type.Presence.IntroducedIn is not null)
                Console.WriteLine($"    Introduced: {type.Presence.IntroducedIn}");
            if (type.Presence.RemovedIn is not null)
                Console.WriteLine($"    Removed:    {type.Presence.RemovedIn}");
            if (type.Presence.ChangedIn is not null)
                Console.WriteLine($"    Changed:    {type.Presence.ChangedIn}");
        }

        // Wrapper generation info
        var wrapperInterface = $"IWrapped{type.Name}";
        var facadeClass = $"{type.Name}Facade";
        Console.WriteLine();
        Console.WriteLine($"  Generated wrapper:");
        Console.WriteLine($"    Interface: {wrapperInterface}");
        Console.WriteLine($"    Facade:    {facadeClass}");

        // Config rules
        if (config is not null)
        {
            var typeConfig = config.Types
                .FirstOrDefault(tc => tc.SourceType.Equals(type.FullName, StringComparison.OrdinalIgnoreCase) ||
                                      tc.SourceType.Equals(type.Name, StringComparison.OrdinalIgnoreCase));

            if (typeConfig is not null)
            {
                Console.WriteLine();
                Console.WriteLine($"  Config rules:");
                Console.WriteLine($"    Include:    {typeConfig.Include?.ToString() ?? "default (true)"}");
                if (!string.IsNullOrEmpty(typeConfig.TargetName))
                    Console.WriteLine($"    TargetName: {typeConfig.TargetName}");
                if (typeConfig.Members.Count > 0)
                    Console.WriteLine($"    Member overrides: {typeConfig.Members.Count}");
            }
        }

        Console.WriteLine();
    }

    private static void PrintMemberInfo(ApiManifest manifest, ApiTypeNode type, ApiMemberNode member, WrapGodConfig? config)
    {
        Console.WriteLine($"Member: {type.FullName}.{member.Name}");
        Console.WriteLine($"  Kind:       {member.Kind}");
        Console.WriteLine($"  Return:     {member.ReturnType ?? "void"}");
        Console.WriteLine($"  Assembly:   {manifest.Assembly.Name}");
        Console.WriteLine($"  Version:    {manifest.Assembly.Version}");

        if (member.Parameters.Count > 0)
        {
            Console.WriteLine($"  Parameters:");
            foreach (var param in member.Parameters)
            {
                Console.WriteLine($"    {param.Type} {param.Name}");
            }
        }

        if (member.Presence is not null)
        {
            Console.WriteLine($"  Version presence:");
            if (member.Presence.IntroducedIn is not null)
                Console.WriteLine($"    Introduced: {member.Presence.IntroducedIn}");
            if (member.Presence.RemovedIn is not null)
                Console.WriteLine($"    Removed:    {member.Presence.RemovedIn}");
            if (member.Presence.ChangedIn is not null)
                Console.WriteLine($"    Changed:    {member.Presence.ChangedIn}");
        }

        Console.WriteLine();
    }
}
