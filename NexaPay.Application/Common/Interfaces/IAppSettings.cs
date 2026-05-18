// ============================================================
// IAppSettings.cs – NexaPay.Application/Common/Interfaces
// ============================================================
// Abstraktion över appsettings.json så att Application-lagret
// inte behöver referera Microsoft.Extensions.Configuration.
// Infrastructure-lagret implementerar interfacet och binder
// faktiska värden från konfigurationen.
//
// StaffDomain: e-postdomänen som krävs för personalroller (t.ex.
// "nexapay.com"). Används av StaffEmailPolicy vid registrering.
// ============================================================

namespace NexaPay.Application.Common.Interfaces
{
    public interface IAppSettings
    {
        string StaffDomain { get; }
    }
}
