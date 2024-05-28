using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;


#nullable disable
namespace PDGExtractor
{
  internal class DataFlowGraph : CSharpSyntaxWalker
  {
    private readonly DirectedGraph _graph;
    private readonly SemanticModel _semanticModel;
    public const string DataFlowEdge = "dataflow";
    private readonly Stack<ImmutableDictionary<ISymbol, ImmutableHashSet<object>>> _previousOutFlow = new Stack<ImmutableDictionary<ISymbol, ImmutableHashSet<object>>>();
    private readonly Stack<ImmutableDictionary<ISymbol, ImmutableHashSet<object>>> _continueOutFlow = new Stack<ImmutableDictionary<ISymbol, ImmutableHashSet<object>>>();
    private readonly Stack<ImmutableDictionary<ISymbol, ImmutableHashSet<object>>> _breakOutFlow = new Stack<ImmutableDictionary<ISymbol, ImmutableHashSet<object>>>();

    public DataFlowGraph(DirectedGraph graph, SemanticModel semanticModel)
      : base()
    {
      this._graph = graph;
      this._semanticModel = semanticModel;
    }

    private void AddNextNode(ExpressionSyntax node)
    {
      SyntaxNode syntaxNode = (SyntaxNode) node;
      while (syntaxNode != null & !this._graph.ContainsNode((object) syntaxNode))
        syntaxNode = syntaxNode.Parent;
      DataFlowAnalysis dataFlowAnalysis = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.AnalyzeDataFlow(this._semanticModel, node);
      Dictionary<ISymbol, ImmutableHashSet<object>> dictionary = this._previousOutFlow.Pop().ToDictionary<KeyValuePair<ISymbol, ImmutableHashSet<object>>, ISymbol, ImmutableHashSet<object>>((Func<KeyValuePair<ISymbol, ImmutableHashSet<object>>, ISymbol>) (s => s.Key), (Func<KeyValuePair<ISymbol, ImmutableHashSet<object>>, ImmutableHashSet<object>>) (s => s.Value));
      foreach (ISymbol symbol in dataFlowAnalysis.ReadInside.Except<ISymbol>((IEnumerable<ISymbol>) dataFlowAnalysis.VariablesDeclared))
      {
        ISymbol originalDefinition = symbol.OriginalDefinition;
        if (dictionary.ContainsKey(originalDefinition))
        {
          if (originalDefinition.IsImplicitlyDeclared && originalDefinition is IParameterSymbol)
          {
            originalDefinition = (ISymbol) originalDefinition.ContainingType.OriginalDefinition;
            dictionary[originalDefinition] = new HashSet<object>()
            {
              (object) originalDefinition
            }.ToImmutableHashSet<object>();
          }
          foreach (object fromNode in dictionary[originalDefinition])
            this._graph.AddEdge(fromNode, (object) syntaxNode, Tuple.Create<string, object>("dataflow", (object) originalDefinition));
        }
      }
      foreach (ISymbol symbol in dataFlowAnalysis.WrittenInside.Union<ISymbol>((IEnumerable<ISymbol>) dataFlowAnalysis.AlwaysAssigned))
      {
        if (symbol.IsImplicitlyDeclared && symbol is IParameterSymbol)
        {
          INamedTypeSymbol originalDefinition = symbol.ContainingType.OriginalDefinition;
          this._graph.AddEdge((object) syntaxNode, (object) symbol, Tuple.Create<string, object>("dataflow", (object) originalDefinition));
        }
        else
          dictionary[symbol] = new HashSet<object>()
          {
            (object) syntaxNode
          }.ToImmutableHashSet<object>();
      }
      foreach (ISymbol key in dataFlowAnalysis.VariablesDeclared)
        dictionary[key] = new HashSet<object>()
        {
          (object) syntaxNode
        }.ToImmutableHashSet<object>();
      this._previousOutFlow.Push(dictionary.ToImmutableDictionary<ISymbol, ImmutableHashSet<object>>());
    }

    public override void Visit(SyntaxNode node)
    {
      switch (node)
      {
        case ExpressionSyntax _:
          this.AddNextNode((ExpressionSyntax) node);
          break;
        case VariableDeclarationSyntax _:
          this.AddDeclarations((VariableDeclarationSyntax) node);
          break;
      }
      base.Visit(node);
    }

