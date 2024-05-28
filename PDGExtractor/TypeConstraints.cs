using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

#nullable disable
namespace PDGExtractor
{
  public class TypeConstraints
  {
    public readonly Dictionary<AbstractNode, HashSet<AbstractNode>> AllRelationships = new Dictionary<AbstractNode, HashSet<AbstractNode>>();

    public void AddFromCompilation(CSharpCompilation compilation)
    {
      foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
        new FileTypeRelationCollector(((Compilation) compilation).GetSemanticModel(syntaxTree), this.AllRelationships).Visit(syntaxTree.GetRoot());
    }

    public void CollectForSingleFile(SyntaxTree tree, SemanticModel model)
    {
      new FileTypeRelationCollector(model, this.AllRelationships).Visit(tree.GetRoot());
    }

    public void RemoveSelfLinks()
    {
      foreach (AbstractNode key in this.AllRelationships.Where<KeyValuePair<AbstractNode, HashSet<AbstractNode>>>((Func<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, bool>) (kv => kv.Value.Contains(kv.Key))).Select<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, AbstractNode>((Func<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, AbstractNode>) (kv => kv.Key)).ToArray<AbstractNode>())
        this.AllRelationships[key].Remove(key);
    }

    public void ToDot(
      string filename,
      Func<string, int, string> pathProcessor,
      List<HashSet<AbstractNode>> grouping)
    {
      using (StreamWriter streamWriter = new StreamWriter(filename))
      {
        streamWriter.WriteLine("digraph \"extractedGraph\"{");
        HashSet<AbstractNode> abstractNodeSet1 = new HashSet<AbstractNode>(this.AllRelationships.Select<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, AbstractNode>((Func<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, AbstractNode>) (n => n.Key)).Concat<AbstractNode>(this.AllRelationships.SelectMany<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, AbstractNode>((Func<KeyValuePair<AbstractNode, HashSet<AbstractNode>>, IEnumerable<AbstractNode>>) (n => (IEnumerable<AbstractNode>) n.Value))));
        Dictionary<AbstractNode, int> dictionary = new Dictionary<AbstractNode, int>();
        int num1 = 0;
        int num2 = 0;
        foreach (HashSet<AbstractNode> abstractNodeSet2 in grouping)
        {
          ++num2;
          streamWriter.WriteLine("subgraph cluster_" + num2.ToString() + " {style=filled; color = lightgrey; node[style = filled, shape=box; color = white]; ");
          foreach (AbstractNode key in abstractNodeSet2)
          {
            Debug.Assert(abstractNodeSet1.Remove(key));
            string str = "Unk/External";
            if (key.Location != (Location) null && key.Location.SourceTree != null)
              str = pathProcessor(key.Location.SourceTree.FilePath, key.Location.GetLineSpan().StartLinePosition.Line);
            streamWriter.WriteLine("n{0} [label=\"{1}\"]; ", (object) num1, (object) key.ToDotString());
            dictionary[key] = num1;
            ++num1;
          }
          streamWriter.WriteLine("} //subgraph cluster_" + num2.ToString());
        }
        foreach (KeyValuePair<AbstractNode, HashSet<AbstractNode>> allRelationship in this.AllRelationships)
        {
          AbstractNode key1 = allRelationship.Key;
          foreach (AbstractNode key2 in allRelationship.Value)
          {
            if (dictionary.ContainsKey(key1) && dictionary.ContainsKey(key2))
              streamWriter.WriteLine("n{0}->n{1};", (object) dictionary[key1], (object) dictionary[key2]);
          }
        }
        streamWriter.WriteLine("}");
      }
    }

    public void ToJson(string filename)
    {
      HashSet<AbstractNode> abstractNodeSet = new HashSet<AbstractNode>();
      abstractNodeSet.UnionWith((IEnumerable<AbstractNode>) this.AllRelationships.Keys);
      abstractNodeSet.UnionWith(this.AllRelationships.Values.SelectMany<HashSet<AbstractNode>, AbstractNode>((Func<HashSet<AbstractNode>, IEnumerable<AbstractNode>>) (n => (IEnumerable<AbstractNode>) n)));
      Dictionary<AbstractNode, int> nodeToId = new Dictionary<AbstractNode, int>();
      List<Dictionary<string, string>> dictionaryList = new List<Dictionary<string, string>>();
      foreach (AbstractNode abstractNode in abstractNodeSet)
      {
        nodeToId.Add(abstractNode, nodeToId.Count);
        dictionaryList.Add(this.NodeAsJsonInfo(abstractNode));
      }
      List<List<int>> list = Enumerable.Range(0, nodeToId.Count).Select<int, List<int>>((Func<int, List<int>>) (i => (List<int>) null)).ToList<List<int>>();
      foreach (KeyValuePair<AbstractNode, HashSet<AbstractNode>> allRelationship in this.AllRelationships)
      {
        int index = nodeToId[allRelationship.Key];
        list[index] = allRelationship.Value.Select<AbstractNode, int>((Func<AbstractNode, int>) (n =>
        {
          try
          {
            return nodeToId[n];
          }
          catch (KeyNotFoundException ex)
          {
            return -1;
          }
        })).ToList<int>();
      }
      using (FileStream fileStream = File.Create(filename))
      {
        using (StreamWriter streamWriter = new StreamWriter((Stream) fileStream, Encoding.UTF8))
          new JsonSerializer()
          {
            NullValueHandling = NullValueHandling.Ignore
          }.Serialize((TextWriter) streamWriter, (object) new Dictionary<string, object>()
          {
            {
              "nodes",
              (object) dictionaryList
            },
            {
              "relations",
              (object) list
            }
          });
      }
    }

    private Dictionary<string, string> NodeAsJsonInfo(AbstractNode node)
    {
      Dictionary<string, string> dictionary = new Dictionary<string, string>();
      dictionary["Location"] = !(node.Location != (Location) null) || !node.Location.IsInSource ? ((object) node.Location ?? (object) "implicit").ToString() : node.Location.SourceTree.FilePath + " : " + (object) node.Location.GetLineSpan().StartLinePosition.Line;
      switch (node)
      {
        case LiteralSymbol _:
          dictionary["value"] = node.ToString();
          dictionary["kind"] = "const";
          break;
        case MethodReturnSymbol _:
          dictionary["name"] = node.Name;
          dictionary["kind"] = "methodReturn";
          dictionary["type"] = node.Type;
          dictionary["symbolKind"] = !((MethodReturnSymbol) node).IsConstructor ? "method" : "constructor";
          break;
        default:
          dictionary["name"] = node.Name;
          VariableSymbol variableSymbol = node as VariableSymbol;
          dictionary["kind"] = "variable";
          dictionary["type"] = variableSymbol.Type;
          dictionary["symbolKind"] = variableSymbol.Symbol.Kind.ToString();
          break;
      }
      return dictionary;
    }

    private static string DotEscape(string input)
    {
      return input.Replace("\"", "''").Replace('\r', ' ').Replace('\n', ' ').Replace("\\", "\\\\").Replace('^', ' ');
    }
  }
}
