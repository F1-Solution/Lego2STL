namespace Lego2STL.Tests;

/// <summary>
/// A fact that needs the reference instruction PDF, and reports itself as skipped with a
/// reason when the document is not available.
/// </summary>
/// <remarks>
/// The document is a third party's copyrighted building instructions and 10.4 MB, so it is
/// deliberately not committed. xUnit v2 has no runtime skip, so the skip reason is decided
/// at discovery time by setting <see cref="FactAttribute.Skip"/>. That way an absent
/// document shows up as "skipped, and here is why" rather than as a pass.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DocumentFactAttribute : FactAttribute
{
    public DocumentFactAttribute()
    {
        if (ReferenceDocument.TryFind() is null)
        {
            Skip = $"{ReferenceDocument.FileName} is not present next to the repository.";
        }
    }
}

/// <summary><see cref="DocumentFactAttribute"/> for data-driven tests.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DocumentTheoryAttribute : TheoryAttribute
{
    public DocumentTheoryAttribute()
    {
        if (ReferenceDocument.TryFind() is null)
        {
            Skip = $"{ReferenceDocument.FileName} is not present next to the repository.";
        }
    }
}
