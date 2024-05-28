using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

#nullable disable
namespace PDGExtractor
{
  internal class FileTypeRelationCollector : CSharpSyntaxWalker
  {
    private readonly Dictionary<AbstractNode, HashSet<AbstractNode>> SubtypingRelationships;
    private readonly SemanticModel _semanticModel;
    private readonly bool _includeExternalSymbols;

    public FileTypeRelationCollector(
      SemanticModel model,
      Dictionary<AbstractNode, HashSet<AbstractNode>> relationships,
      bool includeExternalSymbols = false)
      : base()
    {
      this._semanticModel = model;
      this.SubtypingRelationships = relationships;
      this._includeExternalSymbols = includeExternalSymbols;
    }

    private void AddSubtypingRelation(AbstractNode moreGeneralType, AbstractNode moreSpecificType)
    {
      HashSet<AbstractNode> abstractNodeSet;
      if (!this.SubtypingRelationships.TryGetValue(moreGeneralType, out abstractNodeSet))
      {
        abstractNodeSet = new HashSet<AbstractNode>();
        this.SubtypingRelationships.Add(moreGeneralType, abstractNodeSet);
      }
      abstractNodeSet.Add(moreSpecificType);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
      base.VisitInvocationExpression(node);
      IMethodSymbol symbol = (IMethodSymbol) Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, (ExpressionSyntax) node).Symbol;
      if (symbol == null || !this.IsUsedSymbol((ISymbol) symbol))
        return;
      IMethodSymbol originalDefinition = symbol.OriginalDefinition;
      this.AddAllMethods(originalDefinition);
      foreach (ArgumentSyntax argumentSyntax in node.ArgumentList.Arguments)
      {
        IParameterSymbol parameter = this.DetermineParameter((BaseArgumentListSyntax) node.ArgumentList, argumentSyntax, originalDefinition);
        AbstractNode nodeSymbol = this.GetNodeSymbol((SyntaxNode) argumentSyntax.Expression);
        if (nodeSymbol != null)
          this.AddSubtypingRelation((AbstractNode) new VariableSymbol((ISymbol) parameter), nodeSymbol);
      }
    }

