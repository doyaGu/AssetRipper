using System.Collections.Concurrent;

namespace AssetRipper.Tools.AssetDumper.Utils;

/// <summary>
/// Generic object pool for reusing objects to reduce GC pressure.
/// Thread-safe implementation using ConcurrentBag.
/// </summary>
/// <typeparam name="T">Type of objects to pool. Must be a reference type.</typeparam>
public sealed class ObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _objectFactory;
    private readonly Action<T>? _resetAction;
    private readonly int _maxPoolSize;
    private int _currentSize;

    /// <summary>
    /// Creates a new object pool.
    /// </summary>
    /// <param name="objectFactory">Factory function to create new objects</param>
    /// <param name="resetAction">Optional action to reset objects before returning to pool</param>
    /// <param name="maxPoolSize">Maximum number of objects to keep in pool (default: 1000)</param>
    public ObjectPool(Func<T> objectFactory, Action<T>? resetAction = null, int maxPoolSize = 1000)
    {
        _objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        _resetAction = resetAction;
        _maxPoolSize = maxPoolSize;
        _objects = new ConcurrentBag<T>();
        _currentSize = 0;
    }

    /// <summary>
    /// Gets an object from the pool or creates a new one if pool is empty.
    /// </summary>
    public T Rent()
    {
        if (_objects.TryTake(out T? obj))
        {
            Interlocked.Decrement(ref _currentSize);
            return obj;
        }

        return _objectFactory();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// If pool is at capacity, the object will be discarded and collected by GC.
    /// </summary>
    /// <param name="obj">Object to return to pool</param>
    public void Return(T obj)
    {
        if (obj == null)
        {
            return;
        }

        // Reset object state if reset action is provided
        _resetAction?.Invoke(obj);

        // Only add to pool if under capacity
        if (_currentSize < _maxPoolSize)
        {
            _objects.Add(obj);
            Interlocked.Increment(ref _currentSize);
        }
        // Otherwise let GC collect it
    }

    /// <summary>
    /// Gets the current number of objects in the pool.
    /// This is an approximate count and may not be exact due to concurrent access.
    /// </summary>
    public int CurrentSize => _currentSize;

    /// <summary>
    /// Gets the maximum pool size.
    /// </summary>
    public int MaxPoolSize => _maxPoolSize;

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    public void Clear()
    {
        while (_objects.TryTake(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }
}

/// <summary>
/// Pooled object wrapper that automatically returns object to pool when disposed.
/// Use with 'using' statement for automatic cleanup.
/// </summary>
/// <typeparam name="T">Type of pooled object</typeparam>
public readonly struct PooledObject<T> : IDisposable where T : class
{
    private readonly ObjectPool<T> _pool;
    private readonly T _obj;

    internal PooledObject(ObjectPool<T> pool, T obj)
    {
        _pool = pool;
        _obj = obj;
    }

    /// <summary>
    /// Gets the pooled object.
    /// </summary>
    public T Object => _obj;

    /// <summary>
    /// Returns the object to the pool.
    /// </summary>
    public void Dispose()
    {
        _pool.Return(_obj);
    }
}

/// <summary>
/// Extension methods for ObjectPool.
/// </summary>
public static class ObjectPoolExtensions
{
    /// <summary>
    /// Rents an object from the pool and wraps it in a PooledObject for automatic return.
    /// </summary>
    public static PooledObject<T> RentScoped<T>(this ObjectPool<T> pool) where T : class
    {
        T obj = pool.Rent();
        return new PooledObject<T>(pool, obj);
    }
}
