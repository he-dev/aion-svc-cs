namespace Aion.Data.Templating;

public readonly record struct TemplateString(string Value)
{
    public static TemplateString From(string value) => new(value);

    public static implicit operator TemplateString(string value) => new(value);
}