    private void AddDeclarations(VariableDeclarationSyntax node)
    {
      SyntaxNode node1 = (SyntaxNode) node;
      while (!this._graph.ContainsNode((object) node1))
        node1 = node1.Parent;
      foreach (VariableDeclaratorSyntax variable in node.Variables)
      {
        if (variable.Initializer != null)
          this.Visit((SyntaxNode) variable.Initializer.Value);
      }
      Dictionary<ISymbol, ImmutableHashSet<object>> dictionary = this._previousOutFlow.Pop().ToDictionary<KeyValuePair<ISymbol, ImmutableHashSet<object>>, ISymbol, ImmutableHashSet<object>>((Func<KeyValuePair<ISymbol, ImmutableHashSet<object>>, ISymbol>) (s => s.Key), (Func<KeyValuePair<ISymbol, ImmutableHashSet<object>>, ImmutableHashSet<object>>) (s => s.Value));
      foreach (VariableDeclaratorSyntax variable in node.Variables)
      {
        ISymbol declaredSymbol = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(this._semanticModel, variable);
        dictionary[declaredSymbol] = new HashSet<object>()
        {
          (object) node1
        }.ToImmutableHashSet<object>();
      }
      this._previousOutFlow.Push(dictionary.ToImmutableDictionary<ISymbol, ImmutableHashSet<object>>());
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
      this.Visit((SyntaxNode) node.Expression);
      SyntaxNode syntaxNode = (SyntaxNode) node;
      while (syntaxNode != null & !this._graph.ContainsNode((object) syntaxNode))
        syntaxNode = syntaxNode.Parent;
      foreach (ArgumentSyntax argumentSyntax in node.ArgumentList.Arguments)
      {
        if (argumentSyntax.Expression is AnonymousFunctionExpressionSyntax)
        {
          ISymbol originalDefinition = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, argumentSyntax.Expression).Symbol.OriginalDefinition;
          this._graph.AddEdge((object) syntaxNode, (object) new MethodEntryNode(originalDefinition), Tuple.Create<string, object>("dataflow", (object) originalDefinition));
          this.AddDataFlowEdges((SyntaxNode) ((AnonymousFunctionExpressionSyntax) argumentSyntax.Expression).Body, originalDefinition, this._previousOutFlow.Peek());
        }
        else
          this.Visit((SyntaxNode) argumentSyntax.Expression);
      }
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Condition);
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> immutableDictionary = this._previousOutFlow.Peek();
      this.Visit((SyntaxNode) node.Statement);
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> dict2 = this._previousOutFlow.Pop();
      this._previousOutFlow.Push(immutableDictionary);
      this.Visit((SyntaxNode) node.Else);
      this._previousOutFlow.Push(this.MergeDicts(this._previousOutFlow.Pop(), dict2));
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Condition);
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> dict2 = this._previousOutFlow.Peek();
      this.Visit((SyntaxNode) node.Statement);
      this._previousOutFlow.Push(this.MergeDicts(this._previousOutFlow.Pop(), dict2));
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Expression);
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> dict1 = this._previousOutFlow.Pop().Add(Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(this._semanticModel, node).OriginalDefinition, new HashSet<object>()
      {
        (object) node.Expression
      }.ToImmutableHashSet<object>());
      this._previousOutFlow.Push(dict1);
      this.Visit((SyntaxNode) node.Statement);
      this._previousOutFlow.Push(this.MergeDicts(dict1, this._previousOutFlow.Pop()));
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Declaration);
      foreach (SyntaxNode initializer in node.Initializers)
        this.Visit(initializer);
      this.Visit((SyntaxNode) node.Condition);
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> dict1 = this._previousOutFlow.Peek();
      this.Visit((SyntaxNode) node.Statement);
      foreach (SyntaxNode incrementor in node.Incrementors)
        this.Visit(incrementor);
      this._previousOutFlow.Push(this.MergeDicts(dict1, this._previousOutFlow.Pop()));
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Statement);
      this.Visit((SyntaxNode) node.Condition);
    }

    private ImmutableDictionary<ISymbol, ImmutableHashSet<object>> MergeDicts(
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> dict1,
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> dict2)
    {
      ImmutableHashSet<ISymbol> immutableHashSet = dict1.Keys.Union<ISymbol>(dict2.Keys).ToImmutableHashSet<ISymbol>();
      Dictionary<ISymbol, ImmutableHashSet<object>> source = new Dictionary<ISymbol, ImmutableHashSet<object>>();
      foreach (ISymbol key in immutableHashSet)
      {
        if (dict1.ContainsKey(key) && dict2.ContainsKey(key))
          source.Add(key, dict1[key].Union((IEnumerable<object>) dict2[key]));
        else if (dict1.ContainsKey(key))
          source.Add(key, dict1[key]);
        else
          source.Add(key, dict2[key]);
      }
      return source.ToImmutableDictionary<ISymbol, ImmutableHashSet<object>>();
    }

    public void AddDataFlowEdges(
      SyntaxNode node,
      ISymbol symbol,
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> existingNode = null)
    {
      if (node == null || symbol == null)
        return;
      DataFlowAnalysis dataFlowAnalysis = this._semanticModel.AnalyzeDataFlow(node);
      if (existingNode == null)
        existingNode = ImmutableDictionary<ISymbol, ImmutableHashSet<object>>.Empty;
      ImmutableHashSet<object> startingInFlows = new HashSet<object>()
      {
        (object) new MethodEntryNode(symbol)
      }.ToImmutableHashSet<object>();
      this._previousOutFlow.Push(dataFlowAnalysis.DataFlowsIn.ToImmutableDictionary<ISymbol, ISymbol, ImmutableHashSet<object>>((Func<ISymbol, ISymbol>) (s => s.OriginalDefinition), (Func<ISymbol, ImmutableHashSet<object>>) (s =>
      {
        if (s is IParameterSymbol && !s.IsImplicitlyDeclared && s.ContainingSymbol.Equals(symbol))
          return startingInFlows;
        if (existingNode.ContainsKey(s.OriginalDefinition))
          return existingNode[s.OriginalDefinition];
        return new HashSet<object>()
        {
          (object) s.OriginalDefinition
        }.ToImmutableHashSet<object>();
      })));
      this.Visit(node);
      ImmutableDictionary<ISymbol, ImmutableHashSet<object>> immutableDictionary = this._previousOutFlow.Pop();
      MethodExitNode toNode = new MethodExitNode(symbol);
      foreach (ISymbol key in dataFlowAnalysis.DataFlowsOut)
      {
        ImmutableHashSet<object> immutableHashSet;
        if (immutableDictionary.TryGetValue(key, out immutableHashSet))
        {
          foreach (object fromNode in immutableHashSet)
            this._graph.AddEdge(fromNode, (object) toNode, Tuple.Create<string, object>("dataflow", (object) key));
        }
      }
    }
  }
}
