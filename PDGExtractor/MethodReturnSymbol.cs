using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

#nullable disable
namespace PDGExtractor
{
  internal class MethodReturnSymbol : AbstractNode
  {
    public MethodReturnSymbol(IMethodSymbol symbol) => this.Symbol = symbol.OriginalDefinition;

    public IMethodSymbol Symbol { get; }

    public override bool IsSymbol => true;

    public override bool IsLiteral => false;

    public bool IsConstructor => this.Symbol.Name == ".ctor";

    public override string Name
    {
      get => this.Symbol.Name == ".ctor" ? this.Symbol.ContainingType.Name : this.Symbol.Name;
    }

    public override Location Location
    {
      get => this.Symbol.IsImplicitlyDeclared ? (Location) null : this.Symbol.Locations[0];
    }

    public override string Type => this.Symbol.ReturnType?.ToDisplayString();

    public override int GetHashCode() => this.Symbol.Name.GetHashCode();

    public override bool Equals(object obj)
    {
      if (!(obj is MethodReturnSymbol methodReturnSymbol))
        return false;
      int num;
      if (this.Location.SourceTree.FilePath == methodReturnSymbol.Location.SourceTree.FilePath)
      {
        LinePosition startLinePosition = this.Location.GetLineSpan().StartLinePosition;
        int line1 = startLinePosition.Line;
        startLinePosition = methodReturnSymbol.Location.GetLineSpan().StartLinePosition;
        int line2 = startLinePosition.Line;
        if (line1 == line2)
        {
          num = this.Symbol.Name == methodReturnSymbol.Name ? 1 : 0;
          goto label_6;
        }
      }
      num = 0;
label_6:
      return num != 0;
    }

    public override string ToString() => this.Symbol.ToDisplayString() + " : " + this.Type;

    public override string ToDotString() => "*" + this.Name + "\n<B>" + this.Type + "</B>";
  }
}
