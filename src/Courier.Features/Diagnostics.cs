using System.Diagnostics;

namespace Courier.Features;

public static class CourierDiagnostics
{
    public static readonly ActivitySource JobEngine = new("Courier.JobEngine");
    public static readonly ActivitySource FileMonitor = new("Courier.FileMonitor");
}
