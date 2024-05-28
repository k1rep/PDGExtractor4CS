using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace PDGExtractor
{
  internal class DeltaPdgExtractor
  {
    private readonly Dictionary<SyntaxNode, DeltaPdgExtractor.types> _changes;
    private readonly PdgExtractor _before_pdg;
    private readonly PdgExtractor _after_pdg;
    private readonly DirectedGraph _delta_pdg;

    public DeltaPdgExtractor(PdgExtractor before_pdg, PdgExtractor after_pdg)
    {
      this._changes = new Dictionary<SyntaxNode, DeltaPdgExtractor.types>();
      this._after_pdg = after_pdg;
      this._before_pdg = before_pdg;
      this._delta_pdg = after_pdg.getDirectedGraph();
    }

    public void Extract()
    {
      SyntaxTree syntaxTree1 = this._before_pdg.getSyntaxTree();
      SyntaxTree syntaxTree2 = this._after_pdg.getSyntaxTree();
      List<SyntaxNode> traversal1 = this.getTraversal(syntaxTree1);
      List<SyntaxNode> traversal2 = this.getTraversal(syntaxTree2);
      foreach (SyntaxNode key1 in traversal2)
      {
        Console.WriteLine("Checking Node::\t" + key1.ToString());
        int num = 1;
        foreach (SyntaxNode key2 in traversal1)
        {
          if (key1.ToString() == key2.ToString())
          {
            this._changes.Add(key2, DeltaPdgExtractor.types.Unchanged);
            traversal1.Remove(key2);
            num = 0;
            break;
          }
        }
        if (num == 1)
          this._changes.Add(key1, DeltaPdgExtractor.types.Insertion);
      }
      foreach (SyntaxNode key in traversal1)
        this._changes.Add(key, DeltaPdgExtractor.types.Deletion);
    }

    private List<SyntaxNode> getTraversal(SyntaxTree tree)
    {
      tree.GetRoot();
      List<SyntaxNode> traversal = new List<SyntaxNode>();
      Queue<SyntaxNode> source = new Queue<SyntaxNode>();
      source.Enqueue(tree.GetRoot());
      while (source.Count<SyntaxNode>() != 0)
      {
        SyntaxNode syntaxNode = source.Dequeue();
        traversal.Add(syntaxNode);
        foreach (SyntaxNode childNode in syntaxNode.ChildNodes())
          source.Enqueue(childNode);
      }
      return traversal;
    }

    private static string DotLineType(Tuple<string, object> edgeType)
    {
      switch (edgeType.Item1)
      {
        case "controlflow":
          return "solid";
        case "yield":
          return "bold, color=red";
        case "return":
          return "bold, color=blue";
        case "invoke":
          return "dotted";
        default:
          throw new Exception("Unrecognized edge type: " + (object) edgeType);
      }
    }

    private Func<object, string> generateNodeColourFunc()
    {
      return (Func<object, string>) (node =>
      {
        try
        {
          switch (this._changes[(SyntaxNode) node])
          {
            case DeltaPdgExtractor.types.Unchanged:
              return "black";
            case DeltaPdgExtractor.types.Insertion:
              return "green";
            case DeltaPdgExtractor.types.Deletion:
              return "red";
            default:
              throw new Exception("Unrecognized node type: " + node);
          }
        }
        catch (InvalidCastException ex)
        {
          return "black";
        }
        catch (KeyNotFoundException ex)
        {
          return "black";
        }
      });
    }

    public void ExportToDot(string filename)
    {
      this._delta_pdg.ToDot(filename, (Func<object, string>) (n => n.ToString()), (Func<object, string>) (n => ""), new Func<Tuple<string, object>, string>(DeltaPdgExtractor.DotLineType), this.generateNodeColourFunc());
    }

    private enum types
    {
      Unchanged,
      Insertion,
      Deletion,
    }
  }
}
