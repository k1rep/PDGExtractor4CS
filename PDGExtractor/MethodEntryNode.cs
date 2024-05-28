using Microsoft.CodeAnalysis;

#nullable disable
namespace PDGExtractor
{
    public class MethodEntryNode
    {
        public ISymbol MethodSymbol { get; }

        public MethodEntryNode(ISymbol symbol) => this.MethodSymbol = symbol;

        public override int GetHashCode() => this.MethodSymbol.GetHashCode();

        public override bool Equals(object obj)
        {
            return obj is MethodEntryNode methodEntryNode && methodEntryNode.MethodSymbol.Equals(this.MethodSymbol);
        }

        public override string ToString() => "Entry";

        public string ToSpan()
        {
            FileLinePositionSpan lineSpan = this.MethodSymbol.Locations[0].GetLineSpan();
            return string.Format("{0}-{1}", (object) lineSpan.StartLinePosition.Line, (object) lineSpan.EndLinePosition.Line);
        }
    }
}