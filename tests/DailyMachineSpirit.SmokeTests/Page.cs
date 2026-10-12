using System.Net;
using System.Text;

namespace DailyMachineSpirit.SmokeTests;

public sealed record Page(string Path, HttpStatusCode Status, byte[] Content)
{
    public string Text => Encoding.UTF8.GetString(Content);
}
