#nullable disable
namespace PDGExtractor
{
  internal class DirectedGraph
  {
    private readonly Dictionary<int, object> _contexts = new Dictionary<int, object>();
    private readonly Dictionary<int, HashSet<int>> _edges = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, HashSet<int>> _edgesReverse = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, object> _idToObject = new Dictionary<int, object>();
    private readonly Dictionary<object, int> _objectToId = new Dictionary<object, int>();
    private int _nextId = 0;
    private readonly Dictionary<Tuple<int, int>, HashSet<Tuple<string, object>>> _edgeAnnotations = new Dictionary<Tuple<int, int>, HashSet<Tuple<string, object>>>();

    public void AddEdge(
      object fromNode,
      object toNode,
      Tuple<string, object> annotation,
      object fromNodeContext = null,
      object toNodeContext = null)
    {
      if (fromNode == null || toNode == null)
        return;
      int objectId1 = this.GetObjectId(fromNode);
      int objectId2 = this.GetObjectId(toNode);
      if (fromNodeContext != null)
        this._contexts[objectId1] = fromNodeContext;
      if (toNodeContext != null)
        this._contexts[objectId2] = toNodeContext;
      if (!this._edges.ContainsKey(objectId1))
        this._edges[objectId1] = new HashSet<int>();
      this._edges[objectId1].Add(objectId2);
      if (!this._edgesReverse.ContainsKey(objectId2))
        this._edgesReverse[objectId2] = new HashSet<int>();
      this._edgesReverse[objectId2].Add(objectId1);
      Tuple<int, int> key = Tuple.Create<int, int>(objectId1, objectId2);
      if (!this._edgeAnnotations.ContainsKey(key))
        this._edgeAnnotations[key] = new HashSet<Tuple<string, object>>();
      this._edgeAnnotations[key].Add(annotation);
    }

    public IEnumerable<object> GetOutEdgesFrom(object node)
    {
      int objectId = this.GetObjectId(node);
      try
      {
        return this._edges[objectId].Select<int, object>((Func<int, object>) (i => this._idToObject[i]));
      }
      catch (KeyNotFoundException ex)
      {
        return Enumerable.Empty<object>();
      }
    }

    public object getRootNode() => this._idToObject[0];

    public bool ContainsNode(object node) => node != null && this._objectToId.ContainsKey(node);

    private int GetObjectId(object obj)
    {
      int nextId;
      if (!this._objectToId.TryGetValue(obj, out nextId))
      {
        this._objectToId.Add(obj, this._nextId);
        this._idToObject.Add(this._nextId, obj);
        nextId = this._nextId;
        ++this._nextId;
      }
      return nextId;
    }

    private string DotEscape(string input)
    {
      return input.Replace("\"", "''").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    public void ToDot(
      string filepath,
      Func<object, string> nodeNames,
      Func<object, string> nodeSpans,
      Func<Tuple<string, object>, string> arrowStyle,
      Func<object, string> nodeColour = null)
    {
      Dictionary<object, HashSet<int>> dictionary = new Dictionary<object, HashSet<int>>();
      foreach (KeyValuePair<int, object> keyValuePair in this._idToObject)
      {
        object key;
        this._contexts.TryGetValue(keyValuePair.Key, out key);
        if (key == null)
          key = (object) "none";
        if (!dictionary.ContainsKey(key))
          dictionary[key] = new HashSet<int>();
        dictionary[key].Add(keyValuePair.Key);
      }
      using (StreamWriter streamWriter = new StreamWriter(filepath))
      {
        streamWriter.WriteLine("digraph \"extractedGraph\"{");
        int num1 = 0;
        foreach (KeyValuePair<object, HashSet<int>> keyValuePair in dictionary)
        {
          if (!keyValuePair.Key.Equals((object) "none"))
          {
            streamWriter.WriteLine("subgraph cluster_" + (object) num1 + " {");
            streamWriter.WriteLine("label = \"{0}\";", (object) this.DotEscape(nodeNames(keyValuePair.Key)));
            ++num1;
          }
          foreach (int key in keyValuePair.Value)
          {
            if (nodeColour == null)
              streamWriter.WriteLine("n{0} [label=\"{1}\", span=\"{2}\"];", (object) key, (object) this.DotEscape(nodeNames(this._idToObject[key])), (object) this.DotEscape(nodeSpans(this._idToObject[key])));
            else
              streamWriter.WriteLine("n{0} [label=\"{1}\", color=\"{2}\", span=\"{3}\"];", (object) key, (object) this.DotEscape(nodeNames(this._idToObject[key])), (object) nodeColour(this._idToObject[key]), (object) this.DotEscape(nodeSpans(this._idToObject[key])));
          }
          if (!keyValuePair.Key.Equals((object) "none"))
            streamWriter.WriteLine("}");
        }
        foreach (KeyValuePair<int, HashSet<int>> edge in this._edges)
        {
          foreach (int num2 in edge.Value)
          {
            foreach (Tuple<string, object> tuple in this._edgeAnnotations[Tuple.Create<int, int>(edge.Key, num2)])
              streamWriter.WriteLine("n{0}->n{1} [style={2}];", (object) edge.Key, (object) num2, (object) arrowStyle(tuple));
          }
        }
        streamWriter.WriteLine("}");
      }
    }

    public void ToJson(string filepath, Func<object, string> nodeNames)
    {
    }
  }
}
