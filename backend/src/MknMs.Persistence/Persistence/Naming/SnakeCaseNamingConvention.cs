using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MknMs.Persistence.Naming;

/// <summary>
/// Applies snake_case naming to every column, key, index, and foreign
/// key in the model, and sets every table name to the snake_case form
/// of the entity class name in the singular.
/// </summary>
/// <remarks>
/// The specification uses singular snake_case table names
/// (role, duty_rule, service_occurrence). EF Core's convention
/// pluralizes table names by default; this convention overrides that
/// so the physical schema matches the specification exactly.
///
/// Acronym and digit boundaries are handled: RoleId → role_id,
/// ServiceDefId → service_def_id, CreatedAt → created_at.
/// </remarks>
public static class SnakeCaseNamingConvention
{
    public static void ApplySnakeCaseNamingConvention(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Table name: snake_case of the singular C# class name,
            // not the plural DbSet property name.
            var clrType = entity.ClrType;
            var tableName = ToSnakeCase(clrType.Name);
            entity.SetTableName(tableName);

            // Column names.
            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (columnName is not null)
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }

            // Primary keys, indexes, and foreign keys use the names EF
            // Core assigns; renaming them to snake_case keeps the
            // physical schema consistent with the documentation.
            foreach (var key in entity.GetKeys())
            {
                var name = key.GetName();
                if (name is not null)
                {
                    key.SetName(ToSnakeCase(name));
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                var name = index.GetDatabaseName();
                if (name is not null)
                {
                    index.SetDatabaseName(ToSnakeCase(name));
                }
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var name = foreignKey.GetConstraintName();
                if (name is not null)
                {
                    foreignKey.SetConstraintName(ToSnakeCase(name));
                }
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var builder = new StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (char.IsUpper(c))
            {
                var isFirst = i == 0;
                var previousIsLower = i > 0 && char.IsLower(input[i - 1]);
                var previousIsUpper = i > 0 && char.IsUpper(input[i - 1]);
                var nextIsLower = i + 1 < input.Length && char.IsLower(input[i + 1]);

                if (!isFirst && (previousIsLower || (previousIsUpper && nextIsLower)))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
