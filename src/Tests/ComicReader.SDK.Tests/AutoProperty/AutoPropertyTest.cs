// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.AutoProperty;
using ComicReader.SDK.Data.AutoProperty.Presets;

namespace ComicReader.SDK.Tests.AutoProperty;

[TestFixture]
public class AutoPropertyTest
{
    [Test]
    public void TestMemoryCacheProperty1()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty = new();
        MemoryCacheProperty<int> cacheProperty = new(sourceProperty);
        sourceProperty.Rearrange = true;
        sourceProperty.ProcessOnServerThread = true;
        TestMemoryCachePropertyInternal(server, sourceProperty, cacheProperty);
    }

    [Test]
    public void TestMemoryCacheProperty2()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty = new();
        MemoryCacheProperty<int> cacheProperty = new(sourceProperty);
        sourceProperty.Rearrange = false;
        sourceProperty.ProcessOnServerThread = true;
        TestMemoryCachePropertyInternal(server, sourceProperty, cacheProperty);
    }

    [Test]
    public void TestMemoryCacheProperty3()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty = new();
        MemoryCacheProperty<int> cacheProperty = new(sourceProperty);
        sourceProperty.Rearrange = false;
        sourceProperty.ProcessOnServerThread = false;
        TestMemoryCachePropertyInternal(server, sourceProperty, cacheProperty);
    }

    private void TestMemoryCachePropertyInternal(PropertyServer server, TestProperty<int, int> sourceProperty, MemoryCacheProperty<int> cacheProperty)
    {
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
                    ExternalBatchResponse response = server.Request(batch).Result;
                    // Single batch
                    foreach (IRequestTest test in tests)
                    {
                        test.AssertResult(response);
                    }
                }
                {
                    ExternalBatchResponse response = server.Request(batch).Result;
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
                ExternalBatchResponse response = server.Request(batch).Result;
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
                ExternalBatchResponse response = tasks[i].Result;
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
                ExternalBatchResponse response = server.Request(batch).Result;
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
                ExternalBatchResponse response = server.Request(batch).Result;
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
            ExternalBatchResponse response = server.Request(batch).Result;
            // Read and write
            foreach (IRequestTest test in tests)
            {
                test.AssertResult(response);
            }
        }

        {
            sourceProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Modify)
                {
                    throw new InvalidOperationException();
                }
                return PropertyResponseContent<int>.NewFailedResponse();
            };
            ExternalBatchRequest readBatch = new();
            List<IRequestTest> readTests = AppendSerialReadTest(readBatch, cacheProperty, 0, 10, valueFunc);
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 7;
                }
                return -1;
            };
            ExternalBatchRequest writeBatch = new();
            List<IRequestTest> writeTests = AppendSerialWriteTest(writeBatch, cacheProperty, 0, 10, valueFunc, result: RequestResult.Failed);
            {
                ExternalBatchResponse response = server.Request(writeBatch).Result;
                // Failed write
                foreach (IRequestTest test in writeTests)
                {
                    test.AssertResult(response);
                }
            }
            {
                ExternalBatchResponse response = server.Request(readBatch).Result;
                // Read after failed write
                foreach (IRequestTest test in readTests)
                {
                    test.AssertResult(response);
                }
            }
        }

        {
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 8;
                }
                return -1;
            };
            sourceProperty.ServerFunc = (request) =>
            {
                if (request.Type == RequestType.Modify)
                {
                    return PropertyResponseContent<int>.NewSuccessfulResponse();
                }
                return PropertyResponseContent<int>.NewFailedResponse();
            };
            List<IRequestTest> tests = [];
            ExternalBatchRequest batch = new();
            tests.AddRange(AppendSerialWriteTest(batch, sourceProperty, 0, 10, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, 10, valueFunc, RequestResult.Failed));
            {
                ExternalBatchResponse response = server.Request(batch).Result;
                // Failed read
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }

        {
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 9;
                }
                return -1;
            };
            {
                sourceProperty.ServerFunc = (request) =>
                {
                    if (request.Type == RequestType.Modify)
                    {
                        throw new InvalidOperationException();
                    }
                    return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc(request.Key));
                };
                List<IRequestTest> tests = [];
                ExternalBatchRequest batch = new();
                tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, 10, valueFunc, RequestResult.Failed));
                ExternalBatchResponse response = server.Request(batch).Result;
                // Cached failed read
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
            {
                sourceProperty.ServerFunc = (request) =>
                {
                    if (request.Type == RequestType.Modify)
                    {
                        return PropertyResponseContent<int>.NewSuccessfulResponse();
                    }
                    return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc(request.Key));
                };
                List<IRequestTest> tests = [];
                ExternalBatchRequest batch = new();
                tests.AddRange(AppendSerialWriteTest(batch, sourceProperty, 0, 10, valueFunc));
                tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, 10, valueFunc));
                ExternalBatchResponse response = server.Request(batch).Result;
                // Successful read after failed read
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }
    }

    private static List<IRequestTest> AppendSerialReadTest<T>(ExternalBatchRequest batch, IQRProperty<T, T> property,
        int start, int count, Func<string, T> valueFunc, RequestResult result = RequestResult.Successful)
    {
        List<IRequestTest> tests = [];
        for (int i = start; i < count; i++)
        {
            string key = i.ToString();
            ExternalRequest<T, T> request = new ExternalRequest<T, T>.Builder(property).SetRequestType(RequestType.Read).SetKey(key).Build();
            batch.Requests.Add(request);
            IRequestTest test;
            if (result == RequestResult.Successful)
            {
                test = new ReadRequestTest<T, T>(request, valueFunc(key));
            }
            else
            {
                test = new RequestResultTest<T, T>(request, result);
            }
            tests.Add(test);
        }
        return tests;
    }

    private static List<IRequestTest> AppendSerialWriteTest<T>(ExternalBatchRequest batch, IQRProperty<T, T> property,
        int start, int count, Func<string, T> valueFunc, RequestResult result = RequestResult.Successful)
    {
        List<IRequestTest> tests = [];
        for (int i = start; i < count; i++)
        {
            string key = i.ToString();
            T value = valueFunc(key);
            ExternalRequest<T, T> request = new ExternalRequest<T, T>.Builder(property).SetRequestType(RequestType.Modify).SetKey(key).SetValue(value).Build();
            batch.Requests.Add(request);
            RequestResultTest<T, T> test = new(request, result);
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
