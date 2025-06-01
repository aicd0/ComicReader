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

        int sourceValue = 0;
        TestProperty<int, int> sourceProperty = new((request) =>
        {
            return PropertyResponseContent<int>.NewSuccessfulResponse(sourceValue);
        }, true, false);
        MemoryCacheProperty<int> cacheProperty = new(sourceProperty);

        string key1 = "1";
        string key2 = "2";

        ExternalBatchRequest batch = new();
        ExternalRequest<int, int> key1ReadRequest = new ExternalRequest<int, int>.Builder(cacheProperty).SetRequestType(RequestType.Read).SetKey(key1).Build();
        ExternalRequest<int, int> key2ReadRequest = new ExternalRequest<int, int>.Builder(cacheProperty).SetRequestType(RequestType.Read).SetKey(key2).Build();
        batch.Requests.Add(key1ReadRequest);
        batch.Requests.Add(key2ReadRequest);
        ExternalBatchResponse reponse = await server.Request(batch);
        AssertResponseValue(reponse.GetResponse(key1ReadRequest), sourceValue);
        AssertResponseValue(reponse.GetResponse(key2ReadRequest), sourceValue);
    }

    private void AssertResponseValue<T>(ExternalResponse<T>? response, T value)
    {
        if (response == null)
        {
            Assert.Fail();
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Is.EqualTo(RequestResult.Successful));
            Assert.That(response.Value, Is.EqualTo(value));
        });
    }
}