    private void AddAllMethods(IMethodSymbol methodSymbol)
    {
      if (!this.IsUsedSymbol((ISymbol) methodSymbol))
        return;
      ImmutableArray<IParameterSymbol> parameters;
      foreach (IMethodSymbol methodSymbol1 in methodSymbol.ContainingType.AllInterfaces.SelectMany<INamedTypeSymbol, IMethodSymbol>((Func<INamedTypeSymbol, IEnumerable<IMethodSymbol>>) (iface => iface.GetMembers().OfType<IMethodSymbol>())).Where<IMethodSymbol>((Func<IMethodSymbol, bool>) (method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember((ISymbol) method)))).ToArray<IMethodSymbol>())
      {
        if (this.IsUsedSymbol((ISymbol) methodSymbol1))
        {
          int index = 0;
          while (true)
          {
            int num = index;
            parameters = methodSymbol1.Parameters;
            int length = parameters.Length;
            if (num < length)
            {
              parameters = methodSymbol.Parameters;
              VariableSymbol moreGeneralType = new VariableSymbol((ISymbol) parameters[index]);
              parameters = methodSymbol1.Parameters;
              VariableSymbol moreSpecificType = new VariableSymbol((ISymbol) parameters[index]);
              this.AddSubtypingRelation((AbstractNode) moreGeneralType, (AbstractNode) moreSpecificType);
              ++index;
            }
            else
              break;
          }
          this.AddSubtypingRelation((AbstractNode) new MethodReturnSymbol(methodSymbol1), (AbstractNode) new MethodReturnSymbol(methodSymbol));
          this.AddAllMethods(methodSymbol1);
        }
      }
      IMethodSymbol overriddenMethod = methodSymbol.OverriddenMethod;
      if (overriddenMethod == null || !this.IsUsedSymbol((ISymbol) overriddenMethod))
        return;
      int index1 = 0;
      while (true)
      {
        int num = index1;
        parameters = overriddenMethod.Parameters;
        int length = parameters.Length;
        if (num < length)
        {
          parameters = methodSymbol.Parameters;
          VariableSymbol moreGeneralType = new VariableSymbol((ISymbol) parameters[index1]);
          parameters = overriddenMethod.Parameters;
          VariableSymbol moreSpecificType = new VariableSymbol((ISymbol) parameters[index1]);
          this.AddSubtypingRelation((AbstractNode) moreGeneralType, (AbstractNode) moreSpecificType);
          ++index1;
        }
        else
          break;
      }
      this.AddSubtypingRelation((AbstractNode) new MethodReturnSymbol(overriddenMethod), (AbstractNode) new MethodReturnSymbol(methodSymbol));
      this.AddAllMethods(overriddenMethod);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
      base.VisitAssignmentExpression(node);
      ISymbol symbol1 = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, node.Right).Symbol;
      if (node.Left is ElementAccessExpressionSyntax)
        return;
      ISymbol symbol2 = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, node.Left).Symbol;
      if (!this.IsUsedSymbol(symbol2) || !this.IsUsedSymbol(symbol1))
        return;
      Optional<object> constantValue = this._semanticModel.GetConstantValue((SyntaxNode) node.Right);
      if (symbol1 != null && symbol2 != null)
      {
        AbstractNode moreSpecificType = !(symbol1 is IMethodSymbol) ? (AbstractNode) new VariableSymbol(symbol1) : (AbstractNode) new MethodReturnSymbol(symbol1 as IMethodSymbol);
        this.AddSubtypingRelation((AbstractNode) new VariableSymbol(symbol2), moreSpecificType);
      }
      else if (constantValue.HasValue && symbol2 != null)
        this.AddSubtypingRelation((AbstractNode) new VariableSymbol(symbol2), (AbstractNode) new LiteralSymbol(constantValue.Value, node.GetLocation()));
    }

    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
      base.VisitVariableDeclarator(node);
      ISymbol declaredSymbol = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(this._semanticModel, node);
      if (node.Initializer == null)
        return;
      ISymbol symbol1 = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, node.Initializer.Value).Symbol;
      if (!this.IsUsedSymbol(declaredSymbol))
        return;
      if (this.IsUsedSymbol(symbol1))
      {
        if (symbol1 is IMethodSymbol symbol2)
          this.AddSubtypingRelation((AbstractNode) new VariableSymbol(declaredSymbol), (AbstractNode) new MethodReturnSymbol(symbol2));
        else
          this.AddSubtypingRelation((AbstractNode) new VariableSymbol(declaredSymbol), (AbstractNode) new VariableSymbol(symbol1));
      }
      else
      {
        if (!this._semanticModel.GetConstantValue((SyntaxNode) node.Initializer.Value).HasValue)
          return;
        this.AddSubtypingRelation((AbstractNode) new VariableSymbol(declaredSymbol), (AbstractNode) new LiteralSymbol(this._semanticModel.GetConstantValue((SyntaxNode) node.Initializer.Value).Value, node.Initializer.Value.GetLocation()));
      }
    }

    private AbstractNode GetNodeSymbol(SyntaxNode node)
    {
      if (node is CastExpressionSyntax)
        node = (SyntaxNode) (node as CastExpressionSyntax).Expression;
      Optional<object> constantValue = this._semanticModel.GetConstantValue(node);
      if (!(node is IdentifierNameSyntax) && constantValue.HasValue)
        return (AbstractNode) new LiteralSymbol(constantValue.Value, node.GetLocation());
      ISymbol symbol = this._semanticModel.GetSymbolInfo(node).Symbol ?? this._semanticModel.GetDeclaredSymbol(node);
      if (symbol == null || !this.IsUsedSymbol(symbol))
        return (AbstractNode) null;
      return symbol is IMethodSymbol ? (AbstractNode) new MethodReturnSymbol(symbol as IMethodSymbol) : (AbstractNode) new VariableSymbol(symbol);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
      this.AddAllMethods(Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeclaredSymbol(this._semanticModel, (BaseMethodDeclarationSyntax) node, new CancellationToken()));
      base.VisitMethodDeclaration(node);
    }

    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
      if (node.Expression == null)
        return;
      AbstractNode nodeSymbol = this.GetNodeSymbol((SyntaxNode) node.Expression);
      if (nodeSymbol != null)
      {
        SyntaxNode syntaxNode = (SyntaxNode) node;
        while (true)
        {
          int num;
          switch (syntaxNode)
          {
            case null:
              num = 0;
              break;
            case BaseMethodDeclarationSyntax _:
            case AccessorDeclarationSyntax _:
              num = 0;
              break;
            default:
              num = !(syntaxNode is AnonymousFunctionExpressionSyntax) ? 1 : 0;
              break;
          }
          if (num != 0)
            syntaxNode = syntaxNode.Parent;
          else
            break;
        }
        AbstractNode moreGeneralType;
        if (syntaxNode is BaseMethodDeclarationSyntax || syntaxNode is AnonymousFunctionExpressionSyntax)
        {
          moreGeneralType = (AbstractNode) new MethodReturnSymbol((this._semanticModel.GetDeclaredSymbol(syntaxNode) ?? this._semanticModel.GetSymbolInfo(syntaxNode).Symbol) as IMethodSymbol);
        }
        else
        {
          if (!(syntaxNode is AccessorDeclarationSyntax))
            throw new Exception("Never Happens");
          moreGeneralType = (AbstractNode) new VariableSymbol(this._semanticModel.GetDeclaredSymbol(syntaxNode.Parent.Parent));
        }
        this.AddSubtypingRelation(moreGeneralType, nodeSymbol);
      }
      base.VisitReturnStatement(node);
    }

    private bool IsUsedSymbol(ISymbol symbol)
    {
      if (symbol == null)
        return false;
      return this._includeExternalSymbols || !symbol.IsImplicitlyDeclared && !symbol.Locations[0].IsInMetadata;
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
      base.VisitBinaryExpression(node);
      IMethodSymbol symbol = (IMethodSymbol) Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetSymbolInfo(this._semanticModel, (ExpressionSyntax) node).Symbol;
      if (symbol == null || !this.IsUsedSymbol((ISymbol) symbol))
        return;
      ImmutableArray<IParameterSymbol> parameters = symbol.Parameters;
      if (parameters.Length > 1)
      {
        AbstractNode nodeSymbol = this.GetNodeSymbol((SyntaxNode) node.Right);
        if (nodeSymbol != null)
        {
          parameters = symbol.Parameters;
          this.AddSubtypingRelation((AbstractNode) new VariableSymbol((ISymbol) parameters[1]), nodeSymbol);
        }
      }
      AbstractNode nodeSymbol1 = this.GetNodeSymbol((SyntaxNode) node.Left);
      if (nodeSymbol1 != null)
      {
        parameters = symbol.Parameters;
        this.AddSubtypingRelation((AbstractNode) new VariableSymbol((ISymbol) parameters[0]), nodeSymbol1);
      }
    }

    public IParameterSymbol DetermineParameter(
      BaseArgumentListSyntax argumentList,
      ArgumentSyntax argument,
      IMethodSymbol symbol)
    {
      ImmutableArray<IParameterSymbol> parameters = symbol.Parameters;
      if (argument.NameColon != null && !argument.NameColon.IsMissing)
      {
        string name = argument.NameColon.Name.Identifier.ValueText;
        return parameters.FirstOrDefault<IParameterSymbol>((Func<IParameterSymbol, bool>) (p => p.Name == name));
      }
      int index = argumentList.Arguments.IndexOf(argument);
      if (index < 0)
        return (IParameterSymbol) null;
      if (index < parameters.Length)
        return parameters[index];
      IParameterSymbol parameterSymbol = parameters.LastOrDefault<IParameterSymbol>();
      return parameterSymbol == null || !parameterSymbol.IsParams ? (IParameterSymbol) null : parameterSymbol;
    }
  }
}
