namespace Dapper.PartialUpdate;

public struct Partial<T>
{
    private T _value;

    public Partial(T value)
    {
        _value = value;
        IsSet = true;
    }

    public bool IsSet { get; private set; }

    public T Value
    {
        readonly get => _value;
        set
        {
            _value = value;
            IsSet = true;
        }
    }

    public void Unset()
    {
        _value = default!;
        IsSet = false;
    }

    public static implicit operator Partial<T>(T value) => new(value);

    public override readonly string ToString() => IsSet ? $"{_value}" : "<unset>";
}
