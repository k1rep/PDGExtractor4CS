#nullable disable
namespace PDGExtractor
{
    public class UnkMethodEntryNode
    {
        public string MethodSymbol { get; }

        public UnkMethodEntryNode(string symbol) => this.MethodSymbol = symbol;

        public override int GetHashCode() => this.MethodSymbol.GetHashCode();

        public override bool Equals(object obj)
        {
            return obj is UnkMethodEntryNode unkMethodEntryNode && unkMethodEntryNode.MethodSymbol.Equals(this.MethodSymbol);
        }

        public override string ToString() => "Entry";

        public string ToSpan() => "";
    }
}