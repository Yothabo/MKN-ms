namespace MknMs.Application.Common;

/// <summary>
/// Thrown when a service is asked to operate on an entity that does not
/// exist.
/// </summary>
/// <remarks>
/// This is an unexpected condition — the caller should have supplied a
/// valid identifier. It is a programming error or a race, not a
/// validation failure, and therefore throws rather than returning a
/// result.
/// </remarks>
public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName, int id)
        : base($"{entityName} with id {id} was not found.")
    {
        EntityName = entityName;
        Id = id;
    }

    public string EntityName { get; }

    public int Id { get; }
}
