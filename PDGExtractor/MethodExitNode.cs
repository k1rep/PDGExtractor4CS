using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

#nullable disable
namespace PDGExtractor
{
    internal class MethodExitNode
    {
        public ISymbol MethodSymbol { get; }

        public MethodExitNode(ISymbol symbol) => this.MethodSymbol = symbol;

        public override int GetHashCode() => this.MethodSymbol.GetHashCode();

        public override bool Equals(object obj)
        {
            return obj is MethodExitNode methodExitNode && methodExitNode.MethodSymbol.Equals(this.MethodSymbol);
        }

        public override string ToString() => "Exit";

        public string ToSpan()
        {
            ImmutableArray<Location> locations = this.MethodSymbol.Locations;
            FileLinePositionSpan lineSpan = locations[locations.Length - 1].GetLineSpan();
            return string.Format("{0}-{1}", (object) lineSpan.StartLinePosition.Line, (object) lineSpan.EndLinePosition.Line);
        }
    }
}