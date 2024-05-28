using Microsoft.CodeAnalysis;

#nullable disable
namespace PDGExtractor
{
    public abstract class AbstractNode
    {
        public abstract bool IsSymbol { get; }

        public abstract bool IsLiteral { get; }

        public abstract string Name { get; }

        public abstract Location Location { get; }

        public abstract string Type { get; }

        public abstract string ToDotString();
    }
}