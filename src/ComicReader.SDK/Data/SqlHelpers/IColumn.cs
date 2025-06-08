// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.SqlHelpers;

public interface IColumn<T>
{
    string Name { get; }

    IReaderToken<T> PutQuery<U>(SelectCommand<U> command) where U : ITable;
}

public class Int32Column(string name) : IColumn<int>
{
    public string Name => name;

    public IReaderToken<int> PutQuery<U>(SelectCommand<U> command) where U : ITable
    {
        return command.PutQueryInt32(this);
    }
}

public class Int64Column(string name) : IColumn<long>
{
    public string Name => name;

    public IReaderToken<long> PutQuery<U>(SelectCommand<U> command) where U : ITable
    {
        return command.PutQueryInt64(this);
    }
}

public class StringColumn(string name) : IColumn<string>
{
    public string Name => name;

    public IReaderToken<string> PutQuery<U>(SelectCommand<U> command) where U : ITable
    {
        return command.PutQueryString(this);
    }
}

public class BooleanColumn(string name) : IColumn<bool>
{
    public string Name => name;

    public IReaderToken<bool> PutQuery<U>(SelectCommand<U> command) where U : ITable
    {
        return command.PutQueryBoolean(this);
    }
}

public class DoubleColumn(string name) : IColumn<double>
{
    public string Name => name;

    public IReaderToken<double> PutQuery<U>(SelectCommand<U> command) where U : ITable
    {
        return command.PutQueryDouble(this);
    }
}

public class DateTimeOffsetColumn(string name) : IColumn<DateTimeOffset>
{
    public string Name => name;

    public IReaderToken<DateTimeOffset> PutQuery<U>(SelectCommand<U> command) where U : ITable
    {
        return command.PutQueryDateTimeOffset(this);
    }
}
