using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;


#nullable disable
namespace PDGExtractor
{
  internal class ControlFlowGraph : CSharpSyntaxWalker
  {
    private readonly DirectedGraph _graph;
    private readonly Stack<ImmutableHashSet<object>> _previousNodes = new Stack<ImmutableHashSet<object>>();
    private readonly Stack<ImmutableHashSet<object>> _continueFromNodes = new Stack<ImmutableHashSet<object>>();
    private readonly Stack<ImmutableHashSet<object>> _breakingFromNodes = new Stack<ImmutableHashSet<object>>();
    private readonly Stack<ImmutableHashSet<object>> _returnFromNodes = new Stack<ImmutableHashSet<object>>();
    private readonly Stack<ImmutableHashSet<object>> _throwingNodes = new Stack<ImmutableHashSet<object>>();
    private static readonly ImmutableHashSet<object> EmptySet = new HashSet<object>().ToImmutableHashSet<object>();
    private readonly ISymbol _context;
    public const string ControlFlowEdge = "controlflow";
    public const string YieldEdge = "yield";
    public const string ReturnEdge = "return";

    public ControlFlowGraph(DirectedGraph graph, ISymbol context)
      : base()
    {
      this._graph = graph;
      this._context = context;
    }

    private void AddNextNode(object node)
    {
      foreach (object fromNode in this._previousNodes.Pop())
        this._graph.AddEdge(fromNode, node, Tuple.Create<string, object>("controlflow", (object) null), (object) this._context, (object) this._context);
      this._previousNodes.Push(new HashSet<object>()
      {
        node
      }.ToImmutableHashSet<object>());
    }

