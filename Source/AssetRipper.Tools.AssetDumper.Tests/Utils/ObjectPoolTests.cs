using AssetRipper.Tools.AssetDumper.Utils;
using FluentAssertions;
using Xunit;

namespace AssetRipper.Tools.AssetDumper.Tests.Utils;

/// <summary>
/// Tests for the ObjectPool generic class.
/// </summary>
public class ObjectPoolTests
{
    #region Basic Functionality Tests

    [Fact]
    public void Rent_ShouldCreateNewObject_WhenPoolIsEmpty()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject { Id = 1 });

        // Act
        var obj = pool.Rent();

        // Assert
        obj.Should().NotBeNull();
        obj.Id.Should().Be(1);
    }

    [Fact]
    public void Return_ShouldAddObjectToPool()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        var obj = pool.Rent();

        // Act
        pool.Return(obj);

        // Assert
        pool.CurrentSize.Should().Be(1);
    }

    [Fact]
    public void Rent_ShouldReuseReturnedObject()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        var obj1 = pool.Rent();
        obj1.Id = 42;
        pool.Return(obj1);

        // Act
        var obj2 = pool.Rent();

        // Assert
        obj2.Should().BeSameAs(obj1);
        obj2.Id.Should().Be(42);
    }

    [Fact]
    public void ResetAction_ShouldBeCalledOnReturn()
    {
        // Arrange
        bool resetCalled = false;
        var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            obj => { resetCalled = true; obj.Id = 0; });
        
        var obj = pool.Rent();
        obj.Id = 99;

        // Act
        pool.Return(obj);

        // Assert
        resetCalled.Should().BeTrue();
        obj.Id.Should().Be(0);
    }

    #endregion

    #region Pool Limits Tests

    [Fact]
    public void Return_ShouldNotExceedMaxPoolSize()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject(), maxPoolSize: 2);
        var obj1 = pool.Rent();
        var obj2 = pool.Rent();
        var obj3 = pool.Rent();

        // Act - Return 3 objects to pool with max size of 2
        pool.Return(obj1);
        pool.Return(obj2);
        pool.Return(obj3);

        // Assert
        pool.CurrentSize.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public void MaxPoolSize_ShouldReturnConfiguredValue()
    {
        // Arrange & Act
        var pool = new ObjectPool<TestObject>(() => new TestObject(), maxPoolSize: 50);

        // Assert
        pool.MaxPoolSize.Should().Be(50);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_ShouldRemoveAllObjectsFromPool()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        pool.Return(pool.Rent());
        pool.Return(pool.Rent());
        pool.Return(pool.Rent());
        int sizeBefore = pool.CurrentSize;

        // Act
        pool.Clear();

        // Assert - ConcurrentBag doesn't guarantee all items are immediately visible
        sizeBefore.Should().BeGreaterThan(0);
        pool.CurrentSize.Should().Be(0);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ConcurrentRentAndReturn_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject(), maxPoolSize: 100);
        const int threadCount = 10;
        const int operationsPerThread = 100;

        // Act
        var tasks = new Task[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    var obj = pool.Rent();
                    obj.Id = j;
                    pool.Return(obj);
                }
            });
        }

        Task.WaitAll(tasks);

        // Assert - No exception should be thrown and pool should be within size limits
        pool.CurrentSize.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public void ConcurrentRent_ShouldReturnUniqueOrPooledObjects()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        const int threadCount = 5;
        var rentedObjects = new System.Collections.Concurrent.ConcurrentBag<TestObject>();

        // Act
        var tasks = new Task[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var obj = pool.Rent();
                rentedObjects.Add(obj);
            });
        }

        Task.WaitAll(tasks);

        // Assert
        rentedObjects.Should().HaveCount(threadCount);
        rentedObjects.Should().AllSatisfy(obj => obj.Should().NotBeNull());
    }

    #endregion

    #region Scoped Object Tests

    [Fact]
    public void RentScoped_ShouldReturnPooledObjectWrapper()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject { Id = 1 });

        // Act
        using var scoped = pool.RentScoped();

        // Assert
        scoped.Object.Should().NotBeNull();
        scoped.Object.Id.Should().Be(1);
    }

    [Fact]
    public void RentScoped_ShouldReturnObjectToPoolOnDispose()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        TestObject? rentedObj = null;

        // Act
        using (var scoped = pool.RentScoped())
        {
            rentedObj = scoped.Object;
            rentedObj.Id = 99;
        }

        // Assert - Object should be back in pool
        pool.CurrentSize.Should().Be(1);
        var obj = pool.Rent();
        obj.Should().BeSameAs(rentedObj);
    }

    [Fact]
    public void RentScoped_WithResetAction_ShouldResetOnDispose()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(
            () => new TestObject(),
            obj => obj.Id = 0);

        // Act
        TestObject? rentedObj;
        using (var scoped = pool.RentScoped())
        {
            rentedObj = scoped.Object;
            rentedObj.Id = 123;
        }

        // Assert
        rentedObj!.Id.Should().Be(0);
    }

    #endregion

    #region Null Handling Tests

    [Fact]
    public void Return_ShouldIgnoreNullObject()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());

        // Act
        pool.Return(null!);

        // Assert
        pool.CurrentSize.Should().Be(0);
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullFactory()
    {
        // Act
        Action act = () => new ObjectPool<TestObject>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void PoolReuse_ShouldReduceAllocations()
    {
        // Arrange
        var pool = new ObjectPool<TestObject>(() => new TestObject());
        
        // Act - Prime the pool
        var obj1 = pool.Rent();
        pool.Return(obj1);
        
        // Rent again
        var obj2 = pool.Rent();

        // Assert - Should be same instance (reused)
        obj2.Should().BeSameAs(obj1);
    }

    #endregion

    #region Helper Classes

    private class TestObject
    {
        public int Id { get; set; }
    }

    #endregion
}
