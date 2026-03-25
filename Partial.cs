namespace Dapper.PartialUpdate;

/// <summary>
/// A wrapper struct that tracks whether a value has been explicitly set.
/// Used for partial update operations where only modified fields should be persisted.
/// </summary>
/// <typeparam name="T">The type of the wrapped value.</typeparam>
public struct Partial<T>
{
    private T _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Partial{T}"/> struct with a value.
    /// Sets <see cref="IsSet"/> to <c>true</c>.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public Partial(T value)
    {
        _value = value;
        IsSet = true;
    }

    /// <summary>
    /// Gets a value indicating whether this partial has been explicitly set.
    /// </summary>
    /// <value><c>true</c> if the value has been set; otherwise, <c>false</c>.</value>
    public bool IsSet { get; private set; }

    /// <summary>
    /// Gets or sets the wrapped value. Setting the value automatically marks it as set.
    /// </summary>
    /// <value>The wrapped value.</value>
    public T Value
    {
        readonly get => _value;
        set
        {
            _value = value;
            IsSet = true;
        }
    }

    /// <summary>
    /// Unsets the value, marking it as not set. This causes the field to be omitted
    /// from partial update operations.
    /// </summary>
    public void Unset()
    {
        _value = default!;
        IsSet = false;
    }

    /// <summary>
    /// Implicitly converts a value to a <see cref="Partial{T}"/>.
    /// This allows direct assignment like <c>entity.Name = "Alice"</c>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A new <see cref="Partial{T}"/> with the value and <see cref="IsSet"/> set to <c>true</c>.</returns>
    public static implicit operator Partial<T>(T value) => new(value);

    /// <summary>
    /// Returns a string representation of this partial.
    /// </summary>
    /// <returns>The string representation of the value if set, otherwise "&lt;unset&gt;".</returns>
    public override readonly string ToString() => IsSet ? $"{_value}" : "<unset>";
}