    public override void VisitBreakStatement(BreakStatementSyntax node)
    {
      base.VisitBreakStatement(node);
      this._breakingFromNodes.Push(this._breakingFromNodes.Pop().Union((IEnumerable<object>) this._previousNodes.Pop()));
      this._previousNodes.Push(ControlFlowGraph.EmptySet);
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Condition);
      ImmutableHashSet<object> immutableHashSet = this._previousNodes.Peek();
      this.Visit((SyntaxNode) node.Statement);
      ImmutableHashSet<object> other = this._previousNodes.Pop();
      this._previousNodes.Push(immutableHashSet);
      if (node.Else != null)
        this.Visit((SyntaxNode) node.Else);
      this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) other));
    }

    public override void Visit(SyntaxNode node)
    {
      if (node is ExpressionSyntax || node is VariableDeclarationSyntax)
      {
        if (node is AnonymousFunctionExpressionSyntax)
          return;
        this.AddNextNode((object) node);
      }
      else
        base.Visit(node);
    }

    public override void VisitContinueStatement(ContinueStatementSyntax node)
    {
      ImmutableHashSet<object> other = this._previousNodes.Pop();
      this._continueFromNodes.Push(this._continueFromNodes.Pop().Union((IEnumerable<object>) other));
      this._previousNodes.Push(ControlFlowGraph.EmptySet);
    }

    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
      this.AddNextNode((object) node);
      this._returnFromNodes.Push(this._returnFromNodes.Pop().Add((object) node));
      this._previousNodes.Pop();
      this._previousNodes.Push(ControlFlowGraph.EmptySet);
    }

    public override void VisitYieldStatement(YieldStatementSyntax node)
    {
      this.AddNextNode((object) node);
      MethodEntryNode fromNode = new MethodEntryNode(this._context);
      foreach (object toNode in this._previousNodes.Peek())
        this._graph.AddEdge((object) fromNode, toNode, Tuple.Create<string, object>("yield", (object) null), (object) this._context, (object) this._context);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Expression);
      object toNode = this._previousNodes.Peek().First<object>();
      this._breakingFromNodes.Push(ControlFlowGraph.EmptySet);
      this._continueFromNodes.Push(ControlFlowGraph.EmptySet);
      this.Visit((SyntaxNode) node.Statement);
      foreach (object fromNode in this._continueFromNodes.Pop().Union((IEnumerable<object>) this._previousNodes.Pop()))
        this._graph.AddEdge(fromNode, toNode, Tuple.Create<string, object>("controlflow", (object) null), (object) this._context, (object) this._context);
      this._previousNodes.Push(this._breakingFromNodes.Pop().Add(toNode));
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
      if (node.Declaration != null)
        this.AddNextNode((object) node.Declaration);
      foreach (object initializer in node.Initializers)
        this.AddNextNode(initializer);
      ImmutableHashSet<object> nodes = this._previousNodes.Peek();
      this.Visit((SyntaxNode) node.Condition);
      object commonExitPoint = this.GetCommonExitPoint((IEnumerable<object>) nodes);
      ImmutableHashSet<object> immutableHashSet = this._previousNodes.Peek();
      this._continueFromNodes.Push(ControlFlowGraph.EmptySet);
      this._breakingFromNodes.Push(ControlFlowGraph.EmptySet);
      this.Visit((SyntaxNode) node.Statement);
      foreach (SyntaxNode incrementor in node.Incrementors)
        this.Visit(incrementor);
      foreach (object fromNode in this._previousNodes.Pop().Union((IEnumerable<object>) this._continueFromNodes.Pop()))
        this._graph.AddEdge(fromNode, commonExitPoint, Tuple.Create<string, object>("controlflow", (object) null), (object) this._context, (object) this._context);
      this._previousNodes.Push(immutableHashSet.Union((IEnumerable<object>) this._breakingFromNodes.Pop()));
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
      this.Visit((SyntaxNode) node.Expression);
      ImmutableHashSet<object> other = this._previousNodes.Peek();
      this._breakingFromNodes.Push(ControlFlowGraph.EmptySet);
      foreach (SwitchSectionSyntax section in node.Sections)
      {
        this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) other));
        this.Visit((SyntaxNode) section);
      }
      this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) this._breakingFromNodes.Pop()));
    }

    public override void VisitTryStatement(TryStatementSyntax node)
    {
      if (node.Finally != null)
      {
        this._returnFromNodes.Push(ControlFlowGraph.EmptySet);
        this.VisitTryCatch(node);
        ImmutableHashSet<object> other = this._returnFromNodes.Pop();
        this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) other));
        if (other.Count <= 0)
          return;
        this._returnFromNodes.Push(this._returnFromNodes.Pop().Union((IEnumerable<object>) other));
      }
      else
        this.VisitTryCatch(node);
    }

    public override void VisitThrowStatement(ThrowStatementSyntax node)
    {
      this.AddNextNode((object) node);
      this._throwingNodes.Push(this._throwingNodes.Pop().Add((object) node));
      this._previousNodes.Pop();
      this._previousNodes.Push(ControlFlowGraph.EmptySet);
    }

    private void VisitTryCatch(TryStatementSyntax node)
    {
      this._throwingNodes.Push(ControlFlowGraph.EmptySet);
      this.Visit((SyntaxNode) node.Block);
      if (node.Catches.Count > 0)
      {
        ImmutableHashSet<object> collection = this._previousNodes.Peek().Union((IEnumerable<object>) this._throwingNodes.Pop());
        HashSet<object> other = new HashSet<object>((IEnumerable<object>) collection);
        foreach (SyntaxNode node1 in node.Catches)
        {
          this.Visit(node1);
          other.UnionWith((IEnumerable<object>) this._previousNodes.Pop());
          this._previousNodes.Push(collection);
        }
        this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) other));
      }
      else
        this._returnFromNodes.Push(this._returnFromNodes.Pop().Union((IEnumerable<object>) this._throwingNodes.Pop()));
    }

    public override void VisitUsingStatement(UsingStatementSyntax node)
    {
      base.VisitUsingStatement(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
      this.Visit((SyntaxNode) node.Condition);
      ImmutableHashSet<object> immutableHashSet = this._previousNodes.Peek();
      this.Visit((SyntaxNode) node.WhenTrue);
      ImmutableHashSet<object> other = this._previousNodes.Pop();
      this._previousNodes.Push(immutableHashSet);
      this.Visit((SyntaxNode) node.WhenFalse);
      this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) other));
    }

    public override void VisitGotoStatement(GotoStatementSyntax node)
    {
      throw new Exception("Goto not supported yet");
    }

    private object GetCommonExitPoint(IEnumerable<object> nodes)
    {
      HashSet<object> source = (HashSet<object>) null;
      foreach (IEnumerable<object> objects in nodes.Select<object, IEnumerable<object>>((Func<object, IEnumerable<object>>) (n => this._graph.GetOutEdgesFrom(n))))
      {
        if (source == null)
          source = new HashSet<object>(objects);
        else
          source.IntersectWith(objects);
      }
      return nodes.Count<object>() == 1 && source.Count<object>() == 0 ? nodes.First<object>() : source.First<object>();
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
      ImmutableHashSet<object> nodes = this._previousNodes.Peek();
      this.Visit((SyntaxNode) node.Condition);
      object commonExitPoint = this.GetCommonExitPoint((IEnumerable<object>) nodes);
      ImmutableHashSet<object> immutableHashSet = this._previousNodes.Peek();
      this._continueFromNodes.Push(ControlFlowGraph.EmptySet);
      this._breakingFromNodes.Push(ControlFlowGraph.EmptySet);
      this.Visit((SyntaxNode) node.Statement);
      foreach (object fromNode in this._previousNodes.Pop().Union((IEnumerable<object>) this._continueFromNodes.Pop()))
        this._graph.AddEdge(fromNode, commonExitPoint, Tuple.Create<string, object>("controlflow", (object) null), (object) this._context, (object) this._context);
      this._previousNodes.Push(immutableHashSet.Union((IEnumerable<object>) this._breakingFromNodes.Pop()));
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
      object commonExitPoint = this.GetCommonExitPoint((IEnumerable<object>) this._previousNodes.Peek());
      this._continueFromNodes.Push(ControlFlowGraph.EmptySet);
      this._breakingFromNodes.Push(ControlFlowGraph.EmptySet);
      this.Visit((SyntaxNode) node.Statement);
      this._previousNodes.Push(this._previousNodes.Pop().Union((IEnumerable<object>) this._continueFromNodes.Pop()));
      this.Visit((SyntaxNode) node.Condition);
      ImmutableHashSet<object> other = this._previousNodes.Pop();
      foreach (object fromNode in other)
        this._graph.AddEdge(fromNode, commonExitPoint, Tuple.Create<string, object>("controlflow", (object) null));
      this._previousNodes.Push(this._breakingFromNodes.Pop().Union((IEnumerable<object>) other));
    }

    public void AddMethodDeclaration(SyntaxNode declarationBody, ISymbol rootSymbol)
    {
      MethodEntryNode toNode = new MethodEntryNode(rootSymbol);
      this._previousNodes.Push(new HashSet<object>()
      {
        (object) toNode
      }.ToImmutableHashSet<object>());
      this._returnFromNodes.Push(ControlFlowGraph.EmptySet);
      this._throwingNodes.Push(ControlFlowGraph.EmptySet);
      this.Visit(declarationBody);
      MethodExitNode methodExitNode = new MethodExitNode(rootSymbol);
      foreach (object fromNode in this._previousNodes.Pop().Union((IEnumerable<object>) this._returnFromNodes.Pop()).Union((IEnumerable<object>) this._throwingNodes.Pop()))
        this._graph.AddEdge(fromNode, (object) methodExitNode, Tuple.Create<string, object>("controlflow", (object) null), (object) this._context, (object) this._context);
      this._graph.AddEdge((object) methodExitNode, (object) toNode, Tuple.Create<string, object>("return", (object) rootSymbol));
    }
  }
}
