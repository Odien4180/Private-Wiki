using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ProjectWiki.Core.Model;

namespace ProjectWiki.Core.Analysis;

public sealed record CSharpSourceFile(string RelativePath, string FullPath);

/// <summary>
/// Extracts entities and relations from C# source using the Roslyn
/// compiler APIs (syntax + semantic model), never regular expressions, as
/// required by <c>docs/analysis-rules.md</c>.
/// </summary>
public sealed class CSharpAnalyzer
{
    public AnalysisResult Analyze(IReadOnlyList<CSharpSourceFile> files)
    {
        var result = new AnalysisResult();
        if (files.Count == 0)
        {
            return result;
        }

        var parsed = files
            .Select(f => (File: f, Tree: CSharpSyntaxTree.ParseText(
                File.ReadAllText(f.FullPath),
                path: f.RelativePath)))
            .ToList();

        var references = new List<MetadataReference>();
        var corlibLocation = typeof(object).Assembly.Location;
        if (!string.IsNullOrEmpty(corlibLocation))
        {
            references.Add(MetadataReference.CreateFromFile(corlibLocation));
        }

        var compilation = CSharpCompilation.Create(
            "ProjectWikiAnalysis",
            parsed.Select(p => p.Tree),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var usedIds = new HashSet<string>();
        var symbolToEntity = new Dictionary<INamedTypeSymbol, Entity>(SymbolEqualityComparer.Default);
        var symbolToId = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);

        // Pass 1: discover every project-declared type and create its Entity.
        foreach (var (file, tree) in parsed)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                {
                    continue;
                }

                if (symbolToEntity.TryGetValue(symbol, out var existing))
                {
                    // Partial class/struct declared across multiple files.
                    if (!existing.Sources.Contains(file.RelativePath))
                    {
                        existing.Sources.Add(file.RelativePath);
                    }

                    continue;
                }

                var id = EntityIdGenerator.MakeUnique(symbol.Name, symbol.ContainingNamespace?.ToDisplayString(), usedIds);
                var entity = new Entity
                {
                    Id = id,
                    Type = declaration is InterfaceDeclarationSyntax ? EntityType.Interface : EntityType.Class,
                    Title = symbol.Name,
                    Sources = new List<string> { file.RelativePath },
                    Symbols = new List<string> { symbol.ToDisplayString() },
                };

                symbolToEntity[symbol] = entity;
                symbolToId[symbol] = id;
                result.Entities.Add(entity);
            }
        }

        // Pass 2: build relations only between known, project-declared types.
        var relationKeys = new HashSet<(string Source, string Target, RelationType Type)>();
        foreach (var (file, tree) in parsed)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !symbolToId.TryGetValue(symbol, out var sourceId))
                {
                    continue;
                }

                if (declaration.BaseList is null)
                {
                    continue;
                }

                foreach (var baseType in declaration.BaseList.Types)
                {
                    var baseSymbolInfo = model.GetSymbolInfo(baseType.Type);
                    if (baseSymbolInfo.Symbol is not INamedTypeSymbol baseSymbol
                        || !symbolToId.TryGetValue(baseSymbol, out var targetId))
                    {
                        continue;
                    }

                    var relationType = baseSymbol.TypeKind == TypeKind.Interface
                        ? RelationType.Implements
                        : RelationType.Inherits;

                    AddRelation(result, relationKeys, sourceId, targetId, relationType, Confidence.High, file.RelativePath, baseType.GetLocation());
                }
            }
        }

        return result;
    }

    private static void AddRelation(
        AnalysisResult result,
        HashSet<(string, string, RelationType)> keys,
        string sourceId,
        string targetId,
        RelationType type,
        Confidence confidence,
        string file,
        Location location)
    {
        if (sourceId == targetId)
        {
            return;
        }

        var key = (sourceId, targetId, type);
        var lineSpan = location.GetLineSpan();
        var evidence = new Evidence
        {
            File = file,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
        };

        if (keys.Add(key))
        {
            result.Relations.Add(new Relation
            {
                Source = sourceId,
                Target = targetId,
                Type = type,
                Confidence = confidence,
                Evidence = new List<Evidence> { evidence },
            });
            return;
        }

        var relation = result.Relations.First(r => r.Source == sourceId && r.Target == targetId && r.Type == type);
        if (!relation.Evidence.Any(e => e.File == file && e.StartLine == evidence.StartLine))
        {
            relation.Evidence.Add(evidence);
        }
    }
}
