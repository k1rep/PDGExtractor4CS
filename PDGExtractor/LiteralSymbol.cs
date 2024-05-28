using Microsoft.CodeAnalysis;

#nullable disable
namespace PDGExtractor
{
    internal class LiteralSymbol : AbstractNode
    {
        public LiteralSymbol(object constant, Location location)
        {
            this.Constant = constant;
            this.Location = location;
        }

        public object Constant { get; }

        public override bool IsSymbol => false;

        public override bool IsLiteral => true;

        public override string Name => "";

        public override Location Location { get; }

        public override string Type
        {
            get
            {
                if (this.Constant == null)
                    return "null";
                switch (this.Constant)
                {
                    case string _:
                        return "string";
                    case int _:
                        return "int";
                    default:
                        return this.Constant.GetType().ToString();
                }
            }
        }

        public override int GetHashCode() => this.Constant == null ? 0 : this.Constant.GetHashCode();

        public override bool Equals(object obj)
        {
            return obj is LiteralSymbol literalSymbol && object.Equals(literalSymbol.Constant, this.Constant);
        }

        public override string ToString()
        {
            return this.Constant == null ? "const:null" : "const:" + this.Constant.ToString() + " : " + this.Type;
        }

        public override string ToDotString()
        {
            if (this.Constant == null)
                return "const\n<B>null</B>";
            return "const:" + this.Constant.ToString() + "\n<B>" + this.Type + "</B>";
        }
    }
}