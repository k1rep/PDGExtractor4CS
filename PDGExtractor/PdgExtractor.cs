using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;


#nullable disable
namespace PDGExtractor
{
  internal class PdgExtractor
  {
    private readonly SemanticModel _semanticModel;
    private readonly SyntaxTree _syntaxTree;
    private readonly DirectedGraph _pdg = new DirectedGraph();

    public PdgExtractor(SyntaxTree syntaxTree, SemanticModel semanticModel)
    {
      this._syntaxTree = syntaxTree;
      this._semanticModel = semanticModel;
    }

    private IEnumerable<Tuple<SyntaxNode, ISymbol>> AllMethodDeclarationBodies()
    {
      return this._syntaxTree.GetRoot().DescendantNodes().Where<SyntaxNode>((Func<SyntaxNode, bool>) (n => n is BaseMethodDeclarationSyntax || n is AnonymousFunctionExpressionSyntax)).Select<SyntaxNode, Tuple<SyntaxNode, ISymbol>>((Func<SyntaxNode, Tuple<SyntaxNode, ISymbol>>) (m =>
      {
        if (m is BaseMethodDeclarationSyntax declarationSyntax2)
          return Tuple.Create<SyntaxNode, ISymbol>((SyntaxNode) declarationSyntax2.Body, this._semanticModel.GetDeclaredSymbol(m));
        AnonymousFunctionExpressionSyntax expression = m as AnonymousFunctionExpressionSyntax;
        return Tuple.Create<SyntaxNode, ISymbol>((SyntaxNode) expression.Body, Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, (ExpressionSyntax) expression).Symbol.OriginalDefinition);
      }));
    }

    public void Extract()
    {
      List<Tuple<SyntaxNode, ISymbol>> list = this.AllMethodDeclarationBodies().ToList<Tuple<SyntaxNode, ISymbol>>();
      foreach (Tuple<SyntaxNode, ISymbol> tuple in list)
      {
        new ControlFlowGraph(this._pdg, tuple.Item2).AddMethodDeclaration(tuple.Item1, tuple.Item2);
        new MethodCallGraph(this._pdg, this._semanticModel).Visit(tuple.Item1);
      }
      foreach (Tuple<SyntaxNode, ISymbol> tuple in list.Where<Tuple<SyntaxNode, ISymbol>>((Func<Tuple<SyntaxNode, ISymbol>, bool>) (d => !(d.Item1 is AnonymousFunctionExpressionSyntax))))
        new DataFlowGraph(this._pdg, this._semanticModel).AddDataFlowEdges(tuple.Item1, tuple.Item2);
    }

    private static string DotLineType(Tuple<string, object> edgeType)
    {
      switch (edgeType.Item1)
      {
        case "controlflow":
          return "solid, key=0";
        case "yield":
          return "bold, color=crimson, key=0";
        case "return":
          return "bold, color=blue, key=0";
        case "invoke":
          return "dotted, key=2";
        case "dataflow":
          return "dashed, color=darkseagreen4, key=1, label=\"" + edgeType.Item2 + "\"";
        default:
          throw new Exception("Unrecognized edge type: " + (object) edgeType);
      }
    }

    public SyntaxTree getSyntaxTree() => this._syntaxTree;

    public DirectedGraph getDirectedGraph() => this._pdg;

    public Func<object, string> maybeTrailingTrivia(SourceText src)
    {
      return (Func<object, string>) (n =>
      {
        try
        {
          FileLinePositionSpan lineSpan = this._syntaxTree.GetLineSpan(((SyntaxNode) n).Span);
          LinePosition linePosition = lineSpan.StartLinePosition;
          int line1 = linePosition.Line;
          linePosition = lineSpan.EndLinePosition;
          int line2 = linePosition.Line;
          return string.Format("{0}-{1}", (object) line1, (object) line2);
        }
        catch
        {
          switch (n)
          {
            case MethodEntryNode _:
              return ((MethodEntryNode) n).ToSpan();
            case MethodExitNode _:
              return ((MethodExitNode) n).ToSpan();
            case UnkMethodEntryNode _:
              return ((UnkMethodEntryNode) n).ToSpan();
            default:
              return "";
          }
        }
      });
    }

    public void ExportToDot(string filename)
    {
      this._pdg.ToDot(filename, (Func<object, string>) (n => n.ToString()), this.maybeTrailingTrivia(this._syntaxTree.GetText()), new Func<Tuple<string, object>, string>(PdgExtractor.DotLineType));
    }

    public static Tuple<SyntaxTree, SemanticModel> compile(
      string projectDirectory,
      string targetFile)
    {
      Console.WriteLine(projectDirectory);
      Console.WriteLine(targetFile);
      CSharpCompilation csharpCompilation = CSharpCompilation.Create("SampleCompilation", (IEnumerable<SyntaxTree>) Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories).AsParallel<string>().ToDictionary<string, string, SyntaxTree>((Func<string, string>) (filepath => filepath.ToLower()), (Func<string, SyntaxTree>) (filepath => CSharpSyntaxTree.ParseText(File.ReadAllText(filepath), path: filepath))).Values, (IEnumerable<MetadataReference>) new PortableExecutableReference[8]
      {
        MetadataReference.CreateFromFile(typeof (object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (IEnumerable<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (Stack<>).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (SyntaxNode).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (StringBuilder).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (CSharpCompilation).Assembly.Location),
        MetadataReference.CreateFromFile(typeof (ImmutableHashSet<>).Assembly.Location)
      });
      ((Compilation) csharpCompilation).GetDiagnostics(new CancellationToken());
      SyntaxTree syntaxTree = csharpCompilation.SyntaxTrees.First<SyntaxTree>((Func<SyntaxTree, bool>) (s => s.FilePath.ToLower() == targetFile.ToLower()));
      SemanticModel semanticModel = ((Compilation) csharpCompilation).GetSemanticModel(syntaxTree);
      return Tuple.Create<SyntaxTree, SemanticModel>(syntaxTree, semanticModel);
    }

    private static void Main(string[] args)
    {
      if (args.Length != 2)
      {
        Console.WriteLine("Usage <projectFolder> <file>");
      }
      else
      {
        Tuple<SyntaxTree, SemanticModel> tuple = compile(args[0], args[1]);
        PdgExtractor pdgExtractor = new PdgExtractor(tuple.Item1, tuple.Item2);
        pdgExtractor.Extract();
        pdgExtractor.ExportToDot("pdg.dot");
        TypeConstraints typeConstraints = new TypeConstraints();
        typeConstraints.CollectForSingleFile(tuple.Item1, tuple.Item2);
        typeConstraints.ToJson("nameflows.json");
      }
    }
  }
}
