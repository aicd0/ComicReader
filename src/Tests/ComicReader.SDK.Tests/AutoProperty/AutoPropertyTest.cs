// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.AutoProperty;
using ComicReader.SDK.Data.AutoProperty.Presets;

namespace ComicReader.SDK.Tests.AutoProperty;

[TestFixture]
public class AutoPropertyTest
{
    [Test]
    public void TestPropertyException()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty = new();
        Func<PropertyResponseContent<int>, PropertyResponseContent<int>> responseConversionFunc = (response) => response;
        ConverterProperty<int, int, int, int> converterProperty = new(sourceProperty, (request) => request, (response) => responseConversionFunc(response));

        Func<string, int> valueFunc = (key) =>
        {
            return -1;
        };
        int keyCount = 100;

        void TestInternal(int offset, bool patchRequestCallback)
        {
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + offset;
                }
                return -1;
            };
            if (patchRequestCallback)
            {
                responseConversionFunc = (response) => throw new InvalidOperationException();
            }
            else
            {
                sourceProperty.ServerFunc = (request) =>
                {
                    throw new InvalidOperationException();
                };
            }
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = [];
                tests.AddRange(AppendSerialReadTest(batch, converterProperty, 0, keyCount, valueFunc, RequestResult.Failed));
                tests.AddRange(AppendSerialWriteTest(batch, converterProperty, 0, keyCount, valueFunc, RequestResult.Failed));
                ExternalBatchResponse response = server.Request(batch).Result;
                // Exception in property
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
            if (patchRequestCallback)
            {
                responseConversionFunc = (response) => response;
            }
            else
            {
                sourceProperty.ServerFunc = (request) =>
                {
                    return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc(request.Key));
                };
            }
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = [];
                tests.AddRange(AppendSerialReadTest(batch, converterProperty, 0, keyCount, valueFunc));
                tests.AddRange(AppendSerialWriteTest(batch, converterProperty, 0, keyCount, valueFunc));
                tests.AddRange(AppendSerialWriteTest(batch, sourceProperty, 0, keyCount, valueFunc));
                ExternalBatchResponse response = server.Request(batch).Result;
                // Recover from exception
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }

        // Rearrange
        sourceProperty.Rearrange = true;
        sourceProperty.ProcessOnServerThread = true;
        TestInternal(0, false);

        // Process
        sourceProperty.Rearrange = false;
        sourceProperty.ProcessOnServerThread = true;
        TestInternal(1, false);

        // Post-process
        sourceProperty.Rearrange = false;
        sourceProperty.ProcessOnServerThread = false;
        TestInternal(2, false);

        // Request callback
        sourceProperty.Rearrange = true;
        sourceProperty.ProcessOnServerThread = true;
        TestInternal(3, true);
    }

    [Test]
    public void TestPropertyHang()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty = new();

        Func<string, int> valueFunc = (key) =>
        {
            return -1;
        };
        int keyCount = 100;

        void TestInternal(int offset)
        {
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + offset;
                }
                return -1;
            };
            sourceProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Read)
                {
                    throw new InvalidOperationException();
                }
                return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc(request.Key));
            };
            sourceProperty.Hang = true;
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = AppendSerialReadTest(batch, sourceProperty, 0, keyCount, valueFunc, RequestResult.Failed);
                ExternalBatchResponse response = server.Request(batch).Result;
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
            sourceProperty.Hang = false;
            {
                ExternalBatchRequest batch = new();
                List<IRequestTest> tests = AppendSerialReadTest(batch, sourceProperty, 0, keyCount, valueFunc);
                ExternalBatchResponse response = server.Request(batch).Result;
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }

        sourceProperty.Rearrange = true;
        sourceProperty.ProcessOnServerThread = true;
        TestInternal(0);

        sourceProperty.Rearrange = true;
        sourceProperty.ProcessOnServerThread = false;
        TestInternal(1);
    }

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
        int keyCount = 100;

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
                List<IRequestTest> tests = AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc);
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
                List<IRequestTest> tests = AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc);
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
                int step = keyCount / 2;
                subTests.AddRange(AppendSerialWriteTest(batch, sourceProperty, i * step, i * step + keyCount, valueFunc));
                subTests.AddRange(AppendSerialReadTest(batch, cacheProperty, i * step, i * step + keyCount, valueFunc));
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
                List<IRequestTest> tests = AppendSerialWriteTest(batch, cacheProperty, 0, keyCount, valueFunc);
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
                List<IRequestTest> tests = AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc);
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
            tests.AddRange(AppendSerialWriteTest(batch, cacheProperty, 0, keyCount, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc));
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 5;
                }
                return -1;
            };
            tests.AddRange(AppendSerialWriteTest(batch, cacheProperty, 0, keyCount, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc));
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 6;
                }
                return -1;
            };
            tests.AddRange(AppendSerialWriteTest(batch, cacheProperty, 0, keyCount, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc));
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
            List<IRequestTest> readTests = AppendSerialReadTest(readBatch, cacheProperty, 0, keyCount, valueFunc);
            valueFunc = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + 7;
                }
                return -1;
            };
            ExternalBatchRequest writeBatch = new();
            List<IRequestTest> writeTests = AppendSerialWriteTest(writeBatch, cacheProperty, 0, keyCount, valueFunc, result: RequestResult.Failed);
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
            tests.AddRange(AppendSerialWriteTest(batch, sourceProperty, 0, keyCount, valueFunc));
            tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc, RequestResult.Failed));
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
                tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc, RequestResult.Failed));
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
                tests.AddRange(AppendSerialWriteTest(batch, sourceProperty, 0, keyCount, valueFunc));
                tests.AddRange(AppendSerialReadTest(batch, cacheProperty, 0, keyCount, valueFunc));
                ExternalBatchResponse response = server.Request(batch).Result;
                // Successful read after failed read
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
        }
    }

    [Test]
    public void TestMultiSourceProperty()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty1 = new();
        sourceProperty1.Rearrange = true;
        sourceProperty1.ProcessOnServerThread = true;
        TestProperty<int, int> sourceProperty2 = new();
        sourceProperty2.Rearrange = true;
        sourceProperty2.ProcessOnServerThread = true;
        TestProperty<int, int> sourceProperty3 = new();
        sourceProperty3.Rearrange = true;
        sourceProperty3.ProcessOnServerThread = true;
        MultiSourceProperty<int> multiSourceProperty = new([sourceProperty1, sourceProperty2, sourceProperty3]);

        int keyCount = 100;

        void TestInternal(int offset, bool readTest, bool read1, bool write1, bool read2, bool write2, bool read3, bool write3)
        {
            Dictionary<string, int> writtenValues1 = [];
            Func<string, int> valueFunc1 = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + offset;
                }
                return -1;
            };
            sourceProperty1.ServerFunc = (request) =>
            {
                switch (request.Type)
                {
                    case RequestType.Read:
                        if (read1)
                        {
                            return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc1(request.Key));
                        }
                        return PropertyResponseContent<int>.NewFailedResponse();
                    case RequestType.Modify:
                        if (write1)
                        {
                            writtenValues1[request.Key] = request.Value;
                            return PropertyResponseContent<int>.NewSuccessfulResponse();
                        }
                        return PropertyResponseContent<int>.NewFailedResponse();
                    default:
                        throw new InvalidOperationException();
                }
            };
            Dictionary<string, int> writtenValues2 = [];
            Func<string, int> valueFunc2 = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + offset + 100;
                }
                return -1;
            };
            sourceProperty2.ServerFunc = (request) =>
            {
                switch (request.Type)
                {
                    case RequestType.Read:
                        if (read2)
                        {
                            return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc2(request.Key));
                        }
                        return PropertyResponseContent<int>.NewFailedResponse();
                    case RequestType.Modify:
                        if (write2)
                        {
                            writtenValues2[request.Key] = request.Value;
                            return PropertyResponseContent<int>.NewSuccessfulResponse();
                        }
                        return PropertyResponseContent<int>.NewFailedResponse();
                    default:
                        throw new InvalidOperationException();
                }
            };
            Dictionary<string, int> writtenValues3 = [];
            Func<string, int> valueFunc3 = (key) =>
            {
                if (int.TryParse(key, out int value))
                {
                    return value + offset + 200;
                }
                return -1;
            };
            sourceProperty3.ServerFunc = (request) =>
            {
                switch (request.Type)
                {
                    case RequestType.Read:
                        if (read3)
                        {
                            return PropertyResponseContent<int>.NewSuccessfulResponse(valueFunc3(request.Key));
                        }
                        return PropertyResponseContent<int>.NewFailedResponse();
                    case RequestType.Modify:
                        if (write3)
                        {
                            writtenValues3[request.Key] = request.Value;
                            return PropertyResponseContent<int>.NewSuccessfulResponse();
                        }
                        return PropertyResponseContent<int>.NewFailedResponse();
                    default:
                        throw new InvalidOperationException();
                }
            };
            List<IRequestTest> tests = [];
            ExternalBatchRequest batch = new();
            Dictionary<string, int> expectWrittenValue1 = [];
            Dictionary<string, int> expectWrittenValue2 = [];
            Dictionary<string, int> expectWrittenValue3 = [];
            if (readTest)
            {
                if (read1)
                {
                    tests.AddRange(AppendSerialReadTest(batch, multiSourceProperty, 0, keyCount, valueFunc1));
                }
                else if (read2)
                {
                    tests.AddRange(AppendSerialReadTest(batch, multiSourceProperty, 0, keyCount, valueFunc2));
                    for (int i = 0; i < keyCount; i++)
                    {
                        string key = i.ToString();
                        if (write1)
                        {
                            expectWrittenValue1[key] = valueFunc2(key);
                        }
                    }
                }
                else if (read3)
                {
                    tests.AddRange(AppendSerialReadTest(batch, multiSourceProperty, 0, keyCount, valueFunc3));
                    for (int i = 0; i < keyCount; i++)
                    {
                        string key = i.ToString();
                        if (write1)
                        {
                            expectWrittenValue1[key] = valueFunc3(key);
                        }
                        if (write2)
                        {
                            expectWrittenValue2[key] = valueFunc3(key);
                        }
                    }
                }
                else
                {
                    tests.AddRange(AppendSerialReadTest(batch, multiSourceProperty, 0, keyCount, (key) => -1, RequestResult.Failed));
                }
            }
            else
            {
                Func<string, int> valueFunc = (key) =>
                {
                    if (int.TryParse(key, out int value))
                    {
                        return value + offset + 300;
                    }
                    return -1;
                };
                RequestResult result = write1 && write2 && write3 ? RequestResult.Successful : RequestResult.Failed;
                tests.AddRange(AppendSerialWriteTest(batch, multiSourceProperty, 0, keyCount, valueFunc, result));
                if (write3)
                {
                    for (int i = 0; i < keyCount; i++)
                    {
                        string key = i.ToString();
                        expectWrittenValue3[key] = valueFunc(key);
                    }
                    if (write2)
                    {
                        for (int i = 0; i < keyCount; i++)
                        {
                            string key = i.ToString();
                            expectWrittenValue2[key] = valueFunc(key);
                        }
                        if (write1)
                        {
                            for (int i = 0; i < keyCount; i++)
                            {
                                string key = i.ToString();
                                expectWrittenValue1[key] = valueFunc(key);
                            }
                        }
                    }
                }
            }
            ExternalBatchResponse response = server.Request(batch).Result;
            foreach (IRequestTest test in tests)
            {
                test.AssertResult(response);
            }
            AssertDictionaryEqual(writtenValues1, expectWrittenValue1);
            AssertDictionaryEqual(writtenValues2, expectWrittenValue2);
            AssertDictionaryEqual(writtenValues3, expectWrittenValue3);
        }

        for (int i = 0; i < 1 << 7; i++)
        {
            TestInternal(i, (i & 0x01) != 0, (i & 0x02) != 0, (i & 0x04) != 0, (i & 0x08) != 0, (i & 0x10) != 0, (i & 0x20) != 0, (i & 0x40) != 0);
            TestInternal(i, (i & 0x40) != 0, (i & 0x20) != 0, (i & 0x10) != 0, (i & 0x08) != 0, (i & 0x04) != 0, (i & 0x02) != 0, (i & 0x01) != 0);
        }
    }

    [Test]
    public void TestConverterProperty()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> sourceProperty = new();
        sourceProperty.Rearrange = true;
        sourceProperty.ProcessOnServerThread = true;
        Func<PropertyRequestContent<int>, PropertyRequestContent<int>> requestConversionFunc = (request) => request;
        Func<PropertyResponseContent<int>, PropertyResponseContent<int>> responseConversionFunc = (response) => response;
        ConverterProperty<int, int, int, int> convertProperty = new(sourceProperty, (request) => requestConversionFunc(request), (response) => responseConversionFunc(response));

        int keyCount = 100;

        void TestInternal(int offset, bool requestException, bool responseException, bool read, bool write)
        {
            int valueFunc(string key, int value)
            {
                if (int.TryParse(key, out int keyValue))
                {
                    return keyValue + value + offset + 300;
                }
                return -1;
            }
            int requestValueFunc(string key)
            {
                if (!int.TryParse(key, out int value))
                {
                    value = -1;
                }
                return value + offset;
            }
            int responseValueFunc(int value)
            {
                return value * 7 + offset;
            }
            requestConversionFunc = (request) =>
            {
                if (requestException)
                {
                    throw new InvalidOperationException();
                }
                return request.WithValue(requestValueFunc(request.Key));
            };
            responseConversionFunc = (response) =>
            {
                if (responseException)
                {
                    throw new InvalidOperationException();
                }
                return PropertyResponseContent<int>.NewSuccessfulResponse(responseValueFunc(response.Value));
            };
            Dictionary<string, int> writtenValues = [];
            sourceProperty.ServerFunc = (request) =>
            {
                int result = valueFunc(request.Key, request.Value);
                switch (request.Type)
                {
                    case RequestType.Read:
                        return PropertyResponseContent<int>.NewSuccessfulResponse(result);
                    case RequestType.Modify:
                        writtenValues[request.Key] = result;
                        return PropertyResponseContent<int>.NewSuccessfulResponse();
                    default:
                        throw new InvalidOperationException();
                }
            };
            List<IRequestTest> tests = [];
            ExternalBatchRequest batch = new();
            Dictionary<string, int> expectWrittenValue = [];
            if (read)
            {
                if (requestException || responseException)
                {
                    tests.AddRange(AppendSerialReadTest(batch, convertProperty, 0, keyCount, (key) => -1, RequestResult.Failed));
                }
                else
                {
                    int examFunc(string key)
                    {
                        return responseValueFunc(valueFunc(key, requestValueFunc(key)));
                    }
                    tests.AddRange(AppendSerialReadTest(batch, convertProperty, 0, keyCount, examFunc));
                }
            }
            if (write)
            {
                if (requestException || responseException)
                {
                    tests.AddRange(AppendSerialWriteTest(batch, convertProperty, 0, keyCount, (key) => -1, RequestResult.Failed));
                }
                else
                {
                    tests.AddRange(AppendSerialWriteTest(batch, convertProperty, 0, keyCount, (key) => -1));
                }
                if (!requestException)
                {
                    for (int i = 0; i < keyCount; i++)
                    {
                        string key = i.ToString();
                        expectWrittenValue[key] = valueFunc(key, requestValueFunc(key));
                    }
                }
            }
            ExternalBatchResponse response = server.Request(batch).Result;
            foreach (IRequestTest test in tests)
            {
                test.AssertResult(response);
            }
            AssertDictionaryEqual(writtenValues, expectWrittenValue);
        }

        for (int i = 0; i < 1 << 4; i++)
        {
            TestInternal(i, (i & 0x01) != 0, (i & 0x02) != 0, (i & 0x04) != 0, (i & 0x08) != 0);
        }
    }

    [Test]
    public void TestSplitProperty()
    {
        PropertyServer server = new("Test");
        TestProperty<int, int> readProperty = new();
        TestProperty<int, int> writeProperty = new();
        SplitProperty<int> splitProperty = new(readProperty, writeProperty);

        int keyCount = 100;

        void TestInternal(int offset)
        {
            int readValueFunc(string key)
            {
                if (int.TryParse(key, out int keyValue))
                {
                    return keyValue + offset;
                }
                return -1;
            }
            int writeValueFunc(string key)
            {
                if (int.TryParse(key, out int keyValue))
                {
                    return keyValue + offset + 100;
                }
                return -1;
            }
            Dictionary<string, int> writtenValues = [];
            readProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Read)
                {
                    throw new InvalidOperationException();
                }
                return PropertyResponseContent<int>.NewSuccessfulResponse(readValueFunc(request.Key));
            };
            writeProperty.ServerFunc = (request) =>
            {
                if (request.Type != RequestType.Modify)
                {
                    throw new InvalidOperationException();
                }
                writtenValues[request.Key] = request.Value;
                return PropertyResponseContent<int>.NewSuccessfulResponse();
            };
            {
                List<IRequestTest> tests = [];
                ExternalBatchRequest batch = new();
                tests.AddRange(AppendSerialReadTest(batch, splitProperty, 0, keyCount, readValueFunc));
                ExternalBatchResponse response = server.Request(batch).Result;
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
            }
            {
                List<IRequestTest> tests = [];
                ExternalBatchRequest batch = new();
                Dictionary<string, int> expectWrittenValue = [];
                tests.AddRange(AppendSerialWriteTest(batch, splitProperty, 0, keyCount, writeValueFunc));
                for (int i = 0; i < keyCount; i++)
                {
                    string key = i.ToString();
                    expectWrittenValue[key] = writeValueFunc(key);
                }
                ExternalBatchResponse response = server.Request(batch).Result;
                foreach (IRequestTest test in tests)
                {
                    test.AssertResult(response);
                }
                AssertDictionaryEqual(writtenValues, expectWrittenValue);
            }
        }

        for (int i = 0; i < 1 << 4; i++)
        {
            readProperty.Rearrange = (i & 0x01) != 0;
            readProperty.ProcessOnServerThread = (i & 0x02) != 0;
            writeProperty.Rearrange = (i & 0x04) != 0;
            writeProperty.ProcessOnServerThread = (i & 0x08) != 0;
            TestInternal(i);
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

    private static void AssertDictionaryEqual<A, B>(Dictionary<A, B> actual, Dictionary<A, B> expected) where A : notnull
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            foreach (KeyValuePair<A, B> pair in expected)
            {
                Assert.That(actual.TryGetValue(pair.Key, out B? value), Is.True);
                Assert.That(value, Is.EqualTo(pair.Value));
            }
        });
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
