using Coinwarden.API.Models.Entities;
using Coinwarden.API.Models.QueryParameters;

namespace Coinwarden.API.Data.Repositories;

/// <summary>
/// Repository interface for note data access.
/// </summary>
public interface INoteRepository : IRepository<Note, NoteQueryParameters>
{
}
