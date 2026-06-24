using System;

namespace Coinwarden.API.Models;

public interface IOwnedByUser<TKey> where TKey : IEquatable<TKey>, IComparable<TKey>
{
    TKey UserId { get; }
}