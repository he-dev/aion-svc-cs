using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Aion.Util;

public class SelectProperty<TSubject>
{
    public string Path<TProperty>(Expression<Func<TSubject, TProperty>> selector)
    {
        var visitor = new PropertyPathVisitor();
        visitor.Visit(selector);
        return visitor.ToString();
    }
}

internal sealed class PropertyPathVisitor : ExpressionVisitor
{
    private readonly List<object> _parts = [];
    private Type _subjectType = null!;

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            throw new ArgumentNullException("selector", "Provide a selector with an instance property or indexer path.");
        }

        // note: Does not merge note-types to improve readability.
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return node.NodeType switch
        {
            // core: Handles x => x.Cron.
            ExpressionType.Lambda => base.Visit(node),

            // core: Handles x at the root of the path.
            ExpressionType.Parameter => base.Visit(node),

            // core: Handles x.Cron.
            ExpressionType.MemberAccess => base.Visit(node),

            // core: Handles x.Steps[0].
            ExpressionType.ArrayIndex => base.Visit(node),

            // core: Handles x.Values[key] represented as an IndexExpression.
            ExpressionType.Index => base.Visit(node),

            // core: Handles x.Variables[key], x.Matrix[row, column].
            ExpressionType.Call => base.Visit(node),

            // core: Catches: unsupported expressions like 42 or x.Count + 1.
            _ => throw new ArgumentException($"Expression '{node}' has unsupported node type '{node.NodeType}'; select a property or indexer path.", "selector")
        };
    }

    // core: Handles x => x.Cron, x => x[key].
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var subject = node.Parameters[0];

        // core: Catches: x => x.
        if (node.Body == subject)
        {
            throw new ArgumentException("Select at least one property or indexer; selecting the subject itself makes no sense.", "selector");
        }

        _subjectType = subject.Type;
        Visit(node.Body);
        return node;
    }

    // core: Handles x.Cron, x.Steps[0].Name.
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node is not { Member: PropertyInfo, Expression: not null })
        {
            throw new ArgumentException($"Member '{node.Member.Name}' is not an instance property.", "selector");
        }

        Visit(node.Expression);
        _parts.Add(node.Member.Name); // = (Cron)
        return node;
    }

    // core: Handles x.Steps[0].
    protected override Expression VisitBinary(BinaryExpression node)
    {
        Visit(node.Left);
        _parts.Add(new[] { node.Right }); // = ([0])
        return node;
    }

    // core: Handles x.Values[key] represented as an IndexExpression.
    protected override Expression VisitIndex(IndexExpression node)
    {
        Visit(node.Object);
        _parts.Add(node.Arguments); // = ([key])
        return node;
    }

    // core: Handles x.Variables[key], x.Matrix[row, column].
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // core: Handles x.Variables[key].
        var isIndexer = node.Object is not null && node.Method is { IsSpecialName: true, Name: "get_Item" };

        // core: Handles x.Matrix[row, column].
        var isArrayAccess = node.Object?.Type.IsArray == true && node.Method.Name == "Get";

        // core: Catches unsupported calls like x.ToString() or Math.Abs(x.Count).
        if (!isIndexer && !isArrayAccess)
        {
            throw new ArgumentException($"Method '{node.Method.Name}' is not an indexer getter or array access.", "selector");
        }

        Visit(node.Object);
        _parts.Add(node.Arguments); // = ([key]) or ([row, column])
        return node;
    }

    public override string ToString()
    {
        var path = new StringBuilder(_subjectType.Name);
        foreach (var part in _parts)
        {
            switch (part)
            {
                // core: Handles property names like Cron or Name.
                case string property:
                    path.Append('.').Append(property);
                    break;
                // core: Handles index lists like [key] or [row, column].
                case IReadOnlyList<Expression> indexes:
                    path.Append('[').Append(string.Join(", ", indexes.Select(GetIndexValue))).Append(']');
                    break;
            }
        }

        return path.ToString();
    }

    private static object? GetIndexValue(Expression expression) => expression switch
    {
        // note: Handles: [0], ["key"], or a const variable.
        ConstantExpression constant => constant.Value,

        // note: Handles: [key] where key is a captured local variable.
        MemberExpression { Member: FieldInfo field, Expression: ConstantExpression closure } => field.GetValue(closure.Value),

        // note: Catches: [x.Logger], [GetKey()], or [index + 1].
        _ => throw new ArgumentException($"Index expression '{expression}' must be a constant or captured field.", "selector")
    };
}
