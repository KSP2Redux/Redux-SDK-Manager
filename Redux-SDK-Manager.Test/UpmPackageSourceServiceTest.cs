using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class UpmPackageSourceServiceTest
{
    private static UpmPackageSourceService Client(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status) { Content = new StringContent(body) });
        return new UpmPackageSourceService(new HttpClient(handler.Object));
    }

    [TestCase("ksp2redux.sdk", "0.2.10-26w44a")]
    [TestCase("ksp2community.ksp2unitytools", "0.10.0-26w41a")]
    public void ResolveCommit_UsesProvenanceForEitherPackageIdentity(string name, string version)
    {
        var commit = new string('a', 40);
        var client = Client($$"""{"name":"{{name}}","version":"{{version}}","x-ksp2redux-source": {"repository":"KSP2Redux/SDK","commit":"{{commit}}"} }""");

        Assert.That(client.ResolveCommit(name, version), Is.EqualTo(commit));
    }

    [TestCase("{}")]
    [TestCase("{\"repository\":\"Other/Repo\",\"commit\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}")]
    [TestCase("{\"repository\":\"KSP2Redux/SDK\",\"commit\":\"not-a-commit\"}")]
    public void ResolveCommit_RejectsMissingOrIncorrectProvenance(string source)
    {
        var client = Client($$"""{"name":"ksp2redux.sdk","version":"0.2.10-26w44a","x-ksp2redux-source": {{source}} }""");

        Assert.That(() => client.ResolveCommit("ksp2redux.sdk", "0.2.10-26w44a"),
            Throws.InvalidOperationException.With.Message.Contains("source commit"));
    }

    [Test]
    public void ResolveCommit_DoesNotFallBackToMainWhenRegistryIsUnavailable()
    {
        var client = Client("unavailable", HttpStatusCode.ServiceUnavailable);

        Assert.That(() => client.ResolveCommit("ksp2redux.sdk", "0.2.10-26w44a"),
            Throws.InvalidOperationException.With.Message.Contains("registry"));
    }
}
