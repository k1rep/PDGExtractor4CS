using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

#nullable disable
namespace PDGExtractor
{
  internal class VariableSymbol : AbstractNode
  {
    public VariableSymbol(ISymbol symbol) => this.Symbol = symbol.OriginalDefinition;

    public ISymbol Symbol { get; }

    public override bool IsSymbol => true;

    public override bool IsLiteral => false;

    public override string Name => this.Symbol.Name;

    public override Location Location
    {
      get => this.Symbol.IsImplicitlyDeclared ? (Location) null : this.Symbol.Locations[0];
    }

    public override string Type
    {
      get
      {
        if (this.Symbol is ILocalSymbol)
          return ((ILocalSymbol) this.Symbol).Type.ToDisplayString();
        if (this.Symbol is IPropertySymbol)
          return ((IPropertySymbol) this.Symbol).Type.ToDisplayString();
        if (this.Symbol is IParameterSymbol)
          return ((IParameterSymbol) this.Symbol).Type.ToDisplayString();
        return this.Symbol is IFieldSymbol ? ((IFieldSymbol) this.Symbol).Type.ToDisplayString() : "Unknown";
      }
    }

    public override int GetHashCode() => this.Symbol.Name.GetHashCode();

    public override bool Equals(object obj)
    {
      if (!(obj is VariableSymbol variableSymbol))
        return false;
      int num;
      if (this.Location.SourceTree.FilePath == variableSymbol.Location.SourceTree.FilePath)
      {
        LinePosition startLinePosition = this.Location.GetLineSpan().StartLinePosition;
        int line1 = startLinePosition.Line;
        startLinePosition = variableSymbol.Location.GetLineSpan().StartLinePosition;
        int line2 = startLinePosition.Line;
        if (line1 == line2)
        {
          num = this.Symbol.Name == variableSymbol.Name ? 1 : 0;
          goto label_6;
        }
      }
      num = 0;
label_6:
      return num != 0;
    }

    public override string ToString() => this.Name + " : " + this.Type;

    public override string ToDotString() => this.Name + "\n<B>" + this.Type + "</B>";
  }
}
