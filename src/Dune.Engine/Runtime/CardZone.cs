using Dune.Engine.Content;

namespace Dune.Engine.Runtime;

public readonly record struct CardZoneId(string Value);
public enum ZoneVisibility { Public, OwnerOnly, Hidden }

/// <summary>Trusted runtime zone. Visibility is metadata; observations must project permitted cards explicitly.</summary>
/// <remarks>Index zero is the top of a deck. Transfers append to the destination's bottom.</remarks>
public sealed class CardZone
{
    private readonly List<CardInstance> _cards;

    internal CardZone(CardZoneId id, string name, ZoneVisibility visibility, int? ownerId = null,
        IEnumerable<CardInstance>? cards = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Enum.IsDefined(visibility))
        {
            throw new ArgumentOutOfRangeException(nameof(visibility));
        }
        if (ownerId is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerId));
        }
        if (visibility == ZoneVisibility.OwnerOnly && ownerId is null)
        {
            throw new ArgumentException("An owner-only zone requires an owner.", nameof(ownerId));
        }

        _cards = cards?.ToList() ?? [];
        var ids = new HashSet<CardInstanceId>();
        foreach (var card in _cards)
        {
            ArgumentNullException.ThrowIfNull(card);
            if (!ids.Add(card.Id))
            {
                throw new ArgumentException("A zone cannot contain duplicate physical card IDs.", nameof(cards));
            }
        }
        Id = id;
        Name = name;
        OwnerId = ownerId;
        Visibility = visibility;
        Cards = _cards.AsReadOnly();
    }

    public CardZoneId Id { get; }
    public string Name { get; }
    public int? OwnerId { get; }
    public ZoneVisibility Visibility { get; }
    public IReadOnlyList<CardInstance> Cards { get; }
    public int Count => _cards.Count;

    internal void MoveTo(CardInstanceId cardId, CardZone destination)
    {
        ValidateDestination(destination);
        var index = _cards.FindIndex(card => card.Id == cardId);
        if (index < 0)
        {
            throw new ArgumentException("The physical card is not in the source zone.", nameof(cardId));
        }
        EnsureAbsent(destination, cardId);
        destination._cards.Add(_cards[index]);
        _cards.RemoveAt(index);
    }

    internal CardInstance? TryDrawTo(CardZone destination)
    {
        ValidateDestination(destination);
        if (_cards.Count == 0)
        {
            return null;
        }
        var card = _cards[0];
        MoveTo(card.Id, destination);
        return card;
    }

    /// <summary>Recycles and shuffles the supplied discard only when this deck is empty.</summary>
    internal CardInstance? TryDrawTo(CardZone destination, CardZone discard, SeededRandom random)
    {
        ValidateDestination(destination);
        ValidateDestination(discard);
        discard.ValidateDestination(destination);
        ArgumentNullException.ThrowIfNull(random);
        if (OwnerId != discard.OwnerId)
        {
            throw new ArgumentException("A deck can only recycle a discard with the same owner.", nameof(discard));
        }
        if (_cards.Count != 0)
        {
            return TryDrawTo(destination);
        }

        // Validate the entire refill before changing zones or consuming randomness.
        foreach (var card in discard._cards)
        {
            EnsureAbsent(destination, card.Id);
        }
        var refill = discard._cards.ToList();
        random.Shuffle(refill);
        _cards.AddRange(refill);
        discard._cards.Clear();
        return TryDrawTo(destination);
    }

    internal void Shuffle(SeededRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        random.Shuffle(_cards);
    }

    private void ValidateDestination(CardZone destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (ReferenceEquals(this, destination) || Id == destination.Id)
        {
            throw new ArgumentException("Source and destination must be different zones.", nameof(destination));
        }
    }

    private static void EnsureAbsent(CardZone destination, CardInstanceId cardId)
    {
        if (destination._cards.Any(card => card.Id == cardId))
        {
            throw new ArgumentException("The destination already contains this physical card.", nameof(destination));
        }
    }
}
