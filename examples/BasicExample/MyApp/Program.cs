using WrapGod.Abstractions.Config;
using WrapGod.Fluent;
using WrapGod.Manifest.Config;
using WrapGod.TypeMap;
using WrapGod.TypeMap.Generation;

// ────────────────────────────────────────────────────────────────
// WrapGod Basic Example
//
// This example demonstrates the full WrapGod pipeline:
//   1. Load a JSON config (wrapgod.json) defining which vendor types to wrap
//   2. Build a TypeMappingPlan via the TypeMappingPlanner
//   3. Emit generated mapper source code via the TypeMapperEmitter
//   4. Show the equivalent fluent DSL configuration
// ────────────────────────────────────────────────────────────────

Console.WriteLine("=== WrapGod Basic Example ===");
Console.WriteLine();

// ── Step 1: Load the JSON config ────────────────────────────────
var configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wrapgod.json");
if (!File.Exists(configPath))
{
    // Fallback: try relative to the working directory
    configPath = Path.Combine(Directory.GetCurrentDirectory(), "wrapgod.json");
}

WrapGodConfig config;
if (File.Exists(configPath))
{
    config = JsonConfigLoader.LoadFromFile(configPath);
    Console.WriteLine($"Loaded config from: {configPath}");
}
else
{
    // Build config in-memory as fallback
    config = new WrapGodConfig();
    config.Types.Add(new TypeConfig
    {
        SourceType = "VendorLib.HttpClient",
        Include = true,
        TargetName = "IHttpClient",
    });
    config.Types[0].Members.Add(new MemberConfig { SourceMember = "Get", Include = true, TargetName = "SendGet" });
    config.Types[0].Members.Add(new MemberConfig { SourceMember = "Post", Include = true, TargetName = "SendPost" });
    config.Types[0].Members.Add(new MemberConfig { SourceMember = "Timeout", Include = true });

    config.Types.Add(new TypeConfig
    {
        SourceType = "VendorLib.Logger",
        Include = true,
        TargetName = "ILogger",
    });
    config.Types[1].Members.Add(new MemberConfig { SourceMember = "Info", Include = true });
    config.Types[1].Members.Add(new MemberConfig { SourceMember = "Warn", Include = true });
    config.Types[1].Members.Add(new MemberConfig { SourceMember = "Error", Include = true });

    Console.WriteLine("Config file not found; using in-memory config.");
}

Console.WriteLine($"  Types configured: {config.Types.Count}");
Console.WriteLine();

// ── Step 2: Build a TypeMappingPlan ─────────────────────────────
var overrides = new List<TypeMappingOverride>
{
    new() { SourceType = "VendorLib.LogLevel", Kind = TypeMappingKind.Enum },
};

var plan = TypeMappingPlanner.BuildPlan(config, overrides);
Console.WriteLine($"TypeMappingPlan built: {plan.Mappings.Count} mapping(s)");
foreach (var mapping in plan.Mappings)
{
    Console.WriteLine($"  {mapping.SourceType} -> {mapping.DestinationType} ({mapping.Kind})");
    foreach (var member in mapping.MemberMappings)
    {
        Console.WriteLine($"    {member.SourceMember} -> {member.DestinationMember}");
    }
}

Console.WriteLine();

// ── Step 3: Emit generated mapper source code ───────────────────
var generatedCode = TypeMapperEmitter.Emit(plan);
Console.WriteLine("Generated mapper source code:");
Console.WriteLine(new string('-', 60));
Console.WriteLine(generatedCode);
Console.WriteLine(new string('-', 60));
Console.WriteLine();

// ── Step 4: Equivalent fluent DSL configuration ─────────────────
Console.WriteLine("Equivalent fluent DSL configuration:");
var fluentPlan = WrapGodConfiguration.Create()
    .ForAssembly("VendorLib")
    .WrapType("VendorLib.HttpClient")
        .As("IHttpClient")
        .WrapMethod("Get").As("SendGet")
        .WrapMethod("Post").As("SendPost")
        .WrapProperty("Timeout")
        .ExcludeMember("Dispose")
    .WrapType("VendorLib.Logger")
        .As("ILogger")
        .WrapAllPublicMembers()
    .MapType("VendorLib.LogLevel", "AppLogLevel")
    .Build();

Console.WriteLine($"  Assembly: {fluentPlan.AssemblyName}");
Console.WriteLine($"  Type directives: {fluentPlan.TypeDirectives.Count}");
Console.WriteLine($"  Type mappings: {fluentPlan.TypeMappings.Count}");

foreach (var td in fluentPlan.TypeDirectives)
{
    Console.WriteLine($"  {td.SourceType} -> {td.TargetName ?? "(same)"}");
    foreach (var md in td.MemberDirectives)
    {
        Console.WriteLine($"    {md.SourceName} -> {md.TargetName ?? "(same)"} ({md.Kind})");
    }
}

Console.WriteLine();
Console.WriteLine("Done.");
