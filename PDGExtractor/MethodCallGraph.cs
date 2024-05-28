using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#nullable disable
namespace PDGExtractor
{
  internal class MethodCallGraph : CSharpSyntaxWalker
  {
    private readonly DirectedGraph _graph;
    private readonly SemanticModel _semanticModel;
    public const string MethodInvokeEdge = "invoke";

    public MethodCallGraph(DirectedGraph graph, SemanticModel semanticModel)
      : base()
    {
      this._graph = graph;
      this._semanticModel = semanticModel;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
      base.VisitInvocationExpression(node);
      SyntaxNode syntaxNode = (SyntaxNode) node;
      while (syntaxNode != null & !this._graph.ContainsNode((object) syntaxNode))
        syntaxNode = syntaxNode.Parent;
      IMethodSymbol symbol = (IMethodSymbol) Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, (ExpressionSyntax) node).Symbol;
      if (symbol != null)
      {
        this._graph.AddEdge((object) syntaxNode, (object) new MethodEntryNode((ISymbol) symbol.OriginalDefinition), Tuple.Create<string, object>("invoke", (object) symbol), toNodeContext: (object) symbol.OriginalDefinition);
      }
      else
      {
        string str = "Unk." + (object) node.Expression.DescendantTokens().Last<SyntaxToken>();
        this._graph.AddEdge((object) syntaxNode, (object) new UnkMethodEntryNode(str), Tuple.Create<string, object>("invoke", (object) str), toNodeContext: (object) str);
      }
    }

    public override void Visit(SyntaxNode node)
    {
      if (node is AnonymousFunctionExpressionSyntax)
        return;
      base.Visit(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
      SyntaxNode syntaxNode = (SyntaxNode) node;
      while (!this._graph.ContainsNode((object) syntaxNode))
        syntaxNode = syntaxNode.Parent;
      IMethodSymbol symbol = (IMethodSymbol) Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, (ExpressionSyntax) node).Symbol;
      if (symbol != null)
      {
        this._graph.AddEdge((object) syntaxNode, (object) new MethodEntryNode((ISymbol) symbol.OriginalDefinition), Tuple.Create<string, object>("invoke", (object) symbol), toNodeContext: (object) symbol.OriginalDefinition);
      }
      else
      {
        string str = node.Type.ToString() + ".cstr";
        this._graph.AddEdge((object) syntaxNode, (object) new UnkMethodEntryNode(str), Tuple.Create<string, object>("invoke", (object) str), toNodeContext: (object) str);
      }
      base.VisitObjectCreationExpression(node);
    }
  }
}
