// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.AutoProperty;
using ComicReader.SDK.Data.AutoProperty.Presets;

namespace ComicReader.SDK.Tests.AutoProperty;

[TestFixture]
public class AutoPropertyTest
{
    [Test]
    public async Task TestMemoryCacheProperty()
    {
        PropertyServer server = new("Test");

        TestProperty<int, int> sourceProperty = new()
        {
            Rearrange = true,
            ProcessOnServerThread = true
        };
        MemoryCacheProperty<int> cacheProperty = new(sourceProperty);

        Func<string, int> valueFunc = (key) =>
        {
            return -1;
        };

        {
            sourceProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Read)
                {
                    throw new InvalidOperationException();
                }
                return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc(request.Key));
            };
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value;
                }
                return -1;
            };
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = AppendSerialReadTest(batch, cacheProperty, 0, 100, valueFunc);
                {
                    ExternalBatchResponse response = await server.Request(batch);
                    // Single batch
                    foreach (IRequestTest test in tests)
                    {
                        test.AssertResult(response);
                    }
                }
                {
                    ExternalBatchResponse response = await server.Request(batch);
                    // Request reuse
                    foreach (IRequestTest test in tests)
                    {
                        test.AssertResult(response);
                    }
                }
            }
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = AppendSerialReadTest(batch, cacheProperty, 0, 100, valueFunc);
                valueFunc = (key) =>
                {
                    if (int.TryParse(key, out int value))
                    {
                        return value + 1;
                    }
                    return -1;
                };
                // Memory cache
                ExternalBatchResponse response = await server.Request(batch);
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }

        {
            Dictionary<string, int> writtenValues = [];
            sourceProperty.ServerFunc = (request) =>
            {
                switch (request.Type)
                {
                    case RequestType.Read:
                        return PropertyResponseContent<int>.NewSuccessfulResponse(writtenValues[request.Key]);
                    case RequestType.Modify:
                        writtenValues[request.Key] = request.Value;
                        return PropertyResponseContent<int>.NewSuccessfulResponse();
                    default:
                        throw new InvalidOperationException();
                }
            };
            List<List<IRequestTest>> tests = [];
            List<Task<ExternalBatchResponse>> tasks = [];
            int batchCount = 10;
            for (int i = 0; i < batchCount; i++)
            {
                valueFunc = (key) =>
                {
                    if (int.TryParse(key, out int value))
                    {
                        return value + 2 + i;
                    }
                    return -1;
                };
                ExternalBatchRequest batch = new();
                List<IRequestTest> subTests = [];
                subTests.AddRange(AppendSerialWriteTest(batch, sourceProperty, i * 50, i * 50 + 100, valueFunc));
                subTests.AddRange(AppendSerialReadTest(batch, cacheProperty, i * 50, i * 50 + 100, valueFunc));
                tests.Add(subTests);
                tasks.Add(server.Request(batch));
            }
            // Multiple batches
            // Dependency version
            for (int i = 0; i < batchCount; i++)
            {
                ExternalBatchResponse response = await tasks[i];
                List<IRequestTest> subTests = tests[i];
                foreach (IRequestTest test in subTests)
                {
                    test.AssertResult(response);
                }
            }
        }

        {
            Dictionary<string, int> writtenValues = [];
            sourceProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Modify)
                {
                    throw new InvalidOperationException();
                }
                writtenValues[request.Key] = request.Value;
                return PropertyResponseContent<int>.NewSuccessfulResponse();
            };
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 3;
                }
                return -1;
            };
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = AppendSerialWriteTest(batch, cacheProperty, 0, 100, valueFunc);
                ExternalBatchResponse response = await server.Request(batch);
                // Write
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
            sourceProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Read)
                {
                    throw new InvalidOperationException();
                }
                return PropertyResponseContent<int>.NewSuccessfulResponse(writtenValues[request.Key]);
            };
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = AppendSerialReadTest(batch, cacheProperty, 0, 100, valueFunc);
                ExternalBatchResponse response = await server.Request(batch);
                // Read after write
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }

        {
            Dictionary<string, int> writtenValues = [];
            sourceProperty.ServerFunc = (request) =>
            {
                switch (request.Type)
                {
                    case RequestType.Read:
                        return PropertyResponseContent<int>.NewSuccessfulResponse(writtenValues[request.Key]);
                    case RequestType.Modify:
                        writtenValues[request.Key] = request.Value;
                        return PropertyResponseContent<int>.NewSuccessfulResponse();
                    default:
                        throw new InvalidOperationException();
                }
            };
            List<IRequestTest> tests = [];
            ExternalBatchRequest batch = new();
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 4;
                }
                return -1;
            };
            tests.AddRange(AppendSerialWriteTest(batch, cacheProperty, 0, 10, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, 10, valueFunc));
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 5;
                }
                return -1;
            };
            tests.AddRange(AppendSerialWriteTest(batch, cacheProperty, 0, 10, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, 10, valueFunc));
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 6;
                }
                return -1;
            };
            tests.AddRange(AppendSerialWriteTest(batch, cacheProperty, 0, 10, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, 10, valueFunc));
            ExternalBatchResponse response = await server.Request(batch);
            // Read and write
            foreach (IRequestTest test in tests)
            {
                test.AssertResult(response);
            }
        }
    }

    private List<IRequestTest> AppendSerialReadTest<T>(ExternalBatchRequest batch, IQRProperty<T, T> property, int start, int count, Func<string, T> valueFunc)
    {
        List<IRequestTest> tests = [];
        for (int i = start; i < count; i++)
        {
            string key = i.ToString();
            ExternalRequest<T, T> request = new ExternalRequest<T, T>.Builder(property).SetRequestType(RequestType.Read).SetKey(key).Build();
            batch.Requests.Add(request);
            ReadRequestTest<T, T> test = new(request, valueFunc(key));
            tests.Add(test);
        }
        return tests;
    }

    private List<IRequestTest> AppendSerialWriteTest<T>(ExternalBatchRequest batch, IQRProperty<T, T> property, int start, int count, Func<string, T> valueFunc)
    {
        List<IRequestTest> tests = [];
        for (int i = start; i < count; i++)
        {
            string key = i.ToString();
            T value = valueFunc(key);
            ExternalRequest<T, T> request = new ExternalRequest<T, T>.Builder(property).SetRequestType(RequestType.Modify).SetKey(key).SetValue(value).Build();
            batch.Requests.Add(request);
            RequestResultTest<T, T> test = new(request, RequestResult.Successful);
            tests.Add(test);
        }
        return tests;
    }

    private interface IRequestTest
    {
        void AssertResult(ExternalBatchResponse batchResponse);
    }

    private class RequestResultTest<Q, R>(ExternalRequest<Q, R> request, RequestResult expectResult) : IRequestTest
    {
        public void AssertResult(ExternalBatchResponse batchResponse)
        {
            ExternalResponse<R>? response = batchResponse.GetResponse(request);
            if (response == null)
            {
                Assert.Fail();
                return;
            }
            Assert.Multiple(() =>
            {
                Assert.That(response.Result, Is.EqualTo(expectResult));
            });
        }
    }

    private class ReadRequestTest<Q, R>(ExternalRequest<Q, R> request, R? expectValue) : IRequestTest
    {
        public void AssertResult(ExternalBatchResponse batchResponse)
        {
            ExternalResponse<R>? response = batchResponse.GetResponse(request);
            if (response == null)
            {
                Assert.Fail();
                return;
            }
            Assert.Multiple(() =>
            {
                Assert.That(response.Result, Is.EqualTo(RequestResult.Successful));
                Assert.That(response.Value, Is.EqualTo(expectValue));
            });
        }
    }
}
