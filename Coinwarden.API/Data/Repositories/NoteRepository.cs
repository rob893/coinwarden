using System.Linq;
using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.QueryParameters;
using Microsoft.EntityFrameworkCore;

namespace Coinwarden.API.Data.Repositories;

/// <summary>
/// Repository for note data access.
/// </summary>
public sealed class NoteRepository : Repository<Note, NoteQueryParameters>, INoteRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoteRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public NoteRepository(DataContext context) : base(context)
    {
    }

    /// <inheritdoc />
    protected override IQueryable<Note> AddWhereClauses(IQueryable<Note> query, NoteQueryParameters searchParams)
    {
        if (!string.IsNullOrWhiteSpace(searchParams.Title))
        {
            query = query.Where(n => EF.Functions.ILike(n.Title, $"%{searchParams.Title}%"));
        }

        return query;
    }
}
