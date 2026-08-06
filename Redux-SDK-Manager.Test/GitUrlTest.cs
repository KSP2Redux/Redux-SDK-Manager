using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class GitUrlTest
{
    [TestCase("https://github.com/Falki-git/SASExtended.git", "SASExtended")]
    [TestCase("https://github.com/Falki-git/SASExtended", "SASExtended")]
    [TestCase("https://github.com/Falki-git/SASExtended/", "SASExtended")]
    [TestCase("git@github.com:Falki-git/SASExtended.git", "SASExtended")]
    [TestCase("https://gitlab.com/group/subgroup/My.Mod.git", "My.Mod")]
    [TestCase("ssh://git@host:22/owner/repo.git", "repo")]
    public void RepoName_ParsesLastSegment(string url, string expected)
    {
        Assert.That(GitUrl.RepoName(url), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void RepoName_EmptyForBlank(string url)
    {
        Assert.That(GitUrl.RepoName(url), Is.Empty);
    }
}
